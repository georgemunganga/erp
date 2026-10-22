using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService authorization,
        IServiceProvider services, CancellationToken ct)
    {
        if (!await ProcurementPrincipalApprovalSchemaGuard.ReadyAsync(db,services,false,ct)
            && !await ProcurementFinanceCheckerSchemaGuard.ReadyAsync(db, services, false, ct))
            return ProcurementFinanceCheckerSchemaGuard.Unavailable();
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
        if(request.RequesterIdentitySchemaVersion==1)
            return await PrincipalReadiness(request,evaluation,policy,finance,db,core,scope,http,authorization,services,ct);
        var readiness = RequestApprovalReadiness.Check(request, evaluation, policy, finance);
        var route = ApprovalRoutePreview.FromEvaluation(evaluation,
            request.Lines.Where(x => x.ApprovalStatus != "Cancelled").Select(x => x.Id).ToArray());
        var candidates = route.HasSingleRoute && PurchaseRequestOwnership.HasLegacyWorkerEvidenceIdentity(request) && request.RequesterWorkerId is { } requesterWorker
            && requesterWorker != Guid.Empty
            ? await LoadCandidates(requesterWorker, core, scope, ct)
            : new Dictionary<string, IReadOnlyList<ApprovalAssignee>>();
        var assignments = ApprovalAssigneePreview.Resolve(route, request.CreatedBy, candidates);
        var blockers = readiness.Blockers.Concat(route.Blockers).Concat(assignments.Blockers).Distinct().ToList();
        var options = services.GetService<ProcurementApprovalExecutionOptions>() ?? new ProcurementApprovalExecutionOptions();
        var canOpen = false;
        if (options.Enabled && !options.ExecutionDatabaseConfigured) blockers.Add("ApprovalExecutionDatabaseNotConfigured");
        if (options.Enabled && options.ExecutionDatabaseConfigured && readiness.PrerequisitesMet && route.HasSingleRoute)
        {
            if (await ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, authorization,
                    ProcurementApprovalExecutionRoutes.OpenPermission, ct) is null)
                blockers.Add("ApprovalOpenPermissionRequired");
            else
            {
                try
                {
                    var resolved = await ProcurementApprovalAssigneeResolver.ResolveAsync(request, evaluation!, core, db, scope, DateTimeOffset.UtcNow, ct);
                    // The enabled preview must use the same eligible set as Open. A broad
                    // role preview may be ambiguous while only one scoped finite grant fits.
                    assignments = new(true, resolved.Select(stage => new ApprovalStageAssignees(
                        stage.Route.Code, stage.Route.Assignee, stage.Route.Mode,
                        stage.Assignees.Select(person => new ApprovalAssignee(person.PrincipalId,
                            candidates.GetValueOrDefault(stage.Route.Assignee)?.FirstOrDefault(x => x.SubjectId == person.PrincipalId)?.DisplayName
                                ?? person.PrincipalId)).ToArray(), null)).ToArray(), []);
                    blockers = readiness.Blockers.Concat(route.Blockers).ToList();
                    _ = await ProcurementApprovalFinanceEligibility.CheckAsync(request, finance!, options, core, db, scope, DateTimeOffset.UtcNow, ct);
                    canOpen = true;
                }
                catch (InvalidOperationException error)
                {
                    blockers.Add(error.Message.Length <= 100 && error.Message.All(char.IsAsciiLetterOrDigit)
                        ? error.Message : "ApprovalPrerequisiteUnavailable");
                }
            }
        }
        return Results.Ok(new
        {
            requestId = id, readiness.RequestVersion, readiness.EstimatedAmount,
            readiness.CurrencyCode, prerequisitesMet = readiness.PrerequisitesMet
                && route.HasSingleRoute && assignments.Resolved && (!options.Enabled || blockers.Count == 0),
            blockers = blockers.Distinct().ToArray(),
            approvalRoute = route, assignmentPlan = assignments, canOpenCase = canOpen,
            policyEvaluationId = evaluation?.Id, financeCheckId = finance?.Id,
            isAuthority = false,
            nextStep = request.ApprovalInstanceRef is not null
                ? "Approvals have already started. Check the assigned task or request history."
                : canOpen ? "Start approvals to send this request to the assigned reviewers."
                : !options.Enabled ? "Purchase approvals are not enabled yet. Your request is saved."
                : "Resolve the listed checks before starting approvals.",
        });
    }

    private static async Task<IResult> PrincipalReadiness(Mightyfin.Erp.Procurement.Domain.PurchaseRequest request,
        Mightyfin.Erp.Procurement.Domain.PolicyEvaluation? evaluation,Mightyfin.Erp.Procurement.Domain.PolicyVersion? policy,
        Mightyfin.Erp.Procurement.Domain.BudgetCheckSnapshot? finance,ProcurementDbContext db,HrmDbContext core,
        ProcurementRequestScope scope,HttpContext http,IAuthorizationService authorization,IServiceProvider services,CancellationToken ct)
    {
        var readiness=RequestApprovalReadiness.CheckPrincipal(request,evaluation,policy,finance);
        var route=ApprovalRoutePreview.FromEvaluation(evaluation,request.Lines.Where(x=>x.ApprovalStatus!="Cancelled").Select(x=>x.Id).ToArray());
        var blockers=readiness.Blockers.Concat(route.Blockers).Distinct().ToList();
        var options=services.GetService<ProcurementApprovalExecutionOptions>()??new();
        var assignments=new ApprovalAssigneePreviewResult(false,[],["PrincipalApprovalUnavailable"]);
        var canOpen=false;
        if(!options.Enabled || !options.PrincipalExecutionEnabled)blockers.Add("PrincipalApprovalExecutionUnavailable");
        else if(!options.ExecutionDatabaseConfigured)blockers.Add("ApprovalExecutionDatabaseNotConfigured");
        else if(!await ProcurementPrincipalApprovalSchemaGuard.ExecutionReadyAsync(db,services,ct))blockers.Add("PrincipalApprovalSchemaUnavailable");
        else if(readiness.PrerequisitesMet && route.HasSingleRoute)
        {
            var actor=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,scope.SubjectId,ct);
            if(actor is null || scope.PrincipalId?.ToString("D")!=actor.PrincipalId
                || !actor.Permissions.Contains(ProcurementApprovalExecutionRoutes.OpenPermission)
                || !(await authorization.AuthorizeAsync(http.User,ProcurementApprovalExecutionRoutes.OpenPermission)).Succeeded)
                blockers.Add("ApprovalOpenPermissionRequired");
            else
            {
                try
                {
                    var plan=await ProcurementPrincipalApprovalAssignees.ResolveAsync(request,evaluation!,core,db,scope,DateTimeOffset.UtcNow,ct);
                    assignments=new(true,plan.Select(x=>new ApprovalStageAssignees(x.Route.Code,x.Route.Assignee,x.Route.Mode,
                        x.Assignees.Select(a=>new ApprovalAssignee(a.Person.PrincipalId,a.Person.PrincipalId)).ToArray(),null)).ToArray(),[]);
                    _=await ProcurementApprovalFinanceEligibility.CheckPrincipalAsync(request,finance!,options,core,db,scope,DateTimeOffset.UtcNow,ct);
                    canOpen=true;
                }
                catch(InvalidOperationException error) when(error.InnerException is null && error.Message.Length<=100
                    && error.Message.All(char.IsAsciiLetterOrDigit)) { blockers.Add(error.Message); }
            }
        }
        return Results.Ok(new {requestId=request.Id,readiness.RequestVersion,readiness.EstimatedAmount,readiness.CurrencyCode,
            prerequisitesMet=readiness.PrerequisitesMet && route.HasSingleRoute && assignments.Resolved && blockers.Count==0,
            blockers=blockers.Distinct().ToArray(),approvalRoute=route,assignmentPlan=assignments,canOpenCase=canOpen,
            policyEvaluationId=evaluation?.Id,financeCheckId=finance?.Id,isAuthority=false,
            nextStep=canOpen?"Start approvals to send this request to the assigned reviewers.":"Resolve the listed checks before starting approvals."});
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<ApprovalAssignee>>> LoadCandidates(
        Guid requesterWorkerId, HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var requester = await core.Workers.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.Id == requesterWorkerId && x.Status == "active" && !x.IsArchived)
            .Select(x => new { x.ManagerId }).SingleOrDefaultAsync(ct);
        var lineManagers = new List<ApprovalAssignee>();
        if (requester?.ManagerId is { } managerId)
        {
            var manager = await ProcurementAuthorityIdentity.ResolveCanonicalWorkerAccountAsync(core,
                scope.TenantId, scope.LegalEntityId, managerId, ct);
            if (manager is not null)
            {
                var named = await ProcurementAuthorityIdentity.NamedAccountAsync(core, scope, manager.PrincipalId, ct);
                if (named is not null) lineManagers.Add(new(manager.PrincipalId, named.DisplayName));
            }
        }
        var procurementManagers = new List<ApprovalAssignee>();
        var candidates = await core.LocalUsers.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.IsActive && !x.IsArchived && x.RolesCsv.Contains("procurement_manager"))
            .Select(x => new { x.Id, x.DisplayName }).ToArrayAsync(ct);
        foreach (var candidate in candidates)
        {
            var named = await ProcurementAuthorityIdentity.NamedAccountAsync(core, scope, candidate.Id.ToString("D"), ct);
            if (named is not null && LocalIdentityRoutes.ParseRoles(named.Account.RolesCsv)
                .Contains("procurement_manager", StringComparer.OrdinalIgnoreCase))
                procurementManagers.Add(new(named.Account.PrincipalId, named.DisplayName));
        }
        return new Dictionary<string, IReadOnlyList<ApprovalAssignee>>(StringComparer.Ordinal)
        {
            ["LineManager"] = lineManagers,
            ["BudgetOwner"] = [],
            ["ProcurementManager"] = procurementManagers,
        };
    }
}
