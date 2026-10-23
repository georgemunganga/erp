using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

// Pure synthetic owned shapes only: these tests do not prove actual source/Core
// database provenance, a durable document hold, a current account or publication.
public sealed class PrincipalRfqPrepublicationPlanTests
{
    private sealed record Fixture(SourcingEvent Event,SourcingDraftEvidence Evidence,SourcingDemandBinding Binding,
        PrincipalRfqEvaluationPlanDefinition Definition,RfqPrincipalActor Evaluator,DocumentUseReference Use,
        DocumentUsePin Pin,DateTimeOffset At);
    private static Fixture Original(Guid? buyerAlias=null)
    {
        var at=new DateTimeOffset(2026,10,24,10,0,0,TimeSpan.Zero);var company=Guid.NewGuid();var branch=Guid.NewGuid();
        var buyer=Guid.NewGuid().ToString("D");var maker=Guid.NewGuid().ToString("D");var reviewer=Guid.NewGuid().ToString("D");
        var pin=new RfqPrincipalDemandPin(Guid.NewGuid(),Guid.NewGuid(),4,Guid.NewGuid(),3,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            new string('a',64),new string('b',64),new string('c',64),Guid.NewGuid(),new string('d',64),Guid.NewGuid(),2,"RFQ",0,
            maker,maker,null,null,new string('e',64),Guid.NewGuid(),Guid.NewGuid(),0.0001m,0.0001m,0.00000001m,"EA","OFFICE",null);
        var draft=GovernedPrincipalRfqDraft.NewDraft(Guid.NewGuid(),"tenant",company,branch,"ZMW","Exact original RFQ",buyer,buyerAlias,"Draft",[pin]);
        var createKey=Guid.NewGuid();var(payload,sha)=GovernedPrincipalRfqRestoration.Capture(draft,createKey);
        var evidence=new SourcingDraftEvidence{SchemaVersion=2,SourcingEventId=draft.Id,TenantId="tenant",LegalEntityId=company,
            BranchRef=branch,MakerSubjectId=buyer,MakerPrincipalId=buyer,MakerWorkerId=buyerAlias,CreateCommandKey=createKey,
            OriginalDraftFingerprint=draft.OriginalFingerprint,PayloadJson=payload,PayloadSha256=sha};
        var ev=new SourcingEvent{Id=draft.Id,TenantId="tenant",LegalEntityId=company,BranchRef=branch,ExecutionVersion=2,
            Status="Draft",Number="RF-P32",Title=draft.Title,CurrencyCode="ZMW",OwnerSubjectId=buyer,Method=pin.MethodCode,
            MethodId=pin.MethodId,MethodRevision=pin.MethodRevision,MakerWorkerId=buyerAlias,SourceEvidenceId=evidence.Id,OriginalDraftFingerprint=draft.OriginalFingerprint};
        var binding=new SourcingDemandBinding{TenantId="tenant",LegalEntityId=company,IdentitySchemaVersion=2,SourcingEventId=draft.Id,
            SourceEvidenceId=evidence.Id,SourcingLineId=pin.EventLineId,PurchaseRequestId=pin.RequestId,PurchaseRequestLineId=pin.LineId,
            RequestVersion=pin.RequestVersion,SubmissionId=pin.SubmissionId,SubmittedVersion=pin.SubmittedVersion,CoreApprovalCaseId=pin.CaseId,
            ApprovalOutcomeId=pin.OutcomeId,CoreDecisionId=pin.DecisionId,RequestSha=pin.RequestSha,PolicyDecisionSha=pin.PolicyDecisionSha,
            PolicyRulesSha=pin.PolicyRulesSha,FinanceSnapshotId=pin.FinanceId,FinanceSha=pin.FinanceSha,MethodId=pin.MethodId,
            MethodRevision=pin.MethodRevision,MethodCode=pin.MethodCode,MinimumQuoteCount=pin.MinimumQuoteCount,MakerPrincipalId=maker,
            RequesterPrincipalId=maker,PrincipalIdentitySha256=pin.PrincipalIdentitySha256,AllocationId=pin.AllocationId,Quantity=pin.Quantity,
            UnitEstimate=pin.UnitEstimate,EstimatedValue=pin.EstimatedValue,UnitCode=pin.UnitCode,CategoryCode=pin.CategoryCode};
        var definition=PrincipalRfqEvaluationPlanDefinition.Parse($$"""
            {"schemaVersion":1,"transactionType":"SourcingEvaluationDefinition","normalizationTotal":"1",
             "criteria":[{"code":"TECH","phase":"Technical","kind":"Scored","mandatory":true,
               "maximumScore":"10","weight":"1","evaluatorPrincipalIds":["{{reviewer}}"]}]}
            """);
        var actor=new RfqPrincipalActor(reviewer,null,"tenant",company,branch,new HashSet<Guid>{branch},false,
            new HashSet<string>{PrincipalRfqPrepublicationPlan.EvaluatorPermission});
        var use=new DocumentUseReference(Guid.NewGuid(),"procurement",PrincipalRfqPrepublicationPlan.DocumentSourceType,draft.Id,1,DocumentUseKind.Review);
        var version=Guid.NewGuid();var hash=new string('f',64);
        var scan=new DocumentScanEvidence(Guid.NewGuid(),"tenant",company,version,1,ScannerVerdict.Clean,hash,"fixture-scanner","fixture-definitions",at.AddMinutes(-2),at.AddMinutes(-1));
        var document=DocumentUsePin.Restore(Guid.NewGuid(),"tenant",company,use,
            new("procurement",PrincipalRfqPrepublicationPlan.DocumentOwnerType,draft.Id,Guid.NewGuid(),version,1,1,hash),1,
            "requirements.pdf","application/pdf",100,scan,buyer,at);
        return new(ev,evidence,binding,definition,actor,use,document,at);
    }
    private static T Changed<T>(T value,Action<System.Text.Json.Nodes.JsonObject> change)
    {var root=System.Text.Json.JsonSerializer.SerializeToNode(value)!.AsObject();change(root);return System.Text.Json.JsonSerializer.Deserialize<T>(root.ToJsonString())!;}
    private static PrincipalRfqPrepublicationPlan Prepare(Fixture f,string currency="ZMW",string currencyMode="SameCurrencyOnly",string taxMode="OriginalSeparateComponents")
        =>PrincipalRfqPrepublicationPlan.Prepare(f.Event,f.Evidence,[f.Binding],f.Definition,[f.Evaluator],[f.Pin],
            f.Event.Version,1,f.Use,f.At.AddDays(1),f.At.AddDays(2),f.At,currency,currencyMode,taxMode);
    private static DocumentUsePin ChangePin(Fixture f,string? tenant=null,Guid? company=null,DocumentUseReference? use=null,string? actor=null)
        =>DocumentUsePin.Restore(f.Pin.Id,tenant??f.Pin.TenantId,company??f.Pin.LegalEntityId,use??f.Pin.Source,
            new(f.Pin.OwnerModule,f.Pin.OwnerType,f.Pin.OwnerId,f.Pin.DocumentId,f.Pin.VersionId,f.Pin.DocumentRevision,f.Pin.VersionRevision,f.Pin.Sha256),
            f.Pin.VersionNumber,f.Pin.FileName,f.Pin.MediaType,f.Pin.SizeBytes,
            f.Pin.Scan with{TenantId=tenant??f.Pin.TenantId,LegalEntityId=company??f.Pin.LegalEntityId},actor??f.Pin.CreatedBy,f.Pin.CreatedAt);

