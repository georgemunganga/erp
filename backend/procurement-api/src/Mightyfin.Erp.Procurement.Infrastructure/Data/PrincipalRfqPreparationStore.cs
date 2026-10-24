using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Infrastructure.Data;
public sealed partial class PrincipalRfqCommercialDraftStore
{
    public async Task<PrincipalRfqPreparationReceipt?> RecoverPreparationAsync(Guid eventId,Guid key,string? fingerprint,CancellationToken ct)
    {
        Enlisted();if(key==Guid.Empty)throw new ArgumentException("RfqPreparationCommandRequired");
        var receipt=await db.PrincipalRfqPreparationReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.CommandKey==key
            &&(scope.WorkLocationId==null||x.BranchId==scope.WorkLocationId),ct);
        if(receipt is null)return null;
        if(receipt.ActorPrincipalId!=scope.SubjectId||receipt.SourcingEventId!=eventId)throw new KeyNotFoundException("RfqPreparationCommandNotFound");
        if(fingerprint is not null&&receipt.RequestSha256!=fingerprint)throw new InvalidOperationException("RfqPreparationCommandConflict");
        var plan=await db.PrincipalRfqPreparedPlans.AsNoTracking().SingleAsync(x=>x.Id==receipt.PreparedPlanId,ct);
        var head=await db.PrincipalRfqCommercialDrafts.AsNoTracking().SingleAsync(x=>x.Id==plan.DraftId,ct);
        var row=await db.PrincipalRfqCommercialDraftRevisions.AsNoTracking().SingleAsync(x=>x.Id==plan.DraftRevisionId,ct);
        var command=await db.PrincipalRfqCommercialDraftReceipts.AsNoTracking().SingleAsync(x=>x.Id==receipt.DraftCommandReceiptId,ct);
        _=PrincipalRfqCommercialDraft.RestoreHistorical(head,row,command);
        PrincipalRfqPreparedPlanEvidence.Verify(plan,head,row);PrincipalRfqPreparedPlanEvidence.VerifyReceipt(plan,receipt,command);
        return receipt;
    }
    public PrincipalRfqPreparationReceipt StagePreparation(PrincipalRfqPrepublicationPlan candidate,PrincipalRfqPublishedPlanPolicy policy,
        PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row,RfqPrincipalActor actor,DateTimeOffset at,
        int documentCount,Guid? useSetId,string? selectionDigest,PrincipalRfqCommercialDraftReceipt command)
    {
        Enlisted();RequireActor(actor);PrincipalRfqCommercialDraft.VerifyReceipt(head,row,command);
        if(db.Entry(row).State!=EntityState.Added||db.Entry(command).State!=EntityState.Added||command.Operation!="Revise")
            throw new InvalidOperationException("RfqPreparationSameSaveRevisionRequired");
        var plan=PrincipalRfqPreparedPlanEvidence.Create(candidate,policy,head,row,actor,at,documentCount,useSetId,selectionDigest);
        db.AddScoped(plan);var body=PrincipalRfqPreparedPlanEvidence.Response(plan,command.CommandKey);
        var receipt=new PrincipalRfqPreparationReceipt{PreparedPlanId=plan.Id,DraftRevisionId=row.Id,DraftCommandReceiptId=command.Id,
            SourcingEventId=head.SourcingEventId,BranchId=head.BranchId,CommandKey=command.CommandKey,ActorPrincipalId=scope.SubjectId,
            RequestSha256=command.RequestSha256,SnapshotSha256=plan.SnapshotSha256,ResponseJson=body,ResponseSha256=ProcurementEvidenceEncoding.Digest(body)};
        db.AddScoped(receipt);
        db.AddScoped(new AuditEvent{RecordType="RfqPreparedPlan",RecordId=plan.Id,Action="PreparedCandidate",ActorSubjectId=scope.SubjectId,
            ChangedFieldsJson=System.Text.Json.JsonSerializer.Serialize(new{head.SourcingEventId,row.Revision,command.CommandKey,plan.SnapshotSha256,
                plan.PolicyVersionId,canPublish=false}),CorrelationId=scope.CorrelationId});
        db.AddScoped(new OutboxEvent{AggregateType="RfqPreparedPlan",AggregateId=plan.Id,EventType="sourcing.prepared_candidate_recorded.v1",
            IdempotencyKey=$"rfq.prepared.{command.CommandKey:N}.v1",PayloadJson=System.Text.Json.JsonSerializer.Serialize(new{head.SourcingEventId,
                row.Revision,canPublish=false,internallyPublished=false,supplierSendPerformed=false,financeReservationChanged=false})});
        return receipt;
    }
}
