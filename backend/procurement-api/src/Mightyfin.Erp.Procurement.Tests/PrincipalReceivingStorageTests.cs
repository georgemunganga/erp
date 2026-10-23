using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalControlledReceivingTests
{
    private static PrincipalReceivingSourceState State(PrincipalReceivingSource source,long sequence=0,decimal quantity=0,decimal net=0)
    {
        var json=PrincipalReceivingStorageCodec.Encode(source);var pin=source.Commercial;
        return new(){TenantId=pin.TenantId,LegalEntityId=pin.LegalEntityId,BranchId=pin.BranchId!.Value,
            PurchaseOrderId=pin.PurchaseOrderId,PurchaseOrderLineId=pin.PurchaseOrderLineId,PurchaseRequestLineId=pin.PurchaseRequestLineId,
            IssuedVersionId=pin.IssuedVersionId,SubmissionId=source.SubmissionId,FundingEvidenceId=source.FundingEvidenceId,
            AcknowledgmentId=source.SupplierAcknowledgmentId,SourceJson=json,SourceSha256=ProcurementEvidenceEncoding.Digest(json),
            Sequence=sequence,AcceptedQuantity=quantity,AcceptedNetAmount=net};
    }
    [Fact]
    public void Stored_source_binds_each_normalized_reference_and_does_not_accept_duplicate_JSON()
    {
        var source=Source();var state=State(source);Assert.Equal(source,PrincipalReceivingStorageCodec.Source(state));
        var changed=source with{SupplierAcknowledgmentId=Guid.NewGuid()};var json=PrincipalReceivingStorageCodec.Encode(changed);
        var bad=new PrincipalReceivingSourceState{TenantId=state.TenantId,LegalEntityId=state.LegalEntityId,BranchId=state.BranchId,
            PurchaseOrderId=state.PurchaseOrderId,PurchaseOrderLineId=state.PurchaseOrderLineId,PurchaseRequestLineId=state.PurchaseRequestLineId,
            IssuedVersionId=state.IssuedVersionId,SubmissionId=state.SubmissionId,FundingEvidenceId=state.FundingEvidenceId,
            AcknowledgmentId=state.AcknowledgmentId,SourceJson=json,SourceSha256=ProcurementEvidenceEncoding.Digest(json)};
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStorageCodec.Source(bad));
        var duplicate=state.SourceJson.Replace("\"SchemaVersion\":2","\"SchemaVersion\":1,\"SchemaVersion\":2",StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStorageCodec.Decode<PrincipalReceivingSource>(duplicate,ProcurementEvidenceEncoding.Digest(duplicate)));
        var unknown=state.SourceJson.Replace("\"SchemaVersion\":2","\"Unknown\":false,\"SchemaVersion\":2",StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStorageCodec.Decode<PrincipalReceivingSource>(unknown,ProcurementEvidenceEncoding.Digest(unknown)));
    }
    [Fact]
    public void Empty_history_is_proven_only_by_zero_cursor_and_zero_exact_balances()
    {
        var state=State(Source());Assert.Empty(PrincipalReceivingStorageCodec.Ledger(state,[],[]).Acceptances);
        state.Sequence=1;Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStorageCodec.Ledger(state,[],[]));
        state.Sequence=0;state.AcceptedNetAmount=0.00000001m;
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStorageCodec.Ledger(state,[],[]));
    }
    [Fact]
    public void Original_snapshot_roundtrips_optional_alias_and_eight_decimal_value_without_reinterpreting_today()
    {
        var source=Source(ReceivingKind.Service,0.0001m,0.0001m);var policy=Policy(source);var ledger=Empty(source,policy);
        var draft=Guid.NewGuid();var fact=PrincipalControlledReceiving.CertifyService(ledger,policy,Actor(Inspector),draft,Guid.NewGuid(),0,0.0001m,"Exact small completed deliverable",new(2026,10,22),new(2026,10,22),"REC:exact",At);
        var state=State(source,1,0.0001m,0.00000001m);var json=PrincipalReceivingStorageCodec.Encode(fact);
        var row=new PrincipalReceivingFact{Id=fact.Id,TenantId=state.TenantId,LegalEntityId=state.LegalEntityId,
            SourceStateId=state.Id,DraftId=draft,BranchId=state.BranchId,Kind="Acceptance",Sequence=1,CommandKey=fact.CommandKey,
            ActorPrincipalId=fact.Actor.PrincipalId,PolicyVersionId=policy.Id,Quantity=fact.Quantity,NetAmount=fact.NetAmount,
            PayloadJson=json,PayloadSha256=ProcurementEvidenceEncoding.Digest(json)};
        var recovered=PrincipalReceivingStorageCodec.Ledger(state,[row],[policy]);
        Assert.Equal(0.00000001m,recovered.Acceptances.Single().NetAmount);Assert.Null(recovered.Acceptances.Single().Actor.WorkerAliasId);
        // Even correct typed evidence cannot be attached under a different normalized draft.
        var forged=new PrincipalReceivingFact{Id=row.Id,TenantId=row.TenantId,LegalEntityId=row.LegalEntityId,
            SourceStateId=row.SourceStateId,DraftId=Guid.NewGuid(),BranchId=row.BranchId,Kind=row.Kind,Sequence=row.Sequence,
            CommandKey=row.CommandKey,ActorPrincipalId=row.ActorPrincipalId,PolicyVersionId=row.PolicyVersionId,
            Quantity=row.Quantity,NetAmount=row.NetAmount,PayloadJson=row.PayloadJson,PayloadSha256=row.PayloadSha256};
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingStorageCodec.Ledger(state,[forged],[policy]));
    }
    [Fact]
    public void Terminal_revision_cannot_rewrite_notes_or_original_physical_values()
    {
        var draft=PrincipalReceivingDraftLifecycle.CreateGoods(Actor(Receiver),[new(null,Source(),1,0)],At,"NOTE","Retained notes");
        var cancelled=PrincipalReceivingDraftLifecycle.CancelGoods(draft,Actor(Receiver),1,"Duplicate observation draft");
        PrincipalReceivingDraftRevision Revision(PrincipalGoodsDraft value){var json=PrincipalReceivingStorageCodec.Encode(value);return new(){DraftId=value.Id,Revision=value.Version,SnapshotJson=json,SnapshotSha256=ProcurementEvidenceEncoding.Digest(json)};}
        PrincipalReceivingDraftSnapshot.Transition("Goods",Revision(draft),Revision(cancelled));
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftSnapshot.Transition("Goods",Revision(draft),Revision(cancelled with{Notes="Silently replaced"})));
    }
}

