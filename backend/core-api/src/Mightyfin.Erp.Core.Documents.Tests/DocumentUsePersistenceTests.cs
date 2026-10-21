using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;

namespace Mightyfin.Erp.Core.Documents.Tests;

/// <summary>InMemory proves application/save guards only. Relational model assertions do not execute PostgreSQL constraints.</summary>
public sealed class DocumentUsePersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 11, 0, 0, TimeSpan.Zero);
    private readonly Scope actor = new("tenant-a", Guid.NewGuid(), "buyer-subject");
    private readonly string databaseName = Guid.NewGuid().ToString();
    private readonly InMemoryDatabaseRoot databaseRoot = new();
    private DbContextOptions<DocumentDbContext> Options => new DbContextOptionsBuilder<DocumentDbContext>()
        .UseInMemoryDatabase(databaseName, databaseRoot).Options;

    [Fact]
    public async Task Exact_set_roundtrips_and_replay_preserves_original_rows_without_current_eligibility()
    {
        var f = await Seed(2);
        StoredDocumentUse first;
        await using (var db = Db())
        {
            first = await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            await db.SaveChangesAsync();
        }
        await using (var db = Db())
        {
            var replay = await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use,
                f.Selections.Reverse().ToArray(), Now.AddDays(2));
            Assert.Equal(first.Set.Id, replay.Set.Id);
            Assert.Equal(first.Set.CreatedAt, replay.Set.CreatedAt);
            Assert.Equal(first.Pins.Select(x => x.Id).Order(), replay.Pins.Select(x => x.Id).Order());
            Assert.Empty(db.ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified));
            Assert.Equal(2, await db.UsePins.CountAsync());
        }
    }

    [Fact]
    public async Task Review_and_publication_retain_one_version_with_distinct_immutable_use_sets()
    {
        var f = await Seed(1);
        await using var db = Db();
        var repo = new DocumentUseRepository(db, actor);
        var review = await repo.StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
        var publication = await repo.StageAsync(f.Owner, f.Use with { Key = Guid.NewGuid(), Kind = DocumentUseKind.Publication },
            f.Selections, Now.AddSeconds(3));
        await db.SaveChangesAsync();
        Assert.NotEqual(review.Set.Id, publication.Set.Id);
        Assert.Equal(review.Pins.Single().VersionId, publication.Pins.Single().VersionId);
        Assert.Equal(1, await db.Versions.CountAsync());
        Assert.Equal(2, await db.UseSets.CountAsync());
    }

    [Fact]
    public async Task Same_use_key_cannot_add_remove_replace_or_reauthor_the_original_selection()
    {
        var f = await Seed(2);
        await using (var db = Db())
        {
            await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            await db.SaveChangesAsync();
        }
        await using (var db = Db())
        {
            var repo = new DocumentUseRepository(db, actor);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.StageAsync(f.Owner, f.Use, f.Selections.Take(1).ToArray(), Now.AddSeconds(3)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.StageAsync(f.Owner, f.Use with { Revision = 2 }, f.Selections, Now.AddSeconds(3)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.StageAsync(f.Owner, f.Use,
                f.Selections.Select(x => x with { Sha256 = new string('b', 64) }).ToArray(), Now.AddSeconds(3)));
            Assert.Empty(db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added));
        }
        var alias = actor with { SubjectId = "another-account" };
        await using var other = new DocumentDbContext(Options, alias);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DocumentUseRepository(other, alias)
            .StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(3)));
    }

    [Fact]
    public async Task Both_same_save_and_persisted_uses_deny_managed_context_retirement()
    {
        var f = await Seed(1);
        await using (var db = Db())
        {
            await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            (await db.Documents.SingleAsync()).Retire(actor, "Must not retire retained evidence", Now.AddSeconds(3));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = Db())
        {
            Assert.False((await db.Documents.SingleAsync()).IsRetired);
            Assert.Empty(await db.UseSets.ToListAsync());
            await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            await db.SaveChangesAsync();
        }
        await using (var db = Db())
        {
            (await db.Documents.SingleAsync()).Retire(actor, "Still retained", Now.AddSeconds(3));
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges(false));
        }
        await using var read = Db();
        Assert.False((await read.Documents.SingleAsync()).IsRetired);
    }

    [Fact]
    public async Task Missing_or_changed_pins_and_adoption_into_an_existing_set_are_denied_without_writes()
    {
        var f = await Seed(2);
        await using (var db = Db())
        {
            var use = await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            db.Entry(use.Pins[0]).State = EntityState.Detached;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = Db())
        {
            var use = await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            db.Entry(use.Pins[0]).Property(x => x.ScanEvidenceId).CurrentValue = Guid.NewGuid();
            db.Entry(use.Set).Property(x => x.SelectionDigest).CurrentValue = DocumentUsePinRow.Digest(use.Pins);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using (var db = Db())
        {
            var use = await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            await db.SaveChangesAsync();
            var duplicate = DocumentUsePinRow.FromPin(use.Set.Id, await CurrentPin(db, f, 0));
            db.UsePins.Add(duplicate);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using var read = Db();
        Assert.Equal(1, await read.UseSets.CountAsync());
        Assert.Equal(2, await read.UsePins.CountAsync());
    }

    [Fact]
    public async Task Use_evidence_is_append_only_and_scoped_in_all_save_paths()
    {
        var f = await Seed(1);
        await using (var db = Db())
        {
            await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            await db.SaveChangesAsync();
        }
        await using (var db = Db())
        {
            db.Entry(await db.UseSets.SingleAsync()).Property(x => x.SourceRevision).CurrentValue = 2;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(false));
        }
        await using (var db = Db())
        {
            db.UsePins.Remove(await db.UsePins.SingleAsync());
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        }
        foreach (var foreign in new[] { actor with { TenantId = "foreign" }, actor with { LegalEntityId = Guid.NewGuid() } })
        {
            await using var db = new DocumentDbContext(Options, foreign);
            Assert.Empty(await db.UseSets.ToListAsync());
            Assert.Empty(await db.UsePins.ToListAsync());
            Assert.Null(await new DocumentUseRepository(db, foreign).FindAsync(f.Owner, f.Use));
        }
    }

    [Fact]
    public async Task Stored_recovery_does_not_recreate_current_availability_proof()
    {
        var f = await Seed(1);
        Guid pinId;
        await using (var db = Db())
        {
            var use = await new DocumentUseRepository(db, actor).StageAsync(f.Owner, f.Use, f.Selections, Now.AddSeconds(2));
            pinId = use.Pins.Single().Id;
            await db.SaveChangesAsync();
        }
        // Simulate an administrative bypass through a plain context ONLY in this synthetic InMemory test.
        // This proves original recovery under unavailable current state, NOT an operational retirement path or race fence.
        await using (var db = Db())
        {
            var doc = await db.Documents.SingleAsync();
            db.Entry(doc).State = EntityState.Detached;
            await using var bypass = new DbContext(new DbContextOptionsBuilder()
                .UseInMemoryDatabase(databaseName, databaseRoot).UseModel(db.Model).Options);
            bypass.Attach(doc);
            doc.Retire(actor, "Fixture administrative bypass", Now.AddSeconds(3));
            await bypass.SaveChangesAsync();
        }
        await using (var db = Db())
        {
            Assert.True((await db.Documents.SingleAsync()).IsRetired);
            db.ChangeTracker.Clear();
            var repo = new DocumentUseRepository(db, actor);
            var use = await repo.StageAsync(f.Owner, f.Use, f.Selections, Now.AddDays(1));
            Assert.Equal(pinId, use.Pins.Single().Id);
            Assert.Empty(db.ChangeTracker.Entries<CoreDocument>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.StageAsync(f.Owner,
                f.Use with { Key = Guid.NewGuid() }, f.Selections, Now.AddDays(1)));
        }
    }

    [Fact]
    public async Task Malformed_inputs_and_mismatched_context_scope_do_not_stage_rows()
    {
        var f = await Seed(1);
        await using var db = Db();
        var repo = new DocumentUseRepository(db, actor);
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.StageAsync(f.Owner, null!, f.Selections, Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.StageAsync(f.Owner, f.Use, null!, Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.StageAsync(null!, f.Use, f.Selections, Now));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.StageAsync(f.Owner, f.Use, [null!], Now));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.StageAsync(f.Owner with { Id = Guid.Empty }, f.Use, f.Selections, Now));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new DocumentUseRepository(db, actor with { TenantId = "foreign" })
            .StageAsync(f.Owner, f.Use, f.Selections, Now));
        Assert.Empty(db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added));
        Assert.Empty(await db.UseSets.ToListAsync());
    }

    [Fact]
    public async Task Microsecond_canonical_scan_identity_can_be_pinned_at_exact_finished_boundary()
    {
        await using var db = Db();
        var owner = new DocumentOwner("procurement", "sourcing_event", Guid.NewGuid());
        var at = Now.AddTicks(17);
        var document = CoreDocument.Create(actor, owner.Module, owner.Type, owner.Id, at);
        var version = CoreDocumentVersion.Create(document, actor, 1, "instant.pdf", "application/pdf", 100, new string('a', 64), at);
        var lease = Guid.NewGuid();
        version.BeginScan(lease, at, TimeSpan.FromMinutes(1));
        var scan = version.FinishScan(lease, version.Sha256, ScannerVerdict.Clean, "fixture", "definitions", at);
        var source = new DocumentUseReference(Guid.NewGuid(), "procurement", "sourcing_review", Guid.NewGuid(), 1, DocumentUseKind.Review);
        var selection = new DocumentUseSelection(owner.Module, owner.Type, owner.Id, document.Id, version.Id,
            document.Version, version.Version, version.Sha256);
        var pin = DocumentUsePin.Create(actor, source, selection, document, version, scan, at);
        var set = DocumentUseSetRow.FromPins([pin]);
        db.AddRange(document, version, scan, set, DocumentUsePinRow.FromPin(set.Id, pin));
        await db.SaveChangesAsync();
        Assert.Equal(0, set.CreatedAt.Ticks % 10);
        Assert.Equal(DocumentUsePinRow.StoredInstant(at), (await db.UsePins.SingleAsync()).ScanFinishedAt);
        Assert.Equal(set.SelectionDigest, DocumentUsePinRow.Digest(await db.UsePins.ToListAsync()));
        // InMemory does not prove actual Npgsql round-trip; source test covers the canonical boundary and save guard.
    }

    [Fact]
    public void Npgsql_model_has_scoped_exact_ownership_FKs_and_no_mutable_revision_keys()
    {
        using var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only").Options, actor);
        var row = db.Model.FindEntityType(typeof(DocumentUsePinRow))!;
        Assert.Equal(4, row.GetForeignKeys().Count());
        Assert.Contains(row.GetForeignKeys(), x => x.GetConstraintName() == "fk_doc_pin_version_scan"
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "VersionId", "ScanEvidenceId" }));
        Assert.Contains(row.GetForeignKeys(), x => x.GetConstraintName() == "fk_doc_pin_document_owner"
            && x.Properties.Any(p => p.Name == "OwnerId"));
        Assert.All(row.GetForeignKeys(), x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
        Assert.DoesNotContain(row.GetKeys().SelectMany(x => x.Properties), x => x.Name is "DocumentRevision" or "VersionRevision");
        Assert.Equal(ValueGenerated.Never, row.FindProperty("Id")!.ValueGenerated);
        Assert.Contains(db.Model.FindEntityType(typeof(DocumentUseSetRow))!.GetIndexes(), x => x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "LegalEntityId", "UseKey" }));
        Assert.Equal("ak_doc_owner_id", db.Model.FindEntityType(typeof(CoreDocument))!.GetKeys()
            .Single(x => x.Properties.Any(p => p.Name == "OwnerId")).GetName());
        Assert.Equal("ak_doc_scan_version_id", db.Model.FindEntityType(typeof(DocumentScanEvidence))!.GetKeys()
            .Single(x => x.Properties.Count == 4).GetName());
    }

    private DocumentDbContext Db() => new(Options, actor);
    private async Task<Fixture> Seed(int count)
    {
        await using var db = Db();
        var owner = new DocumentOwner("procurement", "sourcing_event", Guid.NewGuid());
        var selections = new List<DocumentUseSelection>();
        for (var i = 0; i < count; i++)
        {
            var document = CoreDocument.Create(actor, owner.Module, owner.Type, owner.Id, Now);
            var version = CoreDocumentVersion.Create(document, actor, 1, $"requirements-{i}.pdf", "application/pdf", 100, new string('a', 64), Now);
            var lease = Guid.NewGuid();
            version.BeginScan(lease, Now, TimeSpan.FromMinutes(1));
            var evidence = version.FinishScan(lease, version.Sha256, ScannerVerdict.Clean, "fixture", "definitions", Now.AddSeconds(1));
            db.Documents.Add(document); db.Versions.Add(version); db.ScanEvidence.Add(evidence);
            selections.Add(new(owner.Module, owner.Type, owner.Id, document.Id, version.Id, document.Version, version.Version, version.Sha256));
        }
        await db.SaveChangesAsync();
        return new(owner, new(Guid.NewGuid(), "procurement", "sourcing_review", Guid.NewGuid(), 1, DocumentUseKind.Review), selections.ToArray());
    }
    private async Task<DocumentUsePin> CurrentPin(DocumentDbContext db, Fixture f, int index)
    {
        var selection = f.Selections[index];
        var doc = await db.Documents.SingleAsync(x => x.Id == selection.DocumentId);
        var version = await db.Versions.SingleAsync(x => x.Id == selection.VersionId);
        var scan = await db.ScanEvidence.SingleAsync(x => x.VersionId == version.Id);
        return DocumentUsePin.Create(actor, f.Use, selection, doc, version, scan, Now.AddSeconds(2));
    }
    private sealed record Fixture(DocumentOwner Owner, DocumentUseReference Use, DocumentUseSelection[] Selections);
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
}
