using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceProcurementHistoricalOwnerContractTests
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset At=DateTimeOffset.Parse("2026-10-28T15:00:00Z");

    private static (InvoiceProcurementExposureOwnerEnvelope Envelope,InvoiceExposureReadContext Context,
        InvoiceMatchExposureState Exposure,InvoiceMatchConsumption Claim,string[] Names) Fixture()
    {
        var tenant="owner-test";var company=Guid.NewGuid();var branch=Guid.NewGuid();
        var order=Guid.NewGuid();var issued=Guid.NewGuid();var supplier=Guid.NewGuid();var policy=new string('a',64);
        var context=new InvoiceExposureReadContext(tenant,company,branch,order,issued,supplier,"ZMW",policy,At);
        var exposure=new InvoiceMatchExposureState{TenantId=tenant,LegalEntityId=company,
            PurchaseOrderId=order,IssuedVersionId=issued,SupplierId=supplier,CurrencyCode="ZMW",Sequence=1};
        var claim=new InvoiceMatchConsumption(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),tenant,company,branch,
            supplier,order,issued,Guid.NewGuid(),null,"ZMW",InvoiceConsumptionKind.Debit,null,1m,1m,0m,"owner:test");
        string[] names=["invoice_match_allocations","supplier_invoices"];
        var tables=names.Select(x=>new InvoiceProcurementBaselineTable(x,0,new string('b',64),new string('c',64))).ToArray();
        var claims=InvoiceMatchExecutionEvidence.AllocationsDigest([claim]);
        var envelope=new InvoiceProcurementExposureOwnerEnvelope(2,tenant,company,branch,order,issued,supplier,"ZMW",
            policy,"procurement:baseline","sequence:1",Guid.NewGuid(),
            ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(names,Json)),tables,
            ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(tables,Json)),
            new string('d',64),claims,InvoiceMatchExecutionEvidence.AllocationsDigest([]),claims,1,
            At.AddSeconds(-1),At.AddMinutes(1),claims);
        return(envelope,context,exposure,claim,names);
    }

    [Fact]
    public void Explicit_manifest_and_disjoint_debit_partition_have_consistent_typed_shape()
    {
        var f=Fixture();
        InvoiceProcurementHistoricalOwnerContract.VerifyPartition(f.Envelope,f.Context,f.Exposure,f.Names,[f.Claim],[]);
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyPartition(
            f.Envelope,f.Context,f.Exposure,[],[f.Claim],[]));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyPartition(
            f.Envelope,f.Context,f.Exposure,f.Names,[f.Claim],[f.Claim]));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyPartition(
            f.Envelope with{ExposureSequence=2},f.Context,f.Exposure,f.Names,[f.Claim],[]));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyPartition(
            f.Envelope with{CurrentCompleteSetSha256=""},f.Context,f.Exposure,f.Names,[f.Claim],[]));
    }

    [Fact]
    public void Credit_cannot_silently_release_a_debit_or_join_the_active_partition()
    {
        var f=Fixture();
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyPartition(
            f.Envelope,f.Context,f.Exposure,f.Names,[f.Claim with{Kind=InvoiceConsumptionKind.Credit}],[]));
    }

    [Fact]
    public void Historical_cursor_requires_every_increment_and_exact_active_pointer_set()
    {
        var f=Fixture();var inactive=Guid.NewGuid();
        InvoiceProcurementHistoricalOwnerContract.VerifyHistoricalSequence(2,
            [new(inactive,1,false),new(f.Claim.Id,2,true)],[f.Claim]);
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyHistoricalSequence(2,
            [new(f.Claim.Id,2,true)],[f.Claim]));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyHistoricalSequence(2,
            [new(inactive,1,false),new(f.Claim.Id,2,false)],[f.Claim]));
        Assert.Throws<InvalidOperationException>(()=>InvoiceProcurementHistoricalOwnerContract.VerifyHistoricalSequence(2,
            [new(inactive,1,false),new(inactive,2,true)],[f.Claim]));
    }
}
