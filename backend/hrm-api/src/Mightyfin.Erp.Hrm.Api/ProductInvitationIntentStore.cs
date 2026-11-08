using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record ProductInvitationIntent(int Version, string HandleHash, string IntentHash, string BindingHash, string ClientId,
    string Token, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt,
    string? Subject = null, string? AccessToken = null, string? Csrf = null)
{
    public override string ToString() => nameof(ProductInvitationIntent);
}

/// <summary>Private five-minute handoff only. No membership, email or operational session.</summary>
internal sealed class ProductInvitationIntentStore(string directory, string clientId, IDataProtectionProvider protection, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("Kuubu.CompanyInvitation.Intent.v1", clientId);
    internal static bool Opaque(string? text) => text is not null && Regex.IsMatch(text, "^[A-Za-z0-9_-]{43}\\z", RegexOptions.CultureInvariant);
    internal static string Random() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private string PathFor(string handle) => Path.Combine(directory, Hash(handle)+".invitation");
    internal async Task<string> PrepareAsync(string token, string binding, CancellationToken cancellation)
    {
        if (!Opaque(token) || !Opaque(binding)) throw new InvalidOperationException("Invalid invitation intent.");
        var handle = Random(); var now = clock.GetUtcNow();
        await Write(PathFor(handle), new(1, Hash(handle), Hash(handle), Hash(binding), clientId, token, now, now.AddMinutes(5)), cancellation);
        return handle;
    }
    internal async Task<ProductInvitationIntent?> ReadAsync(string handle, string binding, CancellationToken cancellation)
    {
        if (!Opaque(handle) || !Opaque(binding)) return null;
        var value=await ReadFile(PathFor(handle), handle, binding, cancellation);
        return value?.Subject is null && File.Exists(PathFor(handle)+".claimed") ? null : value;
    }
    internal async Task<string?> CompleteAsync(string handle, string binding, string subject, string accessToken,
        DateTimeOffset expires, CancellationToken cancellation)
    {
        if (!Regex.IsMatch(subject, "^[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant)
            || accessToken.Length is < 1 or > 16384 || !Regex.IsMatch(accessToken, "^[A-Za-z0-9._~-]+\\z", RegexOptions.CultureInvariant)
            || expires <= clock.GetUtcNow() || expires > clock.GetUtcNow().AddMinutes(5)) return null;
        var intent = await ReadAsync(handle, binding, cancellation);
        if (intent is null || intent.Subject is not null) return null;
        var path = PathFor(handle); var claimed = path+".claimed";
        var claimOptions=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None};
        if(!OperatingSystem.IsWindows())claimOptions.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
        try { using var claim=new FileStream(claimed,claimOptions); }
        catch (IOException) { return null; }
        // Keep the exclusive CreateNew reservation until expiry cleanup. Late
        // callbacks cannot reuse a released reservation or a rename race.
        intent = await ReadFile(path, handle, binding, cancellation);
        if (intent is null || intent.Subject is not null) return null;
        var next = Random();
        var grant = intent with { HandleHash = Hash(next), Subject = subject, AccessToken = accessToken, Csrf = Random(),
            ExpiresAt = expires < intent.ExpiresAt ? expires : intent.ExpiresAt };
        await Write(PathFor(next), grant, cancellation);
        // A concurrent reader can briefly deny deletion on Windows. The claim
        // still makes the anonymous intent unusable; expiry cleanup can remove it.
        try{File.Delete(path);}catch(IOException){}
        if(await ReadAsync(next,binding,cancellation) is null){Revoke(next);return null;}
        return next;
    }
    private async Task Write(string path, ProductInvitationIntent value, CancellationToken cancellation)
    {
        var bytes = Encoding.UTF8.GetBytes(protector.Protect(JsonSerializer.Serialize(value)));
        if (bytes.Length > 32768) throw new InvalidOperationException("Invalid invitation intent size.");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options);
        await stream.WriteAsync(bytes, cancellation); await stream.FlushAsync(cancellation);
    }
    private async Task<ProductInvitationIntent?> ReadFile(string path, string handle, string binding, CancellationToken cancellation)
    {
        try {
            var info = new FileInfo(path);
            if (!info.Exists || info.LinkTarget is not null || info.Length is < 1 or > 32768) return null;
            if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite
                | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0) return null;
            var value = JsonSerializer.Deserialize<ProductInvitationIntent>(protector.Unprotect(await File.ReadAllTextAsync(path, cancellation)));
            var now = clock.GetUtcNow();
            if (value is null || value.Version != 1 || value.HandleHash != Hash(handle) || value.BindingHash != Hash(binding)
                || value.IntentHash is null || !Regex.IsMatch(value.IntentHash,"^[0-9a-f]{64}\\z")
                || File.Exists(Path.Combine(directory,value.IntentHash+".invitation.revoked"))
                || File.Exists(PathFor(handle)+".revoked")
                || value.ClientId != clientId || !Opaque(value.Token) || value.IssuedAt > now.AddSeconds(5)
                || value.ExpiresAt <= now || value.ExpiresAt <= value.IssuedAt || value.ExpiresAt > value.IssuedAt.AddMinutes(5)) return null;
            if (value.Subject is null) return value.IntentHash==value.HandleHash && value.AccessToken is null && value.Csrf is null ? value : null;
            return Regex.IsMatch(value.Subject, "^[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant)
                && value.AccessToken is { Length: > 0 and <= 16384 } && Regex.IsMatch(value.AccessToken, "^[A-Za-z0-9._~-]+\\z", RegexOptions.CultureInvariant)
                && Opaque(value.Csrf) ? value : null;
        }
        catch (Exception error) when (error is IOException or CryptographicException or JsonException or UnauthorizedAccessException) { return null; }
    }
    internal void Revoke(string handle)
    {
        if(!Opaque(handle))return;
        var marker=PathFor(handle)+".revoked";
        var options=new FileStreamOptions{Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None};
        if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
        try{using var stream=new FileStream(marker,options);}
        catch(IOException) when(File.Exists(marker)){}
        // Revocation is authoritative even while an existing reader holds a file.
        try{File.Delete(PathFor(handle));}catch(IOException){}
    }
    internal void Purge()
    {
        var cutoff = clock.GetUtcNow().AddMinutes(-10).UtcDateTime;
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)) {
            var file = new FileInfo(path);
            if (Regex.IsMatch(file.Name, "^[0-9a-f]{64}\\.invitation(?:\\.(?:claimed|revoked))?\\z")
                && file.LinkTarget is null && file.LastWriteTimeUtc < cutoff) File.Delete(path);
        }
    }
}

internal sealed class ProductInvitationIntentCleanup(ProductInvitationIntentStore store, ILogger<ProductInvitationIntentCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken)) {
            try { store.Purge(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { logger.LogWarning("Invitation intent cleanup is unavailable."); }
        }
    }
}
