using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("CORE_DOCUMENT_TEST_CONNECTION")
    ?? throw new InvalidOperationException("Set an isolated rehearsal database connection.");
if (!(new NpgsqlConnectionStringBuilder(connection).Database?.Contains("rehearsal", StringComparison.OrdinalIgnoreCase) ?? false))
    throw new InvalidOperationException("This probe requires a disposable rehearsal database.");
var options = new DbContextOptionsBuilder<DocumentDbContext>().UseNpgsql(connection,
    p => p.MigrationsHistoryTable("__EFMigrationsHistory", "core_document")).Options;
var scope = new Scope("workflow-probe", Guid.NewGuid(), "synthetic-ap");
var owner = new DocumentOwner("procurement", "supplier_invoice", Guid.NewGuid());
var root = Path.Combine(Path.GetTempPath(), "core-doc-probe-" + Guid.NewGuid().ToString("N"));
var store = new PrivateDocumentStore(root);
var bytes = "%PDF-1.7 Synthetic PostgreSQL document workflow fixture"u8.ToArray();
try
{
    await using (var db = new DocumentDbContext(options, scope))
    {
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
    }
    var key = Guid.NewGuid();
    var first = await Upload(owner, key);
    var replay = await Upload(owner, key);
    Check(first.VersionId == replay.VersionId && first.CanDownload, "Exact replay did not retain the available version.");
    try { await Upload(owner, key, "%PDF-different"u8.ToArray()); throw new Exception("Conflicting replay was accepted."); }
    catch (DocumentProblemException ex) when (ex.Code == "upload-key-conflict") { }
    var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
    {
        try { await Upload(owner, Guid.NewGuid()); return "saved"; }
        catch (DocumentProblemException ex) when (ex.Code == "document-limit") { return "limited"; }
    }));
    Check(outcomes.Count(x => x == "saved") == 9 && outcomes.Count(x => x == "limited") == 3,
        "Concurrent owner file-count limit was not serialized.");
    await using (var db = new DocumentDbContext(options, scope))
    {
        Check(await db.Documents.CountAsync() == 10 && await db.Versions.CountAsync() == 10, "Unexpected stored document count.");
        Check(await db.ScanEvidence.CountAsync() == 10, "Scan evidence did not remain one per clean fixture.");
        Check(await db.RecordEvents.CountAsync() == 30, "Upload and scan operation evidence is incomplete.");
        var service = Service(db, scope);
        var file = await service.DownloadAsync(owner, first.VersionId);
        await file.Content.DisposeAsync();
        Check(await db.RecordEvents.CountAsync(x => x.Action == "DownloadAuthorized") == 1, "Download evidence was not persisted.");
    }
    var foreign = scope with { LegalEntityId = Guid.NewGuid() };
    await using (var db = new DocumentDbContext(options, foreign))
    {
        Check((await Service(db, foreign).ListAsync(owner)).Count == 0, "Cross-company document metadata was visible.");
        try { await Service(db, foreign).DownloadAsync(owner, first.VersionId); throw new Exception("Foreign download was accepted."); }
        catch (DocumentProblemException ex) when (ex.Code == "document-not-found") { }
    }
    Check(Directory.GetFiles(root, ".upload-*", SearchOption.AllDirectories).Length == 0, "Abandoned staging files remain after completed requests.");
    Console.WriteLine("PASS: migrations twice, exact/conflicting replay, twelve concurrent uploads with ten-file cap, retained scan/operation evidence, audited download and company isolation.");
    return 0;
}
finally { Directory.Delete(root, true); }

RecordDocumentService Service(DocumentDbContext db, Scope actor) => new(actor, new Access(),
    new EfDocumentRecords(db, actor), store, new Scanner(), new());
async Task<DocumentView> Upload(DocumentOwner target, Guid uploadKey, byte[]? content = null)
{
    await using var db = new DocumentDbContext(options, scope);
    return await Service(db, scope).UploadAsync(target, uploadKey, "invoice.pdf", new MemoryStream(content ?? bytes));
}
static void Check(bool result, string error) { if (!result) throw new InvalidOperationException(error); }
record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
sealed class Access : IDocumentOwnerAccess
{ public Task RequireAsync(DocumentOwner owner, DocumentAction action, CancellationToken ct) => Task.CompletedTask; }
sealed class Scanner : IDocumentScanner
{
    public async Task<DocumentScanResult> ScanAsync(Stream content, long expectedBytes, string expectedSha256, CancellationToken cancellationToken = default) =>
        new(ScannerVerdict.Clean, Convert.ToHexStringLower(await SHA256.HashDataAsync(content, cancellationToken)), "fixture-scanner", "fixture-definitions");
}
