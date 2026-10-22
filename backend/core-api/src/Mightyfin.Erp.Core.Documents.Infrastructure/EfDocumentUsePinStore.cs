using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

/// <summary>
/// Caller-enlisted use adapter only; no DI/default authorizer, connection configuration,
/// transaction ownership, Save/Commit, release, download or operational publication.
/// Factory/each operation verify the actual pre-BEGIN lease and active owning transaction.
/// Do not concurrently use/dispose/change the context, connection or lease during operations.
/// </summary>
public sealed class EfDocumentUsePinStore : IDocumentUsePinStore
{
    private readonly DocumentDbContext db;
    private readonly IDocumentScope scope;
    private readonly IDocumentUseAccess access;
    private readonly DocumentOwnerLifecycleFence fence;
    private readonly DbTransaction transaction;
    private readonly DocumentOwner[] owners;
    private readonly TimeProvider clock;
    private readonly DocumentUseRepository repository;
    private EfDocumentUsePinStore(DocumentDbContext db, IDocumentScope scope, IDocumentUseAccess access,
        DocumentOwnerLifecycleFence fence, DbTransaction transaction, DocumentOwner[] owners, TimeProvider clock)
    {
        this.db=db; this.scope=scope; this.access=access; this.fence=fence;
        this.transaction=transaction; this.owners=owners; this.clock=clock;
        repository=new(db,scope);
    }

