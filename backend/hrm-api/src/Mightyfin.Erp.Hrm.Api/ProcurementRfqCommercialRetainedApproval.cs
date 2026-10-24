using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record RetainedCommercialApproval(
        PrincipalRfqCommercialRoundOpeningRecord Opening,
        PrincipalRfqCommercialRoundDecisionRecord Decision,
        PrincipalRfqPublishedRoundProof Proof,
        PrincipalRfqPreparedPlan Prepared,
        SourcingEvent Event,
        RfqPrincipalDraft Original);

    // Historical proof only. Callers first require their OWN current action and
    // branch; no permission is borrowed from the past terminal reviewer.
    private static async Task<RetainedCommercialApproval> LoadRetainedCommercialApproval(
        ProcurementDbContext db, ApprovalDbContext cases, ProcurementRequestScope scope,
        PrincipalRfqCommercialRoundOpeningRecord opening, CancellationToken ct)
    {
        var ev = await db.SourcingEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == opening.SourcingEventId
                && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && x.BranchRef == opening.BranchId, ct)
            ?? throw new Problem("RfqCommercialRoundSourceChanged", 409);
        if (ev.Status != "Published" || ev.ExecutionVersion != 2
            || ev.Version != opening.EventVersion)
            throw new Problem("RfqCommercialRoundSourceChanged", 409);
        _ = await PrincipalPublicationEvidence(db, cases, ev, ct);
        var prepared = await db.PrincipalRfqPreparedPlans.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == opening.PreparedPlanId
                && x.SourcingEventId == ev.Id && x.BranchId == opening.BranchId, ct)
            ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
        var head = await db.PrincipalRfqCommercialDrafts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == prepared.DraftId
                && x.SourcingEventId == ev.Id, ct)
            ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
        var revision = await db.PrincipalRfqCommercialDraftRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == prepared.DraftRevisionId
                && x.DraftId == head.Id, ct)
            ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
        PrincipalRfqPreparedPlanEvidence.Verify(prepared, head, revision);
        if (prepared.SnapshotSha256 != opening.PreparedPlanSha256)
            throw new Problem("RfqCommercialPreparedPlanChanged", 409);
        await using (var documents = new DocumentDbContext(
            new DbContextOptionsBuilder<DocumentDbContext>()
                .UseNpgsql(db.Database.GetDbConnection()).Options, scope))
        {
            await documents.Database.UseTransactionAsync(
                db.Database.CurrentTransaction!.GetDbTransaction(), ct);
            await RequireRetainedCommercialRequirementsAsync(db, documents, scope,
                prepared, head, revision, ct, currentPurposeAuthorized: true);
        }
        var policy = await db.PolicyVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == opening.CommercialPolicyVersionId
                && x.PolicyKey == PrincipalRfqCommercialRoundPolicyRules.PolicyKey, ct)
            ?? throw new Problem("RfqCommercialPolicyChanged", 409);
        var lineage = await ProcurementOperationalPolicyWorkerIdentity
            .VerifyPublicationAsync(db, scope, policy, ct,
                OperationalPolicyPublicationMode.RetainedHistorical);
        if (lineage.Published.Id != opening.PublishedPolicyIdentityId
            || ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson)
                != opening.PolicyRulesSha256)
            throw new Problem("RfqCommercialPolicyChanged", 409);
        var workflow = await cases.Cases.AsNoTracking().Include(x => x.Stages)
            .ThenInclude(x => x.Assignments)
            .SingleOrDefaultAsync(x => x.Id == opening.CoreCaseId
                && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId,
                ct) ?? throw new Problem("RfqCommercialReviewChanged", 409);
        if (workflow.State != "Approved" || workflow.SourceId != prepared.Id)
            throw new Problem("RfqCommercialReviewChanged", 409);
        var decisions = await cases.Decisions.AsNoTracking()
            .Where(x => x.CaseId == workflow.Id).ToArrayAsync(ct);
        await RequireCommercialPriorVotes(db, opening, decisions, ct);
        var terminal = await db.PrincipalRfqCommercialRoundDecisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OpeningId == opening.Id
                && x.State == "Approved", ct)
            ?? throw new Problem("RfqCommercialApprovalNotFound", 409);
        var coreDecision = decisions.SingleOrDefault(x => x.Id == terminal.CoreDecisionId);
        var vote = await db.PrincipalRfqCommercialRoundVotes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == terminal.TerminalVoteId
                && x.CoreDecisionId == terminal.CoreDecisionId, ct);
        var receipt = await db.PrincipalRfqCommercialCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.VoteTargetId == terminal.TerminalVoteId
                && x.DecisionTargetId == terminal.Id, ct);
        if (coreDecision is null || vote is null || receipt is null
            || receipt.CommandKey.ToString("D") != coreDecision.IdempotencyKey
            || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new Problem("RfqCommercialApprovalChanged", 409);
        using var json = JsonDocument.Parse(opening.SnapshotJson);
        var suppliers = json.RootElement.GetProperty("supplierIds")
            .EnumerateArray().Select(x => x.GetGuid()).ToArray();
        var retained = new PrincipalRfqCommercialRoundOpening(workflow,
            opening.SnapshotJson, opening.SnapshotSha256, suppliers,
            opening.OpensAt, opening.ClosesAt,
            opening.ClosingOfficerPrincipalId,
            opening.ClosingOfficerWorkerAliasId);
        var proof = PrincipalRfqCommercialRoundCoreWorkflow.VerifyRetainedApproved(
            retained, prepared, ev, policy, workflow, decisions, coreDecision,
            terminal, ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value);
        var evidence = await db.SourcingDraftEvidenceRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == ev.SourceEvidenceId, ct)
            ?? throw new Problem("RfqCommercialSourceEvidenceChanged", 409);
        var bindings = await db.SourcingDemandBindings.AsNoTracking()
            .Where(x => x.SourcingEventId == ev.Id).ToArrayAsync(ct);
        var original = GovernedPrincipalRfqRestoration.RestoreVerified(evidence,
            ev, bindings);
        return new(opening, terminal, proof, prepared, ev, original);
    }
}
