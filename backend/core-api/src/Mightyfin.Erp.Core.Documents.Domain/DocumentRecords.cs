namespace Mightyfin.Erp.Core.Documents.Domain;

public interface IDocumentScope
{
    string TenantId { get; }
    Guid LegalEntityId { get; }
    string SubjectId { get; }
}

public enum DocumentScanState { Quarantined, Scanning, Available, Rejected, ScanFailed }
public enum ScannerVerdict { Failed, Clean, Infected }

public sealed class CoreDocument
{
    private CoreDocument() { }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string TenantId { get; private set; } = null!;
    public Guid LegalEntityId { get; private set; }
    public string OwnerModule { get; private set; } = null!;
    public string OwnerType { get; private set; } = null!;
    public Guid OwnerId { get; private set; }
    public string Classification { get; private set; } = "confidential";
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRetired { get; private set; }
    public string? RetiredBy { get; private set; }
    public string? RetirementReason { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public bool LegalHold { get; private set; }
    public long Version { get; private set; } = 1;

    public static CoreDocument Create(IDocumentScope scope, string module, string ownerType,
        Guid ownerId, DateTimeOffset now, string classification = "confidential")
    {
        DocumentGuards.Scope(scope);
        if (ownerId == Guid.Empty) throw new ArgumentException("An owning record is required.");
        if (classification is not ("public" or "internal" or "confidential" or "restricted"))
            throw new ArgumentException("Choose a supported document classification.");
        return new CoreDocument
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            OwnerModule = DocumentGuards.Token(module, 60), OwnerType = DocumentGuards.Token(ownerType, 80),
            OwnerId = ownerId, CreatedBy = scope.SubjectId, CreatedAt = now, Classification = classification,
        };
    }

    public void RequireScope(IDocumentScope scope)
    {
        DocumentGuards.Scope(scope);
        if (scope.TenantId != TenantId || scope.LegalEntityId != LegalEntityId)
            throw new UnauthorizedAccessException("Document belongs to another organization.");
    }

    public void Retire(IDocumentScope scope, string reason, DateTimeOffset now)
    {
        RequireScope(scope);
        if (IsRetired) throw new InvalidOperationException("Document is already retired.");
        RetirementReason = DocumentGuards.Text(reason, 1000);
        IsRetired = true;
        RetiredBy = scope.SubjectId;
        RetiredAt = now;
        Version++;
    }
}

