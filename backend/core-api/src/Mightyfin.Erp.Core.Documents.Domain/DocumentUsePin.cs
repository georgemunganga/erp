namespace Mightyfin.Erp.Core.Documents.Domain;

public enum DocumentUseKind { Review = 1, Publication = 2 }

/// <summary>A consuming revision's stable use key, not a document scan/approval assertion.</summary>
public sealed record DocumentUseReference(Guid Key, string Module, string RecordType,
    Guid RecordId, long Revision, DocumentUseKind Kind);

/// <summary>Expected stored identity/CAS supplied by the consumer; every field is checked against owner records.</summary>
public sealed record DocumentUseSelection(string OwnerModule, string OwnerType, Guid OwnerId,
    Guid DocumentId, Guid VersionId, long DocumentRevision, long VersionRevision, string Sha256);

/// <summary>
/// Immutable proposed use evidence. Construction validates supplied domain facts, not their persistence or caller authority.
/// Only a future owning-store adapter may load those facts, authorize the purpose and persist a pin atomically.
/// This primitive alone does not lock records, prevent retirement, verify blob presence or enable publication.
/// </summary>
public sealed class DocumentUsePin
{
    private DocumentUsePin() { }
    public Guid Id { get; private init; }
    public string TenantId { get; private init; } = null!;
    public Guid LegalEntityId { get; private init; }
    public DocumentUseReference Source { get; private init; } = null!;
    public string OwnerModule { get; private init; } = null!;
    public string OwnerType { get; private init; } = null!;
    public Guid OwnerId { get; private init; }
    public Guid DocumentId { get; private init; }
    public Guid VersionId { get; private init; }
    public int VersionNumber { get; private init; }
    public long DocumentRevision { get; private init; }
    public long VersionRevision { get; private init; }
    public string FileName { get; private init; } = null!;
    public string MediaType { get; private init; } = null!;
    public long SizeBytes { get; private init; }
    public string Sha256 { get; private init; } = null!;
    public DocumentScanEvidence Scan { get; private init; } = null!;
    public string CreatedBy { get; private init; } = null!;
    public DateTimeOffset CreatedAt { get; private init; }

