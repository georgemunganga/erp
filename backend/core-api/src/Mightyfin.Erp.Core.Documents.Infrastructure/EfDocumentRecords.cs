using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

public sealed class EfDocumentRecords(DocumentDbContext db, IDocumentScope scope) : IDocumentRecords
{
    public async Task<RegisteredDocument> RegisterUploadAsync(CoreDocument document, CoreDocumentVersion version, int maxFiles, CancellationToken ct)
    {
        document.RequireScope(scope);
        if (string.IsNullOrWhiteSpace(document.UploadKey) || maxFiles is < 1 or > 100
            || version.DocumentId != document.Id || version.Number != 1 || version.ScanState != DocumentScanState.Quarantined)
            throw new ArgumentException("A new quarantined upload and bounded file limit are required.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsRelational())
        {
            // Stable owner lock serializes file-count and idempotency decisions across API instances.
            var input = JsonSerializer.Serialize(new[] { scope.TenantId, scope.LegalEntityId.ToString("N"), document.OwnerModule, document.OwnerType, document.OwnerId.ToString("N") });
            var key = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
        }
        var owner = new DocumentOwner(document.OwnerModule, document.OwnerType, document.OwnerId);
        var previous = await Owned(owner).SingleOrDefaultAsync(x => x.UploadKey == document.UploadKey, ct);
        if (previous is not null)
        {
            var original = await db.Versions.SingleAsync(x => x.DocumentId == previous.Id && x.Number == 1, ct);
            if (original.Sha256 != version.Sha256 || original.SizeBytes != version.SizeBytes
                || original.FileName != version.FileName || original.MediaType != version.MediaType)
                throw new DocumentProblemException("upload-key-conflict", "This upload was already used for a different file. Choose the file again.");
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new(previous, original, true);
        }
        if (await Owned(owner).CountAsync(x => !x.IsRetired, ct) >= maxFiles)
            throw new DocumentProblemException("document-limit", "This record has reached its document limit.");
        db.Documents.Add(document);
        db.Versions.Add(version);
        AddEvent(new(document, version), "UploadRegistered", null);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(document, version);
    }

    public async Task<RegisteredDocument?> FindAsync(DocumentOwner owner, Guid versionId, CancellationToken ct)
    {
        var row = await (from document in Owned(owner)
                         join version in db.Versions on document.Id equals version.DocumentId
                         where version.Id == versionId
                         select new { document, version }).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.document, row.version);
    }
    public async Task<IReadOnlyList<RegisteredDocument>> ListAsync(DocumentOwner owner, CancellationToken ct)
    {
        var rows = await (from document in Owned(owner).Where(x => !x.IsRetired)
                          join version in db.Versions on document.Id equals version.DocumentId
                          orderby document.CreatedAt descending, version.Number descending
                          select new { document, version }).ToListAsync(ct);
        return rows.Select(x => new RegisteredDocument(x.document, x.version)).ToList();
    }
    public async Task SaveStateAsync(RegisteredDocument record, string action, DocumentScanEvidence? evidence, string? failureCode, CancellationToken ct)
    {
        record.Document.RequireScope(scope);
        if (action is not ("ScanStarted" or "ScanFinished" or "DownloadAuthorized"))
            throw new ArgumentException("Unsupported document operation.");
        if (evidence is not null) db.ScanEvidence.Add(evidence);
        AddEvent(record, action, failureCode);
        await db.SaveChangesAsync(ct);
    }
    private IQueryable<CoreDocument> Owned(DocumentOwner owner) => db.Documents.Where(x =>
        x.OwnerModule == owner.Module && x.OwnerType == owner.Type && x.OwnerId == owner.Id);
    private void AddEvent(RegisteredDocument record, string action, string? failureCode) =>
        db.RecordEvents.Add(new(Guid.CreateVersion7(), scope.TenantId, scope.LegalEntityId,
            record.Document.Id, record.Version.Id, action, scope.SubjectId, DateTimeOffset.UtcNow, failureCode));
}
