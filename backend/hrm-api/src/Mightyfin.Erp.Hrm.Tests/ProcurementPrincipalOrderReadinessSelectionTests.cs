using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderReadinessSelectionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Composed_governance_proof_is_selected_at_both_stages_without_parent_fallback(
        bool ready, bool write)
    {
        var drafts = new DraftProof();
        var governance = new GovernanceProof(ready);
        await using var owner = Owner();
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(ready, await ProcurementPrincipalOrderDraftRoutes.SelectedStoreReadyAsync(
            drafts, governance, null, false, cancellation.Token));
        Assert.Equal(ready, await ProcurementPrincipalOrderDraftRoutes.SelectedStoreReadyAsync(
            drafts, governance, owner, write, cancellation.Token));
        Assert.Equal(0, drafts.Calls);
        Assert.Equal(1, governance.ConfiguredCalls);
        var call = Assert.Single(governance.TransactionCalls);
        Assert.Same(owner, call.Owner);
        Assert.Equal(write, call.Write);
        Assert.Equal(cancellation.Token, call.Token);
        Assert.Equal(cancellation.Token, governance.ConfiguredToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Draft_only_routes_keep_parent_checks_at_both_stages(bool write)
    {
        var drafts = new DraftProof();
        await using var owner = Owner();
        Assert.True(await ProcurementPrincipalOrderDraftRoutes.SelectedStoreReadyAsync(
            drafts, null, null, false, default));
        Assert.True(await ProcurementPrincipalOrderDraftRoutes.SelectedStoreReadyAsync(
            drafts, null, owner, write, default));
        Assert.Equal(2, drafts.Calls);
        Assert.Same(owner, drafts.Owner);
        Assert.Equal(write, drafts.Write);
    }

    [Fact]
    public async Task Successful_configured_proof_does_not_cache_transaction_or_retry_results()
    {
        var drafts = new DraftProof();
        var governance = new GovernanceProof(true);
        await using var owner = Owner();
        Assert.True(await ProcurementPrincipalOrderDraftRoutes.SelectedStoreReadyAsync(
            drafts, governance, null, false, default));
        governance.Ready = false;
        Assert.False(await ProcurementPrincipalOrderDraftRoutes.SelectedStoreReadyAsync(
            drafts, governance, owner, true, default));
        Assert.False(await ProcurementPrincipalOrderDraftRoutes.SelectedStoreReadyAsync(
            drafts, governance, owner, true, default));
        Assert.Equal(2, governance.TransactionCalls.Count);
        Assert.Equal(0, drafts.Calls);
    }

    private static ProcurementDbContext Owner() => new(
        new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new ProcurementRequestScope());

    private sealed class DraftProof : IProcurementPrincipalOrderDraftReadiness
    {
        public int Calls;
        public ProcurementDbContext? Owner;
        public bool Write;
        public Task<bool> IsReadyAsync(CancellationToken ct) { Calls++; return Task.FromResult(true); }
        public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
        { Calls++; Owner = owner; Write = write; return Task.FromResult(true); }
    }

    private sealed class GovernanceProof(bool ready) : IProcurementPrincipalOrderGovernanceReadiness
    {
        public bool Ready = ready;
        public int ConfiguredCalls;
        public CancellationToken ConfiguredToken;
        public List<(ProcurementDbContext Owner, bool Write, CancellationToken Token)> TransactionCalls = [];
        public Task<bool> IsReadyAsync(CancellationToken ct)
        { ConfiguredCalls++; ConfiguredToken = ct; return Task.FromResult(Ready); }
        public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
        { TransactionCalls.Add((owner, write, ct)); return Task.FromResult(Ready); }
    }
}
