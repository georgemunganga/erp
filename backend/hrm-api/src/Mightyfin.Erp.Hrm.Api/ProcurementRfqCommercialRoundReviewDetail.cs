using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
    private sealed record CommercialReviewSource(
        PrincipalRfqCommercialRoundOpeningRecord Opening,
        PrincipalRfqPreparedPlan Prepared,
        PrincipalRfqCommercialDraftHead Head,
        PrincipalRfqCommercialDraftRevision Revision,
        Mightyfin.Erp.Core.Approvals.Domain.ApprovalCase Case,
        Mightyfin.Erp.Core.Approvals.Domain.ApprovalAssignment Assignment,
        RfqPrincipalActor Actor);

    // Assigned purpose only. A general procurement-read grant is insufficient,
    // and terminal/returned assignments stop exposing current review material.
    private static async Task<CommercialReviewSource> CurrentCommercialReview(
        ProcurementDbContext db, ApprovalDbContext cases,
        Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core,
        IAuthorizationService auth, HttpContext http, ProcurementRequestScope scope,
        Guid id, Guid assignmentId, CancellationToken ct)
    {
        if (id == Guid.Empty || assignmentId == Guid.Empty)
            throw new Problem("RfqCommercialReviewNotFound", 404);
        var actor = await PrincipalActor(core, scope, http, auth,
            PrincipalRfqCommercialRoundPolicyRules.ApprovalAction, null, false, ct);
        var branches = actor.Branches.ToArray();
        var opening = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId
                && (actor.SelectedBranchId == null || x.BranchId == actor.SelectedBranchId)
                && (actor.OrganisationWide || branches.Contains(x.BranchId)), ct)
            ?? throw new Problem("RfqCommercialReviewNotFound", 404);
        actor = await PrincipalActor(core, scope, http, auth,
            PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
            opening.BranchId, true, ct);
        var workflow = await cases.Cases.AsNoTracking().Include(x => x.Stages)
            .ThenInclude(x => x.Assignments)
            .SingleOrDefaultAsync(x => x.Id == opening.CoreCaseId
                && x.TenantId == scope.TenantId
                && x.LegalEntityId == scope.LegalEntityId
                && x.SourceModule == "procurement"
                && x.SourceType == PrincipalRfqCommercialRoundCoreWorkflow.SourceType
                && x.SourceId == opening.PreparedPlanId && x.State == "Pending", ct)
            ?? throw new Problem("RfqCommercialReviewNotFound", 404);
        var assignment = workflow.Stages.Where(x => x.State == "Active")
            .SelectMany(x => x.Assignments)
            .SingleOrDefault(x => x.Id == assignmentId && x.State == "Pending"
                && x.CaseId == workflow.Id && x.AssigneeSubjectId == actor.PrincipalId
                && x.PermissionKey == PrincipalRfqCommercialRoundPolicyRules.ApprovalAction
                && x.DelegatedFromSubjectId is null)
            ?? throw new Problem("RfqCommercialReviewNotFound", 404);
        var source = await db.SourcingEvents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == opening.SourcingEventId
                && x.BranchRef == opening.BranchId && x.Status == "Published"
                && x.ExecutionVersion == 2 && x.Version == opening.EventVersion, ct)
            ?? throw new Problem("RfqCommercialRoundSourceChanged", 409);
        _ = await PrincipalPublicationEvidence(db, cases, source, ct);
        var prepared = await db.PrincipalRfqPreparedPlans.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == opening.PreparedPlanId
                && x.SourcingEventId == source.Id && x.BranchId == opening.BranchId
                && x.SnapshotSha256 == opening.PreparedPlanSha256, ct)
            ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
        var head = await db.PrincipalRfqCommercialDrafts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == prepared.DraftId
                && x.SourcingEventId == source.Id, ct)
            ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
        var revision = await db.PrincipalRfqCommercialDraftRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == prepared.DraftRevisionId
                && x.DraftId == head.Id, ct)
            ?? throw new Problem("RfqCommercialPreparedPlanChanged", 409);
        PrincipalRfqPreparedPlanEvidence.Verify(prepared, head, revision);
        if (workflow.SourceVersion != prepared.DraftRevision
            || opening.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(opening.SnapshotJson))
            throw new Problem("RfqCommercialReviewChanged", 409);
        _ = await RequireCommercialReviewerIndependence(db, core, scope,
            source, actor, ct);
        await using var documents = new DocumentDbContext(
            new DbContextOptionsBuilder<DocumentDbContext>()
                .UseNpgsql(db.Database.GetDbConnection()).Options, scope);
        await documents.Database.UseTransactionAsync(
            db.Database.CurrentTransaction!.GetDbTransaction(), ct);
        await RequireRetainedCommercialRequirementsAsync(db, documents, scope,
            prepared, head, revision, ct, currentPurposeAuthorized: true);
        return new(opening, prepared, head, revision, workflow, assignment, actor);
    }

    private static Task<IResult> CommercialRoundReview(Guid id, Guid assignmentId,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            var review = await CurrentCommercialReview(db, cases, core, auth,
                http, scope, id, assignmentId, token);
            var draft = PrincipalRfqCommercialDraft.Restore(review.Head,
                review.Revision);
            // Criteria and requirements are the frozen prepared plan. Supplier
            // offer bodies remain sealed; no price or bid count is projected.
            return new Reply(JsonSerializer.Serialize(new
            {
                roundId = review.Opening.Id,
                eventId = review.Opening.SourcingEventId,
                branchId = review.Opening.BranchId,
                review.Case.Id,
                caseVersion = review.Case.Version,
                assignmentId = review.Assignment.Id,
                assignmentVersion = review.Assignment.Version,
                openingSha256 = review.Opening.SnapshotSha256,
                review.Opening.OpensAt,
                review.Opening.ClosesAt,
                preparedPlanId = review.Prepared.Id,
                preparedPlanSha256 = review.Prepared.SnapshotSha256,
                evaluationDefinitionJson = draft.EvaluationDefinitionJson,
                requirementDocumentCount = draft.Requirements.Count,
                purposeDocumentRoute = $"/api/procurement/v1/sourcing/commercial-round-reviews/{id:D}/assignments/{assignmentId:D}/documents",
                sealedOffersVisible = false,
                awardMade = false,
            }, Json));
        }, ct);
}
