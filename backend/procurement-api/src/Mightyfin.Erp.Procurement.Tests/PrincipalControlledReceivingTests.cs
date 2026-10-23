using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure synthetic projections only; not current Core, publication, relational or HTTP acceptance evidence.</summary>
public sealed class PrincipalControlledReceivingTests
{
    private static readonly DateTimeOffset At=DateTimeOffset.Parse("2026-10-23T12:00:00Z");
    private static readonly string Buyer=Guid.NewGuid().ToString("D"),Requester=Guid.NewGuid().ToString("D"),Receiver=Guid.NewGuid().ToString("D"),Inspector=Guid.NewGuid().ToString("D");
    private static readonly Guid Company=Guid.NewGuid(),Branch=Guid.NewGuid();
    private static PrincipalReceivingSource Source(ReceivingKind kind=ReceivingKind.Goods,decimal quantity=1m,decimal price=0.0001m)
    {
        var hash=new string('a',64);var commercial=new ReceivingSourcePin("tenant-a",Company,Branch,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,hash,hash,"retained-principal-po2",hash,kind,"SITE",kind==ReceivingKind.Service?"SERVICES":"GOODS","ZMW","each",quantity,price,ControlledReceiving.NetPriceBasis);
        return new(2,commercial,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),hash,new(Buyer,null),new(Requester,null),At.AddDays(-2),At.AddDays(-1));
    }
    private static PrincipalReceivingActor Actor(string who,Guid? alias=null)=>new(who,alias,"tenant-a",Company,new HashSet<Guid>{Branch},new HashSet<string>{ControlledReceiving.RecordPermission,ControlledReceiving.InspectPermission,ControlledReceiving.AcceptPermission,ControlledReceiving.CertifyPermission});
    private static PolicyVersion Policy(PrincipalReceivingSource source,bool inspection=true)
    {
        var service=source.Commercial.Kind==ReceivingKind.Service;
        var json=JsonSerializer.Serialize(new{schemaVersion=2,transactionType="Receiving",currencyCode="ZMW",rules=new[]{new{
            code="EXPLICIT",categoryCode="SITE",purchaseType=source.Commercial.PurchaseType,kind=service?"Service":"Goods",siteIds=new[]{Branch},inspectionRequired=!service&&inspection,
            checklistReference=!service&&inspection?"CHECK-1":null,inspectorPrincipalIds=!service&&inspection?new[]{Inspector}:Array.Empty<string>(),
            acceptorPrincipalIds=service?Array.Empty<string>():new[]{Inspector},serviceOwnerPrincipalIds=service?new[]{Inspector}:Array.Empty<string>(),
            allowRequesterCertification=false,allowBuyerCertification=false,allowReceiverCertification=false,requireIndependentInspector=true,
            evidenceMode="ControlledManualReference",evidenceReferencePrefix="REC:"}}});
        return new(){TenantId="tenant-a",LegalEntityId=Company,PolicyKey="RECEIVING",PolicyVersionNumber=1,State="Published",EffectiveFrom=At.AddDays(-3),RulesJson=json,CreatedBy=Guid.NewGuid().ToString("D"),PublishedBy=Guid.NewGuid().ToString("D"),PublishedAt=At.AddDays(-3),PublicationReason="Independent synthetic policy projection"};
    }
    private static PrincipalReceivingLedger Empty(PrincipalReceivingSource source,PolicyVersion policy)=>new(source,0,[],[],[],[policy]);
    private static PrincipalReceivingLedger Add(PrincipalReceivingLedger ledger,PrincipalGoodsObservation value)=>ledger with{Sequence=value.Sequence,Observations=ledger.Observations.Append(value).ToArray()};
    private static PrincipalReceivingLedger Add(PrincipalReceivingLedger ledger,PrincipalGoodsInspection value)=>ledger with{Sequence=value.Sequence,Inspections=ledger.Inspections.Append(value).ToArray()};
    private static PrincipalReceivingLedger Add(PrincipalReceivingLedger ledger,PrincipalReceivingAcceptance value)=>ledger with{Sequence=value.Sequence,Acceptances=ledger.Acceptances.Append(value).ToArray()};
    private static PrincipalReceivingLedger Recorded(PrincipalReceivingSource source,PolicyVersion policy,decimal physical=1m)
    {var ledger=Empty(source,policy);return Add(ledger,PrincipalControlledReceiving.Record(ledger,policy,Actor(Receiver),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),0,physical,0,Branch,"DELIVERY-1","REC:physical",At.AddMinutes(-2),At.AddMinutes(-1)));}

    [Fact]
    public void Workerless_Core_projections_record_inspect_accept_exact_8dp_and_keep_scope_source_pins()
    {
        var source=Source(quantity:0.0001m);var policy=Policy(source);var ledger=Recorded(source,policy,0.0001m);
        var inspection=PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),1,0.0001m,"REC:inspection",At);ledger=Add(ledger,inspection);
        var accepted=PrincipalControlledReceiving.AcceptGoods(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),2,0.0001m,"REC:accepted",At);ledger=Add(ledger,accepted);
        Assert.Null(ledger.Observations.Single().Receiver.WorkerAliasId);Assert.Null(inspection.Inspector.WorkerAliasId);Assert.Null(accepted.Actor.WorkerAliasId);
        Assert.Equal(0.00000001m,accepted.NetAmount);Assert.Equal((0.0001m,0.00000001m,0m),PrincipalControlledReceiving.Balance(ledger));Assert.Equal(source,accepted.Source);
    }
    [Fact]
    public void Physical_overdelivery_is_truthful_but_issued_capacity_does_not_expand()
    {
        var source=Source();var policy=Policy(source);var ledger=Recorded(source,policy,2m);Assert.Equal(2m,ledger.Observations.Single().ReceivedQuantity);
        ledger=Add(ledger,PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),1,2m,"REC:inspect",At));
        ledger=Add(ledger,PrincipalControlledReceiving.AcceptGoods(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),2,1m,"REC:accept",At));
        Assert.Equal(0m,PrincipalControlledReceiving.Balance(ledger).RemainingQuantity);
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.AcceptGoods(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),3,0.0001m,"REC:excess",At));
    }
    [Fact]
    public void Rejected_inspection_never_creates_invoice_eligible_positive_acceptance()
    {
        var source=Source();var policy=Policy(source);var ledger=Recorded(source,policy);
        var inspection=PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),1,0m,"REC:rejected",At);ledger=Add(ledger,inspection);Assert.Equal(ReceivingInspectionOutcome.Rejected,inspection.Outcome);
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.AcceptGoods(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),2,0.1m,"REC:invalid",At));Assert.Empty(ledger.Acceptances);
    }
    [Fact]
    public void Two_partial_service_certificates_conserve_exact_value_and_require_named_independent_principal()
    {
        var source=Source(ReceivingKind.Service,1,100);var policy=Policy(source);var ledger=Empty(source,policy);
        foreach(var ignored in new[]{1,2})ledger=Add(ledger,PrincipalControlledReceiving.CertifyService(ledger,policy,Actor(Inspector),Guid.NewGuid(),Guid.NewGuid(),ledger.Sequence,0.5m,"Actual half deliverable",new DateOnly(2026,10,22),new DateOnly(2026,10,22),"REC:service",At));
        Assert.Equal((1m,100m,0m),PrincipalControlledReceiving.Balance(ledger));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.CertifyService(ledger,policy,Actor(Inspector),Guid.NewGuid(),Guid.NewGuid(),2,0.0001m,"Excess",new(2026,10,22),new(2026,10,22),"REC:excess",At));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.CertifyService(Empty(source,policy),policy,Actor(Buyer),Guid.NewGuid(),Guid.NewGuid(),0,0.5m,"Self",new(2026,10,22),new(2026,10,22),"REC:self",At));
    }
    [Fact]
    public void Named_inspector_cannot_share_retained_receiver_worker_alias_and_no_fake_worker_is_required()
    {
        var source=Source();var policy=Policy(source);var alias=Guid.NewGuid();var ledger=Empty(source,policy);
        ledger=Add(ledger,PrincipalControlledReceiving.Record(ledger,policy,Actor(Receiver,alias),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),0,1,0,Branch,"NOTE","REC:record",At.AddMinutes(-1),At.AddMinutes(-1)));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector,alias),ledger.Observations.Single().Id,Guid.NewGuid(),1,1,"REC:alias",At));
        var inspected=PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),1,1,"REC:distinct",At);Assert.Null(inspected.Inspector.WorkerAliasId);
    }
    [Fact]
    public void Missing_history_or_policy_and_changed_rules_do_not_become_an_empty_available_balance()
    {
        var source=Source();var policy=Policy(source);var ledger=Recorded(source,policy);
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Balance(ledger with{Observations=[]}));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Balance(ledger with{RetainedPolicies=[]}));
        policy.RulesJson=policy.RulesJson.Replace("REC:","CHANGED:");Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Balance(ledger));
    }
    [Fact]
    public void Retained_superseded_policy_still_explains_old_facts_but_denies_new_effects()
    {
        var source=Source();var policy=Policy(source);var ledger=Recorded(source,policy);policy.State="Superseded";policy.EffectiveTo=At.AddSeconds(-30);
        Assert.Equal((0m,0m,1m),PrincipalControlledReceiving.Balance(ledger));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),1,1,"REC:new",At));
    }
    [Fact]
    public void Stale_sequence_duplicate_command_foreign_branch_and_case_alias_inputs_fail_closed()
    {
        var source=Source();var policy=Policy(source);var ledger=Recorded(source,policy);
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Record(ledger,policy,Actor(Receiver),Guid.NewGuid(),Guid.NewGuid(),ledger.Observations.Single().CommandKey,1,1,0,Branch,"NOTE","REC:repeat",At,At));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector),ledger.Observations.Single().Id,Guid.NewGuid(),0,1,"REC:stale",At));
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Inspect(ledger,policy,Actor(Inspector) with{AllowedBranches=new HashSet<Guid>{Guid.NewGuid()}},ledger.Observations.Single().Id,Guid.NewGuid(),1,1,"REC:foreign",At));
        Assert.ThrowsAny<Exception>(()=>PrincipalReceivingPolicyRules.Parse(policy.RulesJson.Replace("\"schemaVersion\":2","\"schemaVersion\":1")));
        Assert.ThrowsAny<Exception>(()=>PrincipalReceivingPolicyRules.Parse(policy.RulesJson.Replace("\"inspectorPrincipalIds\"","\"inspectorWorkerIds\"")));
        Assert.ThrowsAny<Exception>(()=>PrincipalReceivingPolicyRules.Parse(policy.RulesJson.Replace("\"inspectionRequired\":true","\"inspectionRequired\":\"true\"")));
    }
    [Fact]
    public void Self_hashed_history_cannot_omit_required_inspection_or_invent_qualified_capacity()
    {
        var source=Source();var policy=Policy(source);var ledger=Recorded(source,policy);var observed=ledger.Observations.Single();
        var forged=new PrincipalReceivingAcceptance(Guid.NewGuid(),observed.ReceiptLineId,source,observed.Id,null,1,0.0001m,new(Inspector,null),policy.Id,ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson),"REC:forged",null,null,null,Guid.NewGuid(),2,At);
        Assert.Throws<InvalidOperationException>(()=>PrincipalControlledReceiving.Balance(Add(ledger,forged)));
    }
}
