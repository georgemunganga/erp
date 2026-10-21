using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

/// <summary>
/// Unregistered managed writer. The supplied clean owning context is single-use for this command.
/// No retry/unknown-outcome replay, purge, release, blob IO or connection configuration is supplied.
/// The authorizer factory must compose current owning-module authority on this exact enlisted context.
/// </summary>
public sealed class EfDocumentRetirement(DocumentDbContext db, IDocumentScope scope,
    Func<DocumentDbContext, CancellationToken, Task<IDocumentRetirementAccess>> authorizeOnOwner,
    TimeProvider? timeProvider=null)
{
    public async Task RetireAsync(DocumentOwner owner, Guid documentId, long expectedDocumentRevision,
        string reason, CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(authorizeOnOwner);
        db.RequireDocumentUseScope(scope);
        if(documentId==Guid.Empty || expectedDocumentRevision<1 || expectedDocumentRevision==long.MaxValue || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length>1000)
            throw new ArgumentException("Choose an exact document revision and a bounded retirement reason.");
        _=DocumentOwnerLifecycleFence.OrderedKeys(scope,[owner]);
        if(!db.Database.IsNpgsql() || db.Database.CurrentTransaction is not null || db.Database.GetEnlistedTransaction() is not null
            || System.Transactions.Transaction.Current is not null || db.Database.GetDbConnection().State!=ConnectionState.Closed
            || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Use a fresh owning PostgreSQL context before any reads or transaction.");
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var fence=await DocumentOwnerLifecycleFence.AcquireAsync(db,scope,[owner],true,ct);
            await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var access=await authorizeOnOwner(db,ct) ?? throw new InvalidOperationException("Retirement authority is unavailable.");
            await access.RequireAsync(owner,documentId,expectedDocumentRevision,ct);
            await fence.RequireHeldAsync(db,scope,[owner],transaction.GetDbTransaction(),true,ct);
            var document=await db.Documents.SingleOrDefaultAsync(x=>x.Id==documentId && x.OwnerModule==owner.Module
                && x.OwnerType==owner.Type && x.OwnerId==owner.Id,ct)
                ?? throw new DocumentProblemException("document-not-found","Document not found.");
            if(document.LegalHold)
                throw new DocumentProblemException("document-held","This document is on hold and cannot be retired.");
            if(document.Version!=expectedDocumentRevision || document.IsRetired)
                throw new DocumentProblemException("document-changed","This document has changed. Refresh before retiring it.");
            if(db.UsePins.Local.Any(x=>x.DocumentId==documentId) || await db.UsePins.AnyAsync(x=>x.DocumentId==documentId,ct))
                throw new DocumentProblemException("document-retained","This document is retained by a business record.");
            var version=await db.Versions.Where(x=>x.DocumentId==documentId).OrderByDescending(x=>x.Number).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Owned document version evidence is missing.");
            var now=(timeProvider ?? TimeProvider.System).GetUtcNow();
            if(now<document.CreatedAt) throw new InvalidOperationException("Retirement cannot predate the document.");
            using var permit=db.EnterManagedRetirement(fence,owner,documentId,expectedDocumentRevision,transaction.GetDbTransaction());
            document.Retire(scope,reason,now);
            db.RecordEvents.Add(new(Guid.CreateVersion7(),scope.TenantId,scope.LegalEntityId,
                document.Id,version.Id,"DocumentRetired",scope.SubjectId,now,null));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally
        {
            // On failure caller discards this command context; pending tracked mutations are never a retry recipe.
            await db.Database.CloseConnectionAsync();
        }
    }
}
