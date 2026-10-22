using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
namespace Mightyfin.Erp.Procurement.Tests;

// SaveChanges/model regression, not PostgreSQL constraints or real Core authorization proof.
public sealed class PolicyDraftRetirementIdentityPersistenceTests
{
    const string Maker = "11111111-1111-4111-8111-111111111111", Other = "22222222-2222-4222-8222-222222222222";
    static readonly Guid MakerWorker = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), OtherWorker = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"), Company = Guid.NewGuid();
    static readonly DateTimeOffset At = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);
    static readonly InMemoryDatabaseRoot Store = new();
    sealed record Scope(string SubjectId, Guid? WorkerId) : IProcurementScope
    {
        public string TenantId => "retire-identity-test";
        public Guid LegalEntityId => Company;
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }
    static ProcurementDbContext Db(string name, string actor = Maker, Guid? worker = null) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(name, Store).Options, new Scope(actor, worker));
    static async Task<Guid> Seed(string name, string key)
    {
        await using var db = Db(name, worker: MakerWorker);
        var policy = new PolicyVersion { TenantId = "retire-identity-test", LegalEntityId = Company, PolicyKey = key, PolicyVersionNumber = 1,
            RulesJson = "{\"retained\":true}", EffectiveFrom = At.AddDays(1), State = "Draft", Version = 1, CreatedBy = Maker, CreatedAt = At };
        db.AddScoped(policy);
        if (key == "SOURCING") db.AddScoped(SourcingPolicyIdentity.Capture(policy, policy.TenantId, Company, "Created", Maker, MakerWorker, 1, At));
        else db.AddScoped(OperationalPolicyIdentityStorage.ToRecord(OperationalPolicyWorkerIdentity.Capture(policy, "Created", Maker, MakerWorker, 1, At)));
        await db.SaveChangesAsync(); return policy.Id;
    }
    static async Task<string> Footprint(string name)
    {
        await using var db = Db(name, worker: MakerWorker);
        return JsonSerializer.Serialize(new { policies = await db.PolicyVersions.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            sourcing = await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            operational = await db.OperationalPolicyIdentityRecords.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            audit = await db.AuditEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), outbox = await db.OutboxEvents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync() });
    }
    [Theory] [InlineData("SOURCING")] [InlineData("RECEIVING")] [InlineData("INVOICE_MATCHING")]
    public async Task Original_worker_can_retire_unused_draft_retaining_exact_created_proof_and_all_material_fields(string key)
    {
        var name = Guid.NewGuid().ToString(); var id = await Seed(name, key);
        string evidence; PolicyVersion before;
        await using (var db = Db(name, worker: MakerWorker))
        {
            before = await db.PolicyVersions.AsNoTracking().SingleAsync();
            evidence = JsonSerializer.Serialize(new { sourcing = await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking().ToArrayAsync(), operational = await db.OperationalPolicyIdentityRecords.AsNoTracking().ToArrayAsync() });
            var policy = await db.PolicyVersions.SingleAsync(); policy.State = "Retired"; await db.SaveChangesAsync();
        }
        await using var proof = Db(name, worker: MakerWorker); var after = await proof.PolicyVersions.AsNoTracking().SingleAsync();
        Assert.Equal(id, after.Id); Assert.Equal("Retired", after.State); Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(before.RulesJson, after.RulesJson); Assert.Equal(before.EffectiveFrom, after.EffectiveFrom); Assert.Equal(before.EffectiveTo, after.EffectiveTo);
        Assert.Equal(before.CreatedBy, after.CreatedBy); Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Null(after.ReviewRequestedAt); Assert.Null(after.PublishedAt);
        Assert.Equal(evidence, JsonSerializer.Serialize(new { sourcing = await proof.SourcingPolicyIdentityEvidenceRecords.AsNoTracking().ToArrayAsync(), operational = await proof.OperationalPolicyIdentityRecords.AsNoTracking().ToArrayAsync() }));
        proof.Attach(after); after.State = "Draft"; proof.Entry(after).Property(x => x.State).IsModified = true;
        var retired = await Footprint(name); await Assert.ThrowsAsync<InvalidOperationException>(() => proof.SaveChangesAsync()); Assert.Equal(retired, await Footprint(name));
    }
    public static IEnumerable<object[]> Faults() => new[] { "SOURCING", "RECEIVING", "INVOICE_MATCHING" }
        .SelectMany(key => new[] { "other-principal", "rebound-worker", "missing-worker", "changed-rules", "changed-dates", "review-metadata", "publication-metadata" }.Select(fault => new object[] { key, fault }));
    [Theory] [MemberData(nameof(Faults))]
    public async Task Retirement_does_not_bypass_author_identity_history_or_material_change_guards(string key, string fault)
    {
        var name = Guid.NewGuid().ToString(); await Seed(name, key); var before = await Footprint(name);
        await using var db = Db(name, fault == "other-principal" ? Other : Maker, fault == "missing-worker" ? null : fault == "rebound-worker" ? OtherWorker : MakerWorker);
        var policy = await db.PolicyVersions.SingleAsync(); policy.State = "Retired";
        if (fault == "changed-rules") policy.RulesJson = "{\"retained\":false}";
        if (fault == "changed-dates") policy.EffectiveFrom = policy.EffectiveFrom.AddDays(1);
        if (fault == "review-metadata") policy.ReviewReason = "Must not erase or adopt review";
        if (fault == "publication-metadata") policy.PublicationReason = "Must not erase or adopt publication";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(before, await Footprint(name));
    }
}
