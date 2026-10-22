using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>SaveChanges and PostgreSQL design-model source contracts, not real PostgreSQL execution proof.</summary>
public sealed class FinanceCheckerIdentityPersistenceTests
{
    private sealed class Scope(Guid company) : IProcurementScope
    {
        public string TenantId => "finance-identity-test";
        public Guid LegalEntityId => company;
        public string SubjectId => "finance-recorder";
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "finance-identity";
    }

    private static ProcurementDbContext Context(string database, Guid company) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options, new Scope(company));

    [Fact]
    public void PostgreSQL_model_requires_complete_v1_or_empty_legacy_without_new_mutable_keys_or_core_foreign_keys()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=finance_identity_design_only;Username=invalid").Options, new Scope(Guid.NewGuid()));
        var model = db.GetService<IDesignTimeModel>().Model;
        var finance = model.FindEntityType(typeof(BudgetCheckSnapshot))!;
        var pin = model.FindEntityType(typeof(PurchaseRequestApprovalSubmission))!;
        var sql = finance.GetCheckConstraints().Single(x => x.Name == "ck_budget_checker_identity").Sql;
        Assert.Contains("checker_identity_schema_version = 0 AND checked_by_worker_id IS NULL AND checker_identity_sha256 IS NULL", sql);
        Assert.Contains("checker_identity_schema_version = 1 AND checked_by_worker_id IS NOT NULL", sql);
        Assert.Contains("checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid", sql);
        Assert.Contains("checker_identity_sha256 IS NOT NULL AND checker_identity_sha256 ~ '^[a-f0-9]{64}$'", sql);
        Assert.Equal(0, finance.FindProperty(nameof(BudgetCheckSnapshot.CheckerIdentitySchemaVersion))!.GetDefaultValue());
        Assert.False(finance.FindProperty(nameof(BudgetCheckSnapshot.CheckerIdentitySchemaVersion))!.IsNullable);
        Assert.True(finance.FindProperty(nameof(BudgetCheckSnapshot.CheckedByWorkerId))!.IsNullable);
        Assert.Equal(ValueGenerated.Never, finance.FindProperty(nameof(BudgetCheckSnapshot.CheckedByWorkerId))!.ValueGenerated);
        Assert.Equal(64, finance.FindProperty(nameof(BudgetCheckSnapshot.CheckerIdentitySha256))!.GetMaxLength());
        Assert.Equal(18, finance.FindProperty(nameof(BudgetCheckSnapshot.CheckedAmount))!.GetPrecision());
        Assert.Equal(4, finance.FindProperty(nameof(BudgetCheckSnapshot.CheckedAmount))!.GetScale());
        Assert.Equal(0, pin.FindProperty(nameof(PurchaseRequestApprovalSubmission.FinanceIdentitySchemaVersion))!.GetDefaultValue());
        Assert.Contains("finance_identity_schema_version = 0 OR (finance_identity_schema_version = 1 AND snapshot_schema_version = 2)",
            pin.GetCheckConstraints().Single(x => x.Name == "ck_pr_finance_identity_version").Sql);
        Assert.DoesNotContain(finance.GetKeys().SelectMany(x => x.Properties), x =>
            x.Name is "CheckerIdentitySchemaVersion" or "CheckedByWorkerId" or "CheckerIdentitySha256");
        Assert.DoesNotContain(pin.GetKeys().SelectMany(x => x.Properties), x => x.Name == "FinanceIdentitySchemaVersion");
        Assert.Single(finance.GetForeignKeys());
        Assert.Equal(typeof(PurchaseRequest), finance.GetForeignKeys().Single().PrincipalEntityType.ClrType);
        Assert.Equal(4, pin.GetForeignKeys().Count());
        Assert.Contains(pin.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(BudgetCheckSnapshot)
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "FinanceSnapshotId", "PurchaseRequestId", "SubmittedBusinessVersion" }));
    }

    [Fact]
    public async Task New_bound_snapshot_and_exact_pin_save_with_field_name_only_audit_and_retained_provenance()
    {
        var database = Guid.NewGuid().ToString(); var company = Guid.NewGuid();
        await using var db = Context(database, company);
        var finance = Snapshot(company); Bind(finance); db.AddScoped(finance); await db.SaveChangesAsync();
        var pin = Pin(finance); db.AddScoped(pin); await db.SaveChangesAsync();
        await using var fresh = Context(database, company);
        var stored = await fresh.BudgetCheckSnapshots.SingleAsync();
        Assert.True(FinanceCheckerIdentityEvidence.HasValidBinding(stored));
        Assert.Equal(finance.CheckedByWorkerId, stored.CheckedByWorkerId);
        Assert.Equal(1, (await fresh.ApprovalSubmissions.SingleAsync()).FinanceIdentitySchemaVersion);
        var audits = await fresh.AuditEvents.ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, x => Assert.DoesNotContain(finance.EvidenceRef!, x.ChangedFieldsJson ?? ""));
        Assert.Contains(audits, x => x.RecordType == nameof(BudgetCheckSnapshot)
            && x.ChangedFieldsJson!.Contains(nameof(BudgetCheckSnapshot.CheckedByWorkerId), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing-worker")]
    [InlineData("zero-worker")]
    [InlineData("missing-hash")]
    [InlineData("upper-hash")]
    [InlineData("changed-amount")]
    [InlineData("wrong-actor")]
    [InlineData("partial-legacy")]
    [InlineData("unknown")]
    public async Task Invalid_new_checker_metadata_is_denied_before_snapshot_or_audit_effects(string kind)
    {
        var company = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), company);
        var finance = Snapshot(company);
        if (kind == "wrong-actor") finance.CheckedBySubjectId = "another-account";
        Bind(finance);
        switch (kind)
        {
            case "missing-worker": finance.CheckedByWorkerId = null; break;
            case "zero-worker": finance.CheckedByWorkerId = Guid.Empty; break;
            case "missing-hash": finance.CheckerIdentitySha256 = null; break;
            case "upper-hash": finance.CheckerIdentitySha256 = finance.CheckerIdentitySha256!.ToUpperInvariant(); break;
            case "changed-amount": finance.CheckedAmount += .0001m; break;
            case "wrong-actor": break; // Binding is valid; only authenticated recorder equality denies.
            case "partial-legacy": finance.CheckerIdentitySchemaVersion = 0; break;
            case "unknown": finance.CheckerIdentitySchemaVersion = 99; break;
        }
        db.AddScoped(finance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.BudgetCheckSnapshots.ToListAsync()); Assert.Empty(await db.AuditEvents.ToListAsync());
    }

    [Theory]
    [InlineData("json")]
    [InlineData("digest")]
    [InlineData("request")]
    [InlineData("version")]
    [InlineData("unknown-schema")]
    [InlineData("downgrade")]
    public async Task Pin_cannot_claim_malformed_foreign_or_downgraded_checker_provenance(string kind)
    {
        var company = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), company);
        var finance = Snapshot(company); Bind(finance); db.AddScoped(finance); await db.SaveChangesAsync();
        var json = JsonNode.Parse(JsonSerializer.Serialize(Pin(finance)))!;
        switch (kind)
        {
            case "json": json["FinanceSnapshotJson"] = "{}"; break;
            case "digest": json["FinanceSnapshotSha256"] = new string('f', 64); break;
            case "request": json["PurchaseRequestId"] = Guid.NewGuid().ToString("D"); break;
            case "version": json["SubmittedBusinessVersion"] = 99; break;
            case "unknown-schema": json["FinanceIdentitySchemaVersion"] = 99; break;
            case "downgrade": json["FinanceIdentitySchemaVersion"] = 0; break;
        }
        db.AddScoped(JsonSerializer.Deserialize<PurchaseRequestApprovalSubmission>(json.ToJsonString())!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.ApprovalSubmissions.ToListAsync()); Assert.Single(await db.AuditEvents.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_bound_snapshot_cannot_be_rebound_or_deleted(bool delete)
    {
        var database = Guid.NewGuid().ToString(); var company = Guid.NewGuid();
        await using var db = Context(database, company);
        var finance = Snapshot(company); Bind(finance); db.AddScoped(finance); await db.SaveChangesAsync();
        if (delete) db.Remove(finance); else finance.CheckedByWorkerId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var fresh = Context(database, company);
        var stored = await fresh.BudgetCheckSnapshots.SingleAsync();
        Assert.True(FinanceCheckerIdentityEvidence.HasValidBinding(stored)); Assert.Equal(1, await fresh.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Empty_legacy_metadata_stays_unbound_and_immutable_without_guessed_worker()
    {
        var company = Guid.NewGuid();
        await using var db = Context(Guid.NewGuid().ToString(), company);
        var finance = Snapshot(company); db.AddScoped(finance); await db.SaveChangesAsync();
        Assert.Equal(0, finance.CheckerIdentitySchemaVersion); Assert.Null(finance.CheckedByWorkerId);
        Assert.Null(finance.CheckerIdentitySha256); Assert.False(FinanceCheckerIdentityEvidence.HasValidBinding(finance));
        finance.CheckedByWorkerId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static BudgetCheckSnapshot Snapshot(Guid company) => new()
    {
        TenantId = "finance-identity-test", LegalEntityId = company, PurchaseRequestId = Guid.NewGuid(), RequestVersion = 4,
        FinanceCheckRef = "CHECK-1", Status = "Available", CheckedAmount = 25.9257m, CurrencyCode = "ZMW",
        CheckedAt = new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero).AddTicks(1234567),
        CheckedBySubjectId = "finance-recorder", EvidenceRef = "PRIVATE-EVIDENCE-1",
        ReservationRef = "RESERVE-1", ReservationStatus = "Confirmed",
    };

    private static void Bind(BudgetCheckSnapshot finance)
    {
        var binding = FinanceCheckerIdentityEvidence.CreateForNewSnapshot(finance, finance.CheckedBySubjectId, Guid.NewGuid());
        finance.CheckerIdentitySchemaVersion = binding.SchemaVersion; finance.CheckedByWorkerId = binding.WorkerId;
        finance.CheckerIdentitySha256 = binding.Sha256;
    }

    private static PurchaseRequestApprovalSubmission Pin(BudgetCheckSnapshot finance)
    {
        var json = FinanceSnapshotCodec.Encode(finance, 1);
        return new()
        {
            TenantId = finance.TenantId, LegalEntityId = finance.LegalEntityId, SnapshotSchemaVersion = 2,
            PurchaseRequestId = finance.PurchaseRequestId, SubmittedBusinessVersion = finance.RequestVersion,
            CoreApprovalCaseId = Guid.NewGuid(), OpeningCommandKey = Guid.NewGuid(), MakerSubjectId = "requester",
            MakerWorkerId = Guid.NewGuid(), RequesterWorkerId = Guid.NewGuid(), WorkerIdentitySha256 = new string('a', 64),
            RequestSnapshotJson = "{}", RequestSnapshotSha256 = new string('b', 64), PolicyEvaluationId = Guid.NewGuid(),
            PolicyVersionId = Guid.NewGuid(), PolicyVersionNumber = 1, PolicyDecisionSha256 = new string('c', 64),
            PolicyRulesSha256 = new string('d', 64), FinanceSnapshotId = finance.Id, FinanceIdentitySchemaVersion = 1,
            FinanceSnapshotJson = json, FinanceSnapshotSha256 = ProcurementEvidenceEncoding.Digest(json),
            FinanceEvidenceMode = "ApprovedManualEvidence", ManualEvidenceAuthorityRef = "TEST-POLICY",
            AmountBasis = "EstimatedLineBase", EstimatedAmount = finance.CheckedAmount, CurrencyCode = finance.CurrencyCode,
            PinnedAt = finance.CheckedAt.AddMinutes(1),
        };
    }
}
