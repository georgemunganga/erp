using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class RecurringPurchasePrincipalIdentityTests
{
    [Fact]
    public async Task Principal_without_worker_is_a_real_owner_and_unknown_owner_cannot_be_inserted()
    {
        var scope = new Scope();
        await using var db = new ProcurementDbContext(Options(), scope);
        var row = Record(scope);
        db.AddScoped(row); await db.SaveChangesAsync();
        Assert.Equal(scope.Principal, row.OwnerPrincipalId); Assert.Null(row.OwnerWorkerId);
        Assert.Equal(scope.SubjectId, row.CreatedBy);
        var forged = Record(scope); forged.OwnerPrincipalId = Guid.NewGuid(); forged.OwnerSubjectId = forged.OwnerPrincipalId.ToString("D");
        db.AddScoped(forged);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal("RecurringPurchaseIdentityScopeMismatch", error.Message);
    }

    [Theory]
    [InlineData("principal")][InlineData("worker")][InlineData("creator")][InlineData("branch")]
    public async Task Detached_identity_and_branch_reassignment_are_denied_against_persisted_owner(string field)
    {
        var scope = new Scope(); var options = Options(); RecurringPurchaseCommitment original;
        await using (var seed = new ProcurementDbContext(options, scope))
        { original = Record(scope); seed.AddScoped(original); await seed.SaveChangesAsync(); }
        var before = await State(options, scope);
        var detached = JsonSerializer.Deserialize<RecurringPurchaseCommitment>(JsonSerializer.Serialize(original))!;
        if (field == "principal") { detached.OwnerPrincipalId = Guid.NewGuid(); detached.OwnerSubjectId = detached.OwnerPrincipalId.ToString("D"); }
        if (field == "worker") detached.OwnerWorkerId = Guid.NewGuid();
        if (field == "creator") detached.CreatedBy = Guid.NewGuid().ToString("D");
        if (field == "branch") detached.WorkLocationId = Guid.NewGuid();
        detached.Title = "Denied detached change";
        await using (var writer = new ProcurementDbContext(options, scope))
        {
            writer.Attach(detached); writer.Entry(detached).State = EntityState.Modified;
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            Assert.Equal("RecurringPurchaseIdentityImmutable", error.Message);
        }
        Assert.Equal(before, await State(options, scope));
    }

    [Theory]
    [InlineData("stale")][InlineData("foreign-company")]
    public async Task Detached_recurring_update_requires_current_scoped_CAS(string mutation)
    {
        var scope = new Scope(); var options = Options(); RecurringPurchaseCommitment original;
        await using (var seed = new ProcurementDbContext(options, scope))
        { original = Record(scope); seed.AddScoped(original); await seed.SaveChangesAsync(); }
        var before = await State(options, scope);
        var detached = JsonSerializer.Deserialize<RecurringPurchaseCommitment>(JsonSerializer.Serialize(original))!;
        var writerScope = mutation == "foreign-company" ? new Scope(scope.Principal) : scope;
        if (mutation == "stale") detached.Version = 0;
        detached.TenantId = writerScope.TenantId; detached.LegalEntityId = writerScope.LegalEntityId;
        await using (var writer = new ProcurementDbContext(options, writerScope))
        {
            writer.Attach(detached); writer.Entry(detached).State = EntityState.Modified;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => writer.SaveChangesAsync());
        }
        Assert.Equal(before, await State(options, scope));
    }

    [Fact]
    public async Task Another_actor_in_the_same_company_cannot_modify_the_persisted_owners_plan()
    {
        var owner = new Scope(); var options = Options(); RecurringPurchaseCommitment original;
        await using (var seed = new ProcurementDbContext(options, owner))
        { original = Record(owner); seed.AddScoped(original); await seed.SaveChangesAsync(); }
        var before = await State(options, owner);
        var other = new Scope(Guid.NewGuid(), owner.LegalEntityId);
        var detached = JsonSerializer.Deserialize<RecurringPurchaseCommitment>(JsonSerializer.Serialize(original))!;
        detached.Title = "Denied other-account edit";
        await using (var writer = new ProcurementDbContext(options, other))
        {
            writer.Attach(detached); writer.Entry(detached).State = EntityState.Modified;
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            Assert.Equal("RecurringPurchaseOwnerScopeMismatch", error.Message);
        }
        Assert.Equal(before, await State(options, owner));
    }

    [Theory]
    [InlineData("loaded-title")][InlineData("detached-amount")][InlineData("detached-reopen")]
    public async Task Persisted_closed_history_cannot_be_changed_or_reopened(string mutation)
    {
        var scope = new Scope(); var options = Options(); RecurringPurchaseCommitment original;
        await using (var seed = new ProcurementDbContext(options, scope))
        {
            original = Record(scope); seed.AddScoped(original); await seed.SaveChangesAsync();
            original.Status = "Closed"; original.ClosedAt = DateTimeOffset.UtcNow;
            original.ClosedReason = "Synthetic planning complete"; await seed.SaveChangesAsync();
            Assert.Equal(2, original.Version);
        }
        var before = await State(options, scope);
        await using (var writer = new ProcurementDbContext(options, scope))
        {
            RecurringPurchaseCommitment changed;
            if (mutation == "loaded-title")
            {
                changed = await writer.RecurringPurchaseCommitments.SingleAsync(x => x.Id == original.Id);
                changed.Title = "Denied historical edit";
            }
            else
            {
                changed = JsonSerializer.Deserialize<RecurringPurchaseCommitment>(JsonSerializer.Serialize(original))!;
                if (mutation == "detached-amount") changed.ExpectedAmount = 99.9999m;
                else { changed.Status = "Draft"; changed.ClosedAt = null; changed.ClosedReason = null; }
                writer.Attach(changed); writer.Entry(changed).State = EntityState.Modified;
            }
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            Assert.Equal("RecurringPurchaseClosedImmutable", error.Message);
        }
        Assert.Equal(before, await State(options, scope));
    }

    [Fact]
    public void Recurring_model_has_required_principal_optional_worker_and_principal_schedule_index()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=not-opened;Username=not-opened").Options, new Scope());
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(RecurringPurchaseCommitment))!;
        Assert.False(entity.FindProperty(nameof(RecurringPurchaseCommitment.OwnerPrincipalId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(RecurringPurchaseCommitment.OwnerWorkerId))!.IsNullable);
        Assert.Contains(entity.GetIndexes(), x => x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "TenantId", "LegalEntityId", "OwnerPrincipalId", "NextCycleOn" }));
        var check = Assert.Single(entity.GetCheckConstraints());
        Assert.Contains("owner_principal_id <>", check.Sql); Assert.Contains("owner_worker_id IS NULL", check.Sql);
        Assert.Contains("owner_subject_id = owner_principal_id::text", check.Sql);
    }

    private static RecurringPurchaseCommitment Record(Scope scope) => new() {
        Number = "RPC-" + Guid.NewGuid().ToString("N"), Title = "Synthetic planning only", SupplierId = Guid.NewGuid(),
        OwnerPrincipalId = scope.Principal, OwnerSubjectId = scope.SubjectId, OwnerWorkerId = scope.WorkerId,
        FirstCycleOn = new DateOnly(2026, 11, 1), NextCycleOn = new DateOnly(2026, 11, 1), Frequency = "Monthly",
        ExpectedAmount = 1.0001m, CurrencyCode = "ZMW"
    };
    private static DbContextOptions<ProcurementDbContext> Options() => new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static async Task<string> State(DbContextOptions<ProcurementDbContext> options, Scope scope)
    {
        await using var proof = new ProcurementDbContext(options, scope);
        return JsonSerializer.Serialize(new {
            records = await proof.RecurringPurchaseCommitments.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            audit = await proof.AuditEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            outbox = await proof.OutboxEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()
        });
    }
    private sealed class Scope(Guid? principal = null, Guid? company = null) : IProcurementScope
    {
        public Guid Principal { get; } = principal ?? Guid.NewGuid();
        public string TenantId => "recurring-principal-test";
        public Guid LegalEntityId { get; } = company ?? Guid.NewGuid();
        public string SubjectId => Principal.ToString("D");
        public Guid? WorkerId => null;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "test";
    }
}
