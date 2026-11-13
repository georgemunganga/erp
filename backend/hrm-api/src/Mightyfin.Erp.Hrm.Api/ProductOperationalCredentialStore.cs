using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;

namespace Mightyfin.Erp.Hrm.Api;

internal enum OperationalCredentialStatus { Verified,Denied,Unavailable,Busy,ReauthenticationRequired }
internal sealed record OperationalCredentialMaterial(
    [property:System.Text.Json.Serialization.JsonIgnore] string Handle,
    [property:System.Text.Json.Serialization.JsonIgnore] string BrowserBinding,
    [property:System.Text.Json.Serialization.JsonIgnore] DateTimeOffset ExpiresAt)
{ public override string ToString()=>nameof(OperationalCredentialMaterial); }
internal sealed record OperationalCredentialResult(OperationalCredentialStatus Status,OperationalSubjectObservation? Observation=null,
    [property:System.Text.Json.Serialization.JsonIgnore] OperationalCredentialMaterial? Material=null);
internal sealed record OperationalCredentialPacket(int Version,string HandleHash,string BindingHash,string Client,string Subject,
    string Access,string Refresh,DateTimeOffset AccessExpires,DateTimeOffset IssuedAt,DateTimeOffset ExpiresAt,long Revision)
{public override string ToString()=>nameof(OperationalCredentialPacket);}

