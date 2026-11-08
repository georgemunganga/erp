using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductInvitationIntentStoreTests
{
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564";
    private sealed class Clock:TimeProvider
    { internal DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class Fixture
    {
        internal readonly string Directory=Path.Combine(Path.GetTempPath(),"kuubu-invitation-intent-tests-"+Guid.NewGuid().ToString("N"));
        internal readonly Clock Clock=new();
        internal readonly IDataProtectionProvider Protection=new EphemeralDataProtectionProvider();
        internal ProductInvitationIntentStore Store{get;}
        internal Fixture(){System.IO.Directory.CreateDirectory(Directory);Store=new(Directory,Client,Protection,Clock);}
    }
    [Fact]
    public async Task Anonymous_intent_is_private_encrypted_bound_and_completed_only_once_without_extending_its_deadline()
    {
        var f=new Fixture();var token=ProductInvitationIntentStore.Random();var binding=ProductInvitationIntentStore.Random();
        var handle=await f.Store.PrepareAsync(token,binding,default);
        var path=Assert.Single(System.IO.Directory.GetFiles(f.Directory));var raw=await File.ReadAllTextAsync(path);
        foreach(var secret in new[]{token,binding,handle})Assert.DoesNotContain(secret,raw);
        Assert.DoesNotContain(handle,Path.GetFileName(path));
        var pending=await f.Store.ReadAsync(handle,binding,default);Assert.NotNull(pending);Assert.Null(pending.Subject);
        Assert.DoesNotContain(token,pending.ToString());
        Assert.Null(await f.Store.ReadAsync(handle,ProductInvitationIntentStore.Random(),default));
        f.Clock.Now+=TimeSpan.FromMinutes(2);
        var next=await f.Store.CompleteAsync(handle,binding,"43","synthetic.invitation.token",f.Clock.Now.AddMinutes(5),default);
        Assert.NotNull(next);Assert.NotEqual(handle,next);
        Assert.Null(await f.Store.ReadAsync(handle,binding,default));
        Assert.Null(await f.Store.CompleteAsync(handle,binding,"44","synthetic.other.token",f.Clock.Now.AddMinutes(1),default));
        var ready=await f.Store.ReadAsync(next,binding,default);Assert.NotNull(ready);
        Assert.Equal(pending.ExpiresAt,ready.ExpiresAt);Assert.Equal("43",ready.Subject);Assert.NotNull(ready.Csrf);
        Assert.DoesNotContain(ready.AccessToken!,await File.ReadAllTextAsync(Assert.Single(System.IO.Directory.GetFiles(f.Directory,"*.invitation"))));
        Assert.DoesNotContain(ready.AccessToken!,ready.ToString());
        f.Clock.Now=pending.ExpiresAt;Assert.Null(await f.Store.ReadAsync(next,binding,default));
    }
    [Fact]
    public async Task Concurrent_callback_claims_issue_at_most_one_authenticated_grant()
    {
        var f=new Fixture();
        for(var round=0;round<20;round++){
            var binding=ProductInvitationIntentStore.Random();
            var handle=await f.Store.PrepareAsync(ProductInvitationIntentStore.Random(),binding,default);
            var tasks=Enumerable.Range(0,8).Select(_=>f.Store.CompleteAsync(handle,binding,"43","synthetic.invitation.token",f.Clock.Now.AddMinutes(2),default));
            var handles=await Task.WhenAll(tasks);var winner=Assert.Single(handles,x=>x is not null);
            Assert.NotNull(await f.Store.ReadAsync(winner!,binding,default));
            Assert.Null(await f.Store.ReadAsync(handle,binding,default));
            Assert.Equal(round+1,System.IO.Directory.GetFiles(f.Directory,"*.claimed").Length);
            f.Store.Revoke(winner!);Assert.Null(await f.Store.ReadAsync(winner!,binding,default));
        }
    }
    [Fact]
    public async Task A_file_reader_cannot_turn_a_successful_callback_or_revocation_into_reusable_state()
    {
        var f=new Fixture();var binding=ProductInvitationIntentStore.Random();var handle=await f.Store.PrepareAsync(ProductInvitationIntentStore.Random(),binding,default);
        var path=Assert.Single(System.IO.Directory.GetFiles(f.Directory,"*.invitation"));
        await using var reader=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var next=await f.Store.CompleteAsync(handle,binding,"43","synthetic.invitation.token",f.Clock.Now.AddMinutes(2),default);
        Assert.NotNull(next);Assert.Null(await f.Store.ReadAsync(handle,binding,default));
        f.Store.Revoke(handle);Assert.Null(await f.Store.ReadAsync(next,binding,default));
    }
    [Theory]
    [InlineData("client")][InlineData("binding")][InlineData("handle")][InlineData("tamper")][InlineData("expired")]
    public async Task Swapped_tampered_foreign_or_expired_intents_are_unusable(string fault)
    {
        var f=new Fixture();var binding=ProductInvitationIntentStore.Random();var handle=await f.Store.PrepareAsync(ProductInvitationIntentStore.Random(),binding,default);
        var store=f.Store;
        if(fault=="client")store=new(f.Directory,Guid.NewGuid().ToString("D"),f.Protection,f.Clock);
        if(fault=="binding")binding=ProductInvitationIntentStore.Random();
        if(fault=="handle"){
            var old=Path.Combine(f.Directory,ProductInvitationIntentStore.Hash(handle)+".invitation");handle=ProductInvitationIntentStore.Random();
            File.Move(old,Path.Combine(f.Directory,ProductInvitationIntentStore.Hash(handle)+".invitation"));
        }
        if(fault=="tamper")await File.WriteAllTextAsync(Assert.Single(System.IO.Directory.GetFiles(f.Directory)),"tampered");
        if(fault=="expired")f.Clock.Now+=TimeSpan.FromMinutes(5);
        Assert.Null(await store.ReadAsync(handle,binding,default));
        Assert.Null(await store.CompleteAsync(handle,binding,"43","synthetic.invitation.token",f.Clock.Now.AddMinutes(1),default));
    }
    [Theory]
    [InlineData("../bad")][InlineData("")][InlineData("short")]
    public async Task Malformed_secrets_never_create_files(string token)
    {
        var f=new Fixture();await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Store.PrepareAsync(token,ProductInvitationIntentStore.Random(),default));
        Assert.Empty(System.IO.Directory.GetFiles(f.Directory));
    }
    [Fact]
    public async Task Revocation_and_cleanup_touch_only_owned_ephemeral_intents()
    {
        var f=new Fixture();var binding=ProductInvitationIntentStore.Random();var handle=await f.Store.PrepareAsync(ProductInvitationIntentStore.Random(),binding,default);
        f.Store.Revoke(handle);f.Store.Revoke(handle);Assert.Null(await f.Store.ReadAsync(handle,binding,default));
        await f.Store.PrepareAsync(ProductInvitationIntentStore.Random(),binding,default);
        var path=Assert.Single(System.IO.Directory.GetFiles(f.Directory,"*.invitation"));File.SetLastWriteTimeUtc(path,f.Clock.Now.AddMinutes(-11).UtcDateTime);
        var note=Path.Combine(f.Directory,"retain.txt");await File.WriteAllTextAsync(note,"retain");
        f.Store.Purge();Assert.False(File.Exists(path));Assert.True(File.Exists(note));
    }
    [Fact]
    public async Task Cancelling_the_original_intent_also_invalidates_a_callback_grant_already_issued_from_it()
    {
        var f=new Fixture();var binding=ProductInvitationIntentStore.Random();
        var handle=await f.Store.PrepareAsync(ProductInvitationIntentStore.Random(),binding,default);
        var next=await f.Store.CompleteAsync(handle,binding,"43","synthetic.invitation.token",f.Clock.Now.AddMinutes(2),default);
        Assert.NotNull(next);Assert.NotNull(await f.Store.ReadAsync(next,binding,default));
        f.Store.Revoke(handle);Assert.Null(await f.Store.ReadAsync(next,binding,default));
        Assert.Null(await f.Store.CompleteAsync(handle,binding,"43","synthetic.invitation.token",f.Clock.Now.AddMinutes(2),default));
    }
}
