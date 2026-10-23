using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalControlledReceivingTests
{
    [Fact]
    public void Multi_line_goods_draft_revisions_preserve_real_source_and_line_identity_without_acceptance()
    {
        var first=Source();var second=first with{Commercial=first.Commercial with{PurchaseOrderLineId=Guid.NewGuid(),PurchaseRequestLineId=Guid.NewGuid()}};
        var actor=Actor(Receiver);var draft=PrincipalReceivingDraftLifecycle.CreateGoods(actor,[new(null,first,1,0),new(null,second,2,0)],At.AddMinutes(-2),"NOTE","Original notes");
        Assert.Equal(1,draft.Version);Assert.Equal("Draft",draft.State);Assert.Equal(2,draft.Lines.Count);Assert.Null(draft.Creator.WorkerAliasId);
        var revised=PrincipalReceivingDraftLifecycle.ReviseGoods(draft,actor,1,[new(draft.Lines[0].Id,first,0.5m,0),new(draft.Lines[1].Id,second,2,0.25m)],At.AddMinutes(-2),"NOTE","Changed notes");
        Assert.Equal(2,revised.Version);Assert.Equal(draft.Lines.Select(line=>line.Id),revised.Lines.Select(line=>line.Id));Assert.Equal(1,draft.Version);Assert.Equal("Original notes",draft.Notes);
        var policy=Policy(first);var ledgers=new Dictionary<Guid,PrincipalReceivingLedger>{{first.Commercial.PurchaseOrderLineId,Empty(first,policy)},{second.Commercial.PurchaseOrderLineId,Empty(second,policy)}};
        var policies=new Dictionary<Guid,PolicyVersion>{{first.Commercial.PurchaseOrderLineId,policy},{second.Commercial.PurchaseOrderLineId,policy}};
        var expected=ledgers.ToDictionary(value=>value.Key,_=>0L);
        var recorded=PrincipalReceivingDraftLifecycle.RecordGoods(revised,actor,2,ledgers,policies,expected,Guid.NewGuid(),"REC:physical",At);
        Assert.Equal("Recorded",recorded.Draft.State);Assert.Equal(3,recorded.Draft.Version);Assert.Equal(2,recorded.Observations.Count);Assert.All(recorded.Observations,fact=>Assert.Equal(draft.Id,fact.ReceiptId));
        Assert.All(ledgers.Values,ledger=>Assert.Empty(ledger.Acceptances));
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.RecordGoods(recorded.Draft,actor,3,ledgers,policies,expected,Guid.NewGuid(),"REC:again",At));
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.CancelGoods(recorded.Draft,actor,3,"No physical deletion"));
    }
    [Fact]
    public void Goods_revise_cannot_replace_source_even_when_input_omits_saved_line_ID()
    {
        var source=Source();var actor=Actor(Receiver);var draft=PrincipalReceivingDraftLifecycle.CreateGoods(actor,[new(null,source,1,0)],At,"NOTE",null);
        var changed=source with{Commercial=source.Commercial with{NetUnitPrice=2}};
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.ReviseGoods(draft,actor,1,[new(null,changed,1,0)],At,"NOTE",null));
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.ReviseGoods(draft,actor,1,[new(Guid.NewGuid(),source,1,0)],At,"NOTE",null));
        var preserved=PrincipalReceivingDraftLifecycle.ReviseGoods(draft,actor,1,[new(null,source,1,0)],At,"NOTE",null);Assert.Equal(draft.Lines[0].Id,preserved.Lines[0].Id);
    }
    [Fact]
    public void Service_draft_certification_is_named_current_actor_action_not_creator_employee_permission()
    {
        var source=Source(ReceivingKind.Service,1,100);var policy=Policy(source);var creator=Actor(Receiver);
        var draft=PrincipalReceivingDraftLifecycle.CreateService(creator,source,"Half milestone",new(2026,10,22),new(2026,10,22),0.5m,new string('n',1500));
        var revised=PrincipalReceivingDraftLifecycle.ReviseService(draft,creator,1,"Documented half milestone",draft.PeriodFrom,draft.PeriodTo,0.5m,draft.Notes);
        var completed=PrincipalReceivingDraftLifecycle.CertifyService(revised,Actor(Inspector),2,Empty(source,policy),policy,0,Guid.NewGuid(),"REC:service",At);
        Assert.Equal("Certified",completed.Draft.State);Assert.Equal(3,completed.Draft.Version);Assert.Equal(50m,completed.Acceptance.NetAmount);Assert.Equal(draft.Id,completed.Acceptance.AcceptanceRecordId);Assert.Null(completed.Acceptance.Actor.WorkerAliasId);Assert.Equal(1500,completed.Draft.Notes!.Length);
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.ReviseService(completed.Draft,creator,3,"Cannot rewrite",draft.PeriodFrom,draft.PeriodTo,1,null));
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.CertifyService(revised,Actor(Inspector),2,Empty(source,policy),policy,1,Guid.NewGuid(),"REC:stale-source",At));
    }
    [Fact]
    public void Draft_cancel_preserves_notes_and_separate_reason_and_cannot_grant_negative_effects()
    {
        var source=Source(ReceivingKind.Service);var actor=Actor(Receiver);var draft=PrincipalReceivingDraftLifecycle.CreateService(actor,source,"Deliverable",new(2026,10,22),new(2026,10,22),1,"Original service notes");
        var cancelled=PrincipalReceivingDraftLifecycle.CancelService(draft,actor,1," Duplicate draft ");Assert.Equal("Original service notes",cancelled.Notes);Assert.Equal("Duplicate draft",cancelled.CancellationReason);Assert.Equal("Cancelled",cancelled.State);Assert.Equal(source,cancelled.Source);
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.CancelService(cancelled,actor,2,"Again"));
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.ReviseService(cancelled,actor,2,"Reopen",draft.PeriodFrom,draft.PeriodTo,1,null));
    }
    [Fact]
    public void Source_sequences_and_exact_Creator_scope_are_required_before_recording_or_editing()
    {
        var source=Source();var policy=Policy(source);var actor=Actor(Receiver);var draft=PrincipalReceivingDraftLifecycle.CreateGoods(actor,[new(null,source,1,0)],At,"NOTE",null);
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.ReviseGoods(draft,Actor(Inspector),1,[new(draft.Lines[0].Id,source,1,0)],At,"NOTE",null));
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.ReviseGoods(draft,actor,2,[new(draft.Lines[0].Id,source,1,0)],At,"NOTE",null));
        var ledgers=new Dictionary<Guid,PrincipalReceivingLedger>{{source.Commercial.PurchaseOrderLineId,Empty(source,policy)}};
        var policies=new Dictionary<Guid,PolicyVersion>{{source.Commercial.PurchaseOrderLineId,policy}};
        Assert.Throws<InvalidOperationException>(()=>PrincipalReceivingDraftLifecycle.RecordGoods(draft,actor,1,ledgers,policies,new Dictionary<Guid,long>{{source.Commercial.PurchaseOrderLineId,1}},Guid.NewGuid(),"REC:stale",At));
        Assert.Empty(ledgers.Single().Value.Observations);Assert.Equal("Draft",draft.State);Assert.Equal(1,draft.Version);
    }
}
