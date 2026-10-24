using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record PrincipalOrderAssignee(ProcurementPrincipalPeople.Person Person,
    ProcurementSpendingAuthorityEvidence Authority);
internal sealed record PrincipalOrderStage(PolicyPreviewStage Route,IReadOnlyList<PrincipalOrderAssignee> Assignees);

/// <summary>Exact PO-specific current authority, separate from PR approval. Core
/// account/branch/action and retained/current worker aliases must all agree.</summary>
internal static class ProcurementPrincipalOrderAuthorityResolver
{
    internal static async Task<IReadOnlyList<PrincipalOrderStage>> ResolveAsync(PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> sources,IReadOnlyList<PolicyPreviewStage> stages,
        PrincipalOrderFundingEvidence funding,HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,DateTimeOffset now,CancellationToken ct)
    {
        if(stages.Count==0 || stages.Select(x=>x.Code).Distinct(StringComparer.Ordinal).Count()!=stages.Count)
            throw new InvalidOperationException("OrderApprovalRouteInvalid");
        var grants=await db.SpendingAuthorityGrants.Include(x=>x.Categories)
            .Where(x=>x.Action==ProcurementSpendingActions.PurchaseOrder.Action
                &&x.Permission==ProcurementSpendingActions.PurchaseOrder.Permission)
            .ToArrayAsync(ct);
        var eligible=new List<PrincipalOrderAssignee>();
        foreach(var principal in grants.Select(x=>x.PrincipalId).Distinct(StringComparer.Ordinal))
        {
            var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,principal,ct);
            if(person is null || !person.Permissions.Contains("procurement-po-approve")
                || !person.OrganisationWide && (order.BranchRef is not { } branch
                    || !person.AllowedBranches.Contains(branch)))continue;
            if(!await IndependentAsync(person,order,sources,funding,core,scope,ct))continue;
            var authority=ProcurementSpendingAuthority.Evaluate(PurchaseOrderCommercialWorkflow.SpendingContext(order,sources),
                principal,"procurement-po-approve","PurchaseOrder.Approve",now,
                grants.Where(x=>x.PrincipalId==principal));
            if(!authority.Eligible || authority.Evidence is null)continue;
            var selected=grants.Single(x=>x.Id==authority.Evidence.GrantId);
            await ProcurementSpendingAuthorityPrincipalIdentity.RequireEligibleRecipientAsync(core,db,scope,
                selected,person,ct);
            eligible.Add(new(person,authority.Evidence));
        }
        var resolved=new List<PrincipalOrderStage>();
        foreach(var stage in stages)
        {
            if(stage.Assignee!="ProcurementManager" || stage.Mode is not ("One" or "All"))
                throw new InvalidOperationException("OrderApprovalAssigneeMappingRequired");
            if(eligible.Count==0 || stage.Mode=="One" && eligible.Count!=1)
                throw new InvalidOperationException("OrderApprovalAuthorityUnavailable");
            resolved.Add(new(stage,eligible.OrderBy(x=>x.Person.PrincipalId,StringComparer.Ordinal).ToArray()));
        }
        return resolved;
    }

    internal static async Task<PrincipalOrderAssignee> ValidateAsync(PurchaseOrder order,
        IReadOnlyList<PurchaseOrderSourceLine> sources,PrincipalOrderFundingEvidence funding,
        PrincipalOrderSubmission submission,PrincipalOrderAssignmentEvidence pin,HrmDbContext core,
        ProcurementDbContext db,ProcurementRequestScope scope,DateTimeOffset now,CancellationToken ct)
    {
        if(pin.SubmissionId!=submission.Id || pin.CoreApprovalCaseId!=submission.CoreApprovalCaseId)
            throw new InvalidOperationException("OrderAssignmentChanged");
        var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,pin.PrincipalId,ct)
            ??throw new InvalidOperationException("OrderAssigneeUnavailable");
        if(person.CoreLinkedWorkerId!=pin.WorkerAliasId || !person.Permissions.Contains("procurement-po-approve")
            || !person.OrganisationWide && (order.BranchRef is not { } branch || !person.AllowedBranches.Contains(branch))
            || !await IndependentAsync(person,order,sources,funding,core,scope,ct))
            throw new InvalidOperationException("OrderAssigneeIdentityChanged");
        var grants=await db.SpendingAuthorityGrants.Include(x=>x.Categories)
            .Where(x=>x.PrincipalId==pin.PrincipalId&&x.Action=="PurchaseOrder.Approve"
                &&x.Permission=="procurement-po-approve").ToArrayAsync(ct);
        var context=PurchaseOrderCommercialWorkflow.SpendingContext(order,sources) with{Amount=submission.LineBaseAmount};
        var result=ProcurementSpendingAuthority.Evaluate(context,pin.PrincipalId,"procurement-po-approve",
            "PurchaseOrder.Approve",now,grants);
        if(!result.Eligible || result.Evidence is null || result.Evidence.GrantId!=pin.GrantId
            || result.Evidence.GrantRevision!=pin.GrantRevision
            || result.Evidence.AuthorityVersion!=pin.AuthorityVersion)
            throw new InvalidOperationException("OrderApprovalSpendingAuthorityChanged");
        var selected=grants.Single(x=>x.Id==pin.GrantId);
        await ProcurementSpendingAuthorityPrincipalIdentity.RequireEligibleRecipientAsync(core,db,scope,
            selected,person,ct);
        return new(person,result.Evidence);
    }

    private static async Task<bool> IndependentAsync(ProcurementPrincipalPeople.Person person,
        PurchaseOrder order,IReadOnlyList<PurchaseOrderSourceLine> sources,
        PrincipalOrderFundingEvidence funding,HrmDbContext core,ProcurementRequestScope scope,CancellationToken ct)
    {
        var originals=new List<(string Principal,Guid? Alias)>{(order.BuyerSubjectId,funding.BuyerWorkerAliasId),
            (funding.RecordedByPrincipalId,funding.RecordedByWorkerAliasId)};
        foreach(var source in sources)
        {
            originals.Add((source.Source.Submission.MakerSubjectId,source.Source.Submission.MakerWorkerId));
            originals.Add((source.Source.Submission.RequesterPrincipalId?.ToString("D")??"",source.Source.Submission.RequesterWorkerId));
            originals.Add((source.Source.Finance.CheckedBySubjectId,source.Source.Finance.CheckedByWorkerId));
        }
        foreach(var original in originals.Distinct())
            if(!ProcurementPrincipalEvidence.Canonical(original.Principal)
                || !await ProcurementPrincipalPeople.IndependentNow(core,scope.TenantId,
                    original.Principal,original.Alias,person.PrincipalId,person.CoreLinkedWorkerId,ct))return false;
        return true;
    }
}
