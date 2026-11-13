using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class OperationalCredentialProcessFactAttribute:FactAttribute
{
    public OperationalCredentialProcessFactAttribute(){if(!OperatingSystem.IsLinux()
        || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUUBU_OPERATIONAL_CREDENTIAL_PROBE_DIRECTORY")))
            Skip="Requires an explicitly owned private Linux cross-process credential fixture.";}
}
public sealed class OperationalCredentialProcessTests
{
    [OperationalCredentialProcessFact]
    public async Task Separate_process_rotation_lock_and_logout_tombstone_prevent_resurrection()
    {
        var root=Environment.GetEnvironmentVariable("KUUBU_OPERATIONAL_CREDENTIAL_PROBE_DIRECTORY")!;
        Assert.True(Path.IsPathFullyQualified(root));Assert.Null(new DirectoryInfo(root).LinkTarget);
        Assert.Equal("kuubu-operational-credentials-process-v1",await File.ReadAllTextAsync(Path.Combine(root,"scope.txt")));
        var role=Environment.GetEnvironmentVariable("KUUBU_OPERATIONAL_CREDENTIAL_PROBE_ROLE");Assert.Contains(role,new[]{"holder","observer"});
        var keys=Path.Combine(root,"keys");var directory=Path.Combine(root,"credentials");
        var services=new ServiceCollection();services.AddLogging();services.AddDataProtection().SetApplicationName("Kuubu.ProductIdentity.v1").PersistKeysToFileSystem(new DirectoryInfo(keys));
        using var provider=services.BuildServiceProvider();var clock=TimeProvider.System;
        var store=new ProductOperationalCredentialStore(directory,keys,ProductOperationalCredentialTests.Client,provider.GetRequiredService<IDataProtectionProvider>(),clock);
        async Task Wait(string name,CancellationToken ct=default){
            var started=Stopwatch.StartNew();while(!File.Exists(Path.Combine(root,name)) && started.Elapsed<TimeSpan.FromSeconds(25))await Task.Delay(20,ct);
            Assert.True(File.Exists(Path.Combine(root,name)),"Owned credential probe barrier was not reached.");
        }
        var observations=0;var refreshes=0;
        Task<OperationalSubjectResult> Observe(string s,string t,CancellationToken ct){observations++;throw new InvalidOperationException("Revoked probe must not observe a person.");}
        if(role=="holder"){
            var binding=ProductInvitationIntentStore.Random();var proof=new OperationalSubjectObservation("42",ProductOperationalCredentialTests.Client,clock.GetUtcNow().AddSeconds(30),clock,clock.GetTimestamp(),TimeSpan.FromSeconds(30));
            var handle=await store.IssueAsync(proof,new("initial.private.access","initial.private.refresh",clock.GetUtcNow().AddSeconds(1)),binding);Assert.NotNull(handle);
            await File.WriteAllTextAsync(Path.Combine(root,"private-metadata.json"),JsonSerializer.Serialize(new{handle,binding}));
            var pending=store.ObserveAsync(handle,binding,Observe,async(_,ct)=>{
                refreshes++;await File.WriteAllTextAsync(Path.Combine(root,"rotation-held"),"",ct);await Wait("observer-done",ct);
                return new(OperationalRefreshStatus.Refreshed,new("rotated.private.access","rotated.private.refresh",clock.GetUtcNow().AddMinutes(5)));
            });
            Assert.Equal(OperationalCredentialStatus.Denied,(await pending).Status);Assert.Equal(1,refreshes);Assert.Equal(0,observations);
        }else{
            await Wait("rotation-held");
            using var metadata=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"private-metadata.json")));
            var handle=metadata.RootElement.GetProperty("handle").GetString()!;var binding=metadata.RootElement.GetProperty("binding").GetString()!;
            Task<OperationalRefreshResult> Refresh(string t,CancellationToken ct){refreshes++;throw new InvalidOperationException("Competing probe must not rotate.");}
            Assert.Equal(OperationalCredentialStatus.Busy,(await store.ObserveAsync(handle,binding,Observe,Refresh)).Status);
            await store.RevokeAsync(handle,binding);Assert.Equal(0,refreshes);Assert.Equal(0,observations);
            await File.WriteAllTextAsync(Path.Combine(root,"observer-done"),"");
        }
    }
}
