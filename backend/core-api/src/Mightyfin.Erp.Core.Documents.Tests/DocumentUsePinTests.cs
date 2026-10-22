using Mightyfin.Erp.Core.Documents.Domain;

namespace Mightyfin.Erp.Core.Documents.Tests;

public sealed class DocumentUsePinTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly Scope Actor = new("tenant-a", Guid.NewGuid(), "review-worker-account");
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void Review_and_publication_are_distinct_references_to_the_same_exact_content()
    {
        var f = Ready();
        var review = Pin(f);
        var publication = Pin(f, f.Use with { Key = Guid.NewGuid(), Kind = DocumentUseKind.Publication });
        Assert.NotEqual(review.Id, publication.Id);
        Assert.NotEqual(review.Source, publication.Source);
        Assert.Equal(review.VersionId, publication.VersionId);
        Assert.Equal(review.Sha256, publication.Sha256);
        Assert.Equal(f.Scan, review.Scan);
        Assert.Equal(f.Document.OwnerId, review.OwnerId);
        Assert.Equal(f.Version.FileName, review.FileName);
        Assert.Equal(f.Version.SizeBytes, review.SizeBytes);
    }

    [Fact]
    public void Exact_duplicate_returns_original_identity_and_creation_time()
    {
        var f = Ready();
        var original = Pin(f);
        var retry = DocumentUsePin.Create(Actor, f.Use, f.Selection, f.Document, f.Version, f.Scan, Now.AddMinutes(1));
        Assert.NotEqual(original.Id, retry.Id);
        Assert.Same(original, original.ResolveExactDuplicate(retry));
        Assert.Equal(Now.AddSeconds(2), original.CreatedAt);
    }

    [Fact]
    public void Duplicate_rejects_changed_consumer_revision_kind_and_actor()
    {
        var f = Ready();
        var original = Pin(f);
        Assert.Throws<InvalidOperationException>(() => original.ResolveExactDuplicate(Pin(f, f.Use with { Revision = 2 })));
        Assert.Throws<InvalidOperationException>(() => original.ResolveExactDuplicate(Pin(f, f.Use with { Kind = DocumentUseKind.Publication })));
        var alias = DocumentUsePin.Create(Actor with { SubjectId = "other-account" }, f.Use, f.Selection,
            f.Document, f.Version, f.Scan, Now.AddSeconds(2));
        Assert.Throws<InvalidOperationException>(() => original.ResolveExactDuplicate(alias));
    }

    [Fact]
    public void Another_tenant_or_company_cannot_construct_a_pin()
    {
        var f = Ready();
        foreach (var scope in new[] { Actor with { TenantId = "tenant-b" }, Actor with { LegalEntityId = Guid.NewGuid() } })
            Assert.Throws<UnauthorizedAccessException>(() => DocumentUsePin.Create(scope, f.Use, f.Selection,
                f.Document, f.Version, f.Scan, Now.AddSeconds(2)));
    }

    [Fact]
    public void Owner_document_version_hash_and_concurrency_expectations_are_exact()
    {
        var f = Ready();
        var wrong = new[]
        {
            f.Selection with { OwnerModule = "other" }, f.Selection with { OwnerType = "supplier_invoice" },
            f.Selection with { OwnerId = Guid.NewGuid() }, f.Selection with { DocumentId = Guid.NewGuid() },
            f.Selection with { VersionId = Guid.NewGuid() }, f.Selection with { Sha256 = new string('b', 64) },
            f.Selection with { DocumentRevision = 0 }, f.Selection with { VersionRevision = f.Version.Version - 1 },
        };
        foreach (var selection in wrong)
            Assert.Throws<InvalidOperationException>(() => DocumentUsePin.Create(Actor, f.Use, selection,
                f.Document, f.Version, f.Scan, Now.AddSeconds(2)));
    }

    [Theory]
    [InlineData(ScannerVerdict.Infected)]
    [InlineData(ScannerVerdict.Failed)]
    public void Rejected_or_failed_versions_cannot_be_pinned(ScannerVerdict verdict)
    {
        var f = Ready(verdict);
        Assert.Throws<InvalidOperationException>(() => Pin(f));
    }

    [Fact]
    public void A_quarantined_or_scanning_version_cannot_borrow_another_versions_clean_evidence()
    {
        var f = Ready();
        var pending = CoreDocumentVersion.Create(f.Document, Actor, 2, "new.pdf", "application/pdf", 40, Hash, Now);
        var selection = Selection(f.Document, pending);
        Assert.Throws<InvalidOperationException>(() => DocumentUsePin.Create(Actor, f.Use, selection,
            f.Document, pending, f.Scan, Now.AddSeconds(2)));
        pending.BeginScan(Guid.NewGuid(), Now, TimeSpan.FromMinutes(1));
        Assert.Throws<InvalidOperationException>(() => DocumentUsePin.Create(Actor, f.Use, Selection(f.Document, pending),
            f.Document, pending, f.Scan, Now.AddSeconds(2)));
    }

    [Fact]
    public void Retirement_blocks_new_uses_but_does_not_mutate_already_derived_evidence()
    {
        var f = Ready();
        var original = Pin(f);
        f.Document.Retire(Actor, "Retirement without an implemented use-store guard", Now.AddSeconds(3));
        Assert.Throws<InvalidOperationException>(() => DocumentUsePin.Create(Actor, f.Use, Selection(f.Document, f.Version),
            f.Document, f.Version, f.Scan, Now.AddSeconds(4)));
        Assert.Equal(f.Version.Id, original.VersionId);
        Assert.Equal(1L, original.DocumentRevision);
        // This proves immutable evidence only: the pure primitive does NOT enforce managed retirement.
        Assert.True(f.Document.IsRetired);
    }

    [Fact]
    public void Available_flag_without_matching_latest_clean_scan_evidence_is_insufficient()
    {
        var f = Ready();
        var invalid = new[]
        {
            f.Scan with { Id = Guid.Empty }, f.Scan with { VersionId = Guid.NewGuid() },
            f.Scan with { TenantId = "other" }, f.Scan with { LegalEntityId = Guid.NewGuid() },
            f.Scan with { Attempt = 0 }, f.Scan with { Sha256 = new string('b', 64) },
            f.Scan with { Verdict = ScannerVerdict.Infected }, f.Scan with { Engine = " " },
            f.Scan with { Definitions = null }, f.Scan with { FailureCode = "failed" },
            f.Scan with { StartedAt = Now.AddTicks(1) }, f.Scan with { FinishedAt = Now.AddMinutes(2) },
            f.Scan with { FinishedAt = Now.AddSeconds(-1) },
        };
        foreach (var scan in invalid)
            Assert.Throws<InvalidOperationException>(() => DocumentUsePin.Create(Actor, f.Use, f.Selection,
                f.Document, f.Version, scan, Now.AddSeconds(2)));
    }

    [Fact]
    public void Unknown_purpose_empty_key_and_noncanonical_source_are_rejected()
    {
        var f = Ready();
        foreach (var source in new[] { f.Use with { Kind = 0 }, f.Use with { Key = Guid.Empty },
            f.Use with { RecordId = Guid.Empty }, f.Use with { Revision = 0 }, f.Use with { Module = " procurement " } })
            Assert.Throws<ArgumentException>(() => Pin(f, source));
    }

    private static DocumentUsePin Pin(Fixture f, DocumentUseReference? source = null) =>
        DocumentUsePin.Create(Actor, source ?? f.Use, f.Selection, f.Document, f.Version, f.Scan, Now.AddSeconds(2));

    private static Fixture Ready(ScannerVerdict verdict = ScannerVerdict.Clean)
    {
        var document = CoreDocument.Create(Actor, "procurement", "sourcing_event", Guid.NewGuid(), Now);
        var version = CoreDocumentVersion.Create(document, Actor, 1, "requirements.pdf", "application/pdf", 100, Hash, Now);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, Now, TimeSpan.FromMinutes(1));
        var scan = version.FinishScan(lease, Hash, verdict, "test-scanner", "test-definitions", Now.AddSeconds(1));
        return new(document, version, scan, Selection(document, version),
            new(Guid.NewGuid(), "procurement", "sourcing_review", Guid.NewGuid(), 1, DocumentUseKind.Review));
    }

    private static DocumentUseSelection Selection(CoreDocument document, CoreDocumentVersion version) =>
        new(document.OwnerModule, document.OwnerType, document.OwnerId, document.Id, version.Id,
            document.Version, version.Version, version.Sha256);
    private sealed record Fixture(CoreDocument Document, CoreDocumentVersion Version, DocumentScanEvidence Scan,
        DocumentUseSelection Selection, DocumentUseReference Use);
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
}
