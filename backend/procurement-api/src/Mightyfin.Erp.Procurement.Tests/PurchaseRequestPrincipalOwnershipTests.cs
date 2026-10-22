using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseRequestPrincipalOwnershipTests
{
    [Fact]
    public void Explicit_principal_owns_without_employee_and_cannot_be_overridden_by_creator_or_worker_alias()
    {
        var owner = Guid.NewGuid(); var other = Guid.NewGuid();
        var row = new PurchaseRequest { RequesterPrincipalId = owner, RequesterWorkerId = null,
            CreatedBy = other.ToString("D") };
        Assert.True(PurchaseRequestOwnership.IsOwner(row, owner));
        Assert.False(PurchaseRequestOwnership.IsOwner(row, other));
        row.RequesterWorkerId = other; // Genuine worker reference is still not an ownership grant.
        Assert.True(PurchaseRequestOwnership.IsOwner(row, owner));
        Assert.False(PurchaseRequestOwnership.IsOwner(row, other));
        Assert.Single(new[] { row }.AsQueryable().Where(PurchaseRequestOwnership.OwnedBy(owner)));
        Assert.Empty(new[] { row }.AsQueryable().Where(PurchaseRequestOwnership.OwnedBy(other)));
    }

    [Fact]
    public void Legacy_ownership_uses_only_exact_canonical_creator_and_does_not_backfill_identity()
    {
        var owner = Guid.NewGuid(); var worker = Guid.NewGuid();
        var legacy = new PurchaseRequest { RequesterPrincipalId = null, RequesterWorkerId = worker,
            CreatedBy = owner.ToString("D") };
        Assert.True(PurchaseRequestOwnership.IsOwner(legacy, owner));
        Assert.False(PurchaseRequestOwnership.IsOwner(legacy, worker));
        Assert.Null(legacy.RequesterPrincipalId); Assert.Equal(worker, legacy.RequesterWorkerId);
        legacy.CreatedBy = owner.ToString("N");
        Assert.False(PurchaseRequestOwnership.IsOwner(legacy, owner));
        legacy.CreatedBy = "unknown-imported-user";
        Assert.False(PurchaseRequestOwnership.IsOwner(legacy, owner));
        Assert.False(PurchaseRequestOwnership.IsOwner(legacy, null));
        Assert.False(PurchaseRequestOwnership.IsOwner(legacy, Guid.Empty));
    }

    [Theory]
    [InlineData("principal")][InlineData("worker")][InlineData("creator")]
    public async Task Persisted_request_identity_cannot_be_reassigned_or_legacy_backfilled(string field)
    {
        var scope = new Scope();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var row = new PurchaseRequest { Number = "PR-OWN", CurrencyCode = "ZMW", Purpose = "Own need",
            RequesterPrincipalId = field == "principal" ? null : scope.Principal,
            RequesterWorkerId = field == "principal" ? Guid.NewGuid() : null };
        db.AddScoped(row); await db.SaveChangesAsync();
        var originalCreator = row.CreatedBy;
        if (field == "principal") row.RequesterPrincipalId = scope.Principal;
        if (field == "worker") row.RequesterWorkerId = Guid.NewGuid();
        if (field == "creator") row.CreatedBy = Guid.NewGuid().ToString("D");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("PurchaseRequestIdentityImmutable", error.Message);
        Assert.NotNull(originalCreator);
    }

    [Fact]
    public async Task Missing_both_requester_references_is_rejected_without_records_or_audit()
    {
        var scope = new Scope();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        db.AddScoped(new PurchaseRequest { Number = "PR-NO-OWNER", CurrencyCode = "ZMW", Purpose = "Unattributed need" });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("PurchaseRequestIdentityInvalid", error.Message);
        Assert.Empty(await db.PurchaseRequests.AsNoTracking().ToListAsync());
        Assert.Empty(await db.AuditEvents.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("principal")][InlineData("worker")][InlineData("creator")]
    public async Task Detached_graph_cannot_forge_original_identity_even_with_current_CAS_version(string field)
    {
        var scope = new Scope();
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        PurchaseRequest original;
        await using (var seed = new ProcurementDbContext(options, scope))
        {
            original = new PurchaseRequest { Number = "PR-DETACHED", CurrencyCode = "ZMW", Purpose = "Retained need",
                RequesterPrincipalId = scope.Principal, RequesterWorkerId = Guid.NewGuid() };
            seed.AddScoped(original); await seed.SaveChangesAsync();
        }
        var before = await Snapshot(options, scope);
        // Simulate a detached DTO mapper whose forged values become both EF's current
        // and original values. Only a real owning-store read can detect the substitution.
        var detached = JsonSerializer.Deserialize<PurchaseRequest>(JsonSerializer.Serialize(original))!;
        if (field == "principal") detached.RequesterPrincipalId = Guid.NewGuid();
        if (field == "worker") detached.RequesterWorkerId = Guid.NewGuid();
        if (field == "creator") detached.CreatedBy = Guid.NewGuid().ToString("D");
        detached.Purpose = "Forged detached change";
        await using (var writer = new ProcurementDbContext(options, scope))
        {
            writer.Attach(detached); writer.Entry(detached).State = EntityState.Modified;
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            Assert.Equal("PurchaseRequestIdentityImmutable", error.Message);
        }
        Assert.Equal(before, await Snapshot(options, scope));
    }

    [Theory]
    [InlineData("stale")][InlineData("foreign-company")]
    public async Task Detached_request_requires_real_current_scoped_version_before_any_audit(string mutation)
    {
        var scope = new Scope();
        var options = new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        PurchaseRequest original;
        await using (var seed = new ProcurementDbContext(options, scope))
        {
            original = new PurchaseRequest { Number = "PR-CAS", CurrencyCode = "ZMW", Purpose = "Retained need",
                RequesterPrincipalId = scope.Principal };
            seed.AddScoped(original); await seed.SaveChangesAsync();
        }
        var before = await Snapshot(options, scope);
        var detached = JsonSerializer.Deserialize<PurchaseRequest>(JsonSerializer.Serialize(original))!;
        if (mutation == "stale") detached.Version = 0;
        var writerScope = mutation == "foreign-company" ? new Scope(scope.Principal) : scope;
        detached.TenantId = writerScope.TenantId; detached.LegalEntityId = writerScope.LegalEntityId;
        detached.Purpose = "Denied detached update";
        await using (var writer = new ProcurementDbContext(options, writerScope))
        {
            writer.Attach(detached); writer.Entry(detached).State = EntityState.Modified;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => writer.SaveChangesAsync());
        }
        Assert.Equal(before, await Snapshot(options, scope));
    }

    private static async Task<string> Snapshot(DbContextOptions<ProcurementDbContext> options, Scope scope)
    {
        await using var proof = new ProcurementDbContext(options, scope);
        return JsonSerializer.Serialize(new {
            requests = await proof.PurchaseRequests.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            lines = await proof.PurchaseRequestLines.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            audits = await proof.AuditEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            outbox = await proof.OutboxEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()
        });
    }

    [Theory]
    [InlineData("approval")][InlineData("finance")]
    public async Task Old_employee_evidence_cannot_be_attached_to_a_principal_owned_request(string kind)
    {
        var scope = new Scope();
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var request = new PurchaseRequest { Number = "PR-PRINCIPAL", CurrencyCode = "ZMW", Purpose = "Own need",
            RequesterPrincipalId = scope.Principal, RequesterWorkerId = null };
        db.AddScoped(request); await db.SaveChangesAsync();
        if (kind == "approval")
            db.AddScoped(new PurchaseRequestApprovalSubmission { TenantId = scope.TenantId,
                LegalEntityId = scope.LegalEntityId, PurchaseRequestId = request.Id });
        else db.AddScoped(new BudgetCheckSnapshot { PurchaseRequestId = request.Id });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("PrincipalOperationalEvidenceRequired", error.Message);
    }

    [Fact]
    public void Model_has_nullable_distinct_identity_fields_and_scoped_principal_index()
    {
        var scope = new Scope();
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=not-opened;Username=not-opened").Options, scope);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PurchaseRequest))!;
        Assert.True(entity.FindProperty(nameof(PurchaseRequest.RequesterPrincipalId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(PurchaseRequest.RequesterWorkerId))!.IsNullable);
        Assert.Contains(entity.GetIndexes(), x => x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "LegalEntityId", "RequesterPrincipalId", "Status" }));
        Assert.Contains(entity.GetCheckConstraints(), x => x.Name == "ck_purchase_request_identity"
            && x.Sql.Contains("requester_principal_id IS NULL", StringComparison.Ordinal)
            && x.Sql.Contains("requester_worker_id IS NULL", StringComparison.Ordinal));
        Assert.DoesNotContain(entity.GetForeignKeys(), x => x.PrincipalEntityType.GetSchema() != "procurement");
    }

    private sealed class Scope(Guid? principal = null) : IProcurementScope
    {
        public Guid Principal { get; } = principal ?? Guid.NewGuid();
        public string TenantId => "principal-ownership-test";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId => Principal.ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "test";
    }
}
