using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

/// <summary>
/// Internal staging/recovery primitive only. NOT an IDocumentUsePinStore implementation and never registered in DI.
/// It supplies no module authorization, same-store proof or lifecycle lease. A future adapter must establish all three
/// before using it; relational staging requires an existing caller transaction and never saves/commits on its own.
/// </summary>
internal sealed class DocumentUseRepository(DocumentDbContext db, IDocumentScope scope)
{
    internal async Task<StoredDocumentUse> StageAsync(DocumentOwner owner, DocumentUseReference source,
        IReadOnlyList<DocumentUseSelection> selections, DateTimeOffset at, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selections);
        RequireScopeAndOwner(owner);
        if (selections.Any(x => x is null)) throw new ArgumentException("Every selected version is required.", nameof(selections));
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Document use staging requires the owning enlisted transaction.");
        if (selections.Count is < 1 or > 100 || selections.Select(x => x.VersionId).Distinct().Count() != selections.Count
            || selections.Any(x => x.OwnerModule != owner.Module || x.OwnerType != owner.Type || x.OwnerId != owner.Id))
            throw new InvalidOperationException("Choose a bounded exact set of owned document versions.");
        var original = await FindKeyAsync(source.Key, ct);
        if (original is not null)
        {
            if (original.Set.Reference() != source || original.Set.CreatedBy != scope.SubjectId
                || original.Set.OwnerModule != owner.Module || original.Set.OwnerType != owner.Type || original.Set.OwnerId != owner.Id
                || !original.Pins.OrderBy(x => x.VersionId).Select(x => x.Selection())
                    .SequenceEqual(selections.OrderBy(x => x.VersionId)))
                throw new InvalidOperationException("This use key already describes a different document selection.");
            return original; // No current document/scan/retirement eligibility query on original-key recovery.
        }
        var ids = selections.Select(x => x.VersionId).ToArray();
        var records = await (from version in db.Versions
            join document in db.Documents on version.DocumentId equals document.Id
            where ids.Contains(version.Id) && document.OwnerModule == owner.Module && document.OwnerType == owner.Type && document.OwnerId == owner.Id
            select new { document, version }).ToListAsync(ct);
        if (records.Count != selections.Count) throw new InvalidOperationException("Document version not found.");
        var scanRows = await db.ScanEvidence.Where(x => ids.Contains(x.VersionId)).ToListAsync(ct);
        var pins = selections.Select(selection =>
        {
            var record = records.Single(x => x.version.Id == selection.VersionId);
            var scan = scanRows.SingleOrDefault(x => x.VersionId == record.version.Id && x.Attempt == record.version.ScanAttempts)
                ?? throw new InvalidOperationException("Completed scan evidence not found.");
            return DocumentUsePin.Create(scope, source, selection, record.document, record.version, scan, at);
        }).ToArray();
        var set = DocumentUseSetRow.FromPins(pins);
        var rows = pins.Select(x => DocumentUsePinRow.FromPin(set.Id, x)).ToArray();
        db.UseSets.Add(set);
        db.UsePins.AddRange(rows);
        return new(set, rows);
    }

    // Purpose/current actor authorization belongs to the future caller; this private primitive never grants access.
    internal async Task<StoredDocumentUse?> FindAsync(DocumentOwner owner, DocumentUseReference source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireScopeAndOwner(owner);
        var result = await FindKeyAsync(source.Key, ct);
        return result is not null && result.Set.Reference() == source && result.Set.OwnerModule == owner.Module
            && result.Set.OwnerType == owner.Type && result.Set.OwnerId == owner.Id ? result : null;
    }

    private void RequireScopeAndOwner(DocumentOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(scope);
        if (owner.Id == Guid.Empty || string.IsNullOrWhiteSpace(owner.Module) || owner.Module.Length > 60
            || string.IsNullOrWhiteSpace(owner.Type) || owner.Type.Length > 80)
            throw new ArgumentException("An exact document owner is required.", nameof(owner));
        db.RequireDocumentUseScope(scope);
    }

    private async Task<StoredDocumentUse?> FindKeyAsync(Guid key, CancellationToken ct)
    {
        var set = db.UseSets.Local.SingleOrDefault(x => x.UseKey == key && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId)
            ?? await db.UseSets.AsNoTracking().SingleOrDefaultAsync(x => x.UseKey == key, ct);
        if (set is null) return null;
        var rows = await db.UsePins.AsNoTracking().Where(x => x.UseSetId == set.Id).ToListAsync(ct);
        rows.AddRange(db.UsePins.Local.Where(x => x.UseSetId == set.Id && db.Entry(x).State == EntityState.Added));
        if (rows.Count != set.SelectionCount || rows.Select(x => x.VersionId).Distinct().Count() != rows.Count
            || rows.Any(x => x.OwnerModule != set.OwnerModule || x.OwnerType != set.OwnerType || x.OwnerId != set.OwnerId
                || x.CreatedBy != set.CreatedBy || x.CreatedAt != set.CreatedAt)
            || DocumentUsePinRow.Digest(rows) != set.SelectionDigest)
            throw new InvalidOperationException("Retained document selection evidence is incomplete.");
        return new(set, rows.OrderBy(x => x.VersionId).ToArray());
    }
}
