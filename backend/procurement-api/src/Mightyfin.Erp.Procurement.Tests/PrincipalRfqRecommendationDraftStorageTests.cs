using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqRecommendationDraftStorageTests
{
    private const string Actor="7bf00a55-b537-4a21-b582-2b9055029ae7";
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId):IProcurementScope
    {public Guid? WorkLocationId=>null;public Guid? OrgUnitId=>null;
        public bool IsConfined=>false;public string? CorrelationId=>null;}

    private static (PrincipalRfqRecommendationDraftHead Head,
        PrincipalRfqRecommendationDraftRevision Revision,
        PrincipalRfqRecommendationDraftLine Line,
        PrincipalRfqRecommendationDraftReceipt Receipt) Graph()
    {
        var company=Guid.NewGuid();var round=Guid.NewGuid();var source=Guid.NewGuid();
        var branch=Guid.NewGuid();var offer=Guid.NewGuid();var supplier=Guid.NewGuid();
        var sourcingLine=Guid.NewGuid();var key=Guid.NewGuid();
        var head=new PrincipalRfqRecommendationDraftHead{TenantId="fixture",LegalEntityId=company,
            RoundId=round,UnsealingId=Guid.NewGuid(),SealId=Guid.NewGuid(),SourcingEventId=source,
            BranchId=branch,PreparedPlanId=Guid.NewGuid(),EvaluationPlanRevisionId=Guid.NewGuid(),
            ApprovedRoundSha256=new string('a',64),OpeningSha256=new string('b',64),
            OfferSetSha256=new string('c',64),EvaluationDefinitionSha256=new string('d',64),
            MakerPrincipalId=Actor};
        var revision=new PrincipalRfqRecommendationDraftRevision{TenantId=head.TenantId,
            LegalEntityId=company,DraftId=head.Id,RoundId=round,UnsealingId=head.UnsealingId,
            SourcingEventId=source,BranchId=branch,Revision=1,ActorPrincipalId=Actor,
            CommandKey=key,RequestSha256=new string('e',64),
            RecordedAt=ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value};
        var line=new PrincipalRfqRecommendationDraftLine{TenantId=head.TenantId,
            LegalEntityId=company,DraftId=head.Id,RevisionId=revision.Id,RoundId=round,
            UnsealingId=head.UnsealingId,SourcingEventId=source,BranchId=branch,
            OfferId=offer,OfferSha256=new string('f',64),SupplierId=supplier,
            SourcingLineId=sourcingLine,QuantityExact="0.25",ProposedValueExact="25.00000001",
            CurrencyCode="ZMW",Rationale="Observed quote and line fit"};
        var proposed=new PrincipalRfqRecommendationDraft.Proposed(offer,line.OfferSha256,
            supplier,sourcingLine,line.QuantityExact,line.ProposedValueExact,line.CurrencyCode,line.Rationale);
        revision.SnapshotJson=PrincipalRfqRecommendationDraft.Snapshot(head,revision,[proposed]);
        revision.SnapshotSha256=ProcurementEvidenceEncoding.Digest(revision.SnapshotJson);
        head.CurrentRevision=1;head.CurrentRevisionId=revision.Id;
        head.CurrentSnapshotSha256=revision.SnapshotSha256;
        var response=PrincipalRfqRecommendationDraft.Response(head,revision);
        var receipt=new PrincipalRfqRecommendationDraftReceipt{TenantId=head.TenantId,
            LegalEntityId=company,DraftId=head.Id,RevisionId=revision.Id,RoundId=round,
            SourcingEventId=source,BranchId=branch,ActorPrincipalId=Actor,
            CommandKey=key,RequestSha256=revision.RequestSha256,
            ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response)};
        return(head,revision,line,receipt);
    }

    private static ProcurementDbContext Db(PrincipalRfqRecommendationDraftHead head)
        =>new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new Scope(head.TenantId,head.LegalEntityId,Actor));

    [Fact]
    public void Original_revision_requires_matching_receipt_audit_and_metadata_event_in_same_save()
    {
        var (head,revision,line,receipt)=Graph();
        using var db=Db(head);
        db.AddScoped(head);db.AddScoped(revision);db.AddScoped(line);
        Assert.Equal("RfqRecommendationSameSaveEffectsRequired",
            Assert.Throws<InvalidOperationException>(()=>db.SaveChanges()).Message);
        db.AddScoped(receipt);
        db.AddScoped(new AuditEvent{RecordType=nameof(PrincipalRfqRecommendationDraftRevision),
            RecordId=revision.Id,Action="RecordRecommendationDraft",ActorSubjectId=Actor,
            ChangedFieldsJson=PrincipalRfqRecommendationDraft.AuditPayload(revision)});
        var payload=PrincipalRfqRecommendationDraft.EventPayload(revision);
        Assert.DoesNotContain(line.ProposedValueExact,payload,StringComparison.Ordinal);
        Assert.DoesNotContain(line.Rationale,payload,StringComparison.Ordinal);
        db.AddScoped(new OutboxEvent{EventType=PrincipalRfqRecommendationDraft.EventType,
            AggregateType=nameof(PrincipalRfqRecommendationDraftRevision),AggregateId=revision.Id,
            IdempotencyKey=revision.CommandKey.ToString("D"),PayloadJson="{}"});
        Assert.Equal("RfqRecommendationReceiptOrEffectsChanged",
            Assert.Throws<InvalidOperationException>(()=>db.SaveChanges()).Message);
        db.ChangeTracker.Entries<OutboxEvent>().Single().Entity.PayloadJson=payload;
        db.SaveChanges();
        Assert.Single(db.PrincipalRfqRecommendationDrafts);
        Assert.Single(db.PrincipalRfqRecommendationDraftRevisions);
        Assert.Single(db.PrincipalRfqRecommendationDraftReceipts);
    }

    [Fact]
    public void Same_save_wrong_actor_or_modified_history_fails_before_write()
    {
        var (head,revision,line,receipt)=Graph();
        using var db=Db(head);
        receipt.ActorPrincipalId=Guid.NewGuid().ToString("D");
        db.AddScoped(head);db.AddScoped(revision);db.AddScoped(line);db.AddScoped(receipt);
        db.AddScoped(new AuditEvent{RecordType=nameof(PrincipalRfqRecommendationDraftRevision),
            RecordId=revision.Id,Action="RecordRecommendationDraft",ActorSubjectId=Actor,
            ChangedFieldsJson=PrincipalRfqRecommendationDraft.AuditPayload(revision)});
        db.AddScoped(new OutboxEvent{EventType=PrincipalRfqRecommendationDraft.EventType,
            AggregateType=nameof(PrincipalRfqRecommendationDraftRevision),AggregateId=revision.Id,
            IdempotencyKey=revision.CommandKey.ToString("D"),
            PayloadJson=PrincipalRfqRecommendationDraft.EventPayload(revision)});
        Assert.Equal("RfqRecommendationReceiptOrEffectsChanged",
            Assert.Throws<InvalidOperationException>(()=>db.SaveChanges()).Message);
        db.ChangeTracker.Clear();db.Attach(line);
        db.Entry(line).Property(x=>x.Rationale).CurrentValue="Changed without revision";
        Assert.Equal("RfqRecommendationHistoryImmutable",
            Assert.Throws<InvalidOperationException>(()=>db.SaveChanges()).Message);
    }

    [Fact]
    public void Revision_chain_rejects_missing_predecessor_or_header_cursor_rewrite()
    {
        var (head,first,line,_)=Graph();
        PrincipalRfqRecommendationDraft.VerifyChain(head,first,[first]);
        var second=new PrincipalRfqRecommendationDraftRevision{TenantId=head.TenantId,
            LegalEntityId=head.LegalEntityId,DraftId=head.Id,RoundId=head.RoundId,
            UnsealingId=head.UnsealingId,SourcingEventId=head.SourcingEventId,
            BranchId=head.BranchId,Revision=2,PreviousRevisionId=first.Id,
            PreviousSnapshotSha256=first.SnapshotSha256,ActorPrincipalId=Actor,
            CommandKey=Guid.NewGuid(),RequestSha256=new string('c',64),
            RecordedAt=first.RecordedAt.AddMinutes(1)};
        second.SnapshotJson=PrincipalRfqRecommendationDraft.Snapshot(head,second,[]);
        second.SnapshotSha256=ProcurementEvidenceEncoding.Digest(second.SnapshotJson);
        head.CurrentRevision=2;head.CurrentRevisionId=second.Id;
        head.CurrentSnapshotSha256=second.SnapshotSha256;
        PrincipalRfqRecommendationDraft.VerifyChain(head,second,[first,second]);
        Assert.Throws<InvalidOperationException>(()=>
            PrincipalRfqRecommendationDraft.VerifyChain(head,second,[second]));
        head.CurrentSnapshotSha256=line.OfferSha256;
        Assert.Throws<InvalidOperationException>(()=>
            PrincipalRfqRecommendationDraft.VerifyChain(head,second,[first,second]));
    }

    [Fact]
    public void Draft_graph_has_short_immutable_owner_keys_and_distinct_round_opening_hashes()
    {
        var (head,_,_,_)=Graph();
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-used.invalid;Database=model_only").Options,
            new Scope(head.TenantId,head.LegalEntityId,Actor));
        var model=db.GetService<IDesignTimeModel>().Model;
        foreach(var type in new[]{typeof(PrincipalRfqRecommendationDraftHead),
            typeof(PrincipalRfqRecommendationDraftRevision),
            typeof(PrincipalRfqRecommendationDraftLine),
            typeof(PrincipalRfqRecommendationDraftReceipt)})
        {
            var entity=model.FindEntityType(type)!;
            Assert.Equal(ValueGenerated.Never,entity.FindProperty("Id")!.ValueGenerated);
            Assert.All(entity.GetKeys(),key=>Assert.InRange(key.GetName()!.Length,1,63));
            Assert.All(entity.GetIndexes(),index=>Assert.InRange(index.GetDatabaseName()!.Length,1,63));
            Assert.All(entity.GetForeignKeys(),foreign=>{
                Assert.InRange(foreign.GetConstraintName()!.Length,1,63);
                Assert.Equal(DeleteBehavior.Restrict,foreign.DeleteBehavior);});
            Assert.DoesNotContain(entity.GetKeys().SelectMany(x=>x.Properties),
                property=>property.Name==nameof(ProcurementRecord.Version));
        }
        var entityHead=model.FindEntityType(typeof(PrincipalRfqRecommendationDraftHead))!;
        Assert.Contains(entityHead.GetForeignKeys(),fk=>fk.GetConstraintName()=="fk_p36_rec_round"
            &&fk.Properties.Any(x=>x.Name==nameof(PrincipalRfqRecommendationDraftHead.ApprovedRoundSha256)));
        Assert.Contains(entityHead.GetForeignKeys(),fk=>fk.GetConstraintName()=="fk_p36_rec_opened"
            &&fk.Properties.Any(x=>x.Name==nameof(PrincipalRfqRecommendationDraftHead.OpeningSha256)));
        Assert.Contains(entityHead.GetForeignKeys(),fk=>fk.GetConstraintName()=="fk_p36_rec_seal"
            &&fk.Properties.Any(x=>x.Name==nameof(PrincipalRfqRecommendationDraftHead.OfferSetSha256)));
    }
}
