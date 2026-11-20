using Microsoft.AspNetCore.DataProtection;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductOperationalCredentialTests
{
    [Fact]
    public async Task Parallel_screen_reads_wait_for_one_refresh_and_each_observe_the_current_person()
    {
        var f=new Fixture();var handle=await f.Issue(10);var observations=0;
        var requests=Enumerable.Range(0,16).Select(_=>Task.Run(()=>f.Reopen().ObserveAsync(handle,f.Binding,
            async(subject,token,ct)=> { await Task.Yield();Interlocked.Increment(ref observations);return await f.Observe(subject,token,ct); },
            async(token,ct)=> { await Task.Delay(100,ct);return await f.Refresh(token,ct); }))).ToArray();
        var results=await Task.WhenAll(requests);
        Assert.All(results,result=>Assert.Equal(OperationalCredentialStatus.Verified,result.Status));
        Assert.Equal(1,f.Refreshes);Assert.Equal(16,observations);
        f.Person=OperationalSubjectStatus.Denied;
        Assert.Equal(OperationalCredentialStatus.Denied,(await f.Read(handle)).Status);
    }
    [Fact]
    public async Task Authenticated_person_limits_survive_reopening_and_revocation_does_not_reset_issue_rate()
    {
        var f=new Fixture();var handles=new List<string>();
        for(var i=0;i<4;i++)handles.Add(await f.Issue());
        Assert.Null(await f.Reopen().IssueAsync(f.Proof(),new("access","refresh",f.Clock.Now.AddMinutes(5)),f.Binding));
        await f.Store.RevokeAsync(handles[0],f.Binding);handles.Add(await f.Issue());
        await f.Store.RevokeAsync(handles[1],f.Binding);handles.Add(await f.Issue());
        await f.Store.RevokeAsync(handles[2],f.Binding);
        Assert.Null(await f.Reopen().IssueAsync(f.Proof(),new("access","refresh",f.Clock.Now.AddMinutes(5)),f.Binding));
        Assert.NotNull(await f.Reopen().IssueAsync(f.Proof("43"),new("access","refresh",f.Clock.Now.AddMinutes(5)),f.Binding));
        f.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.NotNull(await f.Reopen().IssueAsync(f.Proof(),new("access","refresh",f.Clock.Now.AddMinutes(5)),f.Binding));
    }
    [Fact]
    public async Task Trusted_launch_material_retains_absolute_expiry_across_refresh_and_is_never_serialized()
    {
        var f=new Fixture();var issued=f.Clock.Now;var handle=await f.Issue(10);
        var first=await f.Read(handle);Assert.Equal(OperationalCredentialStatus.Verified,first.Status);
        var material=Assert.IsType<OperationalCredentialMaterial>(first.Material);
        Assert.Equal(handle,material.Handle);Assert.Equal(f.Binding,material.BrowserBinding);
        Assert.Equal(issued.AddHours(8),material.ExpiresAt);Assert.Equal(1,f.Refreshes);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        var next=await f.Reopen().ObserveAsync(handle,f.Binding,f.Observe,f.Refresh);
        Assert.Equal(material,next.Material);Assert.True(next.Observation!.ExpiresAt<material.ExpiresAt);
        Assert.Equal("{}",System.Text.Json.JsonSerializer.Serialize(material));
        foreach(var text in new[]{System.Text.Json.JsonSerializer.Serialize(first),first.ToString(),material.ToString()}){
            Assert.DoesNotContain(handle,text);Assert.DoesNotContain(f.Binding,text);Assert.DoesNotContain("private.access",text);
        }
        f.Person=OperationalSubjectStatus.Unavailable;Assert.Null((await f.Read(handle)).Material);
        await f.Store.RevokeAsync(handle,f.Binding);Assert.Null((await f.Read(handle)).Material);
    }
    internal const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    internal sealed class Clock:TimeProvider
    {
        internal DateTimeOffset Now=DateTimeOffset.UtcNow;private long ticks;
        public override DateTimeOffset GetUtcNow()=>Now;
        public override long GetTimestamp()=>ticks;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        internal void Advance(TimeSpan duration){Now+=duration;ticks+=duration.Ticks;}
    }
    internal sealed class Fixture
    {
        internal readonly Clock Clock=new();
        internal readonly string Root,Keys,Directory,Binding=ProductInvitationIntentStore.Random();
        internal readonly IDataProtectionProvider Protection=new EphemeralDataProtectionProvider();
        internal readonly ProductOperationalCredentialStore Store;
        internal int Refreshes,Observations;
        internal string ExpectedAccess="initial.private.access";
        internal OperationalSubjectStatus Person=OperationalSubjectStatus.Verified;
        internal Fixture(int capacity=512)
        {
            var parent=Environment.GetEnvironmentVariable("KUUBU_OPERATIONAL_CREDENTIAL_TEST_ROOT")??Path.GetTempPath();
            Root=Path.Combine(parent,"kuubu-operational-credentials-"+Guid.NewGuid().ToString("N"));
            Keys=Path.Combine(Root,"keys");Directory=Path.Combine(Root,"credentials");
            foreach(var path in new[]{Root,Keys,Directory}){
                System.IO.Directory.CreateDirectory(path);
                if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
            }
            Store=new(Directory,Keys,Client,Protection,Clock,capacity);
        }
        internal OperationalSubjectObservation Proof(string subject="42")=>new(subject,Client,Clock.Now.AddSeconds(30),Clock,Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
        internal async Task<string> Issue(int lifetime=300)=>Assert.IsType<string>(await Store.IssueAsync(Proof(),new(ExpectedAccess,"initial.private.refresh",Clock.Now.AddSeconds(lifetime)),Binding));
        internal ProductOperationalCredentialStore Reopen()=>new(Directory,Keys,Client,Protection,Clock);
        internal string State(string handle)=>Path.Combine(Directory,ProductInvitationIntentStore.Hash(handle)+".credentials");
        internal Task<OperationalSubjectResult> Observe(string subject,string token,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();Observations++;Assert.Equal("42",subject);Assert.True(token==ExpectedAccess);
            return Task.FromResult(new OperationalSubjectResult(Person,Person==OperationalSubjectStatus.Verified?Proof():null));
        }
        internal Task<OperationalRefreshResult> Refresh(string token,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();Refreshes++;Assert.True(token=="initial.private.refresh");
            ExpectedAccess="rotated.private.access";
            return Task.FromResult(new OperationalRefreshResult(OperationalRefreshStatus.Refreshed,new(ExpectedAccess,"rotated.private.refresh",Clock.Now.AddMinutes(5))));
        }
        internal Task<OperationalCredentialResult> Read(string handle)=>Store.ObserveAsync(handle,Binding,Observe,Refresh);
    }
    [Fact]
    public async Task Credentials_are_encrypted_and_current_person_is_observed_on_every_read_without_refresh()
    {
        var f=new Fixture();var handle=await f.Issue();
        foreach(var file in Directory.GetFiles(f.Directory)){
            var bytes=await File.ReadAllBytesAsync(file);var text=System.Text.Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("initial.private",text);Assert.DoesNotContain(f.Binding,text);Assert.DoesNotContain(handle,text);
            if(!OperatingSystem.IsWindows())Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite,File.GetUnixFileMode(file));
        }
        Assert.Equal(OperationalCredentialStatus.Verified,(await f.Read(handle)).Status);
        Assert.Equal(OperationalCredentialStatus.Verified,(await f.Read(handle)).Status);
        Assert.Equal(2,f.Observations);Assert.Equal(0,f.Refreshes);
        var foreign=await f.Store.ObserveAsync(handle,ProductInvitationIntentStore.Random(),f.Observe,f.Refresh);
        Assert.Equal(OperationalCredentialStatus.ReauthenticationRequired,foreign.Status);Assert.Equal(2,f.Observations);
        await f.Store.RevokeAsync(handle,ProductInvitationIntentStore.Random());
        Assert.Equal(OperationalCredentialStatus.Verified,(await f.Read(handle)).Status);
    }
    [Fact]
    public async Task Rotation_publishes_once_and_reopened_store_uses_new_credentials_without_extending_absolute_lifetime()
    {
        var f=new Fixture();var handle=await f.Issue(10);var initial=await File.ReadAllBytesAsync(f.State(handle));
        Assert.Equal(OperationalCredentialStatus.Verified,(await f.Read(handle)).Status);Assert.Equal(1,f.Refreshes);
        var rotated=await File.ReadAllBytesAsync(f.State(handle));Assert.False(initial.SequenceEqual(rotated));
        Assert.Equal(OperationalCredentialStatus.Verified,(await f.Reopen().ObserveAsync(handle,f.Binding,f.Observe,f.Refresh)).Status);
        Assert.Equal(1,f.Refreshes);Assert.Equal(2,f.Observations);
        // Restoring an older ciphertext cannot roll the durable rotation fence back.
        await File.WriteAllBytesAsync(f.State(handle),initial);
        Assert.Equal(OperationalCredentialStatus.ReauthenticationRequired,(await f.Read(handle)).Status);Assert.Equal(1,f.Refreshes);
    }
    [Theory]
    [InlineData("unavailable")][InlineData("throw")][InlineData("cancel")][InlineData("reused-token")]
    public async Task Lost_or_invalid_rotation_never_reuses_the_old_refresh_after_restart(string failure)
    {
        var f=new Fixture();var handle=await f.Issue(1);var calls=0;
        using var stop=new CancellationTokenSource();
        async Task<OperationalRefreshResult> Fail(string _,CancellationToken ct){
            calls++;
            if(failure=="throw")throw new IOException();
            if(failure=="cancel"){await stop.CancelAsync();ct.ThrowIfCancellationRequested();}
            return new(failure=="reused-token"?OperationalRefreshStatus.Refreshed:OperationalRefreshStatus.Unavailable,
                failure=="reused-token"?new("new.private.access","initial.private.refresh",f.Clock.Now.AddMinutes(5)):null);
        }
        if(failure=="cancel")await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>f.Store.ObserveAsync(handle,f.Binding,f.Observe,Fail,stop.Token));
        else Assert.Equal(OperationalCredentialStatus.Unavailable,(await f.Store.ObserveAsync(handle,f.Binding,f.Observe,Fail)).Status);
        Assert.Equal(OperationalCredentialStatus.ReauthenticationRequired,(await f.Reopen().ObserveAsync(handle,f.Binding,f.Observe,Fail)).Status);
        Assert.Equal(1,calls);Assert.Equal(0,f.Observations);
    }
    [Fact]
    public async Task Revocation_wins_an_in_flight_rotation_and_competing_requests_do_not_refresh_twice()
    {
        var f=new Fixture();var handle=await f.Issue(1);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=f.Store.ObserveAsync(handle,f.Binding,f.Observe,async(token,ct)=>{entered.SetResult();await release.Task.WaitAsync(ct);return await f.Refresh(token,ct);});
        await entered.Task;
        try {
            Assert.Equal(OperationalCredentialStatus.Busy,(await f.Reopen().ObserveAsync(handle,f.Binding,f.Observe,f.Refresh)).Status);
            await f.Reopen().RevokeAsync(handle,f.Binding);
        }finally{release.TrySetResult();}
        Assert.Equal(OperationalCredentialStatus.Denied,(await pending).Status);Assert.Equal(0,f.Observations);Assert.Equal(1,f.Refreshes);
        Assert.Equal(OperationalCredentialStatus.Denied,(await f.Read(handle)).Status);Assert.Equal(1,f.Refreshes);
    }
    [Fact]
    public async Task Ordinary_person_observations_do_not_hold_rotation_lock_across_the_network()
    {
        var f=new Fixture();var handle=await f.Issue();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=f.Store.ObserveAsync(handle,f.Binding,async(s,t,ct)=>{entered.SetResult();await release.Task.WaitAsync(ct);return await f.Observe(s,t,ct);},f.Refresh);
        await entered.Task;
        try {Assert.Equal(OperationalCredentialStatus.Verified,(await f.Reopen().ObserveAsync(handle,f.Binding,f.Observe,f.Refresh)).Status);}
        finally{release.TrySetResult();}
        Assert.Equal(OperationalCredentialStatus.Verified,(await pending).Status);Assert.Equal(0,f.Refreshes);
    }
    [Fact]
    public async Task Person_denial_is_durable_but_transient_unavailability_never_returns_cached_proof()
    {
        var f=new Fixture();var handle=await f.Issue();Assert.Equal(OperationalCredentialStatus.Verified,(await f.Read(handle)).Status);
        f.Person=OperationalSubjectStatus.Unavailable;var outage=await f.Read(handle);Assert.Equal(OperationalCredentialStatus.Unavailable,outage.Status);Assert.Null(outage.Observation);
        f.Person=OperationalSubjectStatus.Verified;Assert.Equal(OperationalCredentialStatus.Verified,(await f.Read(handle)).Status);
        f.Person=OperationalSubjectStatus.Denied;Assert.Equal(OperationalCredentialStatus.Denied,(await f.Read(handle)).Status);
        f.Person=OperationalSubjectStatus.Verified;Assert.Equal(OperationalCredentialStatus.Denied,(await f.Reopen().ObserveAsync(handle,f.Binding,f.Observe,f.Refresh)).Status);
        Assert.Equal(4,f.Observations);
    }
    [Fact]
    public async Task Absolute_expiry_capacity_and_expired_cleanup_are_owned_and_do_not_reuse_live_lock_inodes()
    {
        var f=new Fixture(1);var handle=await f.Issue();
        Assert.Null(await f.Store.IssueAsync(f.Proof(),new("other.access","other.refresh",f.Clock.Now.AddMinutes(5)),f.Binding));
        Assert.Equal(0,f.Store.PurgeExpired());
        f.Clock.Advance(TimeSpan.FromHours(8));Assert.Equal(OperationalCredentialStatus.ReauthenticationRequired,(await f.Read(handle)).Status);
        f.Clock.Advance(TimeSpan.FromMinutes(16));Assert.Equal(1,f.Store.PurgeExpired());
        Assert.Equal(OperationalCredentialStatus.Denied,(await f.Read(handle)).Status);
        Assert.Empty(Directory.GetFiles(f.Directory,"*.credentials*"));
        Assert.NotNull(await f.Store.IssueAsync(f.Proof(),new("other.access","other.refresh",f.Clock.Now.AddMinutes(5)),f.Binding));
    }
    [Fact]
    public async Task Refresh_cannot_extend_absolute_deadline_or_the_original_proof_monotonic_limit()
    {
        var f=new Fixture();var handle=await f.Issue(1);var absolute=f.Clock.Now.AddHours(8);
        f.Clock.Advance(TimeSpan.FromHours(8)-TimeSpan.FromSeconds(10));
        var result=await f.Read(handle);Assert.Equal(OperationalCredentialStatus.Verified,result.Status);
        Assert.Equal(absolute,result.Observation!.ExpiresAt);Assert.True(result.Observation.IsCurrent);
        f.Clock.Advance(TimeSpan.FromSeconds(5));Assert.Equal(OperationalCredentialStatus.Verified,(await f.Read(handle)).Status);Assert.Equal(1,f.Refreshes);
        f.Clock.Advance(TimeSpan.FromSeconds(5));Assert.False(result.Observation.IsCurrent);
        Assert.Equal(OperationalCredentialStatus.ReauthenticationRequired,(await f.Read(handle)).Status);Assert.Equal(1,f.Refreshes);
    }
    [Fact]
    public async Task Expiry_cleanup_skips_an_owned_live_lock_and_reclaims_after_release()
    {
        var f=new Fixture();var handle=await f.Issue();f.Clock.Advance(TimeSpan.FromHours(9));
        using(var held=new FileStream(f.State(handle)+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(f.State(handle)+".lock",UnixFileMode.UserRead|UnixFileMode.UserWrite);
            Assert.Equal(0,f.Store.PurgeExpired());Assert.True(File.Exists(f.State(handle)));
        }
        Assert.Equal(1,f.Store.PurgeExpired());Assert.False(File.Exists(f.State(handle)));
    }
    [Theory]
    [InlineData("corrupt-state")][InlineData("corrupt-fence")][InlineData("missing-fence")][InlineData("clock-backwards")]
    public async Task Damaged_state_or_clock_regression_never_reaches_the_provider(string fault)
    {
        var f=new Fixture();var handle=await f.Issue();
        if(fault=="corrupt-state")await File.WriteAllTextAsync(f.State(handle),"invalid");
        if(fault=="corrupt-fence")await File.WriteAllTextAsync(f.State(handle)+".fence","invalid");
        if(fault=="missing-fence")File.Delete(f.State(handle)+".fence");
        if(fault=="clock-backwards")f.Clock.Now-=TimeSpan.FromSeconds(1);
        var result=await f.Read(handle);Assert.NotEqual(OperationalCredentialStatus.Verified,result.Status);Assert.Null(result.Observation);
        Assert.Equal(0,f.Refreshes);Assert.Equal(0,f.Observations);
    }
    [Fact]
    public async Task Different_key_or_client_cannot_recover_stored_credentials()
    {
        var f=new Fixture();var handle=await f.Issue();
        var wrongKey=new ProductOperationalCredentialStore(f.Directory,f.Keys,Client,new EphemeralDataProtectionProvider(),f.Clock);
        var wrongClient=new ProductOperationalCredentialStore(f.Directory,f.Keys,Guid.NewGuid().ToString("D"),f.Protection,f.Clock);
        foreach(var store in new[]{wrongKey,wrongClient}){
            var result=await store.ObserveAsync(handle,f.Binding,f.Observe,f.Refresh);Assert.NotEqual(OperationalCredentialStatus.Verified,result.Status);Assert.Null(result.Observation);
        }
        Assert.Equal(0,f.Refreshes);Assert.Equal(0,f.Observations);
    }
    [Fact]
    public void Deadline_constraint_cannot_extend_original_monotonic_observation()
    {
        var f=new Fixture();var proof=f.Proof();f.Clock.Advance(TimeSpan.FromSeconds(5));var constrained=proof.Constrain(f.Clock.Now.AddSeconds(2));
        Assert.True(constrained.IsCurrent);f.Clock.Advance(TimeSpan.FromSeconds(2));Assert.False(constrained.IsCurrent);
        f.Clock.Now-=TimeSpan.FromHours(1);Assert.False(constrained.IsCurrent);
        Assert.Equal(proof.ExpiresAt,proof.Constrain(proof.ExpiresAt.AddHours(1)).ExpiresAt);
    }
}
