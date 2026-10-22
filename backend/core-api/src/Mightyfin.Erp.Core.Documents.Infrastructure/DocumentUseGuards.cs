using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

public sealed partial class DocumentDbContext
{
    internal void RequireDocumentUseScope(IDocumentScope candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.TenantId) || string.IsNullOrWhiteSpace(candidate.SubjectId)
            || candidate.LegalEntityId == Guid.Empty || candidate.TenantId != scope.TenantId
            || candidate.LegalEntityId != scope.LegalEntityId || candidate.SubjectId != scope.SubjectId)
            throw new UnauthorizedAccessException("Document use scope must match the actual owning context.");
    }

    private async Task ValidateDocumentUsesAsync(bool synchronous, CancellationToken ct)
    {
        var sets = ChangeTracker.Entries<DocumentUseSetRow>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var pins = ChangeTracker.Entries<DocumentUsePinRow>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var retiring = ChangeTracker.Entries<CoreDocument>().Where(x => x.State == EntityState.Modified && x.Entity.IsRetired)
            .Select(x => x.Entity.Id).ToArray();
        // Relational managed retirement requires the paired owning writer/fence. InMemory retains
        // only same-context evidence guards; it cannot establish physical session protection.
        if (Database.IsRelational())
            foreach (var entry in ChangeTracker.Entries<CoreDocument>().Where(x=>x.State==EntityState.Modified).ToArray())
            {
                if (entry.Property(x=>x.IsRetired).OriginalValue || !entry.Entity.IsRetired)
                    throw new InvalidOperationException("Document retirement history cannot be rewritten or reversed.");
                await RequireManagedRetirementAsync(entry.Entity,ct);
            }
        if (retiring.Length > 0)
        {
            if (pins.Any(x => retiring.Contains(x.DocumentId)))
                throw new InvalidOperationException("A retained document cannot be retired.");
            var retained = UsePins.AsNoTracking().Where(x => retiring.Contains(x.DocumentId));
            if (synchronous ? retained.Any() : await retained.AnyAsync(ct))
                throw new InvalidOperationException("A retained document cannot be retired.");
        }
        if (sets.Length == 0 && pins.Length == 0) return;
        if (sets.Any(x => x.CreatedBy != scope.SubjectId) || pins.Any(x => x.CreatedBy != scope.SubjectId))
            throw new UnauthorizedAccessException("Document uses must identify the original acting subject.");
        if (pins.Any(x => !sets.Any(s => s.Id == x.UseSetId)))
            throw new InvalidOperationException("A use pin requires its complete new use set in the same save.");
        foreach (var set in sets)
        {
            var children = pins.Where(x => x.UseSetId == set.Id).ToArray();
            if (children.Length is < 1 or > 100 || children.Length != set.SelectionCount
                || children.Select(x => x.VersionId).Distinct().Count() != children.Length
                || children.Any(x => x.OwnerModule != set.OwnerModule || x.OwnerType != set.OwnerType || x.OwnerId != set.OwnerId
                    || x.TenantId != set.TenantId || x.LegalEntityId != set.LegalEntityId || x.CreatedAt != set.CreatedAt)
                || DocumentUsePinRow.Digest(children) != set.SelectionDigest)
                throw new InvalidOperationException("The complete original document selection set is required.");
        }
        var documentIds = pins.Select(x => x.DocumentId).Distinct().ToArray();
        var versionIds = pins.Select(x => x.VersionId).Distinct().ToArray();
        var scanIds = pins.Select(x => x.ScanEvidenceId).Distinct().ToArray();
        var docsQuery = Documents.Where(x => documentIds.Contains(x.Id));
        var versionsQuery = Versions.Where(x => versionIds.Contains(x.Id));
        var scansQuery = ScanEvidence.Where(x => scanIds.Contains(x.Id));
        var documents = (synchronous ? docsQuery.ToList() : await docsQuery.ToListAsync(ct))
            .Concat(Documents.Local.Where(x => Entry(x).State == EntityState.Added)).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var versions = (synchronous ? versionsQuery.ToList() : await versionsQuery.ToListAsync(ct))
            .Concat(Versions.Local.Where(x => Entry(x).State == EntityState.Added)).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var scans = (synchronous ? scansQuery.ToList() : await scansQuery.ToListAsync(ct))
            .Concat(ScanEvidence.Local.Where(x => Entry(x).State == EntityState.Added)).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        foreach (var row in pins)
        {
            if (!documents.TryGetValue(row.DocumentId, out var document) || !versions.TryGetValue(row.VersionId, out var version)
                || !scans.TryGetValue(row.ScanEvidenceId, out var scan))
                throw new InvalidOperationException("Owned document version and scan evidence are required.");
            var set = sets.Single(x => x.Id == row.UseSetId);
            // Stored PostgreSQL microseconds may truncate the same final scan instant; do not reject that exact boundary.
            var validationAt = row.CreatedAt < scan.FinishedAt && row.CreatedAt == DocumentUsePinRow.StoredInstant(scan.FinishedAt)
                ? scan.FinishedAt : row.CreatedAt;
            var derived = DocumentUsePin.Create(scope, set.Reference(), row.Selection(), document, version, scan, validationAt);
            var expected = DocumentUsePinRow.FromPin(set.Id, derived);
            if (DocumentUsePinRow.Digest([row]) != DocumentUsePinRow.Digest([expected]))
                throw new InvalidOperationException("Document use content must match the owning records.");
        }
    }
}
