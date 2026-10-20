using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
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

    private static async Task<IResult> Get(Guid id, ProcurementDbContext db, HrmDbContext core,
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
        var candidates = route.HasSingleRoute
            ? await LoadCandidates(request.RequesterWorkerId, core, scope, ct)
            : new Dictionary<string, IReadOnlyList<ApprovalAssignee>>();
        var assignments = ApprovalAssigneePreview.Resolve(route, request.CreatedBy, candidates);
        var blockers = readiness.Blockers.Concat(route.Blockers).Concat(assignments.Blockers).Distinct().ToArray();
        return Results.Ok(new
        {
            requestId = id, readiness.RequestVersion, readiness.EstimatedAmount,
            readiness.CurrencyCode, prerequisitesMet = readiness.PrerequisitesMet
                && route.HasSingleRoute && assignments.Resolved,
            blockers,
            approvalRoute = route, assignmentPlan = assignments, canOpenCase = false,
            policyEvaluationId = evaluation?.Id, financeCheckId = finance?.Id,
            isAuthority = false,
            nextStep = readiness.PrerequisitesMet && route.HasSingleRoute && assignments.Resolved
                ? "Confirm Finance reservation integration before opening a workflow."
                : "Resolve the listed checks before an approval workflow can open.",
        });
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<ApprovalAssignee>>> LoadCandidates(
        Guid requesterWorkerId, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var requester = await core.Workers.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Id == requesterWorkerId)
            .Select(x => new { x.ManagerId }).SingleOrDefaultAsync(ct);
        var managerUsers = await core.LocalUsers.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.IsActive && x.WorkerId != null
                && x.RolesCsv.Contains("procurement_manager"))
            .Select(x => new { x.Id, x.WorkerId, x.RolesCsv }).ToListAsync(ct);
        var candidateWorkerIds = managerUsers.Where(x => x.WorkerId.HasValue)
            .Select(x => x.WorkerId!.Value)
            .Concat(new[] { requester?.ManagerId }
                .Where(x => x.HasValue).Select(x => x!.Value))
            .Distinct().ToArray();
        var workers = await core.Workers.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && candidateWorkerIds.Contains(x.Id)
                && x.Status == "active" && x.SubjectId != null).ToListAsync(ct);
        var locationIds = workers.Where(x => x.LocationId.HasValue)
            .Select(x => x.LocationId!.Value).Distinct().ToArray();
        var companyLocations = await core.WorkLocations.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && locationIds.Contains(x.Id) && !x.IsArchived)
            .Select(x => x.Id).ToListAsync(ct);
        var currentAssignments = await core.Assignments.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && candidateWorkerIds.Contains(x.WorkerId) && x.Status == "current" && !x.IsArchived)
            .Select(x => x.WorkerId).ToListAsync(ct);
        var validWorkers = workers.Where(x => x.LocationId is { } locationId
                && companyLocations.Contains(locationId) || currentAssignments.Contains(x.Id))
            .ToDictionary(x => x.Id);
        IReadOnlyList<ApprovalAssignee> Person(Guid? workerId) =>
            workerId is { } id && validWorkers.TryGetValue(id, out var worker)
                ? [new(worker.SubjectId!, worker.FullName)] : [];
        var procurementManagers = managerUsers
            .Where(x => LocalIdentityRoutes.ParseRoles(x.RolesCsv)
                .Contains("procurement_manager", StringComparer.OrdinalIgnoreCase)
                && x.WorkerId is { } workerId && validWorkers.ContainsKey(workerId))
            .Select(x => new ApprovalAssignee(x.Id.ToString(), validWorkers[x.WorkerId!.Value].FullName))
            .ToArray();
        return new Dictionary<string, IReadOnlyList<ApprovalAssignee>>(StringComparer.Ordinal)
        {
            ["LineManager"] = Person(requester?.ManagerId),
            // The department manager is not necessarily the Finance budget owner.
            // This stays unresolved until Finance supplies an authoritative owner mapping.
            ["BudgetOwner"] = [],
            ["ProcurementManager"] = procurementManagers,
        };
    }
}
