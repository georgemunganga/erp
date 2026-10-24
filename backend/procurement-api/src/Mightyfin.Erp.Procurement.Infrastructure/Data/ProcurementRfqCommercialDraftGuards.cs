using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardRfqCommercialDraft()
    {
        var entries=ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.Entity is PrincipalRfqCommercialDraftHead
            or PrincipalRfqCommercialDraftRevision or PrincipalRfqCommercialDraftReceipt).ToArray();
        foreach(var entry in entries)
        {
            if(entry.State is EntityState.Unchanged or EntityState.Detached)continue;
            if(entry.Entity.TenantId!=scope.TenantId||entry.Entity.LegalEntityId!=scope.LegalEntityId)
                throw new InvalidOperationException("RfqCommercialDraftScopeMismatch");
            if(entry.State==EntityState.Deleted||entry.State==EntityState.Modified&&entry.Entity is not PrincipalRfqCommercialDraftHead)
                throw new InvalidOperationException("RfqCommercialDraftEvidenceAppendOnly");
            if(entry.State==EntityState.Modified&&entry.Properties.Any(x=>x.IsModified&&x.Metadata.Name is not
                (nameof(PrincipalRfqCommercialDraftHead.Revision) or nameof(PrincipalRfqCommercialDraftHead.SnapshotSha256)
                 or nameof(ProcurementRecord.Version) or nameof(ProcurementRecord.UpdatedAt) or nameof(ProcurementRecord.UpdatedBy))))
                throw new InvalidOperationException("RfqCommercialDraftOriginalPinsImmutable");
        }
        var revisions=ChangeTracker.Entries<PrincipalRfqCommercialDraftRevision>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var receipts=ChangeTracker.Entries<PrincipalRfqCommercialDraftReceipt>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
        var heads=ChangeTracker.Entries<PrincipalRfqCommercialDraftHead>().Where(x=>x.State is EntityState.Added or EntityState.Modified).ToArray();
        foreach(var entry in heads)
        {
            var head=entry.Entity;var rows=revisions.Where(x=>x.DraftId==head.Id).ToArray();
            var commands=receipts.Where(x=>x.DraftId==head.Id).ToArray();
            if(head.SchemaVersion!=1||head.SourceIdentitySchemaVersion!=2||head.OwnerPrincipalId!=scope.SubjectId||rows.Length!=1||commands.Length!=1
                ||entry.State==EntityState.Added&&head.Revision!=1
                ||entry.State==EntityState.Modified&&head.Revision!=entry.Property(x=>x.Revision).OriginalValue+1)
                throw new InvalidOperationException("RfqCommercialDraftExactRevisionRequired");
            var row=rows[0];var receipt=commands[0];
            var ev=SourcingEvents.Local.SingleOrDefault(x=>x.Id==head.SourcingEventId);
            var evidence=SourcingDraftEvidenceRecords.Local.SingleOrDefault(x=>x.Id==head.SourceEvidenceId);
            if(ev is null||evidence is null||ev.ExecutionVersion!=2||ev.Status!="Draft"||ev.BranchRef!=head.BranchId
                ||ev.OwnerSubjectId!=head.OwnerPrincipalId||ev.MakerWorkerId!=head.OwnerWorkerAliasId
                ||ev.SourceEvidenceId!=head.SourceEvidenceId||ev.OriginalDraftFingerprint!=head.OriginalFingerprint
                ||evidence.PayloadSha256!=head.SourceEnvelopeSha256)
                throw new InvalidOperationException("RfqCommercialDraftTrackedSourceRequired");
            var bindings=SourcingDemandBindings.Local.Where(x=>x.SourcingEventId==ev.Id).ToArray();
            if(bindings.Any(x=>x.TargetType!="SourcingEvent"||x.IdentitySchemaVersion!=2))throw new InvalidOperationException("RfqCommercialDraftOriginalBindingRequired");
            var source=GovernedPrincipalRfqRestoration.RestoreVerified(evidence,ev,bindings);
            _=PrincipalRfqCommercialDraft.ValidateInput(PrincipalRfqCommercialDraft.Restore(head,row),source.CurrencyCode,source.Id);
            if(row.ActorPrincipalId!=scope.SubjectId||row.ActorWorkerAliasId!=scope.CoreLinkedWorkerId||row.CommandKey==Guid.Empty
                ||receipt.SchemaVersion!=1||receipt.ActorPrincipalId!=scope.SubjectId||receipt.DraftRevisionId!=row.Id
                ||receipt.CommandKey!=row.CommandKey||receipt.SourcingEventId!=head.SourcingEventId||receipt.SourceEvidenceId!=head.SourceEvidenceId
                ||receipt.BranchId!=head.BranchId||receipt.SnapshotSha256!=row.SnapshotSha256||receipt.ResultRevision!=head.Revision||!RfqCommercialHash(receipt.RequestSha256)
                ||receipt.ResponseJson!=PrincipalRfqCommercialDraft.Response(head,row.CommandKey)
                ||receipt.ResponseSha256!=ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
                ||receipt.Operation!=(entry.State==EntityState.Added?"Create":"Revise")
                ||receipt.ResponseStatus!=(entry.State==EntityState.Added?201:200))
                throw new InvalidOperationException("RfqCommercialDraftReceiptInvalid");
        }
        if(revisions.Length!=heads.Length||receipts.Length!=heads.Length)
            throw new InvalidOperationException("RfqCommercialDraftOrphanEvidenceDenied");
    }
    private static bool RfqCommercialHash(string value)=>value is{Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