public sealed class PrincipalReceivingPersistenceModelTests
{
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId):IProcurementScope
    {public Guid? WorkLocationId=>null;public Guid? OrgUnitId=>null;public bool IsConfined=>false;public string? CorrelationId=>null;}
    private static ProcurementDbContext Context()=>new(new DbContextOptionsBuilder<ProcurementDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,new Scope("test",Guid.NewGuid(),Guid.NewGuid().ToString("D")));
    [Fact]
    public void Cursor_revision_and_balances_are_never_EF_keys_and_owned_names_are_bounded()
    {
        using var db=Context();var model=db.GetService<IDesignTimeModel>().Model;
        foreach(var type in new[]{typeof(PrincipalReceivingSourceState),typeof(PrincipalReceivingFact),typeof(PrincipalReceivingDraftHead),typeof(PrincipalReceivingDraftRevision),typeof(PrincipalReceivingCommandReceipt)})
        {
            var entity=model.FindEntityType(type)!;Assert.Equal(ValueGenerated.Never,entity.FindProperty("Id")!.ValueGenerated);
            Assert.All(entity.GetKeys(),key=>Assert.True(key.GetName()!.Length<=63));
            Assert.All(entity.GetForeignKeys(),fk=>Assert.True(fk.GetConstraintName()!.Length<=63));
            Assert.All(entity.GetKeys().SelectMany(key=>key.Properties),p=>Assert.DoesNotContain(p.Name,new[]{"Version","Sequence","Revision","State","SnapshotSha256","AcceptedQuantity","AcceptedNetAmount"}));
        }
        Assert.Equal(38,model.FindEntityType(typeof(PrincipalReceivingFact))!.FindProperty("NetAmount")!.GetPrecision());
        Assert.Equal(8,model.FindEntityType(typeof(PrincipalReceivingFact))!.FindProperty("NetAmount")!.GetScale());
        var checks=model.FindEntityType(typeof(PrincipalReceivingFact))!.GetCheckConstraints().Single().Sql;
        Assert.Contains("{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",checks);
        Assert.DoesNotContain("{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}",checks);
    }
    [Fact]
    public void Foreign_scope_pins_are_rejected_before_AddScoped_can_relabel_them()
    {
        using var db=Context();var source=new PrincipalReceivingSourceState{TenantId="other",LegalEntityId=Guid.NewGuid()};
        Assert.Throws<InvalidOperationException>(()=>db.AddScoped(source));Assert.Empty(db.ChangeTracker.Entries<PrincipalReceivingSourceState>());
    }
}

public sealed partial class PrincipalControlledReceivingTests
{
    [Fact]
    public void Goods_acceptance_binds_its_receipt_head_while_retaining_the_original_line_target()
    {
        var source=Source();var policy=Policy(source,false);var ledger=Recorded(source,policy);
        var observation=ledger.Observations.Single();var accepted=PrincipalControlledReceiving.AcceptGoods(ledger,policy,Actor(Inspector),observation.Id,Guid.NewGuid(),1,0.0001m,"REC:exact-line",At);
        var state=State(source,2,accepted.Quantity,accepted.NetAmount);
        PrincipalReceivingFact Row(Guid id,string kind,object fact,long sequence,Guid key,string principal,Guid draft,decimal quantity,decimal net)
        {var json=System.Text.Json.JsonSerializer.Serialize(fact,fact.GetType());return new(){Id=id,TenantId=state.TenantId,LegalEntityId=state.LegalEntityId,
            SourceStateId=state.Id,DraftId=draft,BranchId=state.BranchId,Kind=kind,Sequence=sequence,CommandKey=key,ActorPrincipalId=principal,
            PolicyVersionId=policy.Id,Quantity=quantity,NetAmount=net,PayloadJson=json,PayloadSha256=ProcurementEvidenceEncoding.Digest(json)};}
        var observedRow=Row(observation.Id,"Observation",observation,1,observation.CommandKey,observation.Receiver.PrincipalId,observation.ReceiptId,observation.ReceivedQuantity,0);
        var acceptedRow=Row(accepted.Id,"Acceptance",accepted,2,accepted.CommandKey,accepted.Actor.PrincipalId,observation.ReceiptId,accepted.Quantity,accepted.NetAmount);
        var recovered=PrincipalReceivingStorageCodec.Ledger(state,[observedRow,acceptedRow],[policy]);
        Assert.Equal(observation.ReceiptLineId,recovered.Acceptances.Single().AcceptanceRecordId);Assert.NotEqual(observation.ReceiptId,accepted.AcceptanceRecordId);
    }
}
