using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private sealed record RoundLoadedGraph(CommercialRoundEnrollment Enrollment, CommercialRoundPlan Plan,
        CommercialRoundPlanRevision Revision, CommercialRoundCriterion[] Criteria, CommercialRoundParticipant[] Participants,
        CommercialRoundPlanReceipt Receipt);
    private static async Task<RoundLoadedGraph> RoundGraph(ProcurementDbContext db, SourcingEvent source,
        long? version, bool tracked, CancellationToken ct)
    {
        var enrollment = await db.CommercialRoundEnrollments.AsNoTracking().SingleOrDefaultAsync(x => x.SourcingEventId == source.Id, ct)
            ?? throw new Problem("RoundPlanNotFound", 404);
        var plans = tracked ? db.CommercialRoundPlans.AsQueryable() : db.CommercialRoundPlans.AsNoTracking();
        var plan = await plans.SingleOrDefaultAsync(x => x.EnrollmentId == enrollment.Id, ct) ?? throw new Problem("RoundPlanEvidenceInvalid", 409);
        var expected = version ?? plan.Version;
        var revision = await db.CommercialRoundPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.PlanId == plan.Id && x.ResultVersion == expected, ct)
            ?? throw new Problem("RoundPlanNotFound", 404);
        if (expected < 1 || expected > plan.Version || enrollment.SchemaVersion != 1
            || enrollment.SourceEvidenceId != source.SourceEvidenceId || enrollment.SourceEventVersion > source.Version
            || enrollment.OriginalMakerPrincipalId != source.OwnerSubjectId || enrollment.BranchId != source.BranchRef
            || plan.SourcingEventId != source.Id || plan.BranchId != enrollment.BranchId
            || plan.MakerPrincipalId != enrollment.EnrolledByPrincipalId
            || revision.SourcingEventId != source.Id || revision.BranchId != enrollment.BranchId
            || expected == plan.Version && revision.ResultStatus != plan.Status) throw new Problem("RoundPlanEvidenceInvalid", 409);
        var original = await db.SourcingDraftEvidenceRecords.AsNoTracking().SingleAsync(x => x.Id == enrollment.SourceEvidenceId, ct);
        var bindings = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == source.Id).ToArrayAsync(ct);
        _ = GovernedPrincipalRfqRestoration.RestoreVerified(original, source, bindings);
        if (enrollment.SourceSha256 != original.PayloadSha256) throw new Problem("RoundPlanEvidenceInvalid", 409);
        var criteria = await db.CommercialRoundCriteria.AsNoTracking().Where(x => x.RevisionId == revision.Id).OrderBy(x => x.Order).Take(101).ToArrayAsync(ct);
        var participants = await db.CommercialRoundParticipants.AsNoTracking().Where(x => x.RevisionId == revision.Id).OrderBy(x => x.Order).Take(101).ToArrayAsync(ct);
        _ = CommercialRoundPlanDraft.Restore(revision, criteria, participants);
        if (revision.PreviousRevisionId is { } previous)
        {
            if (!await db.CommercialRoundPlanRevisions.AsNoTracking().AnyAsync(x => x.Id == previous && x.PlanId == plan.Id
                && x.ResultVersion == revision.ExpectedVersion, ct)) throw new Problem("RoundPlanHistoryInvalid", 409);
        }
        else if (revision.ResultVersion != 1 || revision.Operation != "Create") throw new Problem("RoundPlanHistoryInvalid", 409);
        var receipt = await db.CommercialRoundPlanReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.RevisionId == revision.Id, ct)
            ?? throw new Problem("RoundPlanReceiptInvalid", 409);
        CommercialRoundPlanDraft.VerifyReceipt(receipt, plan, revision);
        return new(enrollment, plan, revision, criteria, participants, receipt);
    }
    private static async Task<Reply?> RoundReplay(ProcurementDbContext db, ProcurementRequestScope scope,
        Guid eventId, Guid key, string? operation, string? hash, CancellationToken ct)
    {
        var receipt = await db.CommercialRoundPlanReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key, ct);
        if (receipt is null) return null;
        if (receipt.SourcingEventId != eventId || operation is not null && receipt.Operation != operation
            || hash is not null && receipt.RequestSha256 != hash) throw new Problem("RoundPlanCommandKeyConflict", 409);
        var source = await QuotationOwnedSources(db).AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct)
            ?? throw new Problem("RoundPlanNotFound", 404);
        var graph = await RoundGraph(db, source, receipt.ResultVersion, tracked: false, ct);
        if (graph.Receipt.Id != receipt.Id) throw new Problem("RoundPlanReceiptInvalid", 409);
        return new(receipt.ResponseJson, receipt.ResponseStatusCode);
    }
    private static Task<IResult> RoundRecover(Guid id, Guid key, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) => {
            _ = await RoundSource(db, core, scope, http, auth, "procurement-round-plan-read", id, token);
            var receipt = await db.CommercialRoundPlanReceipts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key && x.SourcingEventId == id, token);
            if (receipt is null) throw new Problem("RoundPlanNotFound", 404);
            return await RoundReplay(db, scope, id, key, null, null, token) ?? throw new Problem("RoundPlanNotFound", 404);
        }, ct);
    private static Task<IResult> RoundRead(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RoundReadVersion(id, null, http, scope, ct);
    private static Task<IResult> RoundVersion(Guid id, long version, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RoundReadVersion(id, version, http, scope, ct);
    private static Task<IResult> RoundReadVersion(Guid id, long? version, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) => {
            var source = await RoundSource(db, core, scope, http, auth, "procurement-round-plan-read", id, token);
            var retained=await PrincipalRetainedSourceData(db,source,token,verifyCurrentHoldState:false);
            if (!await db.CommercialRoundEnrollments.AsNoTracking().AnyAsync(x => x.SourcingEventId == id, token))
            {
                if (version is not null) throw new Problem("RoundPlanNotFound", 404);
                var allowedActions=await RoundActions(http,db,cases,core,scope,auth,source,null,true,token);
                return new(JsonSerializer.Serialize(new { enrolled = false, source = RoundSourceView(source,retained), plan = (object?)null,
                    allowedActions,reviewAvailable = false, publicationAvailable = false }, RoundJson), 200);
            }
            var graph = await RoundGraph(db, source, version, tracked: false, token);
            var ids = graph.Participants.Select(x => Guid.Parse(x.PrincipalId)).Append(Guid.Parse(graph.Plan.MakerPrincipalId))
                .Append(Guid.Parse(graph.Revision.ActorPrincipalId)).Distinct().ToArray();
            var names = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == scope.TenantId && ids.Contains(x.Id))
                .Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(token);
            using var content = JsonDocument.Parse(graph.Revision.ContentJson);
            var actions=await RoundActions(http,db,cases,core,scope,auth,source,graph.Plan,
                ProcurementCommercialRoundActions.IsEditableRoute(version),token);
            var reviewReadiness=http.RequestServices.GetRequiredService<IProcurementCommercialRoundReviewReadiness>();
            var reviewReady=reviewReadiness.HasCapturedContract&&await reviewReadiness.ReadyAsync(db,false,token);
            return new(JsonSerializer.Serialize(new { enrolled = true, source = RoundSourceView(source,retained),allowedActions=actions,
                plan = new { graph.Plan.Id, graph.Plan.Status, graph.Plan.Version, graph.Plan.MakerPrincipalId },
                revision = new { graph.Revision.Id, version = graph.Revision.ResultVersion, graph.Revision.Operation,
                    status = graph.Revision.ResultStatus, graph.Revision.ActorPrincipalId, graph.Revision.RecordedAt, graph.Revision.Reason,
                    graph.Revision.ContentSha256 }, content = content.RootElement.Clone(), people = names,
                reviewAvailable = reviewReady, publicationAvailable = false,
                contentIsIncompleteProposal = graph.Revision.ResultStatus != "Published" }, RoundJson), 200);
        }, ct);
    private static Task<IResult> RoundHistory(Guid id, long? afterVersion, int? limit, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) => {
            var after = afterVersion ?? 0; var size = limit ?? 25;
            if (after < 0 || size is < 1 or > 50) throw new Problem("RoundPlanPageInvalid", 422);
            var source = await RoundSource(db, core, scope, http, auth, "procurement-round-plan-read", id, token);
            var graph = await RoundGraph(db, source, null, tracked: false, token);
            var rows = await db.CommercialRoundPlanRevisions.AsNoTracking().Where(x => x.PlanId == graph.Plan.Id
                && x.ResultVersion > after && x.ResultVersion <= graph.Plan.Version).OrderBy(x => x.ResultVersion).Take(size + 1).ToArrayAsync(token);
            var page = rows.Take(size).ToArray();
            var reviewReadiness=http.RequestServices.GetRequiredService<IProcurementCommercialRoundReviewReadiness>();
            var reviewReady=reviewReadiness.HasCapturedContract&&await reviewReadiness.ReadyAsync(db,false,token);
            var ids=page.Select(x=>x.Id).ToArray();
            var submissions=reviewReady?await db.CommercialRoundReviewSubmissions.AsNoTracking()
                .Where(x=>x.PlanId==graph.Plan.Id&&ids.Contains(x.SubmittedRevisionId)).ToArrayAsync(token):Array.Empty<CommercialRoundReviewSubmission>();
            var outcomes=reviewReady?await db.CommercialRoundReviewOutcomes.AsNoTracking()
                .Where(x=>x.PlanId==graph.Plan.Id&&ids.Contains(x.ResultRevisionId)).ToArrayAsync(token):Array.Empty<CommercialRoundReviewOutcome>();
            return new(JsonSerializer.Serialize(new { items = page.Select(x => new { x.Id, version = x.ResultVersion,
                x.Operation, status = x.ResultStatus, x.ActorPrincipalId, x.RecordedAt, x.Reason,
                submissionId=submissions.SingleOrDefault(s=>s.SubmittedRevisionId==x.Id)?.Id,
                caseId=submissions.SingleOrDefault(s=>s.SubmittedRevisionId==x.Id)?.CoreCaseId
                    ?? outcomes.SingleOrDefault(o=>o.ResultRevisionId==x.Id)?.CoreCaseId,
                decisionId=outcomes.SingleOrDefault(o=>o.ResultRevisionId==x.Id)?.CoreDecisionId }),
                nextVersion = rows.Length > size ? (long?)page[^1].ResultVersion : null }, RoundJson), 200);
        }, ct);
    private static Task<IResult> RoundList(int? page, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => RunRound(http, scope, async (db, cases, core, auth, token) => {
            var number = page ?? 1; if (number is < 1 or > 10000) throw new Problem("RoundPlanPageInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth, "procurement-round-plan-read", null, false, token);
            var query = from plan in db.CommercialRoundPlans.AsNoTracking()
                        join source in QuotationOwnedSources(db).AsNoTracking() on plan.SourcingEventId equals source.Id
                        where source.BranchRef != null && (scope.WorkLocationId == null || source.BranchRef == scope.WorkLocationId)
                            && (actor.OrganisationWide || actor.Branches.Contains(source.BranchRef.Value))
                        select new { plan.Id, plan.Status, plan.Version, eventId = source.Id, source.Number, source.Title, source.BranchRef };
            var count = await query.CountAsync(token);
            var rows = await query.OrderBy(x => x.Id).Skip((number - 1) * 25).Take(25).ToArrayAsync(token);
            return new(JsonSerializer.Serialize(new { items = rows, total = count, page = number, pageSize = 25 }, RoundJson), 200);
        }, ct);
}
