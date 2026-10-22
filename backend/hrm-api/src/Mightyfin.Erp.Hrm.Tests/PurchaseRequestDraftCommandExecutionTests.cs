using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Pure coordinator contracts only: not transaction, authorization,
/// lease-order, concurrency, readiness, HTTP or actual PostgreSQL evidence.</summary>
public sealed class PurchaseRequestDraftCommandExecutionTests
{
    private static readonly Guid Actor=Guid.Parse("11111111-2222-4333-8444-555555555555"),Company=Guid.Parse("22222222-3333-4444-8555-666666666666"),
        Branch=Guid.Parse("33333333-4444-4555-8666-777777777777"),Request=Guid.Parse("44444444-5555-4666-8777-888888888888"),
        Key=Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private static ProcurementRequestScope Scope(Guid? actor=null,Guid? company=null,Guid? branch=null,Guid? department=null,string tenant="tenant-a")
    {
        var principal=actor??Actor;var scope=new ProcurementRequestScope();scope.Set(tenant,company??Company,principal.ToString("D"),branch,department,false,"coordinator-test");
        scope.AssignPrincipal(principal);return scope;
    }
    private static JsonElement Body(string json){using var document=JsonDocument.Parse(json);return document.RootElement.Clone();}
    private static PurchaseRequestDraftCommandReceipt Receipt(string operation="Create",Guid? contextBranch=null,Guid? originalBranch=null)
    {
        var version=operation=="Create"?1L:8L;
        var metadata=new PurchaseRequestDraftCommandMetadata("tenant-a",Company,operation,Key,Actor,Request,contextBranch,null,originalBranch,null,
            operation=="Create"?null:7L,version,new string('a',64),new DateTimeOffset(2030,1,2,3,4,5,TimeSpan.Zero));
        var raw=JsonSerializer.Serialize(new {id=Request,requesterPrincipalId=(Guid?)Actor,requesterWorkerId=(Guid?)null,
            branchRef=originalBranch,departmentRef=(Guid?)null,version,requestEditVersion=1,status=operation=="Submit"?"Submitted":"Draft",
            approvalState=operation=="Submit"?"PendingPolicyAndBudget":null,currencyCode="ZMW",lines=Array.Empty<object>()});
        return PurchaseRequestDraftCommandReceipts.Complete(metadata," \n"+raw+"\n");
    }
    [Fact]
    public void Command_lease_has_stable_scoped_namespace_excluding_actor_target_and_context()
    {
        Assert.Equal(-1388348841372049307L,PurchaseRequestDraftCommandExecution.CommandLeaseKey("tenant-a",Company,"Create",Key));
        var original=PurchaseRequestDraftCommandExecution.CommandLeaseKey("tenant-a",Company,"Create",Key);
        Assert.NotEqual(original,PurchaseRequestDraftCommandExecution.CommandLeaseKey("tenant-b",Company,"Create",Key));
        Assert.NotEqual(original,PurchaseRequestDraftCommandExecution.CommandLeaseKey("tenant-a",Branch,"Create",Key));
        Assert.NotEqual(original,PurchaseRequestDraftCommandExecution.CommandLeaseKey("tenant-a",Company,"Update",Key));
        Assert.NotEqual(original,PurchaseRequestDraftCommandExecution.CommandLeaseKey("tenant-a",Company,"Create",Request));
        // Keys, rather than actors/targets/context, serialize the whole unique family.
        var one=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Body("{}"));
        var other=PurchaseRequestDraftCommandIntents.Create(Scope(actor:Request,branch:Branch),Key.ToString("D"),Body("{}"));
        Assert.NotEqual(one.Fingerprint,other.Fingerprint);
        Assert.Equal(PurchaseRequestDraftCommandExecution.CommandLeaseKey(one.Intent.TenantId,one.Intent.LegalEntityId,one.Intent.Operation,one.CommandKey),
            PurchaseRequestDraftCommandExecution.CommandLeaseKey(other.Intent.TenantId,other.Intent.LegalEntityId,other.Intent.Operation,other.CommandKey));
    }
    [Theory]
    [InlineData("")][InlineData("tenant\0other")]
    public void Lease_rejects_ambiguous_tenant_namespace(string tenant)
        =>Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandExecution.CommandLeaseKey(tenant,Company,"Create",Key));
    [Theory]
    [InlineData("Create","procurement-request-create")][InlineData("Update","procurement-request-edit")][InlineData("Submit","procurement-request-submit")]
    public void Requires_exact_original_operation_action(string operation,string action)
        =>Assert.Equal(action,PurchaseRequestDraftCommandExecution.Permission(operation));
    [Theory]
    [InlineData("create")][InlineData("Decide")][InlineData("")]
    public void Unknown_operation_never_becomes_a_default_grant(string operation)
    {var error=Assert.Throws<PurchaseRequestDraftCommandError>(()=>PurchaseRequestDraftCommandExecution.Permission(operation));Assert.Equal(400,error.Status);}
    [Theory]
    [InlineData("40001",true)][InlineData("40P01",true)][InlineData("23505",false)][InlineData("42501",false)][InlineData("57014",false)]
    public void Only_confirmed_postgres_abort_is_retryable(string state,bool retryable)
        =>Assert.Equal(retryable,PurchaseRequestDraftCommandExecution.ConfirmedAbort(new InvalidOperationException("wrapper",
            new PostgresException("synthetic","ERROR","ERROR",state))));
    [Fact]
    public void Transport_timeout_and_unknown_commit_failures_are_not_retryable()
    {
        Assert.False(PurchaseRequestDraftCommandExecution.ConfirmedAbort(new NpgsqlException("synthetic transport")));
        Assert.False(PurchaseRequestDraftCommandExecution.ConfirmedAbort(new TimeoutException()));
        Assert.False(PurchaseRequestDraftCommandExecution.ConfirmedAbort(new OperationCanceledException()));
    }
    [Theory]
    [InlineData("Create",201)][InlineData("Update",200)][InlineData("Submit",200)]
    public void Replay_retains_exact_bytes_status_hash_and_only_create_location(string operation,int status)
    {
        var receipt=Receipt(operation);var reply=PurchaseRequestDraftCommandExecution.MatchCompleted(receipt,Scope(),operation,Key,receipt.CommandFingerprint,
            operation=="Create"?null:Request);
        Assert.Equal(receipt.ResponseJson,reply.RawJson);Assert.StartsWith(" \n",reply.RawJson);Assert.EndsWith("\n",reply.RawJson);
        Assert.Equal(status,reply.StatusCode);Assert.Equal(Key,reply.CommandKey);Assert.Equal(receipt.ResponseSha256,reply.ResponseSha256);
        Assert.Equal(operation=="Create"?$"/api/procurement/v1/requests/{Request:D}":null,reply.Location);
    }
    [Theory]
    [InlineData("actor")][InlineData("company")][InlineData("tenant")][InlineData("branch")][InlineData("department")][InlineData("operation")][InlineData("key")]
    public void Scoped_receipt_family_never_discloses_another_actor_or_original_context(string mismatch)
    {
        var scope=mismatch switch {"actor"=>Scope(actor:Request),"company"=>Scope(company:Request),"tenant"=>Scope(tenant:"other"),
            "branch"=>Scope(branch:Branch),"department"=>Scope(department:Branch),_=>Scope()};
        var error=Assert.Throws<PurchaseRequestDraftCommandError>(()=>PurchaseRequestDraftCommandExecution.MatchCompleted(Receipt(),scope,
            mismatch=="operation"?"Update":"Create",mismatch=="key"?Request:Key,null,null));
        Assert.Equal(404,error.Status);Assert.Equal("RequestDraftCommandNotFound",error.Code);
    }
    [Theory]
    [InlineData(true)][InlineData(false)]
    public void Same_family_with_different_intent_or_target_is_retained_conflict(bool target)
    {
        var error=Assert.Throws<PurchaseRequestDraftCommandError>(()=>PurchaseRequestDraftCommandExecution.MatchCompleted(Receipt(),Scope(),"Create",Key,
            target?null:new string('b',64),target?Branch:null));Assert.Equal(409,error.Status);
    }
    [Fact]
    public void Companywide_context_can_replay_owned_branch_origin_without_reinterpreting_original_result()
    {
        var receipt=Receipt("Update",originalBranch:Branch);
        var result=PurchaseRequestDraftCommandExecution.MatchCompleted(receipt,Scope(),"Update",Key,null,null);
        Assert.Equal(receipt.ResponseJson,result.RawJson);
        // Historical validation accepts no current parent status/CAS: visibility
        // remains an independent owning query, not an original-result rewrite.
        Assert.Contains(Branch.ToString("D"),result.RawJson);
    }
    [Theory]
    [InlineData("version")][InlineData("createdBy")][InlineData("createdAt")][InlineData("updatedAt")][InlineData("updatedBy")]
    public void Stored_receipt_base_metadata_cannot_be_reinterpreted_as_completed(string field)
    {
        var receipt=Receipt();switch(field)
        {case "version":receipt.Version=2;break;case "createdBy":receipt.CreatedBy=Request.ToString("D");break;
            case "createdAt":receipt.CreatedAt=receipt.CompletedAt.AddSeconds(-1);break;
            case "updatedAt":receipt.UpdatedAt=receipt.CompletedAt;break;case "updatedBy":receipt.UpdatedBy=Actor.ToString("D");break;}
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandExecution.MatchCompleted(receipt,Scope(),"Create",Key,null,null));
    }
    [Fact]
    public void Snapshot_detaches_mutable_line_list_before_lease_waits()
    {
        var encoded=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Body("""
            {"purpose":"purpose","lines":[{"description":"first","requestedQuantity":"1","estimatedUnitPrice":"99999999999999.9999"}]}
            """));
        var frozen=PurchaseRequestDraftCommandExecution.SnapshotEncoding(Scope(),encoded);
        Assert.NotSame(encoded.AcceptedInput,frozen.AcceptedInput);Assert.NotSame(encoded.AcceptedInput.Lines,frozen.AcceptedInput.Lines);
        encoded.AcceptedInput.Lines!.Clear();Assert.Single(frozen.AcceptedInput.Lines!);
        Assert.Equal(99999999999999.9999m,frozen.AcceptedInput.Lines![0].EstimatedUnitPrice);
        Assert.Equal(encoded.Fingerprint,frozen.Fingerprint);
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandExecution.ValidateEncoding(Scope(),encoded));
    }
    [Theory]
    [InlineData("Create")][InlineData("Update")][InlineData("Submit")]
    public void Accepted_DTO_snapshots_preserve_operation_specific_input(string operation)
    {
        if(operation=="Create")PurchaseRequestDraftCommandExecution.ValidateEncoding(Scope(),PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Body("{}")));
        else if(operation=="Update")PurchaseRequestDraftCommandExecution.ValidateEncoding(Scope(),PurchaseRequestDraftCommandIntents.Update(Scope(),Request,Key.ToString("D"),Body("{\"version\":7}")));
        else PurchaseRequestDraftCommandExecution.ValidateEncoding(Scope(),PurchaseRequestDraftCommandIntents.Submit(Scope(),Request,Key.ToString("D"),Body("{\"version\":7}")));
    }
    [Fact]
    public void Changed_scope_and_self_consistent_different_intent_cannot_replace_original_fingerprint()
    {
        var encoded=PurchaseRequestDraftCommandIntents.Create(Scope(),Key.ToString("D"),Body("{}"));
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandExecution.ValidateEncoding(Scope(branch:Branch),encoded));
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandExecution.ValidateEncoding(Scope(),encoded with{Fingerprint=new string('b',64)}));
        Assert.Throws<InvalidOperationException>(()=>PurchaseRequestDraftCommandExecution.ValidateEncoding(Scope(),encoded with{IntentJson="{}"}));
    }
}
