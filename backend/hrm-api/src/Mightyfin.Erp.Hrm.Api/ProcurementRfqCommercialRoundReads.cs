using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static readonly string[] CommercialReadActions =
    [
        "procurement-sourcing-commercial-configure",
        "procurement-sourcing-round-open",
        PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
        PrincipalRfqSealedManualOffers.SubmitAction,
        PrincipalRfqSealedManualOffers.CloseAction,
    ];

    private static async Task<RfqPrincipalActor> CommercialReader(HrmDbContext core,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService auth,
        Guid? branch, CancellationToken ct)
    {
        foreach (var action in CommercialReadActions)
        {
            try { return await PrincipalActor(core, scope, http, auth, action, branch,
                branch is not null, ct); }
            catch (Problem error) when (error.Status == 403) { }
        }
        throw new Problem("RfqCommercialReadDenied", 403);
    }

    private sealed record CommercialVisibility(RfqPrincipalActor Actor, bool Maker,
        bool Reviewer, bool Closer, Guid[] AssignedCaseIds);

    private static async Task<CommercialVisibility> CommercialVisible(HrmDbContext core,
        ApprovalDbContext cases, ProcurementRequestScope scope, HttpContext http,
        IAuthorizationService auth, CancellationToken ct)
    {
        var actor = await CommercialReader(core, scope, http, auth, null, ct);
        var person = await ProcurementPrincipalPeople.Resolve(core, scope.TenantId,
            scope.LegalEntityId, actor.PrincipalId, ct)
            ?? throw new Problem("RfqCommercialReadDenied", 403);
        async Task<bool> Granted(string action) => person.Permissions.Contains(action)
            && (await auth.AuthorizeAsync(http.User, action)).Succeeded;
        var maker = await Granted("procurement-sourcing-commercial-configure")
            || await Granted("procurement-sourcing-round-open")
            || await Granted(PrincipalRfqSealedManualOffers.SubmitAction);
        var reviewer = await Granted(PrincipalRfqCommercialRoundPolicyRules.ApprovalAction);
        var closer = await Granted(PrincipalRfqSealedManualOffers.CloseAction);
        Guid[] assigned = [];
        if (reviewer)
        {
            assigned = await (from assignment in cases.Assignments.AsNoTracking()
                join review in cases.Cases.AsNoTracking() on assignment.CaseId equals review.Id
                where assignment.TenantId == scope.TenantId
                    && assignment.LegalEntityId == scope.LegalEntityId
                    && review.TenantId == scope.TenantId && review.LegalEntityId == scope.LegalEntityId
                    && review.SourceModule == "procurement"
                    && review.SourceType == PrincipalRfqCommercialRoundCoreWorkflow.SourceType
                    && assignment.AssigneeSubjectId == actor.PrincipalId
                    && assignment.PermissionKey == PrincipalRfqCommercialRoundPolicyRules.ApprovalAction
                select review.Id).Distinct().Take(10001).ToArrayAsync(ct);
            if (assigned.Length > 10000) throw new Problem("RfqCommercialReadScopeTooLarge", 503);
        }
        return new(actor, maker, reviewer, closer, assigned);
    }

    private static IQueryable<PrincipalRfqCommercialRoundOpeningRecord> VisibleRounds(
        ProcurementDbContext db, ProcurementRequestScope scope, CommercialVisibility visibility)
    {
        var branches = visibility.Actor.Branches.ToArray();
        var cases = visibility.AssignedCaseIds;
        var principal = visibility.Actor.PrincipalId;
        var organisationWide = visibility.Actor.OrganisationWide;
        var selected = visibility.Actor.SelectedBranchId;
        var maker = visibility.Maker;
        var reviewer = visibility.Reviewer;
        var closer = visibility.Closer;
        return db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && (selected == null || x.BranchId == selected)
                && (organisationWide || branches.Contains(x.BranchId))
                && (maker && x.ActorPrincipalId == principal
                    || reviewer && cases.Contains(x.CoreCaseId)
                    || closer && x.ClosingOfficerPrincipalId == principal));
    }

    private static Task<IResult> CommercialRounds(HttpContext http,
        ProcurementRequestScope scope, int? offset, int? limit, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            if (offset is < 0 or > 100000 || limit is < 1 or > 50)
                throw new Problem("RfqCommercialPageInvalid", 422);
            var visibility = await CommercialVisible(core, cases, scope, http, auth, token);
            var rows = VisibleRounds(db, scope, visibility);
            var total = await rows.CountAsync(token);
            var page = await rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip(offset ?? 0).Take(limit ?? 25)
                .Select(x => new { x.Id, x.SourcingEventId, x.PreparedPlanId, x.BranchId,
                    x.OpensAt, x.ClosesAt, x.CreatedAt, x.EventVersion })
                .ToArrayAsync(token);
            return new Reply(JsonSerializer.Serialize(new
            { items = page, total, offset = offset ?? 0, limit = limit ?? 25 }, Json));
        }, ct);

    private static Task<IResult> CommercialRound(Guid id, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            if (id == Guid.Empty) throw new Problem("RfqCommercialRoundNotFound", 404);
            var visibility = await CommercialVisible(core, cases, scope, http, auth, token);
            var opening = await VisibleRounds(db, scope, visibility)
                .SingleOrDefaultAsync(x => x.Id == id, token)
                ?? throw new Problem("RfqCommercialRoundNotFound", 404);
            _ = await CommercialReader(core, scope, http, auth, opening.BranchId, token);
            var decision = await db.PrincipalRfqCommercialRoundDecisions.AsNoTracking()
                .Where(x => x.OpeningId == id && x.BranchId == opening.BranchId)
                .Select(x => new { x.Id, x.State, x.DecidedAt, x.CoreCaseId })
                .SingleOrDefaultAsync(token);
            var seal = await db.PrincipalRfqCommercialRoundSeals.AsNoTracking()
                .Where(x => x.OpeningId == id && x.BranchId == opening.BranchId)
                .Select(x => new { x.Id, x.ClosedAt, x.SubmissionCount })
                .SingleOrDefaultAsync(token);
            // Submitted offer bodies remain sealed; this is only round status.
            return new Reply(JsonSerializer.Serialize(new
            { opening.Id, opening.SourcingEventId, opening.PreparedPlanId,
                opening.BranchId, opening.OpensAt, opening.ClosesAt,
                opening.EventVersion, decision, seal, awardMade = false,
                supplierDispatchConfirmed = false }, Json));
        }, ct);

    // The Core assignment and the retained round branch are both fenced in SQL
    // before count/page. A commercial read action alone is not a review task.
    private static Task<IResult> CommercialRoundAssigned(int? offset, int? limit,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            if (offset is < 0 or > 100000 || limit is < 1 or > 50)
                throw new Problem("RfqCommercialPageInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth,
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction, null, false, token);
            var branches = actor.Branches.ToArray();
            var selected = actor.SelectedBranchId;
            var workflow = cases.Cases.FromSqlInterpolated($"""
                SELECT c.* FROM core_approval.cases c
                JOIN procurement.principal_rfq_commercial_round_openings opening
                  ON opening.core_case_id=c."Id" AND opening.tenant_id=c."TenantId"
                  AND opening.legal_entity_id=c."LegalEntityId"
                WHERE c."TenantId"={scope.TenantId} AND c."LegalEntityId"={scope.LegalEntityId}
                  AND c."SourceModule"='procurement' AND c."SourceType"='sourcing_commercial_round'
                  AND c."State"='Pending' AND opening.prepared_plan_id=c."SourceId"
                  AND ({selected}::uuid IS NULL OR opening.branch_id={selected}::uuid)
                  AND ({actor.OrganisationWide} OR opening.branch_id=ANY({branches}))
                  AND EXISTS (
                    SELECT 1 FROM core_approval.stages stage
                    JOIN core_approval.assignments assignment
                      ON assignment."StageId"=stage."Id"
                      AND assignment."TenantId"=stage."TenantId"
                      AND assignment."LegalEntityId"=stage."LegalEntityId"
                    WHERE stage."CaseId"=c."Id" AND stage."TenantId"=c."TenantId"
                      AND stage."LegalEntityId"=c."LegalEntityId"
                      AND stage."State"='Active' AND assignment."State"='Pending'
                      AND assignment."CaseId"=c."Id"
                      AND assignment."AssigneeSubjectId"={actor.PrincipalId}
                      AND assignment."PermissionKey"='procurement-sourcing-commercial-approve'
                      AND assignment."DelegatedFromSubjectId" IS NULL)
                """).AsNoTracking();
            var total = await workflow.CountAsync(token);
            var rows = await workflow.OrderBy(x => x.OpenedAt).ThenBy(x => x.Id)
                .Skip(offset ?? 0).Take(limit ?? 25).Select(x => new { x.Id, x.Version,
                    x.SourceId, x.OpenedAt }).ToArrayAsync(token);
            var ids = rows.Select(x => x.Id).ToArray();
            var openings = await db.PrincipalRfqCommercialRoundOpenings.AsNoTracking()
                .Where(x => ids.Contains(x.CoreCaseId) && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId)
                .Select(x => new { x.Id, x.CoreCaseId, x.SourcingEventId, x.BranchId,
                    x.OpensAt, x.ClosesAt }).ToArrayAsync(token);
            if (openings.Length != rows.Length || rows.Any(x =>
                openings.Count(y => y.CoreCaseId == x.Id && y.Id == x.Id) != 1))
                throw new Problem("RfqCommercialReviewEvidenceNeedsReview", 409);
            var pending = await cases.Assignments.AsNoTracking()
                .Where(x => ids.Contains(x.CaseId) && x.TenantId == scope.TenantId
                    && x.LegalEntityId == scope.LegalEntityId && x.AssigneeSubjectId == actor.PrincipalId
                    && x.PermissionKey == PrincipalRfqCommercialRoundPolicyRules.ApprovalAction
                    && x.State == "Pending" && x.DelegatedFromSubjectId == null)
                .Select(x => new { x.Id, x.CaseId, x.Version, x.StageId })
                .ToArrayAsync(token);
            var items = rows.Select(row =>
            {
                var opening = openings.Single(x => x.CoreCaseId == row.Id);
                var assignments = pending.Where(x => x.CaseId == row.Id).ToArray();
                if (assignments.Length == 0) throw new Problem("RfqCommercialReviewEvidenceNeedsReview", 409);
                return new { roundId = opening.Id, opening.SourcingEventId,
                    opening.BranchId, opening.OpensAt, opening.ClosesAt,
                    coreCaseId = row.Id, coreCaseVersion = row.Version,
                    assignments };
            }).ToArray();
            return new Reply(JsonSerializer.Serialize(new
            { items, total, offset = offset ?? 0, limit = limit ?? 25,
                scope = "CurrentCoreAssignment" }, Json));
        }, ct);

    private static Task<IResult> CommercialRoundRecovery(string operation, Guid key,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, unusedCases, core, auth, token) =>
        {
            await RequireCommercial(http, db, false, token);
            var action = operation switch
            {
                "Open" => "procurement-sourcing-round-open",
                "Decide" => PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
                "SubmitOffer" => PrincipalRfqSealedManualOffers.SubmitAction,
                "Close" => PrincipalRfqSealedManualOffers.CloseAction,
                _ => throw new Problem("RfqCommercialCommandNotFound", 404),
            };
            if (key == Guid.Empty) throw new Problem("RfqCommercialCommandNotFound", 404);
            var actor = await PrincipalActor(core, scope, http, auth, action, null, false, token);
            var branches = actor.Branches.ToArray();
            var receipt = await db.PrincipalRfqCommercialCommandReceipts.AsNoTracking()
                .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                    && x.CommandKey == key && x.Operation == operation
                    && x.ActorPrincipalId == actor.PrincipalId
                    && (actor.OrganisationWide || branches.Contains(x.BranchId)))
                .SingleOrDefaultAsync(token)
                ?? throw new Problem("RfqCommercialCommandNotFound", 404);
            _ = await PrincipalActor(core, scope, http, auth, action, receipt.BranchId, true, token);
            var original = await new PrincipalRfqCommercialRoundStore(db, scope)
                .RecoverAsync(key, operation, receipt.SourcingEventId, null, token)
                ?? throw new Problem("RfqCommercialCommandNotFound", 404);
            return new Reply(original.ResponseJson, original.ResponseStatus);
        }, ct);
}
