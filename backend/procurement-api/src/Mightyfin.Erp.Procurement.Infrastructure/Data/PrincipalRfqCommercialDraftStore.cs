using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Unregistered owner-enlisted primitive. The caller resolves live Core
/// action/owner/branch and locks source rows BEFORE these reads. No policy, scan,
/// autonomous transaction, Save/Commit or guessed readiness occurs here.</summary>
public sealed class PrincipalRfqCommercialDraftStore(ProcurementDbContext db,IProcurementScope scope)
{
    private void Enlisted()
    {if(!db.Database.IsNpgsql()||db.Database.CurrentTransaction is null)throw new InvalidOperationException("RfqCommercialDraftOwnerTransactionRequired");
        if(scope.OrgUnitId is not null||scope.IsConfined&&scope.WorkLocationId is null&&!scope.IsExplicitAllBranches)throw new UnauthorizedAccessException("RfqCommercialDraftExplicitScopeRequired");}
    public async Task<(PrincipalRfqCommercialDraftHead Head,PrincipalRfqCommercialDraftRevision Revision)> LoadAsync(Guid eventId,CancellationToken ct)
    {
        Enlisted();
        var head=await db.PrincipalRfqCommercialDrafts.SingleOrDefaultAsync(x=>x.SourcingEventId==eventId&&x.OwnerPrincipalId==scope.SubjectId&&(scope.WorkLocationId==null||x.BranchId==scope.WorkLocationId),ct)
            ??throw new KeyNotFoundException("RfqCommercialDraftNotFound");
        var row=await db.PrincipalRfqCommercialDraftRevisions.SingleAsync(x=>x.DraftId==head.Id&&x.Revision==head.Revision,ct);
        var receipt=await db.PrincipalRfqCommercialDraftReceipts.AsNoTracking().SingleAsync(x=>x.DraftRevisionId==row.Id,ct);
        _=PrincipalRfqCommercialDraft.Restore(head,row);PrincipalRfqCommercialDraft.VerifyReceipt(head,row,receipt);return(head,row);
    }
    public async Task<PrincipalRfqCommercialDraftReceipt?> RecoverAsync(Guid eventId,Guid key,string operation,string? fingerprint,CancellationToken ct)
    {
        Enlisted();if(key==Guid.Empty||operation is not("Create" or "Revise"))throw new ArgumentException("RfqCommercialDraftCommandRequired");
        var receipt=await db.PrincipalRfqCommercialDraftReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.CommandKey==key&&(scope.WorkLocationId==null||x.BranchId==scope.WorkLocationId),ct);
        if(receipt is null)return null;
        if(receipt.ActorPrincipalId!=scope.SubjectId||receipt.SourcingEventId!=eventId)throw new KeyNotFoundException("RfqCommercialDraftCommandNotFound");
        if(receipt.Operation!=operation||fingerprint is not null&&receipt.RequestSha256!=fingerprint)
            throw new InvalidOperationException("RfqCommercialDraftCommandConflict");
        var head=await db.PrincipalRfqCommercialDrafts.AsNoTracking().SingleAsync(x=>x.Id==receipt.DraftId&&x.OwnerPrincipalId==scope.SubjectId,ct);
        var row=await db.PrincipalRfqCommercialDraftRevisions.AsNoTracking().SingleAsync(x=>x.Id==receipt.DraftRevisionId,ct);
        _=PrincipalRfqCommercialDraft.RestoreHistorical(head,row,receipt);return receipt;
    }
    public PrincipalRfqCommercialDraftReceipt Create(RfqPrincipalActor actor,Guid key,string requestSha,SourcingEvent ev,
        SourcingDraftEvidence evidence,IReadOnlyList<SourcingDemandBinding> bindings,PrincipalRfqCommercialDraftInput input)
    {
        Enlisted();RequireActor(actor);
        var(head,row)=PrincipalRfqCommercialDraft.Create(actor,Guid.CreateVersion7(),key,ev,evidence,bindings,input);
        db.AddScoped(head);db.AddScoped(row);return Complete(head,row,"Create",requestSha);
    }
    public PrincipalRfqCommercialDraftReceipt Revise(RfqPrincipalActor actor,Guid key,string requestSha,
        PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision previous,long expectedRevision,
        SourcingEvent ev,SourcingDraftEvidence evidence,IReadOnlyList<SourcingDemandBinding> bindings,PrincipalRfqCommercialDraftInput input)
    {
        Enlisted();RequireActor(actor);
        if(db.Entry(head).State!=EntityState.Unchanged)throw new InvalidOperationException("RfqCommercialDraftTrackedCurrentHeadRequired");
        var row=PrincipalRfqCommercialDraft.Revise(actor,head,previous,expectedRevision,key,ev,evidence,bindings,input);
        db.AddScoped(row);return Complete(head,row,"Revise",requestSha);
    }
    private void RequireActor(RfqPrincipalActor actor)
    {if(actor.TenantId!=scope.TenantId||actor.CompanyId!=scope.LegalEntityId||actor.PrincipalId!=scope.SubjectId)
        throw new UnauthorizedAccessException("RfqCommercialDraftOwnerScopeMismatch");}
    private PrincipalRfqCommercialDraftReceipt Complete(PrincipalRfqCommercialDraftHead head,PrincipalRfqCommercialDraftRevision row,string operation,string requestSha)
    {
        var json=PrincipalRfqCommercialDraft.Response(head,row.CommandKey);
        var receipt=new PrincipalRfqCommercialDraftReceipt{DraftId=head.Id,DraftRevisionId=row.Id,SourcingEventId=head.SourcingEventId,
            SourceEvidenceId=head.SourceEvidenceId,BranchId=head.BranchId,Operation=operation,CommandKey=row.CommandKey,
            ActorPrincipalId=scope.SubjectId,RequestSha256=requestSha,SnapshotSha256=row.SnapshotSha256,
            ResponseJson=json,ResponseSha256=ProcurementEvidenceEncoding.Digest(json),ResponseStatus=operation=="Create"?201:200,ResultRevision=head.Revision};
        db.AddScoped(receipt);
        db.AddScoped(new AuditEvent{RecordType="RfqCommercialDraft",RecordId=head.Id,Action=operation,ActorSubjectId=scope.SubjectId,
            ChangedFieldsJson=System.Text.Json.JsonSerializer.Serialize(new{head.SourcingEventId,revisionId=row.Id,row.Revision,row.CommandKey,row.SnapshotSha256,canPublish=false}),CorrelationId=scope.CorrelationId});
        db.AddScoped(new OutboxEvent{AggregateType="RfqCommercialDraft",AggregateId=head.Id,EventType="sourcing.commercial_draft_changed.v1",
            IdempotencyKey=$"rfq.commercial.{row.CommandKey:N}.v1",PayloadJson=System.Text.Json.JsonSerializer.Serialize(new{head.SourcingEventId,row.Revision,operation,
                internallyPublished=false,supplierSendPerformed=false,financeReservationChanged=false})});
        return receipt;
    }
}
