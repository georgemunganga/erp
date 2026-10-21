using System.Security.Cryptography;
using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Application;

public sealed record DocumentOwner(string Module, string Type, Guid Id);
public enum DocumentAction { Read, Upload }
public interface IDocumentOwnerAccess
{
    Task RequireAsync(DocumentOwner owner, DocumentAction action, CancellationToken ct);
}
public sealed record RegisteredDocument(CoreDocument Document, CoreDocumentVersion Version, bool IsReplay = false);
public sealed record DocumentView(Guid DocumentId, Guid VersionId, int VersionNumber, string FileName,
    string MediaType, long SizeBytes, string Sha256, string ScanState, string? FailureCode, bool CanDownload);
public sealed record DocumentDownload(Stream Content, string FileName, string MediaType, long SizeBytes);
public sealed record DocumentLimits(long MaxFileBytes = 10 * 1024 * 1024, int MaxOwnerFiles = 10);
public sealed class DocumentProblemException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IDocumentRecords
{
    Task<RegisteredDocument> RegisterUploadAsync(CoreDocument document, CoreDocumentVersion version, int maxFiles, CancellationToken ct);
    Task<RegisteredDocument?> FindAsync(DocumentOwner owner, Guid versionId, CancellationToken ct);
    Task<IReadOnlyList<RegisteredDocument>> ListAsync(DocumentOwner owner, CancellationToken ct);
    Task SaveStateAsync(RegisteredDocument record, string action, DocumentScanEvidence? evidence, string? failureCode, CancellationToken ct);
}

/// <summary>Core owns document behavior; the registered module adapter authorizes the actual business record.</summary>
public sealed class RecordDocumentService(IDocumentScope scope, IDocumentOwnerAccess access,
    IDocumentRecords records, IDocumentBlobStore blobs, IDocumentScanner scanner,
    DocumentLimits limits, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    public async Task<DocumentView> UploadAsync(DocumentOwner owner, Guid uploadKey, string fileName,
        Stream content, CancellationToken ct = default)
    {
        await access.RequireAsync(owner, DocumentAction.Upload, ct);
        if (uploadKey == Guid.Empty) throw new DocumentProblemException("upload-key-required", "Try uploading the file again.");
        if (limits.MaxOwnerFiles is < 1 or > 100 || limits.MaxFileBytes is < 1 or > 100 * 1024 * 1024)
            throw new InvalidOperationException("Document limits are not configured correctly.");
        await using var staged = await blobs.StageAsync(content, limits.MaxFileBytes, ct);
        var document = CoreDocument.Create(scope, owner.Module, owner.Type, owner.Id, clock.GetUtcNow(), uploadKey: uploadKey);
        var stamp = staged.Content;
        var version = CoreDocumentVersion.Create(document, scope, 1, fileName, stamp.MediaType,
            stamp.SizeBytes, stamp.Sha256, clock.GetUtcNow(), limits.MaxFileBytes);
        var registered = await records.RegisterUploadAsync(document, version, limits.MaxOwnerFiles, ct);
        // Metadata commits first as Quarantined. A failed final file move is recoverable with the same upload key.
        await staged.CommitAsync(registered.Version.StorageKey, ct);
        return await ScanAuthorizedAsync(registered, ct);
    }

    public async Task<IReadOnlyList<DocumentView>> ListAsync(DocumentOwner owner, CancellationToken ct = default)
    {
        await access.RequireAsync(owner, DocumentAction.Read, ct);
        return (await records.ListAsync(owner, ct)).Select(View).ToList();
    }

    public async Task<DocumentView> RetryScanAsync(DocumentOwner owner, Guid versionId, CancellationToken ct = default)
    {
        await access.RequireAsync(owner, DocumentAction.Upload, ct);
        return await ScanAuthorizedAsync(await RequireRecord(owner, versionId, ct), ct);
    }

    public async Task<DocumentDownload> DownloadAsync(DocumentOwner owner, Guid versionId, CancellationToken ct = default)
    {
        await access.RequireAsync(owner, DocumentAction.Read, ct);
        var record = await RequireRecord(owner, versionId, ct);
        if (!record.Version.IsAvailableFor(record.Document))
            throw new DocumentProblemException("document-not-ready", "This file has not passed its security check.");
        var stream = await blobs.OpenPrivateReadAsync(record.Version.StorageKey, ct);
        try
        {
            // Verify immutable content before returning a stream to the HTTP layer.
            if (!stream.CanSeek || stream.Length != record.Version.SizeBytes ||
                !Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)).Equals(record.Version.Sha256, StringComparison.Ordinal))
                throw new DocumentProblemException("document-content-changed", "The file could not be verified. Contact support.");
            stream.Position = 0;
            await records.SaveStateAsync(record, "DownloadAuthorized", null, null, ct);
            return new DocumentDownload(stream, record.Version.FileName, record.Version.MediaType, record.Version.SizeBytes);
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    private async Task<RegisteredDocument> RequireRecord(DocumentOwner owner, Guid id, CancellationToken ct) =>
        await records.FindAsync(owner, id, ct)
        ?? throw new DocumentProblemException("document-not-found", "Document not found.");

    private async Task<DocumentView> ScanAuthorizedAsync(RegisteredDocument record, CancellationToken ct)
    {
        var version = record.Version;
        if (record.Document.IsRetired) throw new DocumentProblemException("document-retired", "This document has been retired.");
        if (version.ScanState is DocumentScanState.Available or DocumentScanState.Rejected) return View(record);
        var now = clock.GetUtcNow();
        if (version.ScanState == DocumentScanState.Scanning && version.ScanLeaseExpiresAt > now) return View(record);
        if (version.ScanAttempts >= 3) throw new DocumentProblemException("scan-retry-limit", "The security check needs support. No more retries are available.");
        var lease = Guid.NewGuid();
        version.BeginScan(lease, now, TimeSpan.FromMinutes(2));
        await records.SaveStateAsync(record, "ScanStarted", null, null, ct);
        DocumentScanResult result;
        try
        {
            await using var input = await blobs.OpenPrivateReadAsync(version.StorageKey, ct);
            result = await scanner.ScanAsync(input, version.SizeBytes, version.Sha256, ct);
        }
        catch (IOException) { result = new(ScannerVerdict.Failed, version.Sha256, "storage", null, "content-unavailable"); }
        // Caller cancellation leaves the durable lease for bounded recovery; it must not release content.
        var evidence = version.FinishScan(lease, result.Sha256, result.Verdict, result.Engine,
            result.Definitions, clock.GetUtcNow(), result.FailureCode);
        await records.SaveStateAsync(record, "ScanFinished", evidence, result.FailureCode, ct);
        return View(record);
    }

    private static DocumentView View(RegisteredDocument record) => new(record.Document.Id, record.Version.Id,
        record.Version.Number, record.Version.FileName, record.Version.MediaType, record.Version.SizeBytes,
        record.Version.Sha256, record.Version.ScanState.ToString(), record.Version.ScanFailureCode,
        record.Version.IsAvailableFor(record.Document));
}
