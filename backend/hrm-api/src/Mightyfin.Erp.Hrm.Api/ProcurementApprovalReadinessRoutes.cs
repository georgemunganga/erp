using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Buyer view of unmet purchase approval prerequisites; no state change.</summary>
public static class ProcurementApprovalReadinessRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/procurement/v1/requests/{id:guid}/approval-readiness", Get)
            .RequireAuthorization("procurement-request-return");
    }

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var request = await db.PurchaseRequests.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null || scope.IsConfined && request.BranchRef != scope.WorkLocationId)
            return Results.NotFound(new { code = "not-found", message = "Request not found." });
        var version = request.SubmittedVersion ?? request.Version;
        var evaluation = await db.PolicyEvaluations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PurchaseRequestId == id && x.RequestVersion == version, ct);
        var policy = evaluation is null ? null : await db.PolicyVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == evaluation.PolicyVersionId, ct);
        var finance = await db.BudgetCheckSnapshots.AsNoTracking()
            .Where(x => x.PurchaseRequestId == id && x.RequestVersion == version)
            .OrderByDescending(x => x.CheckedAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        var readiness = RequestApprovalReadiness.Check(request, evaluation, policy, finance);
        var route = ApprovalRoutePreview.FromEvaluation(evaluation,
            request.Lines.Where(x => x.ApprovalStatus != "Cancelled").Select(x => x.Id).ToArray());
        var blockers = readiness.Blockers.Concat(route.Blockers).Distinct().ToArray();
        return Results.Ok(new
        {
            requestId = id, readiness.RequestVersion, readiness.EstimatedAmount,
            readiness.CurrencyCode, prerequisitesMet = readiness.PrerequisitesMet && route.HasSingleRoute,
            blockers,
            approvalRoute = route, canOpenCase = false,
            policyEvaluationId = evaluation?.Id, financeCheckId = finance?.Id,
            isAuthority = false,
            nextStep = readiness.PrerequisitesMet && route.HasSingleRoute
                ? "Assign independent approvers and verify Finance integration before opening a workflow."
                : "Resolve the listed checks before an approval workflow can open.",
        });
    }
}
