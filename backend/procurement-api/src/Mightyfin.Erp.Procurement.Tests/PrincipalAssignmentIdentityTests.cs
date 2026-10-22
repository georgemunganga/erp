using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalAssignmentIdentityTests
{
    private static PurchaseRequestAssignmentAuthorityEvidence Evidence(string? fault=null)
    {
        var principal=Guid.NewGuid().ToString("D"); var company=Guid.NewGuid();
        var authority=new PurchaseRequestAuthorityEvidence(Guid.NewGuid(),4,"FINITE",1,"test",company,principal,
            "procurement-approval-decide","PurchaseRequest.Approve","ZMW","EstimatedLineBase",0.00000001m,1m,
            DateTimeOffset.UtcNow.AddDays(-1),null,DateTimeOffset.UtcNow,true,null,true,null,true,[]);
        var id=Guid.NewGuid();var caseId=Guid.NewGuid();var stage=Guid.NewGuid();var assignment=Guid.NewGuid();
        var typed=new PurchaseRequestPrincipalAssignmentEvidence(2,"test",company,id,caseId,stage,assignment,
            principal,null,"ProcurementManager",authority);
        var node=JsonNode.Parse(JsonSerializer.Serialize(typed))!;
        if(fault=="principal")node["PrincipalId"]=Guid.NewGuid().ToString("D");
        if(fault=="stage")node["CoreStageId"]=Guid.NewGuid();
        if(fault=="scope")node["LegalEntityId"]=Guid.NewGuid();
        if(fault=="alias")node["KnownCoreWorkerId"]=Guid.NewGuid();
        if(fault=="version")node["SchemaVersion"]=1;
        if(fault=="authority")node["Authority"]!["GrantRevision"]=5;
        if(fault=="unknown")node["UnknownAuthority"]=true;
        var json=node.ToJsonString();
        if(fault=="duplicate")json=json.Replace("\"SchemaVersion\":2","\"SchemaVersion\":2,\"SchemaVersion\":2",StringComparison.Ordinal);
        return new() {IdentitySchemaVersion=2,TenantId="test",LegalEntityId=company,SubmissionId=id,
            CoreApprovalCaseId=caseId,CoreStageId=stage,CoreAssignmentId=assignment,PrincipalId=principal,
            AssigneeKind="ProcurementManager",GrantId=authority.GrantId,GrantRevision=authority.GrantRevision,
            AuthorityVersion=authority.AuthorityVersion,EvidenceJson=json,EvidenceSha256=ProcurementEvidenceEncoding.Digest(json)};
    }

    [Fact]
    public void Workerless_assignment_retains_exact_amount_and_cannot_be_read_as_worker_v1()
    {
        var evidence=Evidence();var typed=Assert.IsType<PurchaseRequestPrincipalAssignmentEvidence>(PurchaseRequestPrincipalAssignmentIdentity.ReadVerified(evidence));
        Assert.Null(typed.KnownCoreWorkerId);Assert.Equal(0.00000001m,typed.Authority.RequestAmount);
        Assert.Null(PurchaseRequestAssignmentIdentity.ReadVerified(evidence));
    }

    [Theory]
    [InlineData("principal")][InlineData("stage")][InlineData("scope")][InlineData("alias")]
    [InlineData("version")][InlineData("authority")][InlineData("unknown")][InlineData("duplicate")]
    public void Rehashed_mismatched_or_ambiguous_envelope_is_denied(string fault)
        => Assert.Null(PurchaseRequestPrincipalAssignmentIdentity.ReadVerified(Evidence(fault)));
}
