using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped SaveChanges guards; genuine PostgreSQL constraints and serializable
/// concurrency remain separate private-schema/runtime gates.</summary>
public sealed class ProcurementMasterImportPersistenceTests
{
    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string database = "master-import-" + Guid.NewGuid().ToString("N");
        private readonly Guid company = Guid.NewGuid();
        private readonly ProcurementRequestScope scope = new();
        public Fixture() => scope.Set("tenant-a", company, Guid.NewGuid().ToString("D"),
            null, null, false, "test");
        public ProcurementDbContext Db() => new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(database).Options, scope);
        public async ValueTask DisposeAsync() { await using var db = Db(); await db.Database.EnsureDeletedAsync(); }
    }

    private static ImportBatch NewPreview(string kind = "Item") => new()
    {
        RecordType = kind, FileName = "upload.csv", FileSha256 = new string('a', 64),
        ActorSubjectId = Guid.NewGuid().ToString("D"), SchemaVersion = 1,
        DuplicatePolicy = "reject", PreviewSnapshotText = "{}", PreviewSha256 = Hash("{}"),
        State = "Previewed", TotalRows = 1, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
    };

    private static void Commit(ImportBatch batch)
    {
        batch.State = "Committed"; batch.AppliedRows = 1;
        batch.CommitCommandKey = Guid.NewGuid(); batch.CommitRequestSha256 = new string('b', 64);
        batch.ResultText = "{\"created\":1}"; batch.ResultSha256 = Hash(batch.ResultText);
    }

    [Fact]
    public async Task Preview_kind_and_snapshot_are_immutable_even_if_a_writer_casts_kind_away()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.Db();
        var batch = NewPreview(); db.AddScoped(batch); await db.SaveChangesAsync();
        batch.RecordType = "Other";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        batch = await db.ImportBatches.SingleAsync();
        batch.PreviewSnapshotText = "{\"substituted\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Commit_refuses_missing_or_orphan_receipts_and_unbacked_created_record()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.Db();
        var batch = NewPreview(); db.AddScoped(batch); await db.SaveChangesAsync();
        Commit(batch);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        batch = await db.ImportBatches.SingleAsync();
        db.AddScoped(new ImportRowReceipt { ImportBatchId = batch.Id, RowNumber = 2,
            StartLine = 2, EndLine = 2, Disposition = "create", CreatedRecordId = Guid.NewGuid(),
            RowSha256 = new string('c', 64) });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task One_item_and_its_scoped_receipt_commit_together_and_cannot_be_replayed_as_an_update()
    {
        await using var fixture = new Fixture();
        await using var db = fixture.Db();
        var batch = NewPreview(); db.AddScoped(batch); await db.SaveChangesAsync();
        var item = new CatalogItem { Code = "SKU-1", Name = "Bolt", Kind = "Goods",
            CategoryCode = "FAST", UnitCode = "EA", Status = "Inactive", IsPurchasable = false };
        db.AddScoped(item);
        db.AddScoped(new ImportRowReceipt { ImportBatchId = batch.Id, RowNumber = 2,
            StartLine = 2, EndLine = 2, Disposition = "create", CreatedRecordId = item.Id,
            RowSha256 = new string('c', 64) });
        Commit(batch);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.CatalogItems.CountAsync());
        Assert.Equal(item.Id, (await db.ImportRowReceipts.SingleAsync()).CreatedRecordId);
        batch = await db.ImportBatches.SingleAsync();
        batch.ResultText = "{\"created\":2}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
