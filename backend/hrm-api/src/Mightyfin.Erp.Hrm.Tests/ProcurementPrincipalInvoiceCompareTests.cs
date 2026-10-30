using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceCompareTests
{
    [Fact]
    public void Principal_compare_fingerprint_is_distinct_and_rejects_changed_version_mapping_or_reason()
    {
        var id=Guid.NewGuid();var input=new ProcurementInvoiceMatchingRoutes.CompareInput(4,Guid.NewGuid(),"reviewed");
        var actual=ProcurementInvoiceMatchingRoutes.PrincipalCompareFingerprint(id,input);
        var legacy=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new{schemaVersion=1,id,input},
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.NotEqual(legacy,actual);
        Assert.Equal(actual,ProcurementInvoiceMatchingRoutes.PrincipalCompareFingerprint(id,input));
        Assert.NotEqual(actual,ProcurementInvoiceMatchingRoutes.PrincipalCompareFingerprint(id,input with{Version=5}));
        Assert.NotEqual(actual,ProcurementInvoiceMatchingRoutes.PrincipalCompareFingerprint(id,input with{MappingVersionId=Guid.NewGuid()}));
        Assert.NotEqual(actual,ProcurementInvoiceMatchingRoutes.PrincipalCompareFingerprint(id,input with{Reason="changed"}));
    }

    [Fact]
    public void Only_one_actual_company_baseline_approval_can_admit_the_initial_owner()
    {
        var at=new DateTimeOffset(2026,10,28,18,0,0,TimeSpan.Zero);var company=Guid.NewGuid();
        var baseline=new InvoiceProcurementOwnerBaseline{TenantId="m6",LegalEntityId=company,
            CapturedAtUtc=at.AddMinutes(-5),CoreCaseId=Guid.NewGuid()};
        var workflow=Approved(baseline,at.AddMinutes(-4));
        Assert.Equal(workflow.Id,ProcurementInvoiceMatchingRoutes.SelectSingleApprovedBaselineCase(
            [baseline],[workflow],"m6",company,at));
        workflow.State="Pending";
        Assert.Throws<InvalidOperationException>(()=>ProcurementInvoiceMatchingRoutes.SelectSingleApprovedBaselineCase(
            [baseline],[workflow],"m6",company,at));
        workflow.State="Approved";
        var second=new InvoiceProcurementOwnerBaseline{TenantId="m6",LegalEntityId=company,
            CapturedAtUtc=at.AddMinutes(-3),CoreCaseId=Guid.NewGuid()};
        Assert.Throws<InvalidOperationException>(()=>ProcurementInvoiceMatchingRoutes.SelectSingleApprovedBaselineCase(
            [baseline,second],[workflow,Approved(second,at.AddMinutes(-2))],"m6",company,at));
    }

    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("source")][InlineData("time")]
    public void Foreign_wrong_source_or_future_Core_approval_cannot_stand_in_for_the_owner(string mutation)
    {
        var at=new DateTimeOffset(2026,10,28,18,0,0,TimeSpan.Zero);var company=Guid.NewGuid();
        var baseline=new InvoiceProcurementOwnerBaseline{TenantId="m6",LegalEntityId=company,
            CapturedAtUtc=at.AddMinutes(-5),CoreCaseId=Guid.NewGuid()};
        var workflow=Approved(baseline,at.AddMinutes(-4));
        if(mutation=="tenant")workflow.TenantId="foreign";
        if(mutation=="company")workflow.LegalEntityId=Guid.NewGuid();
        if(mutation=="source")workflow.SourceId=Guid.NewGuid();
        if(mutation=="time")workflow.CompletedAt=at.AddMinutes(1);
        Assert.Throws<InvalidOperationException>(()=>ProcurementInvoiceMatchingRoutes.SelectSingleApprovedBaselineCase(
            [baseline],[workflow],"m6",company,at));
    }

    private static ApprovalCase Approved(InvoiceProcurementOwnerBaseline baseline,DateTimeOffset at)=>new()
    {
        Id=baseline.CoreCaseId,TenantId=baseline.TenantId,LegalEntityId=baseline.LegalEntityId,
        SourceModule="procurement",SourceType=InvoiceProcurementBaselineEvidence.SourceType,
        SourceId=baseline.Id,SourceVersion=1,State="Approved",CompletedAt=at
    };
}
