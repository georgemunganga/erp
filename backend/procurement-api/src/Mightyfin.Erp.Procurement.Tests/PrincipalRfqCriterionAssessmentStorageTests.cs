using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqCriterionAssessmentStorageTests
{
    private const string Actor="7bf00a55-b537-4a21-b582-2b9055029ae7";
    private sealed record Scope(string TenantId,Guid LegalEntityId,string SubjectId):IProcurementScope
    {public Guid? WorkLocationId=>null;public Guid? OrgUnitId=>null;public bool IsConfined=>false;public string? CorrelationId=>null;}
    private static PrincipalRfqEvaluationPlanDefinition Definition()=>PrincipalRfqEvaluationPlanDefinition.Parse($$"""
        {"schemaVersion":1,"transactionType":"SourcingEvaluationDefinition","normalizationTotal":"1",
         "criteria":[{"code":"TECH","phase":"Technical","kind":"Scored","mandatory":true,
           "maximumScore":"10","weight":"1","evaluatorPrincipalIds":["{{Actor}}"]}]}
        """);
    private static (PrincipalRfqCriterionAssessmentRecord Row,PrincipalRfqCriterionAssessmentReceipt Ack,
        PrincipalRfqOfferOpeningRecord Opening,PrincipalRfqCommercialOfferSubmissionRecord Offer) Rows()
    {
        var company=Guid.NewGuid();var round=Guid.NewGuid();var ev=Guid.NewGuid();var branch=Guid.NewGuid();
        var document=Guid.NewGuid();var docSha=new string('d',64);var definition=Definition();
        var opening=new PrincipalRfqOfferOpeningRecord{TenantId="fixture",LegalEntityId=company,
            OpeningId=round,SourcingEventId=ev,BranchId=branch,
            SnapshotSha256=new string('a',64),OpenedAt=DateTimeOffset.UtcNow.AddMinutes(-1)};
        var offer=new PrincipalRfqCommercialOfferSubmissionRecord{TenantId="fixture",LegalEntityId=company,
            OpeningId=round,SourcingEventId=ev,BranchId=branch,
            SnapshotSha256=new string('b',64),OriginalDocumentVersionId=document,OriginalDocumentSha256=docSha};
        var row=new PrincipalRfqCriterionAssessmentRecord{TenantId="fixture",LegalEntityId=company,
            RoundId=round,UnsealingId=opening.Id,OfferId=offer.Id,EventId=ev,BranchId=branch,
            PreparedPlanId=Guid.NewGuid(),EvaluationPlanRevisionId=Guid.NewGuid(),
            EvaluationDefinitionSha256=definition.RulesSha256,OpeningSha256=opening.SnapshotSha256,
            OfferSha256=offer.SnapshotSha256,CriterionCode="TECH",Kind="Scored",
            EvaluatorPrincipalId=Actor,Revision=1,Score=0m,Rationale="Observed no compliance evidence",
            OriginalDocumentVersionId=document,OriginalDocumentSha256=docSha,
            CommandKey=Guid.NewGuid(),RequestSha256=new string('c',64),RecordedAt=DateTimeOffset.UtcNow};
        row.SnapshotJson=PrincipalRfqCriterionAssessmentStorage.Snapshot(row);
        row.SnapshotSha256=ProcurementEvidenceEncoding.Digest(row.SnapshotJson);
        var response=PrincipalRfqCriterionAssessmentStorage.Response(row);
        var ack=new PrincipalRfqCriterionAssessmentReceipt{TenantId=row.TenantId,LegalEntityId=row.LegalEntityId,
            AssessmentId=row.Id,RoundId=round,UnsealingId=opening.Id,OfferId=offer.Id,EventId=ev,
            BranchId=branch,ActorPrincipalId=Actor,CommandKey=row.CommandKey,RequestSha256=row.RequestSha256,
            ResponseJson=response,ResponseSha256=ProcurementEvidenceEncoding.Digest(response)};
        return(row,ack,opening,offer);
    }
    [Fact]
    public void Original_zero_score_and_ack_bind_one_offer_original_document_and_named_evaluator()
    {
        var (row,ack,opening,offer)=Rows();
        PrincipalRfqCriterionAssessmentStorage.Verify(row,opening,offer,Definition());
        PrincipalRfqCriterionAssessmentStorage.VerifyReceipt(row,ack);
        Assert.Equal(0m,row.Score);
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqCriterionAssessmentStorage.Verify(row,
            opening,new PrincipalRfqCommercialOfferSubmissionRecord{Id=offer.Id,TenantId=offer.TenantId,
                LegalEntityId=offer.LegalEntityId,OpeningId=offer.OpeningId,SourcingEventId=offer.SourcingEventId,
                BranchId=offer.BranchId,SnapshotSha256=offer.SnapshotSha256,
                OriginalDocumentVersionId=Guid.NewGuid(),OriginalDocumentSha256=offer.OriginalDocumentSha256},Definition()));
        Assert.Throws<InvalidOperationException>(()=>PrincipalRfqCriterionAssessmentStorage.VerifyReceipt(row,
            new PrincipalRfqCriterionAssessmentReceipt{AssessmentId=ack.AssessmentId,TenantId=ack.TenantId,
                LegalEntityId=ack.LegalEntityId,RoundId=ack.RoundId,UnsealingId=ack.UnsealingId,
                OfferId=ack.OfferId,EventId=ack.EventId,BranchId=ack.BranchId,
                ActorPrincipalId=Guid.NewGuid().ToString("D"),CommandKey=ack.CommandKey,
                RequestSha256=ack.RequestSha256,ResponseJson=ack.ResponseJson,ResponseSha256=ack.ResponseSha256}));
    }
    [Fact]
    public void Missing_same_save_ack_and_later_rewrite_are_denied()
    {
        var (row,_,_,_)=Rows();var scope=new Scope(row.TenantId,row.LegalEntityId,row.EvaluatorPrincipalId);
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,scope);
        db.AddScoped(row);
        Assert.Equal("RfqAssessmentSameSaveReceiptRequired",
            Assert.Throws<InvalidOperationException>(()=>db.SaveChanges()).Message);
        db.ChangeTracker.Clear();db.Attach(row);db.Entry(row).Property(x=>x.Rationale).CurrentValue="Rewrite";
        Assert.Equal("RfqAssessmentAppendOnlyScopeRequired",
            Assert.Throws<InvalidOperationException>(()=>db.SaveChanges()).Message);
    }
    [Fact]
    public void Physical_owner_keys_are_explicit_and_no_mutable_cursor_is_a_key()
    {
        var (row,_,_,_)=Rows();
        using var db=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-used.invalid;Database=model_only").Options,
            new Scope(row.TenantId,row.LegalEntityId,row.EvaluatorPrincipalId));
        var model=db.GetService<IDesignTimeModel>().Model;
        foreach(var type in new[]{typeof(PrincipalRfqCriterionAssessmentRecord),typeof(PrincipalRfqCriterionAssessmentReceipt)})
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
        var assessment=model.FindEntityType(typeof(PrincipalRfqCriterionAssessmentRecord))!;
        Assert.Contains(assessment.GetForeignKeys(),x=>x.GetConstraintName()=="fk_p32_assess_offer"
            && x.Properties.Any(p=>p.Name=="OfferSha256"));
    }
}