/// <summary>Content metadata is immutable; scanning changes only controlled lifecycle fields.</summary>
public sealed class CoreDocumentVersion
{
    private CoreDocumentVersion() { }
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string TenantId { get; private set; } = null!;
    public Guid LegalEntityId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int Number { get; private set; }
    public string FileName { get; private set; } = null!;
    public string MediaType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public string CreatedBy { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DocumentScanState ScanState { get; private set; } = DocumentScanState.Quarantined;
    public int ScanAttempts { get; private set; }
    public Guid? ScanLeaseId { get; private set; }
    public DateTimeOffset? ScanStartedAt { get; private set; }
    public DateTimeOffset? ScanLeaseExpiresAt { get; private set; }
    public long Version { get; private set; } = 1;

    public static CoreDocumentVersion Create(CoreDocument document, IDocumentScope scope, int number,
        string fileName, string mediaType, long sizeBytes, string sha256, DateTimeOffset now,
        long maxFileBytes = 10 * 1024 * 1024)
    {
        document.RequireScope(scope);
        if (document.IsRetired) throw new InvalidOperationException("Document is retired.");
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        if (maxFileBytes is < 1 or > 100 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        if (sizeBytes < 1 || sizeBytes > maxFileBytes) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        fileName = DocumentGuards.Text(fileName, 255);
        if (fileName.IndexOfAny(['/', '\\']) >= 0 || fileName.Any(char.IsControl))
            throw new ArgumentException("Use a file name without a path or control characters.");
        if (mediaType is not ("application/pdf" or "image/png" or "image/jpeg"))
            throw new ArgumentException("Unsupported file type.");
        if (sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("A SHA-256 content hash is required.");
        var version = new CoreDocumentVersion
        {
            TenantId = scope.TenantId, LegalEntityId = scope.LegalEntityId,
            DocumentId = document.Id, Number = number, FileName = fileName,
            MediaType = mediaType, SizeBytes = sizeBytes, Sha256 = sha256.ToLowerInvariant(),
            CreatedBy = scope.SubjectId, CreatedAt = now,
        };
        version.StorageKey = $"{document.Id:N}/{version.Id:N}";
        return version;
    }

    public void BeginScan(Guid leaseId, DateTimeOffset now, TimeSpan leaseDuration, int maxAttempts = 3)
    {
        if (leaseId == Guid.Empty || leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(10))
            throw new ArgumentException("A bounded scan lease is required.");
        if (maxAttempts is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        if (ScanAttempts >= maxAttempts) throw new InvalidOperationException("Scan retry limit reached.");
        if (now < CreatedAt) throw new InvalidOperationException("Scan cannot predate the upload.");
        var expired = ScanState == DocumentScanState.Scanning && ScanLeaseExpiresAt <= now;
        if (ScanState is not (DocumentScanState.Quarantined or DocumentScanState.ScanFailed) && !expired)
            throw new InvalidOperationException("This version cannot start another scan.");
        ScanState = DocumentScanState.Scanning;
        ScanLeaseId = leaseId;
        ScanStartedAt = now;
        ScanLeaseExpiresAt = now + leaseDuration;
        ScanAttempts++;
        Version++;
    }

    public DocumentScanEvidence FinishScan(Guid leaseId, string scannedSha256, ScannerVerdict verdict,
        string engine, string? definitions, DateTimeOffset now)
    {
        if (leaseId == Guid.Empty || ScanState != DocumentScanState.Scanning || ScanLeaseId != leaseId)
            throw new InvalidOperationException("The scan lease is unavailable or expired.");
        if (ScanLeaseExpiresAt is not { } expires || ScanStartedAt is not { } started
            || expires <= now || started > now)
            throw new InvalidOperationException("The scan lease is unavailable or expired.");
        if (!string.Equals(Sha256, scannedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The scan result does not describe this content.");
        if (!Enum.IsDefined(verdict)) throw new ArgumentException("Unknown scan verdict.");
        engine = DocumentGuards.Text(engine, 120);
        definitions = string.IsNullOrWhiteSpace(definitions) ? null : DocumentGuards.Text(definitions, 120);
        if (verdict == ScannerVerdict.Clean && definitions is null)
            throw new InvalidOperationException("A clean result requires scanner definition evidence.");
        var evidence = new DocumentScanEvidence(Guid.CreateVersion7(), TenantId, LegalEntityId,
            Id, ScanAttempts, verdict, Sha256, engine, definitions, started, now);
        ScanState = verdict switch
        {
            ScannerVerdict.Clean => DocumentScanState.Available,
            ScannerVerdict.Infected => DocumentScanState.Rejected,
            _ => DocumentScanState.ScanFailed,
        };
        ScanLeaseId = null;
        ScanLeaseExpiresAt = null;
        Version++;
        return evidence;
    }

    // Availability only; the module adapter must authorize the owning record for the reader.
    public bool IsAvailableFor(CoreDocument document) => document.Id == DocumentId
        && document.TenantId == TenantId && document.LegalEntityId == LegalEntityId
        && !document.IsRetired && ScanState == DocumentScanState.Available;
}

public sealed record DocumentScanEvidence(Guid Id, string TenantId, Guid LegalEntityId,
    Guid VersionId, int Attempt, ScannerVerdict Verdict, string Sha256, string Engine,
    string? Definitions, DateTimeOffset StartedAt, DateTimeOffset FinishedAt);

internal static class DocumentGuards
{
    internal static void Scope(IDocumentScope scope)
    {
        Text(scope.TenantId, 150);
        Text(scope.SubjectId, 150);
        if (scope.LegalEntityId == Guid.Empty) throw new UnauthorizedAccessException("Organization is required.");
    }
    internal static string Text(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength
            ? value.Trim() : throw new ArgumentException("A bounded value is required.");
    internal static string Token(string value, int maxLength)
    {
        value = Text(value, maxLength);
        if (!value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
            throw new ArgumentException("A stable owner identifier is required.");
        return value;
    }
}