/// <summary>Private person credentials only; never authenticates an ERP session or grants company/module access.</summary>
internal sealed class ProductOperationalCredentialStore
{
    private readonly string directory,client;
    private readonly IDataProtector protector;
    private readonly TimeProvider clock;
    private readonly int capacity;
    private static readonly TimeSpan Lifetime=TimeSpan.FromHours(8),Grace=TimeSpan.FromMinutes(15);
    internal ProductOperationalCredentialStore(string directory,string keyDirectory,string client,IDataProtectionProvider protection,TimeProvider clock,int capacity=512)
    {
        ProductSetupGrantStore.ValidateDirectory(directory,keyDirectory);
        ProductSetupGrantStore.ValidateDirectory(keyDirectory,directory);
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        bool Inside(string child,string parent){var p=Path.GetRelativePath(parent,child);return !Path.IsPathRooted(p) && p!=".." && !p.StartsWith(".."+Path.DirectorySeparatorChar,comparison);}
        if(!Path.IsPathFullyQualified(keyDirectory) || !Directory.Exists(keyDirectory) || Inside(directory,keyDirectory) || Inside(keyDirectory,directory)
            || !Guid.TryParseExact(client,"D",out var id) || id==Guid.Empty || id.ToString("D")!=client || capacity is <1 or >2048)
            throw new InvalidOperationException("Private operational credential configuration is required.");
        this.directory=directory;this.client=client;this.clock=clock;this.capacity=capacity;
        protector=protection.CreateProtector("Kuubu.OperationalCredentials.v1",client);
    }
    private static string Hash(string value)=>ProductInvitationIntentStore.Hash(value);
    private string PathFor(string handle)=>Path.Combine(directory,Hash(handle)+".credentials");
    private static bool Private(string path,int max)
    {
        var info=new FileInfo(path);
        return info.Exists && info.LinkTarget is null && info.Length<=max && (OperatingSystem.IsWindows()
            || (File.GetUnixFileMode(path)&(UnixFileMode.GroupRead|UnixFileMode.GroupWrite|UnixFileMode.GroupExecute|UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute))==0);
    }
    private static FileStream Open(string path,FileMode mode,FileShare share=FileShare.None)
    {
        if(new FileInfo(path).LinkTarget is not null)throw new IOException("Operational state unavailable.");
        var options=new FileStreamOptions{Mode=mode,Access=FileAccess.ReadWrite,Share=share,Options=FileOptions.WriteThrough};
        if(!OperatingSystem.IsWindows() && mode is FileMode.Create or FileMode.CreateNew or FileMode.OpenOrCreate)
            options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
        var stream=new FileStream(path,options);
        if(!Private(path,98304)){stream.Dispose();throw new IOException("Operational state unavailable.");}
        return stream;
    }
    private FileStream Gate()=>Open(Path.Combine(directory,".operational-credentials.lock"),FileMode.OpenOrCreate);
    private async Task<FileStream?> ObservationLockAsync(string path,CancellationToken cancellation)
    {
        // Parallel screen reads must wait for brief publication locks, without
        // replaying a refresh or holding the directory gate while waiting.
        var start=System.Diagnostics.Stopwatch.GetTimestamp();
        while(true) {
            cancellation.ThrowIfCancellationRequested();
            try {
                using var gate=Gate();
                if(!File.Exists(path+".capacity"))return null;
                return Open(path+".lock",FileMode.OpenOrCreate);
            }catch(IOException)when(System.Diagnostics.Stopwatch.GetElapsedTime(start)<TimeSpan.FromSeconds(2)) {
                await Task.Delay(20,cancellation);
            }
        }
    }
    private static DateTimeOffset? CapacityExpiry(string path)
    {
        if(!Private(path,32))return null;
        return long.TryParse(File.ReadAllText(path),NumberStyles.None,CultureInfo.InvariantCulture,out var ticks) && ticks>0 && ticks<=DateTimeOffset.MaxValue.UtcTicks
            ?new DateTimeOffset(ticks,TimeSpan.Zero):null;
    }
    private static void Durable(FileStream stream,ReadOnlySpan<byte> bytes)
    {stream.Position=0;stream.Write(bytes);stream.SetLength(bytes.Length);stream.Flush(true);}
    private static long Fence(string path)
    {
        if(!Private(path,16))throw new IOException("Operational state unavailable.");
        var bytes=File.ReadAllBytes(path);if(bytes.Length!=16)throw new IOException("Operational state unavailable.");
        var value=BinaryPrimitives.ReadInt64LittleEndian(bytes);var inverse=BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8));
        if(value<0 || value>4096 || inverse!=~value)throw new IOException("Operational state unavailable.");return value;
    }
    private static void WriteFence(string path,long value,FileMode mode)
    {
        Span<byte> bytes=stackalloc byte[16];BinaryPrimitives.WriteInt64LittleEndian(bytes,value);BinaryPrimitives.WriteInt64LittleEndian(bytes[8..],~value);
        using var stream=Open(path,mode);Durable(stream,bytes);
    }
    private bool Revoked(string path)=>File.Exists(path+".revoked") || new FileInfo(path+".revoked").LinkTarget is not null;
    private OperationalCredentialPacket? Read(string path,string handle,string binding,bool allowExpired=false)
    {
        if(!Private(path,98304) || new FileInfo(path).Length==0)return null;
        var value=JsonSerializer.Deserialize<OperationalCredentialPacket>(protector.Unprotect(File.ReadAllText(path)));
        var now=clock.GetUtcNow();
        return value is not null && value.Version==1 && value.Client==client && value.HandleHash==Hash(handle) && value.BindingHash==Hash(binding)
            && Regex.IsMatch(value.Subject??"",@"\A[1-9][0-9]{0,18}\z",RegexOptions.CultureInvariant)
            && value.Revision is >=1 and <=4096 && value.IssuedAt<=now && value.ExpiresAt-value.IssuedAt==Lifetime
            && (allowExpired || value.ExpiresAt>now) && value.AccessExpires>value.IssuedAt && value.AccessExpires<=value.ExpiresAt
            && OperationalTokenPair.ValidAccess(value.Access) && OperationalTokenPair.ValidRefresh(value.Refresh)
            && CapacityExpiry(path+".capacity")==value.ExpiresAt?value:null;
    }
    private void Write(string path,OperationalCredentialPacket packet,bool replace)
    {
        var encrypted=Encoding.UTF8.GetBytes(protector.Protect(JsonSerializer.Serialize(packet)));
        if(encrypted.Length>98304)throw new IOException("Operational state unavailable.");
        var temp=path+".next-"+Guid.NewGuid().ToString("N");
        using(var stream=Open(temp,FileMode.CreateNew))Durable(stream,encrypted);
        File.Move(temp,path,replace);
    }
    internal Task<string?> IssueAsync(OperationalSubjectObservation observation,OperationalTokenPair tokens,string binding,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(!observation.IsCurrent || observation.ClientId!=client || !Regex.IsMatch(observation.Subject,@"\A[1-9][0-9]{0,18}\z",RegexOptions.CultureInvariant) || !ProductInvitationIntentStore.Opaque(binding)
            || !OperationalTokenPair.ValidAccess(tokens.Access) || !OperationalTokenPair.ValidRefresh(tokens.Refresh)
            || tokens.ExpiresAt<=clock.GetUtcNow() || tokens.ExpiresAt>clock.GetUtcNow().AddMinutes(5))return Task.FromResult<string?>(null);
        using var gate=Gate();var entries=Directory.EnumerateFileSystemEntries(directory).Take(16385).ToArray();
        if(entries.Length>16384 || entries.Count(x=>Regex.IsMatch(Path.GetFileName(x),@"\A[0-9a-f]{64}\.credentials\.capacity\z"))>=capacity)
            return Task.FromResult<string?>(null);
        // Authenticated, durable admission limits share the directory gate across
        // workers. Revocation frees a concurrent slot, never the recent-issue budget.
        var active=0;var recent=0;var admissionNow=clock.GetUtcNow();
        foreach(var marker in entries.Where(x=>Regex.IsMatch(Path.GetFileName(x),@"\A[0-9a-f]{64}\.credentials\.capacity\z"))) {
            var candidate=marker[..^".capacity".Length];
            if(!Private(candidate,98304))return Task.FromResult<string?>(null);
            var packet=JsonSerializer.Deserialize<OperationalCredentialPacket>(protector.Unprotect(File.ReadAllText(candidate)));
            if(packet is null || packet.Version!=1 || packet.Client!=client || packet.ExpiresAt-packet.IssuedAt!=Lifetime
                || packet.IssuedAt>admissionNow || CapacityExpiry(marker)!=packet.ExpiresAt)return Task.FromResult<string?>(null);
            if(packet.Subject!=observation.Subject)continue;
            if(packet.ExpiresAt>admissionNow && !Revoked(candidate))active++;
            if(packet.IssuedAt>admissionNow.AddMinutes(-10))recent++;
        }
        if(active>=4 || recent>=6)return Task.FromResult<string?>(null);
        var handle=ProductInvitationIntentStore.Random();var path=PathFor(handle);var now=clock.GetUtcNow();var expires=now+Lifetime;
        using(var marker=Open(path+".capacity",FileMode.CreateNew))Durable(marker,Encoding.ASCII.GetBytes(expires.UtcTicks.ToString(CultureInfo.InvariantCulture)));
        WriteFence(path+".fence",0,FileMode.CreateNew);
        Write(path,new(1,Hash(handle),Hash(binding),client,observation.Subject,tokens.Access,tokens.Refresh,tokens.ExpiresAt,now,expires,1),false);
        if(!observation.IsCurrent){Tombstone(path);return Task.FromResult<string?>(null);}
        return Task.FromResult<string?>(handle);
    }
    private static void Tombstone(string path)
    {
        try {using var marker=Open(path+".revoked",FileMode.CreateNew);marker.Flush(true);}
        catch(IOException)when(File.Exists(path+".revoked")){}
    }
    internal Task RevokeAsync(string handle,string binding,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(!ProductInvitationIntentStore.Opaque(handle) || !ProductInvitationIntentStore.Opaque(binding))return Task.CompletedTask;
        var path=PathFor(handle);
        try {var value=Read(path,handle,binding);if(value is not null && value.ExpiresAt>clock.GetUtcNow())Tombstone(path);}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or CryptographicException or JsonException){}
        return Task.CompletedTask;
    }
    internal async Task<OperationalCredentialResult> ObserveAsync(string handle,string binding,
        Func<string,string,CancellationToken,Task<OperationalSubjectResult>> observe,
        Func<string,CancellationToken,Task<OperationalRefreshResult>> refresh,CancellationToken cancellation=default)
    {
        if(!ProductInvitationIntentStore.Opaque(handle) || !ProductInvitationIntentStore.Opaque(binding))return new(OperationalCredentialStatus.Denied);
        var path=PathFor(handle);FileStream held;
        try {
            var acquired=await ObservationLockAsync(path,cancellation);
            if(acquired is null)return new(OperationalCredentialStatus.Denied);
            held=acquired;
        }catch(IOException){return new(OperationalCredentialStatus.Busy);}
        catch(UnauthorizedAccessException){return new(OperationalCredentialStatus.Unavailable);}
        using(held) {
            try {
                cancellation.ThrowIfCancellationRequested();
                if(Revoked(path))return new(OperationalCredentialStatus.Denied);
                var value=Read(path,handle,binding);
                if(value is null)return new(OperationalCredentialStatus.ReauthenticationRequired);
                if(Fence(path+".fence")!=value.Revision-1)return new(OperationalCredentialStatus.ReauthenticationRequired);
                using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellation);budget.CancelAfter(TimeSpan.FromSeconds(30));
                if(value.AccessExpires<value.ExpiresAt && value.AccessExpires<=clock.GetUtcNow().AddSeconds(30)) {
                    if(value.Revision>=4096)return new(OperationalCredentialStatus.ReauthenticationRequired);
                    budget.Token.ThrowIfCancellationRequested();
                    // Durable, never reset. A crash/cancellation/uncertain response
                    // cannot submit the same rotating token again after restart.
                    WriteFence(path+".fence",value.Revision,FileMode.Open);
                    var result=await refresh(value.Refresh,budget.Token);
                    if(Revoked(path))return new(OperationalCredentialStatus.Denied);
                    if(result.Status==OperationalRefreshStatus.Denied){Tombstone(path);return new(OperationalCredentialStatus.Denied);}
                    if(result.Status!=OperationalRefreshStatus.Refreshed || result.Tokens is not { } pair
                        || !OperationalTokenPair.ValidAccess(pair.Access) || !OperationalTokenPair.ValidRefresh(pair.Refresh) || pair.Refresh==value.Refresh || pair.Access==value.Access
                        || pair.ExpiresAt<=clock.GetUtcNow() || pair.ExpiresAt>clock.GetUtcNow().AddMinutes(5))return new(OperationalCredentialStatus.Unavailable);
                    var until=pair.ExpiresAt<value.ExpiresAt?pair.ExpiresAt:value.ExpiresAt;
                    if(until<=clock.GetUtcNow())return new(OperationalCredentialStatus.ReauthenticationRequired);
                    value=value with{Access=pair.Access,Refresh=pair.Refresh,AccessExpires=until,Revision=value.Revision+1};
                    Write(path,value,true);
                }
                if(Revoked(path))return new(OperationalCredentialStatus.Denied);
                // Concurrent requests may observe the person in parallel. Only
                // credential rotation/publication is serialized, not business reads.
                held.Dispose();
                var current=await observe(value.Subject,value.Access,budget.Token);
                budget.Token.ThrowIfCancellationRequested();
                using var finalLock=await ObservationLockAsync(path,cancellation);
                if(finalLock is null)return new(OperationalCredentialStatus.Denied);
                if(Revoked(path))return new(OperationalCredentialStatus.Denied);
                var latest=Read(path,handle,binding);
                if(latest is null || latest.Subject!=value.Subject || Fence(path+".fence")!=latest.Revision-1)
                    return new(OperationalCredentialStatus.ReauthenticationRequired);
                if(current.Status==OperationalSubjectStatus.Denied){Tombstone(path);return new(OperationalCredentialStatus.Denied);}
                if(current.Status!=OperationalSubjectStatus.Verified || current.Observation is not { } proof
                    || !proof.IsCurrent || proof.Subject!=value.Subject || proof.ClientId!=client)return new(OperationalCredentialStatus.Unavailable);
                var observation=proof.Constrain(value.AccessExpires<value.ExpiresAt?value.AccessExpires:value.ExpiresAt);
                return observation.IsCurrent?new(OperationalCredentialStatus.Verified,observation,new(handle,binding,latest.ExpiresAt)):new(OperationalCredentialStatus.ReauthenticationRequired);
            }catch(OperationCanceledException)when(!cancellation.IsCancellationRequested){return new(OperationalCredentialStatus.Unavailable);}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or CryptographicException or JsonException or InvalidOperationException
                or FormatException or ArgumentOutOfRangeException){return new(OperationalCredentialStatus.Unavailable);}
        }
    }
    internal int PurgeExpired()
    {
        using var gate=Gate();var all=Directory.EnumerateFileSystemEntries(directory).Take(16385).ToArray();
        if(all.Length>16384)throw new IOException("Operational state unavailable.");var removed=0;
        foreach(var marker in all.Where(x=>Regex.IsMatch(Path.GetFileName(x),@"\A[0-9a-f]{64}\.credentials\.capacity\z"))) {
            var expires=CapacityExpiry(marker);if(expires is null || expires>=clock.GetUtcNow()-Grace)continue;
            var path=marker[..^".capacity".Length];FileStream held;
            try {held=Open(path+".lock",FileMode.OpenOrCreate);}catch(IOException){continue;}
            using(held) {
                foreach(var file in all.Where(x=>x==path || x==path+".fence" || x==path+".revoked"
                    || x.StartsWith(path+".next-",StringComparison.Ordinal) && Regex.IsMatch(x[(path.Length+6)..],@"\A[0-9a-f]{32}\z"))) {
                    if(new FileInfo(file).LinkTarget is not null)throw new IOException("Operational state unavailable.");File.Delete(file);
                }
            }
            // Gate prevents new readers opening this inode while it is removed.
            File.Delete(path+".lock");File.Delete(marker);removed++;
        }
        return removed;
    }
}
