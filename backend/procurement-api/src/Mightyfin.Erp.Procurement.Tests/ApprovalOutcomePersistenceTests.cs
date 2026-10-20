using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Context/model contracts. PostgreSQL constraints/rollback require the combined P22 rehearsal.</summary>
public sealed class ApprovalOutcomePersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "outcome-persistence-test";
    }
    private static ProcurementDbContext Context(string database, Guid company, string tenant = "tenant-a") =>
        new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(database).Options,
            new Scope(tenant, company, "reviewer"));

    [Theory]
    [InlineData("outcome")]
    [InlineData("release")]
    [InlineData("confirmation")]
    public async Task All_source_effect_evidence_is_append_only_including_boolean_save_overload(string kind)
    {
        using var db = Context(Guid.NewGuid().ToString(), Guid.NewGuid());
        var chain = Seed(db);
        await db.SaveChangesAsync();
        ProcurementRecord row = kind switch { "outcome" => chain.Outcome, "release" => chain.Release, _ => chain.Confirmation };
        db.Entry(row).Property("EffectKey").CurrentValue = "rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(true));
        db.Entry(row).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Evidence_queries_are_tenant_company_isolated_and_audit_omits_private_values()
    {
        var company = Guid.NewGuid(); var database = Guid.NewGuid().ToString();
        using (var db = Context(database, company))
        {
            Seed(db);
            await db.SaveChangesAsync();
            Assert.DoesNotContain(await db.AuditEvents.ToListAsync(), x =>
                (x.ChangedFieldsJson ?? "").Contains("PRIVATE-RESERVATION"));
        }
        using var foreignTenant = Context(database, company, "tenant-b");
        using var foreignCompany = Context(database, Guid.NewGuid());
        foreach (var db in new[] { foreignTenant, foreignCompany })
        {
            Assert.Empty(await db.ApprovalOutcomes.ToListAsync());
            Assert.Empty(await db.FinanceReleaseRequests.ToListAsync());
            Assert.Empty(await db.FinanceReleaseConfirmations.ToListAsync());
        }
    }

    [Fact]
    public void AddScoped_cannot_relabel_any_approval_effect_evidence()
    {
        using var db = Context(Guid.NewGuid().ToString(), Guid.NewGuid());
        var records = new ProcurementRecord[] { new PurchaseRequestApprovalOutcome(),
            new PurchaseRequestFinanceReleaseRequest(), new PurchaseRequestFinanceReleaseConfirmation() };
        foreach (var row in records)
        {
            row.TenantId = "foreign"; row.LegalEntityId = Guid.NewGuid();
            var foreign = row.LegalEntityId;
            Assert.Throws<InvalidOperationException>(() => db.AddScoped(row));
            Assert.Equal(foreign, row.LegalEntityId);
        }
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Exact_reservation_values_are_required_even_when_pinned_finance_ID_matches()
    {
        using var db = Context(Guid.NewGuid().ToString(), Guid.NewGuid());
        var chain = Seed(db);
        db.Entry(chain.Release).Property(x => x.ReservedAmount).CurrentValue = 249m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_release_cannot_be_attached_to_an_approved_outcome()
    {
        using var db = Context(Guid.NewGuid().ToString(), Guid.NewGuid());
        var chain = Seed(db);
        db.Entry(chain.Outcome).Property(x => x.Outcome).CurrentValue = "Approved";
        db.Entry(chain.Outcome).Property(x => x.EffectKey).CurrentValue =
            $"procurement.pr.approval.{chain.Outcome.CoreApprovalCaseId:N}.approved.v1";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void Relational_model_binds_exact_source_and_reservation_without_making_historical_reservation_required()
    {
        using var db = Context(Guid.NewGuid().ToString(), Guid.NewGuid());
        foreach (var type in new[] { typeof(PurchaseRequestApprovalOutcome), typeof(PurchaseRequestFinanceReleaseRequest),
                     typeof(PurchaseRequestFinanceReleaseConfirmation) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Contains(entity.GetIndexes(), x => x.IsUnique
                && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "EffectKey" }));
            Assert.All(entity.GetForeignKeys(), fk =>
            {
                Assert.Equal(new[] { "TenantId", "LegalEntityId" }, fk.Properties.Take(2).Select(x => x.Name));
                Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
            });
        }
        var outcome = db.Model.FindEntityType(typeof(PurchaseRequestApprovalOutcome))!;
        Assert.Contains(outcome.GetForeignKeys(), x => x.Properties.Any(p => p.Name == "SubmittedBusinessVersion")
            && x.Properties.Any(p => p.Name == "CoreApprovalCaseId"));
        var release = db.Model.FindEntityType(typeof(PurchaseRequestFinanceReleaseRequest))!;
        Assert.Contains(release.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(PurchaseRequestApprovalOutcome)
            && x.Properties.Any(p => p.Name == "Outcome"));
        var confirmation = db.Model.FindEntityType(typeof(PurchaseRequestFinanceReleaseConfirmation))!;
        Assert.Contains(confirmation.GetForeignKeys(), x => x.Properties.Any(p => p.Name == "ReleasedAmount")
            && x.Properties.Any(p => p.Name == "ReservationRef") && x.Properties.Any(p => p.Name == "CurrencyCode"));
        Assert.True(db.Model.FindEntityType(typeof(BudgetCheckSnapshot))!.FindProperty("ReservationRef")!.IsNullable);
    }

    private static (PurchaseRequestApprovalOutcome Outcome, PurchaseRequestFinanceReleaseRequest Release,
        PurchaseRequestFinanceReleaseConfirmation Confirmation) Seed(ProcurementDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new PurchaseRequest { Number = "PR-EVIDENCE", RequesterWorkerId = Guid.NewGuid(),
            Purpose = "Synthetic source", CurrencyCode = "ZMW", SubmittedVersion = 4 };
        db.AddScoped(request);
        var policy = new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 1 };
        db.AddScoped(policy);
        var evaluation = new PolicyEvaluation { PurchaseRequestId = request.Id, RequestVersion = 4,
            PolicyVersionId = policy.Id, PolicyVersionNumber = 1, AmountBasis = "EstimatedLineBase", Outcome = "Matched", DecisionJson = "{}" };
        db.AddScoped(evaluation);
        var finance = new BudgetCheckSnapshot { PurchaseRequestId = request.Id, RequestVersion = 4, CheckedAmount = 250,
            CurrencyCode = "ZMW", FinanceCheckRef = "CHECK", Status = "Available", ReservationRef = "PRIVATE-RESERVATION",
            ReservationStatus = "Confirmed", CheckedAt = now, CheckedBySubjectId = "finance", EvidenceRef = "PRIVATE-FUNDING" };
        db.AddScoped(finance);
        var pin = new PurchaseRequestApprovalSubmission { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            PurchaseRequestId = request.Id, SubmittedBusinessVersion = 4, CoreApprovalCaseId = Guid.NewGuid(),
            OpeningCommandKey = Guid.NewGuid(), MakerSubjectId = "maker", RequestSnapshotJson = "{}", RequestSnapshotSha256 = new string('a',64),
            FinanceSnapshotJson = "{}", FinanceSnapshotSha256 = new string('b',64), PolicyDecisionSha256 = new string('c',64),
            PolicyRulesSha256 = new string('d',64), PolicyEvaluationId = evaluation.Id, PolicyVersionId = policy.Id,
            PolicyVersionNumber = 1, FinanceSnapshotId = finance.Id, FinanceEvidenceMode = "AutomatedFinance",
            AmountBasis = "EstimatedLineBase", EstimatedAmount = 250, CurrencyCode = "ZMW", PinnedAt = now };
        db.AddScoped(pin);
        var outcome = new PurchaseRequestApprovalOutcome { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            SubmissionId = pin.Id, PurchaseRequestId = request.Id, SubmittedBusinessVersion = 4,
            CoreApprovalCaseId = pin.CoreApprovalCaseId, CoreDecisionId = Guid.NewGuid(), DecisionCommandKey = "return-command",
            Outcome = "Returned", ActorSubjectId = "reviewer", AppliedAt = now.AddSeconds(1),
            EffectKey = $"procurement.pr.approval.{pin.CoreApprovalCaseId:N}.returned.v1" };
        db.AddScoped(outcome);
        var release = new PurchaseRequestFinanceReleaseRequest { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            ApprovalOutcomeId = outcome.Id, Outcome = outcome.Outcome, SubmissionId = pin.Id, PurchaseRequestId = request.Id,
            SubmittedBusinessVersion = 4, CoreApprovalCaseId = pin.CoreApprovalCaseId, FinanceSnapshotId = finance.Id,
            ReservationRef = finance.ReservationRef, ReservedAmount = 250, CurrencyCode = "ZMW", RequestedBySubjectId = "reviewer",
            RequestedAt = outcome.AppliedAt, EffectKey = $"procurement.pr.release.{pin.Id:N}.v1" };
        db.AddScoped(release);
        var confirmed = new PurchaseRequestFinanceReleaseConfirmation { TenantId = request.TenantId, LegalEntityId = request.LegalEntityId,
            ReleaseRequestId = release.Id, ReservationRef = release.ReservationRef, ReleasedAmount = 250, CurrencyCode = "ZMW",
            EvidenceRef = "PRIVATE-RELEASE", EvidenceSource = "approved-finance", VerifiedBySubjectId = "finance-verifier",
            FinanceConfirmedAt = now.AddSeconds(2), RecordedAt = now.AddSeconds(3),
            EffectKey = $"procurement.pr.release_confirmed.{release.Id:N}.v1" };
        db.AddScoped(confirmed);
        return (outcome, release, confirmed);
    }
}