    public static async Task<EfDocumentUsePinStore> CreateAsync(DocumentDbContext ownerContext,
        IDocumentScope scope, IDocumentUseAccess access, DocumentOwnerLifecycleFence fence,
        IReadOnlyList<DocumentOwner> owners, CancellationToken ct=default, TimeProvider? timeProvider=null)
    {
        ArgumentNullException.ThrowIfNull(ownerContext);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(fence);
        ArgumentNullException.ThrowIfNull(owners);
        ownerContext.RequireDocumentUseScope(scope);
        var copy=owners.ToArray();
        _=DocumentOwnerLifecycleFence.OrderedKeys(scope,copy);
        if(!ownerContext.Database.IsNpgsql() || ownerContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("The actual owning PostgreSQL transaction is required.");
        var transaction=ownerContext.Database.CurrentTransaction.GetDbTransaction();
        await fence.RequireHeldAsync(ownerContext,scope,copy,transaction,false,ct);
        return new(ownerContext,scope,access,fence,transaction,copy,timeProvider ?? TimeProvider.System);
    }

    private Task RequireAsync(DocumentOwner owner, bool exclusive, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if(!owners.Contains(owner)) throw new InvalidOperationException("This owner is outside the original fenced set.");
        return fence.RequireHeldAsync(db,scope,[owner],transaction,exclusive,ct);
    }

    public async Task<IReadOnlyList<DocumentUsePin>> AcquireAsync(DocumentOwner owner, DocumentUseReference source,
        IReadOnlyList<DocumentUseSelection> selections, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        RequireReference(source);
        ArgumentNullException.ThrowIfNull(selections);
        var copy=selections.ToArray();
        if(copy.Length is < 1 or > 100 || copy.Any(x=>x is null)
            || copy.Select(x=>x.VersionId).Distinct().Count()!=copy.Length
            || copy.Any(x=>x.VersionId==Guid.Empty || x.OwnerModule!=owner.Module || x.OwnerType!=owner.Type || x.OwnerId!=owner.Id))
            throw new ArgumentException("Choose a bounded exact set of owned versions.",nameof(selections));
        await RequireAsync(owner,false,cancellationToken);
        await access.RequireAsync(owner,source,DocumentUseOperation.Acquire,
            Array.AsReadOnly(copy.Select(x=>x.VersionId).Order().ToArray()),cancellationToken);
        await RequireAsync(owner,false,cancellationToken);
        var stored=await repository.StageAsync(owner,source,copy,clock.GetUtcNow(),cancellationToken);
        var result=Restore(stored);
        await RequireAsync(owner,false,cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<DocumentUsePin>> FindAsync(DocumentOwner owner, DocumentUseReference source,
        CancellationToken cancellationToken)
    {
        RequireReference(source);
        await RequireAsync(owner,false,cancellationToken);
        // Authorize reference before lookup, then its exact original version set before returning metadata.
        await access.RequireAsync(owner,source,DocumentUseOperation.Read,Array.Empty<Guid>(),cancellationToken);
        await RequireAsync(owner,false,cancellationToken);
        var stored=await repository.FindAsync(owner,source,cancellationToken);
        var pins=stored is null ? Array.Empty<DocumentUsePin>() : Restore(stored);
        await access.RequireAsync(owner,source,DocumentUseOperation.Read,
            Array.AsReadOnly(pins.Select(x=>x.VersionId).Order().ToArray()),cancellationToken);
        await RequireAsync(owner,false,cancellationToken);
        return pins;
    }

    public async Task RequireNoRetainedUsesAsync(DocumentOwner owner, Guid documentId, CancellationToken cancellationToken)
    {
        if(documentId==Guid.Empty) throw new ArgumentException("An exact document is required.",nameof(documentId));
        // Coupled persistence guard, NOT retirement authorization. Future managed writer owns that check.
        await RequireAsync(owner,true,cancellationToken);
        if(!await db.Documents.AnyAsync(x=>x.Id==documentId && x.OwnerModule==owner.Module
            && x.OwnerType==owner.Type && x.OwnerId==owner.Id,cancellationToken))
            throw new InvalidOperationException("Document owner not found.");
        if(db.UsePins.Local.Any(x=>x.DocumentId==documentId)
            || await db.UsePins.AnyAsync(x=>x.DocumentId==documentId,cancellationToken))
            throw new InvalidOperationException("Retained document uses cannot be retired or released.");
        await RequireAsync(owner,true,cancellationToken);
    }

    /// <summary>Read only immutable already-retained evidence on the caller's enlisted
    /// owner context. This creates no use and asserts no current availability or lifecycle
    /// permission. Current module purpose is authorized before lookup and on the exact set.
    /// New acquisition/retirement still requires the pre-BEGIN lifecycle fence APIs above.</summary>
    public static async Task<IReadOnlyList<DocumentUsePin>> ReadRetainedAsync(DocumentDbContext ownerContext,
        IDocumentScope scope, IDocumentUseAccess access, DocumentOwner owner, DocumentUseReference source,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerContext); ArgumentNullException.ThrowIfNull(access);
        ownerContext.RequireDocumentUseScope(scope); RequireReference(source);
        if (!ownerContext.Database.IsNpgsql() || ownerContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("The caller's actual owning transaction is required.");
        await access.RequireAsync(owner, source, DocumentUseOperation.Read, Array.Empty<Guid>(), ct);
        var stored = await new DocumentUseRepository(ownerContext, scope).FindAsync(owner, source, ct);
        var pins = stored is null ? Array.Empty<DocumentUsePin>() : Restore(stored);
        await access.RequireAsync(owner, source, DocumentUseOperation.Read, pins.Select(x => x.VersionId).Order().ToArray(), ct);
        return pins;
    }

    private static void RequireReference(DocumentUseReference source)
    {
        ArgumentNullException.ThrowIfNull(source);
        static bool Token(string? value,int max) => !string.IsNullOrWhiteSpace(value) && value.Length<=max
            && value.All(c=>char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');
        if(source.Key==Guid.Empty || source.RecordId==Guid.Empty || source.Revision<1 || !Enum.IsDefined(source.Kind)
            || !Token(source.Module,60) || !Token(source.RecordType,80))
            throw new ArgumentException("An exact canonical use reference is required.",nameof(source));
    }

    internal static DocumentUsePin[] Restore(StoredDocumentUse stored)
    {
        var set=stored.Set;
        if(set.Id==Guid.Empty || set.SelectionCount is < 1 or > 100 || stored.Pins.Count!=set.SelectionCount
            || stored.Pins.Select(x=>x.Id).Distinct().Count()!=stored.Pins.Count
            || stored.Pins.Select(x=>x.VersionId).Distinct().Count()!=stored.Pins.Count
            || DocumentUsePinRow.Digest(stored.Pins)!=set.SelectionDigest
            || stored.Pins.Any(x=>x.UseSetId!=set.Id || x.TenantId!=set.TenantId || x.LegalEntityId!=set.LegalEntityId
                || x.OwnerModule!=set.OwnerModule || x.OwnerType!=set.OwnerType || x.OwnerId!=set.OwnerId
                || x.CreatedBy!=set.CreatedBy || x.CreatedAt!=set.CreatedAt))
            throw new InvalidOperationException("Retained document use evidence is incomplete or inconsistent.");
        return stored.Pins.OrderBy(x=>x.VersionId).Select(x=>x.Restore(set.Reference())).ToArray();
    }
}
