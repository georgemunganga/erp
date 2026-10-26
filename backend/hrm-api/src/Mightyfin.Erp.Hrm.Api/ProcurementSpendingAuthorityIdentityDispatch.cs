using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSpendingAuthorityRoutes
{
    private sealed record IdentityPerson(string PrincipalId,Guid? WorkerId);
    private sealed record IdentityAuthor(SpendingAuthorityIdentityEvidence Created,IdentityPerson Actor,IdentityPerson Recipient);
    private sealed record IdentityReview(SpendingAuthorityIdentityEvidence Created,SpendingAuthorityIdentityEvidence Submitted,
        IdentityPerson Actor,IdentityPerson Recipient);

    private static bool PrincipalOption(HttpContext http)=>http.RequestServices.GetService<ProcurementApprovalExecutionOptions>()
        is {PrincipalExecutionEnabled:true};

    // Authority configuration follows the owning action's explicit successor gate.
    // No employee link is invented for current Core principals.
    private static bool PrincipalActionOption(HttpContext http,string action)=>PrincipalOption(http)
        && (action==SpendingAuthorityConfiguration.ApprovalAction
            || action==ProcurementSpendingActions.PurchaseOrder.Action
                && http.RequestServices.GetService<ProcurementOrderExecutionOptions>() is {PrincipalExecutionEnabled:true});

    private static async Task<bool> NewPrincipalIdentity(HttpContext http,ProcurementDbContext db,string action,CancellationToken ct)
    {
        if(!PrincipalActionOption(http,action))return false;
        await RequirePrincipalOrderAuthoritySchema(http,db,action,ct);
        return await ProcurementPrincipalApprovalSchemaGuard.ReadyAsync(db,http.RequestServices,true,ct);
    }

    private static async Task RequirePrincipalOrderAuthoritySchema(HttpContext http,ProcurementDbContext db,
        string action,CancellationToken ct)
    {
        if(action!=ProcurementSpendingActions.PurchaseOrder.Action)return;
        var readiness=http.RequestServices.GetService<IProcurementPrincipalOrderGovernanceReadiness>();
        if(readiness is null||!await readiness.IsReadyAsync(db,true,ct))
            throw new InvalidOperationException("OrderAuthoritySchemaUnavailable");
    }

    private static async Task<bool> RetainedPrincipalIdentity(HttpContext http,ProcurementDbContext db,
        ProcurementRequestScope scope,ProcurementSpendingAuthorityGrant grant,CancellationToken ct)
    {
        var origin=await ProcurementSpendingAuthorityWorkerIdentity.LoadCreatedAsync(db,scope,grant,ct);
        if(origin.SchemaVersion==1)return false;
        if(origin.SchemaVersion!=2 || !PrincipalActionOption(http,grant.Action)
            || !await ProcurementPrincipalApprovalSchemaGuard.ReadyAsync(db,http.RequestServices,true,ct))
            throw new InvalidOperationException("AuthorityPrincipalIdentityUnavailable");
        await RequirePrincipalOrderAuthoritySchema(http,db,grant.Action,ct);
        return true;
    }

    private static async Task<IdentityAuthor> IdentityAuthorAsync(HttpContext http,HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,ProcurementSpendingAuthorityGrant grant,CancellationToken ct)
    {
        if(await RetainedPrincipalIdentity(http,db,scope,grant,ct))
        {
            var proof=await ProcurementSpendingAuthorityPrincipalIdentity.RequireAuthorAsync(core,db,scope,grant,ct);
            return new(proof.Created,new(proof.People.Actor.PrincipalId,proof.People.Actor.CoreLinkedWorkerId),
                new(proof.People.Recipient.PrincipalId,proof.People.Recipient.CoreLinkedWorkerId));
        }
        var old=await ProcurementSpendingAuthorityWorkerIdentity.RequireAuthorAsync(core,db,scope,grant,ct);
        return new(old.Created,new(old.Actor.PrincipalId,old.Actor.WorkerId),new(old.Recipient.PrincipalId,old.Recipient.WorkerId));
    }

    private static async Task<IdentityReview> IdentityReviewerAsync(HttpContext http,HrmDbContext core,ProcurementDbContext db,
        ProcurementRequestScope scope,ProcurementSpendingAuthorityGrant grant,CancellationToken ct)
    {
        if(await RetainedPrincipalIdentity(http,db,scope,grant,ct))
        {
            var proof=await ProcurementSpendingAuthorityPrincipalIdentity.RequireReviewerAsync(core,db,scope,grant,ct);
            return new(proof.Created,proof.Submitted,new(proof.People.Actor.PrincipalId,proof.People.Actor.CoreLinkedWorkerId),
                new(proof.People.Recipient.PrincipalId,proof.People.Recipient.CoreLinkedWorkerId));
        }
        var old=await ProcurementSpendingAuthorityWorkerIdentity.RequireReviewerAsync(core,db,scope,grant,ct);
        return new(old.Created,old.Submitted,new(old.Actor.PrincipalId,old.Actor.WorkerId),new(old.Recipient.PrincipalId,old.Recipient.WorkerId));
    }

    private static async Task<SpendingAuthoritySubject?> PrincipalSubject(HrmDbContext core,ProcurementRequestScope scope,
        string principal,CancellationToken ct)
    {
        var person=await ProcurementPrincipalPeople.Resolve(core,scope.TenantId,scope.LegalEntityId,principal,ct);
        return person is null?null:new(scope.TenantId,scope.LegalEntityId,person.PrincipalId,true,true);
    }
}
