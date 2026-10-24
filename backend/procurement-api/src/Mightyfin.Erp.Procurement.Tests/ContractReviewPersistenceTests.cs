using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Mapped same-save boundary only. Physical FK/CHECK/Core transaction
/// behavior still requires the genuine P35 PostgreSQL rehearsal.</summary>
public sealed class ContractReviewPersistenceTests
{
    [Fact]
    public async Task Submitted_draft_requires_original_terms_and_actor_owned_ack_in_the_same_save()
    {
        var database = "contract-review-" + Guid.CreateVersion7();
        var tenant = "synthetic-contract-review"; var company = Guid.CreateVersion7();
        var actor = Guid.CreateVersion7(); var branch = Guid.CreateVersion7();
        Guid contractId;
        await using (var db = Db(database, Scope(tenant, company, actor)))
        {
            var contract = new SupplierContract { Number = "CON-SYNTHETIC", SupplierId = Guid.CreateVersion7(),
                Title = "Synthetic", ContractType = "Framework", CurrencyCode = "ZMW", CeilingAmount = 100m,
                OwnerSubjectId = actor.ToString("D"), Status = "Draft" };
            var version = new SupplierContractVersion { SupplierContractId = contract.Id,
                RevisionNumber = 1, CeilingAmount = 100m, Status = "Draft" };
            var rate = new ContractRate { SupplierContractVersionId = version.Id, LineNumber = 1,
                Status = "Active", Description = "One", UnitCode = "EA", UnitPrice = 10m };
            db.AddScoped(contract); db.AddScoped(version); db.AddScoped(rate);
            await db.SaveChangesAsync(); contractId = contract.Id;
        }
        await using (var db = Db(database, Scope(tenant, company, actor)))
        {
            var contract = await db.SupplierContracts.SingleAsync(x => x.Id == contractId);
            var version = await db.SupplierContractVersions.SingleAsync(x => x.SupplierContractId == contractId);
            var rates = await db.ContractRates.AsNoTracking().Where(x => x.SupplierContractVersionId == version.Id).ToArrayAsync();
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var reviewer = Guid.CreateVersion7().ToString("D");
            var policy = new PolicyVersion { TenantId = tenant, LegalEntityId = company,
                PolicyKey = ProcurementContractReviewPolicyRules.PolicyKey, PolicyVersionNumber = 1,
                State = "Published", EffectiveFrom = now.AddDays(-1), RulesJson =
                    $$"""{"schemaVersion":1,"transactionType":"SupplierContractReview","stages":[{"code":"LEGAL","mode":"All","principalIds":["{{reviewer}}"]}]}""" };
            var named = new ContractReviewCurrentReviewer(reviewer, null, false,
                new HashSet<Guid> { branch }, new HashSet<string> { "procurement-access", "procurement-contract-review" });
            var opening = ContractReviewCoreWorkflow.Open(contract, version, rates, policy,
                branch, actor.ToString("D"), null, [named], 1, now);
            contract.Status = "PendingReview"; version.Status = "PendingReview";
            version.ApprovalInstanceRef = opening.Case.Id.ToString("D");
            db.AddScoped(opening.Submission);
            var ack = new ContractReviewCommandReceipt { TenantId = tenant, LegalEntityId = company,
                ContractId = contract.Id, SubmissionId = opening.Submission.Id, ReviewBranchId = branch,
                Operation = "Submit", CommandKey = Guid.CreateVersion7(), ActorPrincipalId = actor.ToString("D"),
                CommandSha256 = new string('a', 64), ResultContractVersion = checked(contract.Version + 1),
                ResponseStatus = 201, CompletedAt = now, CreatedAt = now, CreatedBy = actor.ToString("D") };
            ack.ResponseJson = ContractReviewReceiptProof.Response(ack, "PendingReview");
            ack.ResponseSha256 = ProcurementEvidenceEncoding.Digest(ack.ResponseJson);
            db.AddScoped(ack);
            await db.SaveChangesAsync();
            Assert.Equal("PendingReview", (await db.SupplierContracts.AsNoTracking().SingleAsync(x => x.Id == contractId)).Status);
            Assert.Single(await db.ContractReviewCommandReceipts.AsNoTracking().ToArrayAsync());
        }
    }

    [Fact]
    public async Task Bare_vote_without_original_ack_is_denied_before_save()
    {
        var actorId = Guid.CreateVersion7();
        var company = Guid.CreateVersion7();
        await using var db = Db("contract-review-denied-" + Guid.CreateVersion7(),
            Scope("synthetic-contract-review", company, actorId));
        var actor = actorId.ToString("D");
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        db.AddScoped(new ContractReviewVote { TenantId = "synthetic-contract-review", LegalEntityId = company,
            SubmissionId = Guid.CreateVersion7(), ContractId = Guid.CreateVersion7(),
            ReviewBranchId = Guid.CreateVersion7(), CoreCaseId = Guid.CreateVersion7(),
            CoreStageId = Guid.CreateVersion7(), CoreAssignmentId = Guid.CreateVersion7(),
            CoreDecisionId = Guid.CreateVersion7(), ActorPrincipalId = actor,
            Action = "Approve", DecidedAt = at, CreatedAt = at, CreatedBy = actor });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Bare_correction_cannot_reopen_or_append_without_returned_parent_and_original_ack()
    {
        var actorId = Guid.CreateVersion7();
        var company = Guid.CreateVersion7();
        await using var db = Db("contract-correction-denied-" + Guid.CreateVersion7(),
            Scope("synthetic-contract-review", company, actorId));
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        db.AddScoped(new ContractReviewCorrection { TenantId = "synthetic-contract-review", LegalEntityId = company,
            ContractId = Guid.CreateVersion7(), ContractVersionId = Guid.CreateVersion7(),
            SubmissionId = Guid.CreateVersion7(), ReviewBranchId = Guid.CreateVersion7(),
            CoreCaseId = Guid.CreateVersion7(), FromContractVersion = 3, ToContractVersion = 4,
            ActorPrincipalId = actorId.ToString("D"), Reason = "Correct returned terms",
            CorrectedAt = at, CreatedAt = at, CreatedBy = actorId.ToString("D") });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private sealed record TestScope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "contract-review-test";
    }

    private static TestScope Scope(string tenant, Guid company, Guid actor)
        => new(tenant, company, actor.ToString("D"));

    private static ProcurementDbContext Db(string name, IProcurementScope scope) => new(
        new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(name).Options, scope);
}