    [Fact]
    public void Exact_workerless_candidate_retains_source_document_scan_plan_assignment_without_authority()
    {
        var f=Original();var result=Prepare(f);
        Assert.False(result.CanPublish);Assert.Equal(f.Event.Id,result.EventId);
        Assert.Equal(f.Event.OriginalDraftFingerprint,result.OriginalSourceFingerprint);
        Assert.Equal(f.Definition.RulesSha256,result.EvaluationRulesSha256);
        Assert.Equal(f.Evaluator.PrincipalId,Assert.Single(result.Assignments).PrincipalId);Assert.Null(result.Assignments[0].WorkerAliasId);
        Assert.Contains("0.00000001",result.SnapshotJson);Assert.Contains(f.Pin.Scan.Id.ToString("D"),result.SnapshotJson);
        Assert.Contains(f.Pin.VersionId.ToString("D"),result.SnapshotJson);Assert.Equal(64,result.SnapshotSha256.Length);
    }
    [Fact]
    public void Owner_record_mutation_cannot_rewrite_prepared_bytes_or_assignments()
    {
        var f=Original();var result=Prepare(f);var json=result.SnapshotJson;var hash=result.SnapshotSha256;
        f.Event.Title="Changed later";
        Assert.Equal(json,result.SnapshotJson);Assert.Equal(hash,result.SnapshotSha256);
        Assert.Throws<NotSupportedException>(()=>((IList<PrincipalRfqEvaluatorAssignment>)result.Assignments).Clear());
    }
    [Fact]
    public void Event_CAS_state_source_envelope_and_normalized_binding_are_exact()
    {
        var f=Original();Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPrepublicationPlan.Prepare(f.Event,f.Evidence,[f.Binding],f.Definition,[f.Evaluator],[f.Pin],
            f.Event.Version+1,1,f.Use,f.At.AddDays(1),f.At.AddDays(2),f.At,"ZMW","SameCurrencyOnly","OriginalSeparateComponents"));
        f.Event.Status="Published";Assert.Throws<InvalidOperationException>(()=>Prepare(f));
        f=Original();f=f with{Binding=Changed(f.Binding,root=>root["Quantity"]=1m)};Assert.Throws<InvalidOperationException>(()=>Prepare(f));
        f=Original();f.Event.SourceEvidenceId=Guid.NewGuid();Assert.Throws<InvalidOperationException>(()=>Prepare(f));
        f=Original();f=f with{Evidence=Changed(f.Evidence,root=>root["PayloadJson"]=f.Evidence.PayloadJson.Replace("Exact original RFQ","Changed original RFQ",StringComparison.Ordinal))};
        Assert.Throws<InvalidOperationException>(()=>Prepare(f));
    }
    [Fact]
    public void Missing_scope_current_action_named_actor_and_maker_self_assignment_are_denied()
    {
        var f=Original();Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Evaluator=f.Evaluator with{Permissions=new HashSet<string>()}}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Evaluator=f.Evaluator with{Branches=new HashSet<Guid>()}}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Evaluator=f.Evaluator with{CompanyId=Guid.NewGuid()}}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Evaluator=f.Evaluator with{SelectedBranchId=Guid.NewGuid()}}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Evaluator=f.Evaluator with{PrincipalId=f.Event.OwnerSubjectId}}));
    }
    [Fact]
    public void Retained_maker_worker_alias_cannot_evaluate_under_another_account()
    {
        var alias=Guid.NewGuid();var f=Original(alias);
        var failure=Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Evaluator=f.Evaluator with{LinkedWorkerId=alias}}));
        Assert.Equal("RfqPrepublicationEvaluatorNotIndependent",failure.Message);
    }
    [Fact]
    public void Two_named_accounts_for_one_worker_cannot_be_counted_as_distinct_evaluators()
    {
        var f=Original();var second=f.Evaluator with{PrincipalId=Guid.NewGuid().ToString("D"),LinkedWorkerId=Guid.NewGuid()};
        var first=f.Evaluator with{LinkedWorkerId=second.LinkedWorkerId};
        var rules=System.Text.Json.JsonSerializer.Serialize(new{schemaVersion=1,transactionType="SourcingEvaluationDefinition",normalizationTotal="1",
            criteria=new[]{new{code="TECH",phase="Technical",kind="Scored",mandatory=true,maximumScore="10",weight="1",
                evaluatorPrincipalIds=new[]{first.PrincipalId,second.PrincipalId}}}});
        var definition=PrincipalRfqEvaluationPlanDefinition.Parse(rules);
        var failure=Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPrepublicationPlan.Prepare(f.Event,f.Evidence,[f.Binding],definition,[first,second],[f.Pin],
            f.Event.Version,1,f.Use,f.At.AddDays(1),f.At.AddDays(2),f.At,"ZMW","SameCurrencyOnly","OriginalSeparateComponents"));
        Assert.Equal("RfqPrepublicationEvaluatorAliasConflict",failure.Message);
    }
    [Fact]
    public void Retained_document_exact_scope_actor_reference_and_purpose_are_required()
    {
        var f=Original();
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Pin=ChangePin(f,tenant:"other")}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Pin=ChangePin(f,company:Guid.NewGuid())}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Pin=ChangePin(f,actor:f.Evaluator.PrincipalId)}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Pin=ChangePin(f,use:f.Use with{Kind=DocumentUseKind.Publication})}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Pin=ChangePin(f,use:f.Use with{Revision=2})}));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{Use=f.Use with{Key=Guid.NewGuid()}}));
    }
    [Fact]
    public void Exact_pack_and_named_assignment_sets_cannot_be_missing_duplicated_or_padded()
    {
        var f=Original();
        foreach(var pins in new IReadOnlyList<DocumentUsePin>[]{[],[f.Pin,f.Pin]})
            Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPrepublicationPlan.Prepare(f.Event,f.Evidence,[f.Binding],f.Definition,[f.Evaluator],pins,
                f.Event.Version,1,f.Use,f.At.AddDays(1),f.At.AddDays(2),f.At,"ZMW","SameCurrencyOnly","OriginalSeparateComponents"));
        foreach(var people in new IReadOnlyList<RfqPrincipalActor>[]{[],[f.Evaluator,f.Evaluator]})
            Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPrepublicationPlan.Prepare(f.Event,f.Evidence,[f.Binding],f.Definition,people,[f.Pin],
                f.Event.Version,1,f.Use,f.At.AddDays(1),f.At.AddDays(2),f.At,"ZMW","SameCurrencyOnly","OriginalSeparateComponents"));
    }
    [Theory]
    [InlineData("USD","SameCurrencyOnly","OriginalSeparateComponents")]
    [InlineData("ZMW","ConvertAtOneToOne","OriginalSeparateComponents")]
    [InlineData("ZMW","SameCurrencyOnly","AssumedTaxExclusive")]
    public void Unowned_FX_or_tax_normalization_is_unavailable(string currency,string currencyMode,string taxMode)
        =>Assert.Throws<InvalidOperationException>(()=>Prepare(Original(),currency,currencyMode,taxMode));
    [Fact]
    public void Explicit_future_window_and_database_timestamp_precision_are_required()
    {
        var f=Original();
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqPrepublicationPlan.Prepare(f.Event,f.Evidence,[f.Binding],f.Definition,[f.Evaluator],[f.Pin],
            f.Event.Version,1,f.Use,f.At,f.At.AddDays(1),f.At,"ZMW","SameCurrencyOnly","OriginalSeparateComponents"));
        Assert.Throws<InvalidOperationException>(()=>Prepare(f with{At=f.At.AddTicks(1)}));
    }
}
