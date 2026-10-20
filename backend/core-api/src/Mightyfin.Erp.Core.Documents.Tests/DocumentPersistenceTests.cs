using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;

namespace Mightyfin.Erp.Core.Documents.Tests;

public sealed class DocumentPersistenceTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
    private readonly Scope scope = new("tenant-a", Guid.NewGuid(), "uploader");
    private readonly DbContextOptions<DocumentDbContext> options = new DbContextOptionsBuilder<DocumentDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Fact]
    public async Task Metadata_versions_and_scan_evidence_are_isolated_by_tenant_and_company()
    {
        await Seed();
        foreach (var foreign in new[] { scope with { TenantId = "tenant-b" }, scope with { LegalEntityId = Guid.NewGuid() } })
        {
            await using var db = new DocumentDbContext(options, foreign);
            Assert.Empty(await db.Documents.ToListAsync());
            Assert.Empty(await db.Versions.ToListAsync());
            Assert.Empty(await db.ScanEvidence.ToListAsync());
        }
        await using var own = new DocumentDbContext(options, scope);
        Assert.Single(await own.Documents.ToListAsync());
        Assert.Single(await own.Versions.ToListAsync());
        Assert.Single(await own.ScanEvidence.ToListAsync());
    }

    [Fact]
    public async Task Foreign_writes_are_denied_even_with_explicit_save_overload()
    {
        var foreign = scope with { TenantId = "tenant-b" };
        await using var db = new DocumentDbContext(options, scope);
        db.Documents.Add(CoreDocument.Create(foreign, "procurement", "invoice", Guid.NewGuid(), DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync(false));
    }

    [Fact]
    public async Task File_metadata_cannot_be_overwritten_and_evidence_cannot_be_rewritten_or_deleted()
    {
        await Seed();
        await using (var db = new DocumentDbContext(options, scope))
        {
            var version = await db.Versions.SingleAsync();
            db.Entry(version).Property(x => x.Sha256).CurrentValue = new string('b', 64);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = new DocumentDbContext(options, scope))
        {
            var evidence = await db.ScanEvidence.SingleAsync();
            db.Entry(evidence).Property(x => x.Engine).CurrentValue = "rewritten";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = new DocumentDbContext(options, scope))
        {
            db.Documents.Remove(await db.Documents.SingleAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Concurrent_scan_claims_cannot_both_commit()
    {
        var now = DateTimeOffset.UtcNow;
        await using (var db = new DocumentDbContext(options, scope))
        {
            var document = CoreDocument.Create(scope, "procurement", "invoice", Guid.NewGuid(), now);
            db.Documents.Add(document);
            db.Versions.Add(CoreDocumentVersion.Create(document, scope, 1, "invoice.pdf", "application/pdf", 12, new string('a', 64), now));
            await db.SaveChangesAsync();
        }
        await using var first = new DocumentDbContext(options, scope);
        await using var second = new DocumentDbContext(options, scope);
        var a = await first.Versions.SingleAsync();
        var b = await second.Versions.SingleAsync();
        a.BeginScan(Guid.NewGuid(), now, TimeSpan.FromMinutes(1));
        b.BeginScan(Guid.NewGuid(), now, TimeSpan.FromMinutes(1));
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private async Task Seed()
    {
        await using var db = new DocumentDbContext(options, scope);
        var now = DateTimeOffset.UtcNow;
        var document = CoreDocument.Create(scope, "procurement", "invoice", Guid.NewGuid(), now);
        var version = CoreDocumentVersion.Create(document, scope, 1, "invoice.pdf", "application/pdf", 12, new string('a', 64), now);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, now, TimeSpan.FromMinutes(1));
        var evidence = version.FinishScan(lease, version.Sha256, ScannerVerdict.Clean, "test-scanner", "test-definitions", now.AddSeconds(1));
        db.Documents.Add(document);
        db.Versions.Add(version);
        db.ScanEvidence.Add(evidence);
        await db.SaveChangesAsync();
    }
}
