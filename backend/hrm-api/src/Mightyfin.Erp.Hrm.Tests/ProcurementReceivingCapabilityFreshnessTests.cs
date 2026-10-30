using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Receiving_capability_rejects_unrelated_permissions_before_native_readiness_and_rechecks_revocation()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        var branches = await fixture.SeedAllRequestBranches();
        var readiness = new FreshReceivingReadiness();
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-invoice-read");
        Assert.False(await fixture.ReceivingCapability(branches.First, readiness));
        Assert.Equal(0, readiness.Calls);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-receipt-view");
        Assert.True(await fixture.ReceivingCapability(branches.First, readiness));
        Assert.Equal(1, readiness.Calls);
        // Native ACL/schema readiness is deliberately not a cached model result.
        readiness.Ready = false;
        Assert.False(await fixture.ReceivingCapability(branches.First, readiness));
        Assert.Equal(2, readiness.Calls);
        readiness.Ready = true;
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access");
        Assert.False(await fixture.ReceivingCapability(branches.First, readiness));
        Assert.Equal(2, readiness.Calls);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-receipt-view");
        await fixture.DeactivatePrincipalAccount(fixture.Owner);
        Assert.False(await fixture.ReceivingCapability(branches.First, readiness));
        Assert.Equal(2, readiness.Calls);
    }

    [Fact]
    public async Task Receiving_capability_checks_current_branch_before_native_probe_without_requiring_a_worker()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        var branches = await fixture.SeedAllRequestBranches();
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-receipt-view");
        var readiness = new FreshReceivingReadiness();
        Assert.True(await fixture.ReceivingCapability(branches.First, readiness));
        Assert.False(await fixture.ReceivingCapability(Guid.NewGuid(), readiness));
        Assert.Equal(1, readiness.Calls);
        // The read profile remains available when new receiving work is disabled.
        Assert.True(await fixture.ReceivingCapability(branches.Second, readiness));
        Assert.Equal(2, readiness.Calls);
    }

    private sealed class FreshReceivingReadiness : IProcurementPrincipalReceivingReadiness
    {
        internal int Calls;
        internal bool Ready = true;
        public Task<bool> IsReadyAsync(CancellationToken ct)
        { Calls++; return Task.FromResult(Ready); }
        public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct) =>
            throw new InvalidOperationException("Capability must use the configured installed read profile");
    }

    private sealed partial class Fixture
    {
        public async Task<bool> ReceivingCapability(Guid branch, FreshReceivingReadiness readiness)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            // Borrow the fixture's current owning Core store; do not fabricate
            // permission/person projections or replace the production resolver.
            var provider = new ReceivingCapabilityServices(core, readiness);
            var scope = new ProcurementRequestScope();
            scope.Set("tenant-a", company, Owner.ToString("D"), branch, null, true, "receiving-freshness");
            scope.AssignPrincipal(Owner);
            return await ProcurementPrincipalReceivingCapability.ReadReadyAsync(provider, scope, false, default);
        }
    }

    private sealed class ReceivingCapabilityServices(HrmDbContext core, FreshReceivingReadiness readiness) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(HrmDbContext) ? core
            : type == typeof(IProcurementPrincipalReceivingReadiness) ? readiness
            : type == typeof(ProcurementPrincipalReceivingOptions)
                ? new ProcurementPrincipalReceivingOptions { Enabled = false, ExecutionDatabaseConfigured = true }
            : null;
    }
}
