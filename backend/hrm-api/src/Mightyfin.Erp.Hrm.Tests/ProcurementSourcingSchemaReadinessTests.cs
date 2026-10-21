using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Negative provider/unit contracts only. No fake positive PostgreSQL readiness.</summary>
public sealed class ProcurementSourcingSchemaReadinessTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task NonPostgres_owning_context_never_advertises_read_or_write_and_never_uses_Finance_override(bool write)
    {
        var scope = new ProcurementRequestScope(); scope.Set("rfq-schema", Guid.NewGuid(), "buyer", null, null, false, "test");
        await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var finance = new UnreachableFinance();
        var reader = new ProcurementSourcingDraftSchemaReadiness(new ConfigurationBuilder().Build(), scope, finance);
        Assert.False(await reader.IsReadyAsync(owner, write, default));
        Assert.Equal(0, finance.Calls); Assert.False(owner.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Metadata_requires_explicit_execution_store_never_normal_host_connection()
    {
        var scope = new ProcurementRequestScope(); scope.Set("rfq-schema", Guid.NewGuid(), "buyer", null, null, false, "test");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "Host=never-used.invalid;Database=normal-host" }).Build();
        var finance = new UnreachableFinance();
        Assert.False(await new ProcurementSourcingDraftSchemaReadiness(config, scope, finance).IsReadyAsync(default));
        Assert.Equal(0, finance.Calls);
    }

    [Fact]
    public async Task Old_metadata_only_fake_cannot_authorize_coordinator_owner_context()
    {
        var scope = new ProcurementRequestScope(); scope.Set("rfq-schema", Guid.NewGuid(), "buyer", null, null, false, "test");
        await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        IProcurementSourcingDraftSchemaReadiness fake = new MetadataOnlyFake();
        Assert.True(await fake.IsReadyAsync(default)); // Explicit fake-only control, not production availability.
        Assert.False(await fake.IsReadyAsync(owner, true, default));
    }

    [Fact]
    public void Write_privilege_contract_requires_original_request_UPDATE_but_historical_read_does_not()
    {
        Assert.Contains("has_table_privilege(current_user,'procurement.purchase_requests','UPDATE')",
            ProcurementSourcingDraftSchemaReadiness.PrivilegeSql(true));
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.purchase_requests','UPDATE')",
            ProcurementSourcingDraftSchemaReadiness.PrivilegeSql(false));
    }

    private sealed class MetadataOnlyFake : IProcurementSourcingDraftSchemaReadiness
    { public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(true); }
    private sealed class UnreachableFinance : IProcurementFinanceCheckerSchemaReadiness
    {
        public int Calls { get; private set; }
        public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool capture, CancellationToken ct)
        { Calls++; throw new InvalidOperationException("Must not substitute fake Finance readiness for actual RFQ store"); }
    }
}
