using System.Security.Cryptography;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Hrm.Api;
namespace Mightyfin.Erp.Hrm.Tests;
public sealed class InvoiceUnitReviewDocumentPurposeTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
    private sealed class Access : IDocumentOwnerAccess
    {
        public Task RequireAsync(DocumentOwner owner, DocumentAction action, CancellationToken ct)
        { if (action != DocumentAction.Read) throw new UnauthorizedAccessException(); return Task.CompletedTask; }
    }
    private sealed class Records(RegisteredDocument record) : IDocumentRecords
    {
        public int Downloads;
        public Task<RegisteredDocument> RegisterUploadAsync(CoreDocument document, CoreDocumentVersion version, int maxFiles, CancellationToken ct) => throw new UnauthorizedAccessException();
        public Task<RegisteredDocument?> FindAsync(DocumentOwner owner, Guid versionId, CancellationToken ct)
            => Task.FromResult<RegisteredDocument?>(owner.Id == record.Document.OwnerId && owner.Module == record.Document.OwnerModule && owner.Type == record.Document.OwnerType && versionId == record.Version.Id ? record : null);
        public Task<IReadOnlyList<RegisteredDocument>> ListAsync(DocumentOwner owner, CancellationToken ct) => throw new InvalidOperationException("GenericListMustNotBeCalled");
        public Task SaveStateAsync(RegisteredDocument value, string action, DocumentScanEvidence? evidence, string? failureCode, CancellationToken ct)
        { Assert.Same(record, value); Assert.Equal("DownloadAuthorized", action); Assert.Null(evidence); Assert.Null(failureCode); Downloads++; return Task.CompletedTask; }
    }
    [Fact]
    public async Task Pinned_original_download_keeps_actual_Core_hash_check_and_required_download_audit()
    {
        var scope = new Scope("unit-doc-test", Guid.NewGuid(), "assigned-reviewer"); var invoice = Guid.NewGuid();
        byte[] bytes = "%PDF-1.7\nSynthetic pinned review document"u8.ToArray();
        var now = DateTimeOffset.UtcNow; var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var owner = new DocumentOwner("procurement", "supplier_invoice", invoice);
        var doc = CoreDocument.Create(scope, owner.Module, owner.Type, invoice, now);
        var version = CoreDocumentVersion.Create(doc, scope, 1, "original.pdf", "application/pdf", bytes.Length, sha, now);
        var lease = Guid.NewGuid(); version.BeginScan(lease, now, TimeSpan.FromMinutes(1));
        _ = version.FinishScan(lease, sha, ScannerVerdict.Clean, "SyntheticTestOnly", "known-bytes", now);
        var records = new Records(new(doc, version));
        var pinned = new ProcurementInvoiceUnitReviewRoutes.PinnedRecords(records, invoice, new Dictionary<Guid,string> { [version.Id] = sha });
        var root = Path.Combine(Path.GetTempPath(), "invoice-unit-purpose-" + Guid.NewGuid().ToString("N"));
        try
        {
            var blobs = new PrivateDocumentStore(root);
            await using (var staged = await blobs.StageAsync(new MemoryStream(bytes), 1024, default)) await staged.CommitAsync(version.StorageKey, default);
            var service = new RecordDocumentService(scope, new Access(), pinned, blobs, new NeverScanner(), new());
            var result = await service.DownloadAsync(owner, version.Id);
            await using (result.Content) { using var copy = new MemoryStream(); await result.Content.CopyToAsync(copy); Assert.Equal(bytes, copy.ToArray()); }
            Assert.Equal(1, records.Downloads);
            var missing = await Assert.ThrowsAsync<DocumentProblemException>(() => service.DownloadAsync(owner, Guid.NewGuid()));
            Assert.Equal("document-not-found", missing.Code);
            Assert.Equal(1, records.Downloads);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => pinned.SaveStateAsync(new(doc, version), "ScanFinished", null, null, default));
            Assert.Null(await pinned.FindAsync(owner with { Id = Guid.NewGuid() }, version.Id, default));
            Assert.Single(await pinned.ListAsync(owner, default));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private sealed class NeverScanner : IDocumentScanner
    {
        public Task<DocumentScanResult> ScanAsync(Stream content, long expectedBytes, string expectedSha256, CancellationToken ct) => throw new InvalidOperationException("DownloadsDoNotScan");
    }
}
