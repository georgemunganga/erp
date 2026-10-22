using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

public sealed partial class DocumentDbContext
{
    private RetirementPermit? retirementPermit;
    internal IDisposable EnterManagedRetirement(DocumentOwnerLifecycleFence fence, DocumentOwner owner,
        Guid documentId,long originalRevision,DbTransaction transaction)
    {
        if(retirementPermit is not null) throw new InvalidOperationException("A retirement command is already active.");
        var permit=new RetirementPermit(this,fence,owner,documentId,originalRevision,transaction);
        retirementPermit=permit;
        return permit;
    }
    private async Task RequireManagedRetirementAsync(CoreDocument document,CancellationToken ct)
    {
        var permit=retirementPermit;
        if(document.LegalHold || permit is null || permit.DocumentId!=document.Id || permit.Owner.Module!=document.OwnerModule
            || permit.Owner.Type!=document.OwnerType || permit.Owner.Id!=document.OwnerId
            || Entry(document).Property(x=>x.Version).OriginalValue!=permit.OriginalRevision
            || document.Version!=checked(permit.OriginalRevision+1) || document.RetiredBy!=scope.SubjectId
            || document.RetiredAt is null || string.IsNullOrWhiteSpace(document.RetirementReason))
            throw new InvalidOperationException("Managed document retirement requires the exact authorized writer and exclusive lifecycle fence.");
        await permit.Fence.RequireHeldAsync(this,scope,[permit.Owner],permit.Transaction,true,ct);
        if(!RecordEvents.Local.Any(x=>Entry(x).State==EntityState.Added && x.Action=="DocumentRetired"
            && x.DocumentId==document.Id && x.ActorSubjectId==scope.SubjectId && x.At==document.RetiredAt))
            throw new InvalidOperationException("Managed document retirement requires its durable operation evidence.");
    }
    private sealed class RetirementPermit(DocumentDbContext db,DocumentOwnerLifecycleFence fence,
        DocumentOwner owner,Guid documentId,long originalRevision,DbTransaction transaction):IDisposable
    {
        internal DocumentOwnerLifecycleFence Fence { get; }=fence;
        internal DocumentOwner Owner { get; }=owner;
        internal Guid DocumentId { get; }=documentId;
        internal long OriginalRevision { get; }=originalRevision;
        internal DbTransaction Transaction { get; }=transaction;
        public void Dispose() { if(ReferenceEquals(db.retirementPermit,this)) db.retirementPermit=null; }
    }
}
