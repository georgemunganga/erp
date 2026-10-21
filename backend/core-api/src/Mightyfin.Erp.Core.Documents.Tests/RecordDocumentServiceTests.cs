using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;

namespace Mightyfin.Erp.Core.Documents.Tests;

public sealed class RecordDocumentServiceTests : IAsyncDisposable
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
    private readonly Scope scope = new("tenant-a", Guid.NewGuid(), "ap-user");
    private readonly string root = Path.Combine(Path.GetTempPath(), "erp-doc-usecase-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentOwner owner = new("procurement", "supplier_invoice", Guid.NewGuid());
    private readonly DbContextOptions<DocumentDbContext> options = new DbContextOptionsBuilder<DocumentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static readonly byte[] Content = "%PDF-1.7\nOriginal invoice evidence"u8.ToArray();
    private readonly Access access = new();
    private readonly Scanner scanner = new();
    private RecordDocumentService Service(DocumentDbContext db, IDocumentScope? actor = null, IDocumentBlobStore? store = null) => new(actor ?? scope,
        access, new EfDocumentRecords(db, actor ?? scope), store ?? new PrivateDocumentStore(root), scanner, new());

    [Fact]
    public async Task Upload_retry_preserves_exact_version_and_conflicting_content_is_rejected()
    {
        var key = Guid.NewGuid();
        await using var db = new DocumentDbContext(options, scope);
        var service = Service(db);
        var first = await service.UploadAsync(owner, key, "invoice.pdf", new MemoryStream(Content));
        var retry = await service.UploadAsync(owner, key, "invoice.pdf", new MemoryStream(Content));
        Assert.Equal(first.VersionId, retry.VersionId);
        Assert.True(retry.CanDownload);
        Assert.Equal(1, scanner.Calls);
        Assert.Single(await db.Documents.ToListAsync());
        Assert.Single(await db.Versions.ToListAsync());
        var conflict = await Assert.ThrowsAsync<DocumentProblemException>(() => service.UploadAsync(owner,
            key, "invoice.pdf", new MemoryStream("%PDF-different"u8.ToArray())));
        Assert.Equal("upload-key-conflict", conflict.Code);
        Assert.Empty(Directory.GetFiles(root, ".upload-*", SearchOption.AllDirectories));
        var download = await service.DownloadAsync(owner, first.VersionId);
        await using (download.Content)
        {
            using var bytes = new MemoryStream();
            await download.Content.CopyToAsync(bytes);
            Assert.Equal(Content, bytes.ToArray());
        }
        Assert.Contains(await db.RecordEvents.ToListAsync(), x => x.Action == "DownloadAuthorized" && x.ActorSubjectId == scope.SubjectId);
    }

    [Theory]
    [InlineData(ScannerVerdict.Infected)]
    [InlineData(ScannerVerdict.Failed)]
    public async Task Rejected_or_failed_scan_never_releases_a_download(ScannerVerdict verdict)
    {
        scanner.Verdict = verdict;
        await using var db = new DocumentDbContext(options, scope);
        var service = Service(db);
        var uploaded = await service.UploadAsync(owner, Guid.NewGuid(), "invoice.pdf", new MemoryStream(Content));
        Assert.False(uploaded.CanDownload);
        var failure = await Assert.ThrowsAsync<DocumentProblemException>(() => service.DownloadAsync(owner, uploaded.VersionId));
        Assert.Equal("document-not-ready", failure.Code);
        Assert.Single(await db.ScanEvidence.ToListAsync());
    }

    [Fact]
    public async Task Failed_scan_can_retry_but_rejected_content_remains_blocked()
    {
        scanner.Verdict = ScannerVerdict.Failed;
        await using var db = new DocumentDbContext(options, scope);
        var service = Service(db);
        var uploaded = await service.UploadAsync(owner, Guid.NewGuid(), "invoice.pdf", new MemoryStream(Content));
        Assert.Equal("scanner-unavailable", uploaded.FailureCode);
        scanner.Verdict = ScannerVerdict.Clean;
        var retried = await service.RetryScanAsync(owner, uploaded.VersionId);
        Assert.True(retried.CanDownload);
        Assert.Equal(2, await db.ScanEvidence.CountAsync());
        Assert.Null(retried.FailureCode);
    }

    [Fact]
    public async Task Owner_access_is_checked_before_any_file_write_and_for_every_download()
    {
        await using var db = new DocumentDbContext(options, scope);
        var service = Service(db);
        access.Deny = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(owner, Guid.NewGuid(), "invoice.pdf", new MemoryStream(Content)));
        Assert.Empty(await db.Documents.ToListAsync());
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        access.Deny = false;
        var saved = await service.UploadAsync(owner, Guid.NewGuid(), "invoice.pdf", new MemoryStream(Content));
        access.Deny = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DownloadAsync(owner, saved.VersionId));
    }

    [Fact]
    public async Task Wrong_owner_and_other_company_cannot_resolve_a_guessed_version()
    {
        await using var db = new DocumentDbContext(options, scope);
        var saved = await Service(db).UploadAsync(owner, Guid.NewGuid(), "invoice.pdf", new MemoryStream(Content));
        var wrongOwner = await Assert.ThrowsAsync<DocumentProblemException>(() => Service(db).DownloadAsync(owner with { Id = Guid.NewGuid() }, saved.VersionId));
        Assert.Equal("document-not-found", wrongOwner.Code);
        var foreign = scope with { LegalEntityId = Guid.NewGuid() };
        await using var other = new DocumentDbContext(options, foreign);
        var failure = await Assert.ThrowsAsync<DocumentProblemException>(() => Service(other, foreign).DownloadAsync(owner, saved.VersionId));
        Assert.Equal("document-not-found", failure.Code);
    }

    [Fact]
    public async Task Mutated_blob_is_denied_even_after_a_clean_scan()
    {
        await using var db = new DocumentDbContext(options, scope);
        var service = Service(db);
        var saved = await service.UploadAsync(owner, Guid.NewGuid(), "invoice.pdf", new MemoryStream(Content));
        var version = await db.Versions.SingleAsync();
        await File.WriteAllBytesAsync(Path.Combine(root, version.StorageKey), new byte[Content.Length]);
        var failure = await Assert.ThrowsAsync<DocumentProblemException>(() => service.DownloadAsync(owner, saved.VersionId));
        Assert.Equal("document-content-changed", failure.Code);
    }

    [Fact]
    public async Task Metadata_commit_followed_by_file_failure_recovers_with_same_upload_key()
    {
        await using var db = new DocumentDbContext(options, scope);
        var store = new FailFirstCommit(new PrivateDocumentStore(root));
        var service = Service(db, store: store);
        var key = Guid.NewGuid();
        await Assert.ThrowsAsync<IOException>(() => service.UploadAsync(owner, key, "invoice.pdf", new MemoryStream(Content)));
        var original = await db.Versions.SingleAsync();
        Assert.Equal(DocumentScanState.Quarantined, original.ScanState);
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
        var recovered = await service.UploadAsync(owner, key, "invoice.pdf", new MemoryStream(Content));
        Assert.Equal(original.Id, recovered.VersionId);
        Assert.True(recovered.CanDownload);
        Assert.Equal(1, await db.Documents.CountAsync());
        Assert.Equal(1, await db.Versions.CountAsync());
        var evidence = await db.RecordEvents.FirstAsync();
        db.Entry(evidence).Property(x => x.Action).CurrentValue = "Rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private sealed class FailFirstCommit(IDocumentBlobStore inner) : IDocumentBlobStore
    {
        private int remainingFailures = 1;
        public async Task<StagedDocumentContent> StageAsync(Stream source, long maxFileBytes, CancellationToken cancellationToken = default)
            => new Stage(this, await inner.StageAsync(source, maxFileBytes, cancellationToken));
        public Task<StoredDocumentContent> StoreQuarantinedAsync(string key, Stream source, long max, CancellationToken cancellationToken = default)
            => inner.StoreQuarantinedAsync(key, source, max, cancellationToken);
        public Task<Stream> OpenPrivateReadAsync(string key, CancellationToken cancellationToken = default)
            => inner.OpenPrivateReadAsync(key, cancellationToken);
        private sealed class Stage(FailFirstCommit owner, StagedDocumentContent inner) : StagedDocumentContent(inner.Content)
        {
            public override Task CommitAsync(string key, CancellationToken cancellationToken = default)
            {
                if (Interlocked.Exchange(ref owner.remainingFailures, 0) == 1) throw new IOException("Simulated final move failure");
                return inner.CommitAsync(key, cancellationToken);
            }
            public override ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }

    public ValueTask DisposeAsync() { if (Directory.Exists(root)) Directory.Delete(root, true); return ValueTask.CompletedTask; }
    private sealed class Access : IDocumentOwnerAccess
    {
        public bool Deny { get; set; }
        public Task RequireAsync(DocumentOwner owner, DocumentAction action, CancellationToken ct)
        { if (Deny) throw new UnauthorizedAccessException(); return Task.CompletedTask; }
    }
    private sealed class Scanner : IDocumentScanner
    {
        public ScannerVerdict Verdict { get; set; } = ScannerVerdict.Clean;
        public int Calls { get; private set; }
        public async Task<DocumentScanResult> ScanAsync(Stream content, long expectedBytes, string expectedSha256, CancellationToken cancellationToken = default)
        {
            Calls++;
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(content, cancellationToken));
            return new(Verdict, hash, "test-scanner", Verdict == ScannerVerdict.Failed ? null : "test-definitions",
                Verdict == ScannerVerdict.Failed ? "scanner-unavailable" : null);
        }
    }
}
