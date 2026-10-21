using System.Security.Cryptography;
using System.Text.Json;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Infrastructure;

/// <summary>Flat immutable persistence representation; not an operational retention lease or authorization result.</summary>
public sealed class DocumentUseSetRow
{
    private DocumentUseSetRow() { }
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = null!;
    public Guid LegalEntityId { get; private set; }
    public Guid UseKey { get; private set; }
    public string SourceModule { get; private set; } = null!;
    public string SourceRecordType { get; private set; } = null!;
    public Guid SourceRecordId { get; private set; }
    public long SourceRevision { get; private set; }
    public DocumentUseKind Kind { get; private set; }
    public string OwnerModule { get; private set; } = null!;
    public string OwnerType { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public int SelectionCount { get; private set; }
    public string SelectionDigest { get; private set; } = null!;
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    internal static DocumentUseSetRow FromPins(IReadOnlyList<DocumentUsePin> pins)
    {
        if (pins.Count is < 1 or > 100 || pins.Select(x => x.VersionId).Distinct().Count() != pins.Count)
            throw new InvalidOperationException("A bounded exact set of distinct document versions is required.");
        var first = pins[0];
        if (pins.Any(x => x.TenantId != first.TenantId || x.LegalEntityId != first.LegalEntityId || x.Source != first.Source
            || x.OwnerModule != first.OwnerModule || x.OwnerType != first.OwnerType || x.OwnerId != first.OwnerId
            || x.CreatedBy != first.CreatedBy || x.CreatedAt != first.CreatedAt))
            throw new InvalidOperationException("All pins must describe one owner, use reference and original actor/time.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = first.TenantId, LegalEntityId = first.LegalEntityId,
            UseKey = first.Source.Key, SourceModule = first.Source.Module, SourceRecordType = first.Source.RecordType,
            SourceRecordId = first.Source.RecordId, SourceRevision = first.Source.Revision, Kind = first.Source.Kind,
            OwnerModule = first.OwnerModule, OwnerType = first.OwnerType, OwnerId = first.OwnerId,
            SelectionCount = pins.Count, SelectionDigest = DocumentUsePinRow.Digest(pins.Select(x => DocumentUsePinRow.FromPin(Guid.Empty, x))),
            CreatedBy = first.CreatedBy, CreatedAt = DocumentUsePinRow.StoredInstant(first.CreatedAt),
        };
    }
    internal DocumentUseReference Reference() => new(UseKey, SourceModule, SourceRecordType, SourceRecordId, SourceRevision, Kind);
}

/// <summary>Snapshots scalar evidence; Scan is NOT mapped as another owned DocumentScanEvidence entity.</summary>
public sealed class DocumentUsePinRow
{
    private DocumentUsePinRow() { }
    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = null!;
    public Guid LegalEntityId { get; private set; }
    public Guid UseSetId { get; private set; }
    public string OwnerModule { get; private set; } = null!;
    public string OwnerType { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid VersionId { get; private set; }
    public Guid ScanEvidenceId { get; private set; }
    public int VersionNumber { get; private set; }
    public long DocumentRevision { get; private set; }
    public long VersionRevision { get; private set; }
    public string FileName { get; private set; } = null!;
    public string MediaType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public int ScanAttempt { get; private set; }
    public ScannerVerdict ScanVerdict { get; private set; }
    public string ScanEngine { get; private set; } = null!;
    public string ScanDefinitions { get; private set; } = null!;
    public DateTimeOffset ScanStartedAt { get; private set; }
    public DateTimeOffset ScanFinishedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    internal static DocumentUsePinRow FromPin(Guid setId, DocumentUsePin pin) => new()
    {
        Id = pin.Id, TenantId = pin.TenantId, LegalEntityId = pin.LegalEntityId, UseSetId = setId,
        OwnerModule = pin.OwnerModule, OwnerType = pin.OwnerType, OwnerId = pin.OwnerId,
        DocumentId = pin.DocumentId, VersionId = pin.VersionId, ScanEvidenceId = pin.Scan.Id,
        VersionNumber = pin.VersionNumber, DocumentRevision = pin.DocumentRevision, VersionRevision = pin.VersionRevision,
        FileName = pin.FileName, MediaType = pin.MediaType, SizeBytes = pin.SizeBytes, Sha256 = pin.Sha256,
        ScanAttempt = pin.Scan.Attempt, ScanVerdict = pin.Scan.Verdict, ScanEngine = pin.Scan.Engine,
        ScanDefinitions = pin.Scan.Definitions!, ScanStartedAt = StoredInstant(pin.Scan.StartedAt), ScanFinishedAt = StoredInstant(pin.Scan.FinishedAt),
        CreatedBy = pin.CreatedBy, CreatedAt = StoredInstant(pin.CreatedAt),
    };
    // PostgreSQL timestamp precision: canonical UTC microseconds, avoiding digest changes on storage round-trip.
    internal static DateTimeOffset StoredInstant(DateTimeOffset at) => new(at.UtcTicks - at.UtcTicks % 10, TimeSpan.Zero);
    internal DocumentUseSelection Selection() => new(OwnerModule, OwnerType, OwnerId, DocumentId, VersionId,
        DocumentRevision, VersionRevision, Sha256);
    // Deliberately excludes generated row IDs/set FK and wall-clock creation time; all original selection facts are included.
    internal static string Digest(IEnumerable<DocumentUsePinRow> rows) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(rows.OrderBy(x => x.VersionId).Select(x => new
        {
            x.OwnerModule, x.OwnerType, x.OwnerId, x.DocumentId, x.VersionId, x.ScanEvidenceId,
            x.VersionNumber, x.DocumentRevision, x.VersionRevision, x.FileName, x.MediaType, x.SizeBytes, x.Sha256,
            x.ScanAttempt, x.ScanVerdict, x.ScanEngine, x.ScanDefinitions, x.ScanStartedAt, x.ScanFinishedAt, x.CreatedBy,
        }))));
}

/// <summary>Owner-loaded original flat evidence, never reconstructed from current scan/retirement status.</summary>
internal sealed record StoredDocumentUse(DocumentUseSetRow Set, IReadOnlyList<DocumentUsePinRow> Pins);
