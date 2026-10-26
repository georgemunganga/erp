using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class SpendingAuthorityIdentityTests
{
    [Fact]
    public void Order_authority_retains_independent_Core_accounts_without_employee_links()
    {
        var grant=OrderAuthority();
        var created=SpendingAuthorityIdentity.CaptureCreation(grant,null,null,2);
        SpendingAuthorityConfiguration.Submit(grant,grant.Version,
            Actor(Maker,SpendingAuthorityConfiguration.ConfigurePermission),Subject(),Now.AddMinutes(1));
        var submitted=SpendingAuthorityIdentity.CaptureSubmission(grant,created,Maker,null,null);
        SpendingAuthorityIdentity.RequireIndependentReviewer(grant,created,submitted,Reviewer,null);
        Activate(grant);
        var activated=SpendingAuthorityIdentity.CaptureActivation(grant,created,submitted,Reviewer,null,null);
        SpendingAuthorityIdentity.VerifyActivation(grant,created,submitted,activated);
        Assert.Equal(2,activated.SchemaVersion);
        Assert.Equal(ProcurementSpendingActions.PurchaseOrder.Action,activated.Action);
        Assert.Equal(ProcurementSpendingActions.PurchaseOrder.Permission,activated.Permission);
        Assert.Null(activated.ActorWorkerId);
        Assert.Null(activated.RecipientWorkerId);
        Assert.Equal(created.Id,activated.CreatedEvidenceId);
    }

    [Fact]
    public void Rehashing_an_order_grant_with_request_permission_cannot_change_its_authority_type()
    {
        var original=SpendingAuthorityIdentity.CaptureCreation(OrderAuthority(),null,null,2);
        var wrong=original with {Permission=SpendingAuthorityConfiguration.ApprovalPermission};
        wrong=wrong with {EvidenceSha256=SpendingAuthorityIdentity.Digest(wrong)};
        Assert.Throws<InvalidOperationException>(()=>SpendingAuthorityIdentity.Verify(wrong));
    }

    [Fact]
    public void Principal_order_authority_does_not_reinterpret_missing_legacy_employee_evidence()
    {
        var grant=OrderAuthority();
        Assert.Throws<InvalidOperationException>(()=>SpendingAuthorityIdentity.CaptureCreation(grant,null,null,1));
        var legacy=SpendingAuthorityIdentity.CaptureCreation(grant,MakerWorker,RecipientWorker,1);
        SpendingAuthorityIdentity.Verify(legacy);
        Assert.Equal(1,legacy.SchemaVersion);
        Assert.Equal(MakerWorker,legacy.ActorWorkerId);
        Assert.Equal(RecipientWorker,legacy.RecipientWorkerId);
    }

    private static ProcurementSpendingAuthorityGrant OrderAuthority()=>SpendingAuthorityConfiguration.CreateDraft(
        "PRIVATE-ORDER-AUTHORITY",Recipient,
        new("ZMW","ApprovedLineBase",100,Now.AddDays(-1),Now.AddDays(1),
            AllBranches:true,AllDepartments:true,AllCategories:true),
        Actor(Maker,SpendingAuthorityConfiguration.ConfigurePermission),Now,[],
        ProcurementSpendingActions.PurchaseOrder.Action);
}
