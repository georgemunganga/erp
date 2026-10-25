using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Routes=Mightyfin.Erp.Hrm.Api.ProcurementPrincipalReceivingRoutes;

namespace Mightyfin.Erp.Hrm.Tests;

// API projection contracts only. These are not physical/readiness/HTTP proofs.
public sealed class PrincipalGoodsReturnProjectionTests
{
    private sealed record ReturnScope(string TenantId,Guid LegalEntityId,string SubjectId):IProcurementScope
    {
        public Guid? WorkLocationId=>null;
        public Guid? OrgUnitId=>null;
        public bool IsConfined=>false;
        public string? CorrelationId=>null;
    }
    private const string Tenant="p37-projection";
    private static readonly Guid Company=Guid.NewGuid(),Branch=Guid.NewGuid();
    private const string Maker="00000000-0000-4000-8000-000000000001";
    private static PrincipalGoodsReturnDraft Head()=>new(){TenantId=Tenant,LegalEntityId=Company,
        BranchId=Branch,CreatorPrincipalId=Maker,AcceptedFactQuantity=2.5m};
    private static PrincipalReceivingActor Actor()=>new(Maker,null,Tenant,Company,
        new HashSet<Guid>{Branch},new HashSet<string>{"procurement-return-read","procurement-return-edit"});

    [Fact]
    public void Create_receipt_is_scoped_before_tracking_and_a_foreign_scope_is_denied()
    {
        var scope=new ReturnScope(Tenant,Company,Maker);
        var options=new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        using var db=new ProcurementDbContext(options,scope);
        var key=Guid.NewGuid();
        var head=new PrincipalGoodsReturnDraft {TenantId=Tenant,LegalEntityId=Company,BranchId=Branch,
            CreatorPrincipalId=Maker,AcceptedFactQuantity=0.9999m};
        var revision=new PrincipalGoodsReturnRevision {TenantId=Tenant,LegalEntityId=Company,
            DraftId=head.Id,Revision=1,CommandKey=key,ActorPrincipalId=Maker,Quantity=0.5m,Reason="Damaged"};
        var request=JsonSerializer.Serialize(new{operation="Create",input=new{
            acceptanceFactId=head.AcceptanceFactId,quantity="0.5",reason="Damaged",evidenceReference=(string?)null}});
        var response=Routes.ReturnComplete(db,head,revision,"Create",key,Maker,request,201);
        Assert.Equal(201,response.Status);
        var receipt=Assert.Single(db.PrincipalGoodsReturnCommandReceipts.Local);
        Assert.Equal(Tenant,receipt.TenantId);
        Assert.Equal(Company,receipt.LegalEntityId);

        var foreign=new PrincipalGoodsReturnCommandReceipt {TenantId="other-tenant",LegalEntityId=Company};
        Assert.Throws<InvalidOperationException>(()=>db.AddScoped(foreign));
        Assert.Single(db.PrincipalGoodsReturnCommandReceipts.Local);
    }

    [Theory]
    [InlineData("valid")][InlineData("wrong-header")][InlineData("wrong-line")]
    [InlineData("wrong-fact")][InlineData("wrong-observation")][InlineData("wrong-source")]
    public void Goods_acceptance_binds_distinct_receipt_header_and_line_ids(string variant)
    {
        var sourceId=Guid.NewGuid();var headerId=Guid.NewGuid();var lineId=Guid.NewGuid();
        var factId=Guid.NewGuid();var observationId=Guid.NewGuid();
        Assert.NotEqual(headerId,lineId);
        var source=new PrincipalReceivingSourceState{Id=sourceId,BranchId=Branch,Kind="Goods"};
        var fact=new PrincipalReceivingFact{Id=factId,SourceStateId=sourceId,BranchId=Branch,
            DraftId=variant=="wrong-header"?Guid.NewGuid():headerId,
            Kind="Acceptance",Operation="Accept",Quantity=0.9999m};
        var observation=new PrincipalGoodsObservation(observationId,headerId,lineId,null!,Branch,
            1m,0m,new(Maker,null),"delivery","evidence",Guid.NewGuid(),new string('a',64),
            Guid.NewGuid(),1,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch);
        var acceptance=new PrincipalReceivingAcceptance(variant=="wrong-fact"?Guid.NewGuid():factId,
            variant=="wrong-line"?Guid.NewGuid():lineId,null!,
            variant=="wrong-observation"?Guid.NewGuid():observationId,null,
            0.9999m,0.9999m,new(Maker,null),Guid.NewGuid(),new string('a',64),
            "accepted",null,null,null,Guid.NewGuid(),2,DateTimeOffset.UnixEpoch);
        if(variant=="wrong-source")source=new PrincipalReceivingSourceState{Id=Guid.NewGuid(),BranchId=Branch,Kind="Goods"};
        Assert.Equal(variant=="valid"?headerId:(Guid?)null,
            Routes.VerifiedReturnReceiptId(source,fact,acceptance,observation));
    }

    [Fact]
    public void Original_workerless_creator_with_current_edit_permission_gets_only_draft_actions()
        =>Assert.Equal(new[]{"Edit","Cancel"},Routes.ReturnAllowedActions(Head(),Actor(),true));

