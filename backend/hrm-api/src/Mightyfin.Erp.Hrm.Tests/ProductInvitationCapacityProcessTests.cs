using Microsoft.AspNetCore.DataProtection;
using Mightyfin.Erp.Hrm.Api;
using System.Diagnostics;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class InvitationCapacityProcessFactAttribute:FactAttribute
{
    public InvitationCapacityProcessFactAttribute()
    {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_INVITATION_CAPACITY_PROBE_DIRECTORY")))
            Skip="Requires an explicitly owned private cross-process capacity fixture.";
    }
}

public sealed class ProductInvitationCapacityProcessTests
{
    [InvitationCapacityProcessFact]
    public async Task Separate_processes_cannot_overbook_the_same_private_directory()
    {
        var path=Environment.GetEnvironmentVariable("KUUBU_INVITATION_CAPACITY_PROBE_DIRECTORY")!;
        Assert.True(Path.IsPathFullyQualified(path));
        Assert.True(Directory.Exists(path));Assert.Null(new DirectoryInfo(path).LinkTarget);
        Assert.Equal("kuubu-invitation-capacity-process-v1",await File.ReadAllTextAsync(Path.Combine(path,"scope.txt")));
        if(!OperatingSystem.IsWindows())Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute,File.GetUnixFileMode(path));
        var store=new ProductInvitationIntentStore(path,"391bdeaa-87b3-44b3-8e64-2110dcc1a564",
            new EphemeralDataProtectionProvider(),TimeProvider.System,12);
        async Task Wait(string marker) {
            var timer=Stopwatch.StartNew();
            while(!File.Exists(Path.Combine(path,marker)) && timer.Elapsed<TimeSpan.FromSeconds(30))await Task.Delay(25);
            Assert.True(File.Exists(Path.Combine(path,marker)),"Competing capacity process did not reach its barrier.");
        }
        var role=Environment.GetEnvironmentVariable("KUUBU_INVITATION_CAPACITY_PROBE_ROLE");
        Assert.Contains(role,new[]{"holder","writer"});
        if(role=="holder") {
            var options=new FileStreamOptions{Mode=FileMode.OpenOrCreate,Access=FileAccess.ReadWrite,Share=FileShare.None};
            if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            using(var held=new FileStream(Path.Combine(path,".invitation-capacity.lock"),options)) {
                await File.WriteAllTextAsync(Path.Combine(path,"holder-ready"),"");await Wait("observer-done");
            }
            await File.WriteAllTextAsync(Path.Combine(path,"released"),"");
        } else {
            await Wait("holder-ready");
            await Assert.ThrowsAsync<IOException>(()=>store.PrepareAsync(ProductInvitationIntentStore.Random(),ProductInvitationIntentStore.Random(),default));
            Assert.Empty(Directory.GetFiles(path,"*.capacity"));
            await File.WriteAllTextAsync(Path.Combine(path,"observer-done"),"");await Wait("released");
        }
        for(var i=0;i<25;i++) {
            try {await store.PrepareAsync(ProductInvitationIntentStore.Random(),ProductInvitationIntentStore.Random(),default);}
            catch(IOException) { /* Expected: capacity or another process owns the lock. */ }
        }
        Assert.InRange(Directory.GetFiles(path,"*.capacity").Length,1,6);
    }
}
