using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementOperationalPolicyCurrentPublicationTests
{
    [Theory]
    [InlineData("missing-registration")]
    [InlineData("unavailable-schema")]
    [InlineData("actual-non-postgres-provider")]
    public async Task New_use_cannot_skip_schema_controls_or_write_evidence(string kind)
    {
        var scope = Scope(); await using var db = Db(scope);
        var services = new ServiceCollection();
        if (kind == "unavailable-schema") services.AddSingleton<IProcurementOperationalPolicyIdentitySchemaReadiness>(new Readiness(false));
        if (kind == "actual-non-postgres-provider") services.AddSingleton<IProcurementOperationalPolicyIdentitySchemaReadiness, ProcurementOperationalPolicyIdentitySchemaReadiness>();
        using var provider = services.BuildServiceProvider();
        var error = await Assert.ThrowsAsync<OperationalPolicyPublicationProblem>(() =>
            ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, provider, Policy(scope), default));
        Assert.Equal(503, error.Status); Assert.Equal("OperationalPolicyIdentityUnavailable", error.Code);
        Assert.False(db.ChangeTracker.HasChanges()); Assert.Empty(await db.OperationalPolicyIdentityRecords.ToArrayAsync());
    }

    [Theory]
    [InlineData("RECEIVING")]
    [InlineData("INVOICE_MATCHING")]
    public async Task Published_status_and_ready_schema_cannot_adopt_missing_original_approval(string key)
    {
        var scope = Scope(); await using var db = Db(scope);
        using var provider = new ServiceCollection().AddSingleton<IProcurementOperationalPolicyIdentitySchemaReadiness>(new Readiness(true)).BuildServiceProvider();
        var policy = Policy(scope); policy.PolicyKey = key;
        var error = await Assert.ThrowsAsync<OperationalPolicyPublicationProblem>(() =>
            ProcurementOperationalPolicyCurrentPublication.RequireAsync(db, scope, provider, policy, default));
        Assert.Equal(409, error.Status); Assert.Equal("OperationalPolicyIdentityUnverified", error.Code);
        Assert.False(db.ChangeTracker.HasChanges()); Assert.Empty(await db.OperationalPolicyIdentityRecords.ToArrayAsync());
    }

    private static ProcurementRequestScope Scope()
    {
        var scope = new ProcurementRequestScope();
        scope.Set("publication-test", Guid.NewGuid(), Guid.NewGuid().ToString("D"), null, null, false, "test");
        return scope;
    }
    private static ProcurementDbContext Db(ProcurementRequestScope scope) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
    private static PolicyVersion Policy(ProcurementRequestScope scope) => new()
    {
        TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
        PolicyKey = "RECEIVING", PolicyVersionNumber = 1, State = "Published",
        RulesJson = "{}", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
    };
    // Test-only capability boundary; this does not certify PostgreSQL readiness.
    private sealed class Readiness(bool ready) : IProcurementOperationalPolicyIdentitySchemaReadiness
    {
        public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) => Task.FromResult(ready);
        public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => Task.FromResult(ready);
    }
}