    [Theory]
    [InlineData("other-creator")][InlineData("revoked-edit")][InlineData("policy-denied")]
    [InlineData("other-branch")][InlineData("other-company")][InlineData("other-tenant")]
    [InlineData("cancelled")][InlineData("unknown-status")]
    public void Current_actor_state_and_scope_are_all_required(string mismatch)
    {
        var head=Head();var actor=Actor();var authorized=true;
        switch(mismatch)
        {
            case "other-creator":actor=actor with{PrincipalId=Guid.NewGuid().ToString("D")};break;
            case "revoked-edit":actor=actor with{Permissions=new HashSet<string>{"procurement-return-read"}};break;
            case "policy-denied":authorized=false;break;
            case "other-branch":actor=actor with{AllowedBranches=new HashSet<Guid>{Guid.NewGuid()}};break;
            case "other-company":actor=actor with{LegalEntityId=Guid.NewGuid()};break;
            case "other-tenant":actor=actor with{TenantId="elsewhere"};break;
            case "cancelled":head.State="Cancelled";break;
            default:head.State="Unknown";break;
        }
        Assert.Empty(Routes.ReturnAllowedActions(head,actor,authorized));
    }

    [Fact]
    public void Dynamic_read_hints_never_enter_original_write_or_recovery_acknowledgement()
    {
        var head=Head();var revision=new PrincipalGoodsReturnRevision{Revision=1,Quantity=1.2345m,
            Reason="Stored original reason",EvidenceReference="Original reference"};
        var original=PrincipalGoodsReturnRules.ResponseJson(head,revision);
        var labels=new Routes.ReturnSourceLabels("Retained PO","Original supplier",1,"Retained line","EA","ZMW","Physical note",DateTimeOffset.UnixEpoch);
        var permitted=JsonSerializer.SerializeToElement(Routes.ReturnView(head,revision,Actor(),true,labels),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(2,permitted.GetProperty("allowedActions").GetArrayLength());
        Assert.Equal("1.2345",permitted.GetProperty("quantityExact").GetString());
        Assert.Equal("Retained PO",permitted.GetProperty("labels").GetProperty("purchaseOrderNumber").GetString());
        var revoked=Actor() with{Permissions=new HashSet<string>{"procurement-return-read"}};
        var denied=JsonSerializer.SerializeToElement(Routes.ReturnView(head,revision,revoked,true,labels),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Empty(denied.GetProperty("allowedActions").EnumerateArray());
        Assert.Equal(original,PrincipalGoodsReturnRules.ResponseJson(head,revision));
        using var acknowledgement=JsonDocument.Parse(original);
        Assert.False(acknowledgement.RootElement.TryGetProperty("allowedActions",out _));
        Assert.False(acknowledgement.RootElement.TryGetProperty("labels",out _));
        Assert.True(acknowledgement.RootElement.GetProperty("draftOnly").GetBoolean());
        Assert.False(acknowledgement.RootElement.GetProperty("stockChanged").GetBoolean());
    }

    [Fact]
    public void Source_labels_keep_issued_supplier_and_verified_physical_note_without_current_master_lookup()
    {
        var order=new PurchaseOrder{Number="PO-retained",TenantId=Tenant,LegalEntityId=Company};
        var line=new PurchaseOrderLine{LineNumber=3,Description="Original ordered description"};
        var issued=new PrincipalOrderIssuedVersion{CommercialSnapshotJson="{\"DisplayName\":\"Original issued supplier\"}"};
        var selected=new ProcurementPrincipalOrderRetainedIssued.Source(line,null!,null!,null!,null,null);
        var retained=new ProcurementPrincipalOrderRetainedIssued.Result(order,[line],[selected],selected,
            issued,null!,null!,null!,null!,[],[]);
        var pin=new ReceivingSourcePin(Tenant,Company,Branch,order.Id,issued.Id,line.Id,Guid.NewGuid(),1,
            new string('a',64),new string('b',64),"original",new string('c',64),ReceivingKind.Goods,
            "GENERAL","GOODS","ZMW","EA",5m,1m,"net");
        var source=new PrincipalReceivingSource(2,pin,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            new string('d',64),new(Maker,null),new(Maker,null),DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,Guid.NewGuid());
        var observedAt=DateTimeOffset.UnixEpoch.AddHours(1);
        var observation=new PrincipalGoodsObservation(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),source,Branch,
            5m,0m,new(Maker,null),"Actual physical note","Observed evidence",Guid.NewGuid(),new string('e',64),
            Guid.NewGuid(),1,observedAt,observedAt.AddHours(1));
        var labels=Routes.VerifiedReturnLabels(retained,observation);
        Assert.Equal("Original issued supplier",labels.SupplierDisplayName);
        Assert.Equal("Original ordered description",labels.Description);Assert.Equal(3,labels.LineNumber);
        Assert.Equal("Actual physical note",labels.DeliveryNoteReference);Assert.Equal(observedAt,labels.ReceivedAt);
        Assert.Throws<InvalidOperationException>(()=>Routes.VerifiedReturnLabels(retained,
            observation with{Source=source with{Commercial=pin with{PurchaseOrderLineId=Guid.NewGuid()}}}));
    }
}