    public static DocumentUsePin Create(IDocumentScope scope, DocumentUseReference source,
        DocumentUseSelection selection, CoreDocument document, CoreDocumentVersion version,
        DocumentScanEvidence scan, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(scan);
        document.RequireScope(scope);
        if (source.Key == Guid.Empty || source.RecordId == Guid.Empty || source.Revision < 1 || !Enum.IsDefined(source.Kind))
            throw new ArgumentException("A stable use key, consuming revision and supported purpose are required.");
        var normalizedSource = source with
        {
            Module = DocumentGuards.Token(source.Module, 60), RecordType = DocumentGuards.Token(source.RecordType, 80),
        };
        if (source != normalizedSource) throw new ArgumentException("Use identifiers must be canonical.");
        if (document.OwnerModule != selection.OwnerModule || document.OwnerType != selection.OwnerType
            || document.OwnerId != selection.OwnerId || document.Id != selection.DocumentId || version.Id != selection.VersionId)
            throw new InvalidOperationException("The selected document does not belong to the expected owner/version.");
        if (selection.DocumentRevision != document.Version || selection.VersionRevision != version.Version
            || !string.Equals(selection.Sha256, version.Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException("The selected document version has changed.");
        if (!version.IsAvailableFor(document) || version.ScanLeaseId is not null || version.ScanLeaseExpiresAt is not null)
            throw new InvalidOperationException("A nonretired, available document version is required.");
        if (scan.Id == Guid.Empty || scan.TenantId != document.TenantId || scan.LegalEntityId != document.LegalEntityId
            || scan.VersionId != version.Id || scan.Attempt != version.ScanAttempts || scan.Attempt < 1
            || scan.Verdict != ScannerVerdict.Clean || scan.Sha256 != version.Sha256 || scan.FailureCode is not null
            || string.IsNullOrWhiteSpace(scan.Engine) || scan.Engine.Length > 120
            || string.IsNullOrWhiteSpace(scan.Definitions) || scan.Definitions.Length > 120
            || scan.StartedAt != version.ScanStartedAt || scan.StartedAt < version.CreatedAt
            || scan.FinishedAt < scan.StartedAt || scan.FinishedAt > at || document.CreatedAt > at)
            throw new InvalidOperationException("Matching completed clean scan evidence is required.");
        return new DocumentUsePin
        {
            Id = Guid.CreateVersion7(), TenantId = document.TenantId, LegalEntityId = document.LegalEntityId,
            Source = normalizedSource, OwnerModule = document.OwnerModule, OwnerType = document.OwnerType, OwnerId = document.OwnerId,
            DocumentId = document.Id, VersionId = version.Id, VersionNumber = version.Number,
            DocumentRevision = document.Version, VersionRevision = version.Version,
            FileName = version.FileName, MediaType = version.MediaType, SizeBytes = version.SizeBytes, Sha256 = version.Sha256,
            Scan = scan, CreatedBy = scope.SubjectId, CreatedAt = at,
        };
    }

    /// <summary>
    /// Validate an owner-loaded historical snapshot. This establishes internal evidence consistency,
    /// never persistence, current permission, current availability or a new document use.
    /// The owning adapter must additionally verify the complete stored set/digest and scope.
    /// </summary>
    public static DocumentUsePin Restore(Guid id, string tenantId, Guid legalEntityId,
        DocumentUseReference source, DocumentUseSelection selection, int versionNumber,
        string fileName, string mediaType, long sizeBytes, DocumentScanEvidence scan,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(scan);
        static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value)
            && value == value.Trim() && value.Length <= max && !value.Any(char.IsControl);
        static bool Token(string? value, int max) => Text(value, max)
            && value!.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');
        static bool Instant(DateTimeOffset value) => value.Offset == TimeSpan.Zero
            && value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue && value.UtcTicks % 10 == 0;
        if (id == Guid.Empty || !Text(tenantId,150) || legalEntityId == Guid.Empty || !Text(createdBy,150)
            || !Instant(createdAt) || source.Key == Guid.Empty || source.RecordId == Guid.Empty
            || source.Revision < 1 || !Enum.IsDefined(source.Kind) || !Token(source.Module,60) || !Token(source.RecordType,80)
            || !Token(selection.OwnerModule,60) || !Token(selection.OwnerType,80) || selection.OwnerId == Guid.Empty
            || selection.DocumentId == Guid.Empty || selection.VersionId == Guid.Empty
            || selection.DocumentRevision < 1 || selection.VersionRevision < 1 || versionNumber < 1
            || !Text(fileName,255) || fileName.IndexOfAny(['/', '\\', ':']) >= 0
            || sizeBytes is < 1 or > 100 * 1024 * 1024
            || selection.Sha256 is null || selection.Sha256.Length != 64
            || selection.Sha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidOperationException("Retained document identity is malformed.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!(mediaType == "application/pdf" && extension == ".pdf"
            || mediaType == "image/png" && extension == ".png"
            || mediaType == "image/jpeg" && extension is ".jpg" or ".jpeg")
            || scan.Id == Guid.Empty || scan.TenantId != tenantId || scan.LegalEntityId != legalEntityId
            || scan.VersionId != selection.VersionId || scan.Attempt < 1 || scan.Verdict != ScannerVerdict.Clean
            || scan.Sha256 != selection.Sha256 || scan.FailureCode is not null
            || !Text(scan.Engine,120) || !Text(scan.Definitions,120)
            || !Instant(scan.StartedAt) || !Instant(scan.FinishedAt)
            || scan.StartedAt > scan.FinishedAt || scan.FinishedAt > createdAt)
            throw new InvalidOperationException("Retained completed scan evidence is malformed.");
        return new DocumentUsePin
        {
            Id=id, TenantId=tenantId, LegalEntityId=legalEntityId, Source=source,
            OwnerModule=selection.OwnerModule, OwnerType=selection.OwnerType, OwnerId=selection.OwnerId,
            DocumentId=selection.DocumentId, VersionId=selection.VersionId, VersionNumber=versionNumber,
            DocumentRevision=selection.DocumentRevision, VersionRevision=selection.VersionRevision,
            FileName=fileName, MediaType=mediaType, SizeBytes=sizeBytes, Sha256=selection.Sha256,
            Scan=scan, CreatedBy=createdBy, CreatedAt=createdAt,
        };
    }

    /// <summary>
    /// Resolve an independently reauthorized duplicate of this single reference. Returns the original evidence, including time/ID.
    /// Full-set equality (no extra/missing selected versions) and original-key recovery belong to the future atomic store port.
    /// Historical recovery must read the stored pin; it must not rebuild it through current availability checks.
    /// </summary>
    public DocumentUsePin ResolveExactDuplicate(DocumentUsePin candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (TenantId != candidate.TenantId || LegalEntityId != candidate.LegalEntityId
            || Source != candidate.Source || OwnerModule != candidate.OwnerModule || OwnerType != candidate.OwnerType
            || OwnerId != candidate.OwnerId || DocumentId != candidate.DocumentId || VersionId != candidate.VersionId
            || VersionNumber != candidate.VersionNumber || DocumentRevision != candidate.DocumentRevision
            || VersionRevision != candidate.VersionRevision || FileName != candidate.FileName || MediaType != candidate.MediaType
            || SizeBytes != candidate.SizeBytes || Sha256 != candidate.Sha256 || Scan != candidate.Scan || CreatedBy != candidate.CreatedBy)
            throw new InvalidOperationException("This document use reference already describes different evidence.");
        return this;
    }
}
