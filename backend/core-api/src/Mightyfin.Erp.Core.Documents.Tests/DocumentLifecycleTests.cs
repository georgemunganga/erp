using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Tests;

public sealed class DocumentLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 5, 0, 0, TimeSpan.Zero);
    private static readonly Scope Actor = new("tenant-a", Guid.NewGuid(), "ap-user");
    private static readonly string Hash = new('a', 64);
    private static CoreDocument Document() => CoreDocument.Create(Actor, "procurement", "SupplierInvoice", Guid.NewGuid(), Now);
    private static CoreDocumentVersion Upload(CoreDocument document) => CoreDocumentVersion.Create(
        document, Actor, 1, "invoice.pdf", "application/pdf", 100, Hash, Now);

    [Theory]
    [InlineData("invoice.exe", "application/pdf")]
    [InlineData("invoice.png", "application/pdf")]
    [InlineData("unsafe:photo.jpg", "image/jpeg")]
    public void File_extension_and_name_must_match_an_allowed_document_type(string name, string mediaType)
    {
        Assert.Throws<ArgumentException>(() => CoreDocumentVersion.Create(Document(), Actor, 1, name, mediaType, 100, Hash, Now));
    }

    [Fact]
    public void Matching_image_extension_is_case_insensitive()
    {
        Assert.Equal("photo.JPEG", CoreDocumentVersion.Create(Document(), Actor, 1, "photo.JPEG", "image/jpeg", 100, Hash, Now).FileName);
    }

    [Fact]
    public void Upload_stays_unavailable_until_a_clean_evidenced_scan()
    {
        var document = Document();
        var version = Upload(document);
        Assert.Equal(DocumentScanState.Quarantined, version.ScanState);
        Assert.False(version.IsAvailableFor(document));
        var lease = Guid.NewGuid();
        version.BeginScan(lease, Now, TimeSpan.FromMinutes(1));
        Assert.False(version.IsAvailableFor(document));
        var evidence = version.FinishScan(lease, Hash, ScannerVerdict.Clean, "scanner-1", "definitions-1", Now.AddSeconds(1));
        Assert.Equal(version.Id, evidence.VersionId);
        Assert.Equal(Hash, evidence.Sha256);
        Assert.Equal(ScannerVerdict.Clean, evidence.Verdict);
        Assert.Equal(DocumentScanState.Available, version.ScanState);
        Assert.True(version.IsAvailableFor(document));
        Assert.False(version.IsAvailableFor(Document()));
        Assert.Throws<InvalidOperationException>(() => version.BeginScan(Guid.NewGuid(), Now.AddSeconds(2), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void An_unset_scanner_verdict_cannot_release_content()
    {
        var document = Document();
        var version = Upload(document);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, Now, TimeSpan.FromMinutes(1));
        version.FinishScan(lease, Hash, default, "scanner-1", "definitions-1", Now.AddSeconds(1));
        Assert.Equal(DocumentScanState.ScanFailed, version.ScanState);
        Assert.False(version.IsAvailableFor(document));
    }

    [Fact]
    public void A_changed_hash_or_missing_definitions_cannot_release_content()
    {
        var document = Document();
        var version = Upload(document);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, Now, TimeSpan.FromMinutes(1));
        Assert.Throws<InvalidOperationException>(() => version.FinishScan(lease, new string('b', 64),
            ScannerVerdict.Clean, "scanner-1", "definitions-1", Now.AddSeconds(1)));
        Assert.Throws<InvalidOperationException>(() => version.FinishScan(lease, Hash,
            ScannerVerdict.Clean, "scanner-1", null, Now.AddSeconds(1)));
        Assert.False(version.IsAvailableFor(document));
        Assert.Equal(DocumentScanState.Scanning, version.ScanState);
    }

    [Theory]
    [InlineData(ScannerVerdict.Infected, DocumentScanState.Rejected)]
    [InlineData(ScannerVerdict.Failed, DocumentScanState.ScanFailed)]
    public void A_failed_or_infected_scan_never_makes_content_available(ScannerVerdict verdict, DocumentScanState state)
    {
        var document = Document();
        var version = Upload(document);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, Now, TimeSpan.FromMinutes(1));
        version.FinishScan(lease, Hash, verdict, "scanner-1", null, Now.AddSeconds(1));
        Assert.Equal(state, version.ScanState);
        Assert.False(version.IsAvailableFor(document));
        if (verdict == ScannerVerdict.Infected)
            Assert.Throws<InvalidOperationException>(() => version.BeginScan(Guid.NewGuid(), Now.AddSeconds(2), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void An_expired_worker_cannot_override_a_reclaimed_scan()
    {
        var document = Document();
        var version = Upload(document);
        var oldLease = Guid.NewGuid();
        version.BeginScan(oldLease, Now, TimeSpan.FromMinutes(1));
        Assert.Throws<InvalidOperationException>(() => version.FinishScan(oldLease, Hash,
            ScannerVerdict.Clean, "scanner-1", "definitions-1", Now.AddMinutes(1)));
        var newLease = Guid.NewGuid();
        version.BeginScan(newLease, Now.AddMinutes(2), TimeSpan.FromMinutes(1));
        Assert.Throws<InvalidOperationException>(() => version.FinishScan(oldLease, Hash,
            ScannerVerdict.Clean, "scanner-1", "definitions-1", Now.AddMinutes(2).AddSeconds(1)));
        var evidence = version.FinishScan(newLease, Hash, ScannerVerdict.Clean, "scanner-1",
            "definitions-2", Now.AddMinutes(2).AddSeconds(1));
        Assert.Equal(2, evidence.Attempt);
        Assert.True(version.IsAvailableFor(document));
    }

    [Fact]
    public void Retry_policy_is_bounded_and_does_not_erase_prior_attempts()
    {
        var document = Document();
        var version = Upload(document);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, Now, TimeSpan.FromMinutes(1), maxAttempts: 1);
        var failure = version.FinishScan(lease, Hash, ScannerVerdict.Failed, "scanner-1", null, Now.AddSeconds(1));
        Assert.Throws<InvalidOperationException>(() => version.BeginScan(Guid.NewGuid(), Now.AddSeconds(2), TimeSpan.FromMinutes(1), maxAttempts: 1));
        version.BeginScan(Guid.NewGuid(), Now.AddSeconds(2), TimeSpan.FromMinutes(1), maxAttempts: 2);
        Assert.Equal(2, version.ScanAttempts);
        Assert.Equal(1, failure.Attempt);
        Assert.Equal(ScannerVerdict.Failed, failure.Verdict);
    }

    [Theory]
    [InlineData("../invoice.pdf")]
    [InlineData("C:\\invoice.pdf")]
    [InlineData("invoice\r\n.pdf")]
    public void File_names_cannot_supply_a_storage_path_or_response_header(string fileName)
    {
        Assert.Throws<ArgumentException>(() => CoreDocumentVersion.Create(Document(), Actor, 1,
            fileName, "application/pdf", 100, Hash, Now));
    }

    [Fact]
    public void File_size_limits_can_be_configured_within_the_platform_bound()
    {
        var document = Document();
        Assert.Throws<ArgumentOutOfRangeException>(() => CoreDocumentVersion.Create(document, Actor, 1,
            "large.pdf", "application/pdf", 11 * 1024 * 1024, Hash, Now));
        var accepted = CoreDocumentVersion.Create(document, Actor, 1, "large.pdf", "application/pdf",
            11 * 1024 * 1024, Hash, Now, maxFileBytes: 12 * 1024 * 1024);
        Assert.Equal(11 * 1024 * 1024, accepted.SizeBytes);
        Assert.Throws<ArgumentException>(() => CoreDocumentVersion.Create(document, Actor, 2,
            "program.exe", "application/octet-stream", 100, Hash, Now));
    }

    [Fact]
    public void Another_tenant_or_company_cannot_append_or_retire_evidence()
    {
        var document = Document();
        foreach (var other in new[] { Actor with { TenantId = "tenant-b" }, Actor with { LegalEntityId = Guid.NewGuid() } })
        {
            Assert.Throws<UnauthorizedAccessException>(() => CoreDocumentVersion.Create(document, other,
                2, "invoice.pdf", "application/pdf", 100, Hash, Now));
            Assert.Throws<UnauthorizedAccessException>(() => document.Retire(other, "Wrong owner", Now));
        }
        Assert.False(document.IsRetired);
    }

    [Fact]
    public void Retirement_preserves_the_version_and_its_scan_evidence()
    {
        var document = Document();
        var version = Upload(document);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, Now, TimeSpan.FromMinutes(1));
        var evidence = version.FinishScan(lease, Hash, ScannerVerdict.Clean, "scanner-1", "definitions-1", Now.AddSeconds(1));
        Assert.Throws<ArgumentException>(() => document.Retire(Actor, "", Now.AddSeconds(2)));
        document.Retire(Actor, "Replaced by corrected invoice", Now.AddSeconds(2));
        Assert.False(version.IsAvailableFor(document));
        Assert.Equal(Hash, version.Sha256);
        Assert.Equal(version.Id, evidence.VersionId);
        Assert.Equal(DocumentScanState.Available, version.ScanState);
        Assert.Equal("ap-user", document.RetiredBy);
    }

    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
}
