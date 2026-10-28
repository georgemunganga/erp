using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalInvoiceMatchingBridgeTests
{
    [Theory]
    [InlineData(null, 3)]
    [InlineData(3, null)]
    [InlineData(null, null)]
    [InlineData(2, 3)]
    [InlineData(3, 2)]
    public void Claims_reject_legacy_or_unknown_issued_family_before_consuming_other_evidence(
        int? resultSchema, int? exposureSchema)
    {
        var result = new InvoiceMatchResult { SourceSchemaVersion = resultSchema };
        var exposure = new InvoiceMatchExposureState { SourceSchemaVersion = exposureSchema };
        var error = Assert.Throws<InvalidOperationException>(() => PrincipalInvoiceMatchingBridge.Claims(
            null!, null!, result, exposure, null!, default, null!, [], null!, null!, null!,
            null!, null!, [], null!, []));
        Assert.Equal("PrincipalInvoiceIssuedFamilyRequired", error.Message);
    }

    [Fact]
    public void A_verified_boolean_without_retained_Core_review_never_supplies_units()
    {
        var mapping=new InvoiceMatchMappingVersion{SourceSchemaVersion=3};
        var invoice=new SupplierInvoice();
        var context=new InvoiceMappingReviewContext("m6",Guid.NewGuid(),Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),null,Guid.NewGuid(),new string('a',64),
            DateTimeOffset.UtcNow,new Dictionary<Guid,string>());
        Assert.False(PrincipalInvoiceMatchingBridge.ReviewCovers3(mapping,invoice,[],
            InvoiceMappingReview.Unavailable("NoOriginalDocumentReview"),context));
        Assert.False(PrincipalInvoiceMatchingBridge.ReviewCovers3(mapping,invoice,[],
            new(true,"Verified","{}",new Dictionary<Guid,string>()),context));
    }

    [Theory]
    [InlineData(ReceivingKind.Goods,InvoiceAcceptanceKind.Goods)]
    [InlineData(ReceivingKind.Service,InvoiceAcceptanceKind.Service)]
    public void Accepted_projection_keeps_actual_fact_identity_unit_and_exact_value(
        ReceivingKind kind,InvoiceAcceptanceKind projectedKind)
    {
        var source=Guid.NewGuid();var fact=Guid.NewGuid();var company=Guid.NewGuid();
        var at=new DateTimeOffset(2026,10,28,12,0,0,TimeSpan.Zero);
        var history=new PrincipalInvoiceAcceptedHistory(source,new string('a',64),3,"EACH",
            [new(fact,source,3,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),kind,1m,5.1234m,new string('b',64),at)])
            {SupplierId=Guid.NewGuid(),BranchId=Guid.NewGuid(),CurrencyCode="ZMW"};
        var accepted=Assert.Single(PrincipalInvoiceMatchingBridge.ProjectAcceptances([history],"m6",company));
        Assert.Equal(fact,accepted.Id);Assert.Equal("EACH",accepted.UnitCode);
        Assert.Equal(projectedKind,accepted.Kind);Assert.Equal(5.1234m,accepted.AcceptedNetAmount);
        Assert.Equal(1m,accepted.AcceptedQuantity);Assert.Equal(0m,accepted.ReturnedQuantity);
        Assert.Equal(0m,accepted.ReturnedNetAmount);Assert.False(accepted.Reversed);
        Assert.Equal(at,accepted.AcceptedAt);
    }

    [Fact]
    public void Principal_fact_uses_existing_foundation_same_unit_rule_without_conversion_defaults()
    {
        var company=Guid.NewGuid();var branch=Guid.NewGuid();var supplier=Guid.NewGuid();
        var order=Guid.NewGuid();var issued=Guid.NewGuid();var line=Guid.NewGuid();
        var invoiceId=Guid.NewGuid();var invoiceLine=Guid.NewGuid();var source=Guid.NewGuid();var fact=Guid.NewGuid();
        var at=new DateTimeOffset(2026,10,28,12,0,0,TimeSpan.Zero);
        var history=new PrincipalInvoiceAcceptedHistory(source,new string('a',64),1,"EACH",
            [new(fact,source,1,order,issued,line,ReceivingKind.Service,1m,5.1234m,new string('b',64),at.AddHours(-1))])
            {SupplierId=supplier,BranchId=branch,CurrencyCode="ZMW"};
        var accepted=PrincipalInvoiceMatchingBridge.ProjectAcceptances([history],"m6",company);
        var documentLine=new InvoiceMatchDocumentLine(invoiceLine,1,invoiceId,"m6",company,line,
            null,fact,1m,5.1234m,0,"Original Service","EACH",null,null);
        var invoice=new InvoiceMatchDocument(invoiceId,1,"m6",company,branch,supplier,order,"INV1","INV1",
            "ZMW","Received",new DateOnly(2026,10,28),null,5.1234m,0,5.1234m,null,null,null,[documentLine]);
        var exact=new InvoiceFieldTolerance(0,0,InvoiceToleranceCombination.AllLimits);
        var rule=new InvoiceCategoryMatchRule("SERVICE","ZMW",InvoiceMatchingMethod.ThreeWay,
            InvoiceAcceptanceKind.Service,exact,exact,exact,exact);
        var input=new InvoiceMatchingInput(invoice,new(order,issued,1,1,"m6",company,branch,supplier,"ZMW",
            "Issued","M5:issued",at.AddDays(-1),[new(line,1,"SERVICE","EACH",1,5.1234m,0)]),accepted,
            new(Guid.NewGuid(),1,"m6",company,null,"Published","CORE:policy",at.AddDays(-1),null,
                4,InvoiceMoneyRounding.ToEven,[rule]),new("complete",true,[]),
            new("all-identities",true,[new(invoiceId,"m6",company,supplier,"INV1","ZMW")]));
        var scope=new TestScope(company,branch);
        var command=new InvoiceMatchCommand(Guid.NewGuid(),1,at,null);
        Assert.True(InvoiceMatchingFoundation.Evaluate(command,scope,input).ComparisonPassed);
        var wrong=input with{Invoice=invoice with{Lines=[documentLine with{UnitCode="BOX"}]}};
        Assert.Contains(InvoiceMatchingFoundation.Evaluate(command,scope,wrong).Discrepancies,
            x=>x.Code=="VerifiedInvoiceUnitMappingRequired");
    }

    private sealed record TestScope(Guid LegalEntityId,Guid Branch):IProcurementScope
    {
        public string TenantId=>"m6";
        public Guid? WorkLocationId=>Branch;
        public Guid? OrgUnitId=>null;
        public bool IsConfined=>true;
        public string SubjectId=>"principal-matcher";
        public string? CorrelationId=>null;
    }

    [Fact]
    public void Hard_quantity_and_net_ceiling_include_existing_claims_with_no_tolerance_escape()
    {
        var fact=Guid.NewGuid();var line=Guid.NewGuid();var invoiceLine=Guid.NewGuid();
        var accepted=new InvoiceAcceptedFulfilment(fact,1,"m6",Guid.NewGuid(),Guid.NewGuid(),
            Guid.NewGuid(),Guid.NewGuid(),line,"ZMW","EACH",InvoiceAcceptanceKind.Goods,
            "Accepted","M5:fact",DateTimeOffset.UtcNow,false,1m,0m,5.1234m,0m);
        var used=new InvoiceMatchConsumption(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"m6",
            accepted.LegalEntityId,accepted.BranchRef,Guid.NewGuid(),accepted.PurchaseOrderId,
            accepted.RevisionId,line,fact,"ZMW",InvoiceConsumptionKind.Debit,null,.5m,2.5617m,0,"MATCH:old");
        var input=new InvoiceMatchingInput(null!,null!,[accepted],null!,new("verified",true,[used]),null!);
        InvoiceMatchingEvaluation Compared(string quantity,string net)=>new(Guid.NewGuid(),Guid.NewGuid(),1,
            "actor","fingerprint","Compared",[],[new(invoiceLine,line,fact,InvoiceMatchingMethod.ThreeWay,
                quantity,net,"0","5.1234")],"{}","sha");
        PrincipalInvoiceMatchingBridge.RequireHardAcceptedCeilings(Compared("0.5","2.5617"),input);
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMatchingBridge.RequireHardAcceptedCeilings(
            Compared("0.5001","2.5617"),input));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMatchingBridge.RequireHardAcceptedCeilings(
            Compared("0.5","2.5618"),input));
        Assert.Throws<InvalidOperationException>(()=>PrincipalInvoiceMatchingBridge.RequireHardAcceptedCeilings(
            Compared("0.5","2.5617"),input with{Acceptances=[accepted with{ReturnedQuantity=.0001m}]}));
    }
}
