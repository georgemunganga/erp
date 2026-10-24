using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class PrincipalOrderRevisionCapabilityTests
{
    private sealed class Ready(bool ready) : IPrincipalOrderRevisionReadiness
    {
        public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct)
            => Task.FromResult(ready);
    }

    [Fact]
    public async Task Revision_capability_is_off_without_actual_option_provider_and_prior_draft_readiness()
    {
        using var absent = new ServiceCollection().BuildServiceProvider();
        Assert.False(await PrincipalOrderRevisionCapability.ReadyAsync(absent, null!, false, true,
            CancellationToken.None));
        using var disabled = new ServiceCollection().AddSingleton(new PrincipalOrderRevisionOptions())
            .AddSingleton<IPrincipalOrderRevisionReadiness>(new Ready(true)).BuildServiceProvider();
        Assert.False(await PrincipalOrderRevisionCapability.ReadyAsync(disabled, null!, false, true,
            CancellationToken.None));
        using var missingStore = new ServiceCollection()
            .AddSingleton(new PrincipalOrderRevisionOptions { Enabled = true }).BuildServiceProvider();
        Assert.False(await PrincipalOrderRevisionCapability.ReadyAsync(missingStore, null!, false, true,
            CancellationToken.None));
        using var ready = new ServiceCollection().AddSingleton(new PrincipalOrderRevisionOptions { Enabled = true })
            .AddSingleton<IPrincipalOrderRevisionReadiness>(new Ready(true)).BuildServiceProvider();
        Assert.False(await PrincipalOrderRevisionCapability.ReadyAsync(ready, null!, true, true,
            CancellationToken.None));
        Assert.False(await PrincipalOrderRevisionCapability.ReadyAsync(ready, null!, false, false,
            CancellationToken.None));
        Assert.True(await PrincipalOrderRevisionCapability.ReadyAsync(ready, null!, false, true,
            CancellationToken.None));
    }

    [Fact]
    public void Only_an_unfunded_unissued_principal_draft_is_a_record_edit_candidate()
    {
        var order = new PurchaseOrder { PrincipalDraftSchemaVersion = 2, Status = "Draft",
            CurrentRevision = 0, OriginalDirectSourceSha256 = new string('a', 64) };
        Assert.True(PrincipalOrderRevisionCapability.RetainedDraftEligible(order, false, false, false, false));
        Assert.False(PrincipalOrderRevisionCapability.RetainedDraftEligible(order, true, false, false, false));
        Assert.False(PrincipalOrderRevisionCapability.RetainedDraftEligible(order, false, true, false, false));
        Assert.False(PrincipalOrderRevisionCapability.RetainedDraftEligible(order, false, false, true, false));
        Assert.False(PrincipalOrderRevisionCapability.RetainedDraftEligible(order, false, false, false, true));
        order.Status = "Approved";
        Assert.False(PrincipalOrderRevisionCapability.RetainedDraftEligible(order, false, false, false, false));
    }
}
