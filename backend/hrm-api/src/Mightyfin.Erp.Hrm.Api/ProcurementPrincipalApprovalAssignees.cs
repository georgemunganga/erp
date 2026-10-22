using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record ProcurementPrincipalApprovalAssignee(ProcurementPrincipalPeople.Person Person,
    PurchaseRequestAuthorityEvidence Authority);
internal sealed record ProcurementPrincipalApprovalStage(PolicyPreviewStage Route,
    IReadOnlyList<ProcurementPrincipalApprovalAssignee> Assignees);

/// <summary>Explicit successor resolution. Callers require successor schema readiness and
/// current command scope before invoking it; this class does not enable any route.</summary>
internal static class ProcurementPrincipalApprovalAssignees
{
    internal static async Task<IReadOnlyList<ProcurementPrincipalApprovalStage>> ResolveAsync(
        PurchaseRequest request,PolicyEvaluation evaluation,HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,DateTimeOffset now,CancellationToken ct)
    {
        if(!PurchaseRequestPrincipalIdentity.Valid(request))throw new InvalidOperationException("PrincipalApprovalOriginalIdentityRequired");
        var route=ApprovalRoutePreview.FromEvaluation(evaluation,request.Lines.Where(x=>x.ApprovalStatus!="Cancelled").Select(x=>x.Id).ToArray());
        if(!route.HasSingleRoute || route.Lines.Count==0)throw new InvalidOperationException("ApprovalSingleRouteRequired");
        var stages=route.Lines[0].Stages;
        if(stages.Count==0 || stages.Select(x=>x.Code).Distinct(StringComparer.Ordinal).Count()!=stages.Count)
            throw new InvalidOperationException("ApprovalRouteInvalid");
        var output=new List<ProcurementPrincipalApprovalStage>();
        foreach(var stage in stages)
        {
            if(stage.Mode is not ("One" or "All"))throw new InvalidOperationException("ApprovalModeUnsupported");
            var candidates=new List<ProcurementPrincipalPeople.Person>();
            if(stage.Assignee=="LineManager")
            {
                // This strategy alone requires HR. Core permission never manufactures a manager.
                if(request.RequesterWorkerId is not { } worker || worker==Guid.Empty)
                    throw new InvalidOperationException("ApprovalLineManagerUnavailable");
                var requesterAccount=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,request.CreatedBy,ct);
                if(requesterAccount?.WorkerId!=worker)throw new InvalidOperationException("ApprovalLineManagerUnavailable");
                var requester=await core.Workers.AsNoTracking().Where(x=>x.TenantId==scope.TenantId
                    && x.Id==worker && !x.IsArchived && x.Status=="active")
                    .Select(x=>new {x.ManagerId}).SingleOrDefaultAsync(ct);
                if(requester?.ManagerId is not { } manager)throw new InvalidOperationException("ApprovalLineManagerUnavailable");
                var ids=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==scope.TenantId && x.WorkerId==manager)
                    .Select(x=>x.Id).Take(2).ToArrayAsync(ct);
                if(ids.Length!=1)throw new InvalidOperationException("ApprovalLineManagerUnavailable");
                var current=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,ids[0].ToString("D"),ct);
                if(current?.WorkerId==manager)candidates.Add(current);
            }
            else if(stage.Assignee=="ProcurementManager")
            {
                var ids=await ProcurementPrincipalPeople.Candidates(core,scope,now)
                    .Where(x=>x.RolesCsv.Contains("procurement_manager")).Select(x=>x.Id).ToArrayAsync(ct);
                foreach(var id in ids)
                {
                    var current=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,id.ToString("D"),ct);
                    var roles=await core.LocalUsers.AsNoTracking().Where(x=>x.TenantId==scope.TenantId && x.Id==id)
                        .Select(x=>x.RolesCsv).SingleAsync(ct);
                    if(current is not null && LocalIdentityRoutes.ParseRoles(roles).Contains("procurement_manager",StringComparer.OrdinalIgnoreCase))candidates.Add(current);
                }
            }
            else throw new InvalidOperationException(stage.Assignee=="BudgetOwner"?"BudgetOwnerMappingRequired":"ApprovalAssigneeKindUnsupported");
            var eligible=new List<ProcurementPrincipalApprovalAssignee>();
            foreach(var candidate in candidates.DistinctBy(x=>x.PrincipalId))
            {
                if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,request.CreatedBy,
                    request.RequesterCoreWorkerId,candidate.PrincipalId,candidate.CoreLinkedWorkerId,ct))continue;
                var authority=await AuthorityAsync(candidate,request,core,db,scope,now,ct);
                if(authority is not null)eligible.Add(new(candidate,authority));
            }
            if(eligible.Count==0)throw new InvalidOperationException("ApprovalAssigneeAuthorityUnavailable");
            if(stage.Mode=="One" && eligible.Count!=1)throw new InvalidOperationException("ApprovalAssigneeAmbiguous");
            output.Add(new(stage,eligible.OrderBy(x=>x.Person.PrincipalId,StringComparer.Ordinal).ToArray()));
        }
        return output;
    }

    internal static async Task ValidateAssignmentAsync(PurchaseRequest request,PurchaseRequestAssignmentAuthorityEvidence value,
        string stageCode,HrmDbContext core,ProcurementDbContext db,ProcurementRequestScope scope,
        DateTimeOffset now,CancellationToken ct)
    {
        var retained=PurchaseRequestPrincipalAssignmentIdentity.ReadVerified(value)
            ?? throw new InvalidOperationException("ApprovalPrincipalAssignmentEvidenceInvalid");
        var source=await db.ApprovalSubmissions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==scope.TenantId
            && x.LegalEntityId==scope.LegalEntityId && x.Id==value.SubmissionId && x.PurchaseRequestId==request.Id,ct);
        if(source is null || !PurchaseRequestApprovalPinning.HasRetainedPrincipalIdentity(source)
            || source.CoreApprovalCaseId!=value.CoreApprovalCaseId || source.MakerSubjectId!=request.CreatedBy
            || source.RequesterPrincipalId!=request.RequesterPrincipalId
            || source.RequesterIdentitySha256!=request.RequesterIdentitySha256 || source.RequesterWorkerId!=request.RequesterCoreWorkerId)
            throw new InvalidOperationException("ApprovalPrincipalSourceIdentityInvalid");
        if(!await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,source.MakerSubjectId,source.MakerWorkerId,
            value.PrincipalId,value.AssigneeWorkerId,ct)
            || !await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,request.CreatedBy,source.RequesterWorkerId,
                value.PrincipalId,value.AssigneeWorkerId,ct))
            throw new InvalidOperationException("ApprovalIdentityChanged");
        var evaluation=await db.PolicyEvaluations.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==source.PolicyEvaluationId,ct)
            ?? throw new InvalidOperationException("ApprovalPolicyEvaluationMissing");
        var plan=await ResolveAsync(request,evaluation,core,db,scope,now,ct);
        var stage=plan.SingleOrDefault(x=>x.Route.Code==stageCode);
        var current=stage?.Assignees.SingleOrDefault(x=>x.Person.PrincipalId==value.PrincipalId);
        if(stage is null || stage.Route.Assignee!=value.AssigneeKind || current is null
            || current.Authority.GrantId!=value.GrantId || current.Authority.GrantRevision!=value.GrantRevision
            || current.Authority.AuthorityVersion!=value.AuthorityVersion
            || retained.Authority.RequestAmount!=source.EstimatedAmount || retained.Authority.CurrencyCode!=source.CurrencyCode)
            throw new InvalidOperationException("ApprovalSpendingAuthorityChanged");
    }

    internal static async Task<PurchaseRequestAuthorityEvidence?> AuthorityAsync(ProcurementPrincipalPeople.Person person,
        PurchaseRequest request,HrmDbContext core,ProcurementDbContext db,ProcurementRequestScope scope,
        DateTimeOffset now,CancellationToken ct)
    {
        if(!person.Permissions.Contains(SpendingAuthorityConfiguration.ApprovalPermission)
            || !person.OrganisationWide && (request.BranchRef is not { } branch || !person.AllowedBranches.Contains(branch)))return null;
        var grants=await db.SpendingAuthorityGrants.Include(x=>x.Categories).Where(x=>x.PrincipalId==person.PrincipalId
            && x.Permission==SpendingAuthorityConfiguration.ApprovalPermission && x.Action==SpendingAuthorityConfiguration.ApprovalAction).ToListAsync(ct);
        var result=PurchaseRequestSpendingAuthority.Evaluate(request,person.PrincipalId,
            SpendingAuthorityConfiguration.ApprovalPermission,SpendingAuthorityConfiguration.ApprovalAction,
            PurchaseRequestSpendingAuthority.SupportedAmountBasis,now,grants);
        if(!result.Eligible || result.Evidence is null)return null;
        await ProcurementSpendingAuthorityPrincipalIdentity.RequireEligibleRecipientAsync(core,db,scope,
            grants.Single(x=>x.Id==result.Evidence.GrantId),person,ct);
        return result.Evidence;
    }
}
