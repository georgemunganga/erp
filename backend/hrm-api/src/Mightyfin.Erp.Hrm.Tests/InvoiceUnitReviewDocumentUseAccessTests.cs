using System.Text.Json;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Hrm.Tests;

public sealed class InvoiceUnitReviewDocumentUseAccessTests
{
    private sealed class Fixture
    {
        internal readonly string Principal = Guid.NewGuid().ToString("D");
        internal readonly Guid Version = Guid.NewGuid();
        internal readonly ProcurementRequestScope Scope = new();
        internal readonly InvoiceUnitReviewSource Source;
        internal DocumentOwner Owner => new("procurement", "supplier_invoice", Source.SupplierInvoiceId);
        internal DocumentUseReference Reference => new(Source.Id,"procurement","invoice_unit_review",Source.Id,1,DocumentUseKind.Review);
        internal Fixture()
        {
            var company=Guid.NewGuid(); Scope.Set("test",company,Principal,null,null,false,"use-access"); Scope.AssignPrincipal(Guid.Parse(Principal));
            var now=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value; var hash=new string('a',64);
            var review = new InvoiceUnitReviewPolicyRules.Projection(Guid.NewGuid(),1,hash,"test",company,null,Principal,null,[]);
            var typed = new InvoiceUnitReviewTransitions.Source(Guid.NewGuid(),1,Guid.NewGuid(),"test",company,null,Guid.NewGuid(),1,
                Guid.NewGuid(),1,hash,hash,Guid.NewGuid(),Guid.NewGuid(),hash,Guid.NewGuid(),1,hash,review,now,
                [new(Guid.NewGuid(),Version,hash,"page 1 line 1","EACH")]) { IdentitySchemaVersion=3 };
            var plan=new InvoiceUnitReviewTransitions.OpeningPlan(typed,JsonSerializer.Serialize(typed),ProcurementEvidenceEncoding.Digest(JsonSerializer.Serialize(typed)),new(),[],Guid.NewGuid(),null);
            Source=InvoiceUnitReviewStorage.Source(plan,new InvoiceIssuedOrder(typed.PurchaseOrderId,typed.IssuedVersionId,1,1,"test",company,null,Guid.NewGuid(),"ZMW","Issued",hash,now,[]));
        }
    }
    [Fact]
    public void Retirement_is_a_distinct_action_with_no_bootstrap_role_grants()
    {
        Assert.Empty(Mightyfin.Erp.Hrm.Application.ProcurementActionCatalog.BootstrapGrants["procurement-invoice-document-retire"]);
    }
    [Fact]
    public async Task Workerless_owned_reference_allows_only_exact_acquisition_and_two_phase_retained_read()
    {
        var f=new Fixture(); Assert.Null(f.Scope.WorkerId);
        var access=new ProcurementInvoiceUnitReviewRoutes.ReviewDocumentUseAccess(f.Scope,f.Source,f.Principal,true);
        await access.RequireAsync(f.Owner,f.Reference,DocumentUseOperation.Acquire,[f.Version],default);
        await access.RequireAsync(f.Owner,f.Reference,DocumentUseOperation.Read,[],default);
        await access.RequireAsync(f.Owner,f.Reference,DocumentUseOperation.Read,[f.Version],default);
        var history=new ProcurementInvoiceUnitReviewRoutes.ReviewDocumentUseAccess(f.Scope,f.Source,f.Principal,false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>history.RequireAsync(f.Owner,f.Reference,DocumentUseOperation.Acquire,[f.Version],default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>access.RequireAsync(f.Owner,f.Reference,DocumentUseOperation.Download,[f.Version],default));
    }
    [Theory]
    [InlineData("owner")][InlineData("source")][InlineData("revision")][InlineData("kind")][InlineData("version")][InlineData("duplicates")][InlineData("empty")][InlineData("actor")][InlineData("company")]
    public async Task Wrong_owner_source_purpose_set_or_current_context_never_authorizes_a_pin(string fault)
    {
        var f=new Fixture();var owner=f.Owner;var reference=f.Reference;IReadOnlyList<Guid> ids=[f.Version];
        var access=new ProcurementInvoiceUnitReviewRoutes.ReviewDocumentUseAccess(f.Scope,f.Source,f.Principal,true);
        if(fault=="owner")owner=owner with {Id=Guid.NewGuid()};
        if(fault=="source")reference=reference with {RecordId=Guid.NewGuid()};
        if(fault=="revision")reference=reference with {Revision=2};
        if(fault=="kind")reference=reference with {Kind=DocumentUseKind.Publication};
        if(fault=="version")ids=[Guid.NewGuid()];if(fault=="duplicates")ids=[f.Version,f.Version];if(fault=="empty")ids=[];
        if(fault is "actor" or "company")
        {
            var changed=new ProcurementRequestScope();
            var principal=fault=="actor"?Guid.NewGuid().ToString("D"):f.Principal;
            changed.Set("test",fault=="company"?Guid.NewGuid():f.Source.LegalEntityId,principal,null,null,false,"changed");
            changed.AssignPrincipal(Guid.Parse(principal));
            access=new ProcurementInvoiceUnitReviewRoutes.ReviewDocumentUseAccess(changed,f.Source,f.Principal,true);
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>access.RequireAsync(owner,reference,DocumentUseOperation.Acquire,ids,default));
    }
    [Theory]
    [InlineData("mapping")][InlineData("finance")][InlineData("original")][InlineData("state")][InlineData("actor")]
    public async Task Explicit_retirement_authority_does_not_retire_a_bound_invoice_original(string fault)
    {
        var f=new Fixture();var invoice=new SupplierInvoice {TenantId="test",LegalEntityId=f.Source.LegalEntityId,Status="Received"};var document=Guid.NewGuid();
        var owner=new DocumentOwner("procurement","supplier_invoice",invoice.Id);
        var access=new ProcurementInvoiceUnitReviewRoutes.UnusedInvoiceDocumentRetirementAccess(f.Scope,invoice,f.Principal,document,1);
        await access.RequireAsync(owner,document,1,default);
        if(fault=="mapping")invoice.CurrentMappingVersionId=Guid.NewGuid();if(fault=="finance")invoice.FinancePostingRef="owned";
        if(fault=="original")invoice.EvidenceDocumentRefId=Guid.NewGuid();if(fault=="state")invoice.Status="Cancelled";
        if(fault=="actor")
        {
            var changed=new ProcurementRequestScope(); var principal=Guid.NewGuid().ToString("D");
            changed.Set("test",invoice.LegalEntityId,principal,null,null,false,"changed");
            changed.AssignPrincipal(Guid.Parse(principal));
            access=new ProcurementInvoiceUnitReviewRoutes.UnusedInvoiceDocumentRetirementAccess(changed,invoice,f.Principal,document,1);
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>access.RequireAsync(owner,document,1,default));
    }
}
