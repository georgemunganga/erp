using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Documents.Application;
using Mightyfin.Erp.Core.Documents.Domain;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Npgsql;

/// <summary>Opt-in internal persistence proof only; no module purpose authorization or cross-transaction lifecycle lease.</summary>
internal static class DocumentUseVerification
{
    internal static async Task RunAsync(string connection, DbContextOptions<DocumentDbContext> options, IDocumentScope scope,
        DocumentOwner owner, Func<DocumentOwner, Task<DocumentView>> upload)
    {
        DocumentDbContext Context(IDocumentScope? actor = null) => new(options, actor ?? scope);
        DocumentUseSelection[] selections;
        DateTimeOffset at;
        await using (var db = Context())
        {
            // The opt-in mode needs the additive Core use model/migration, not EnsureCreated or an alternate schema.
            Check(!db.Database.HasPendingModelChanges(), "Use-pin model needs its central Core migration.");
            var rows = await (from version in db.Versions join document in db.Documents on version.DocumentId equals document.Id
                where document.OwnerModule == owner.Module && document.OwnerType == owner.Type && document.OwnerId == owner.Id
                orderby version.Id select new { document, version }).Take(2).ToListAsync();
            Check(rows.Count == 2, "Two genuine uploaded versions are required.");
            selections = rows.Select(x => new DocumentUseSelection(owner.Module, owner.Type, owner.Id,
                x.document.Id, x.version.Id, x.document.Version, x.version.Version, x.version.Sha256)).ToArray();
            var ids = selections.Select(x => x.VersionId).ToArray();
            var lastScan = await db.ScanEvidence.Where(x => ids.Contains(x.VersionId)).MaxAsync(x => x.FinishedAt);
            // PostgreSQL persisted scan is at microsecond precision. This exact boundary adds a submicrosecond input tick.
            at = lastScan.AddTicks(7);
        }
        var source = new DocumentUseReference(Guid.NewGuid(), "core", "workflow_probe", Guid.NewGuid(), 1, DocumentUseKind.Review);
        StoredDocumentUse original;
        await using (var db = Context())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            original = await new DocumentUseRepository(db, scope).StageAsync(owner, source, selections, at);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await using (var db = Context())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var repo = new DocumentUseRepository(db, scope);
            var replay = await repo.StageAsync(owner, source, selections.Reverse().ToArray(), at.AddDays(1));
            Check(replay.Set.Id == original.Set.Id && replay.Set.CreatedAt == original.Set.CreatedAt
                && replay.Set.SelectionDigest == original.Set.SelectionDigest, "Original use-set evidence changed after storage.");
            Check(replay.Pins.Select(x => x.Id).Order().SequenceEqual(original.Pins.Select(x => x.Id).Order()), "Replay replaced original pins.");
            Check(replay.Set.CreatedAt == DocumentUsePinRow.StoredInstant(at) && replay.Set.CreatedAt.Ticks % 10 == 0,
                "Submicrosecond input did not retain canonical PostgreSQL time.");
            Check(replay.Pins.All(x => x.ScanStartedAt.Ticks % 10 == 0 && x.ScanFinishedAt.Ticks % 10 == 0), "Scan timestamps changed precision.");
            Check(DocumentUsePinRow.Digest(replay.Pins) == replay.Set.SelectionDigest, "Exact selection digest did not round-trip.");
            Check(!db.ChangeTracker.HasChanges(), "Exact recovery staged a write.");
            await ExpectInvalid(() => repo.StageAsync(owner, source, selections.Take(1).ToArray(), at), "Changed selection was accepted.");
            await ExpectInvalid(() => repo.StageAsync(owner, source with { Revision = 2 }, selections, at), "Changed use revision was accepted.");
            await transaction.CommitAsync();
        }
        // Publication is another use, not another blob or replacement of the review's retained evidence.
        await using (var db = Context())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var publication = await new DocumentUseRepository(db, scope).StageAsync(owner,
                source with { Key = Guid.NewGuid(), Kind = DocumentUseKind.Publication }, selections, at.AddSeconds(1));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            Check(publication.Set.Id != original.Set.Id && publication.Pins.Select(x => x.VersionId).Order()
                .SequenceEqual(original.Pins.Select(x => x.VersionId).Order()), "Publication did not retain the same versions separately.");
        }
        var baseline = await Footprint(connection);
        foreach (var saveBeforeRollback in new[] { false, true })
        {
            await using (var db = Context())
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                await new DocumentUseRepository(db, scope).StageAsync(owner, source with { Key = Guid.NewGuid() }, selections, at.AddSeconds(2));
                if (saveBeforeRollback) await db.SaveChangesAsync();
                await transaction.RollbackAsync();
            }
            Check(await Footprint(connection) == baseline, "Use rollback left evidence or orphan rows.");
        }
        await using (var db = Context())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var document = await db.Documents.SingleAsync(x => x.Id == selections[0].DocumentId);
            document.Retire(scope, "Must be denied while retained", at.AddSeconds(3));
            await ExpectInvalid(async () => { await db.SaveChangesAsync(); }, "Persisted retained document was retired.");
            await transaction.RollbackAsync();
        }
        Check(await Footprint(connection) == baseline, "Denied retirement changed persisted state.");
        foreach (var foreign in new[] { new ProbeScope("foreign-tenant", scope.LegalEntityId, scope.SubjectId),
                     new ProbeScope(scope.TenantId, Guid.NewGuid(), scope.SubjectId) })
        {
            await using var db = Context(foreign);
            Check(!await db.UseSets.AnyAsync() && !await db.UsePins.AnyAsync(), "Foreign scope saw document uses.");
            Check(await new DocumentUseRepository(db, foreign).FindAsync(owner, source) is null, "Foreign scope recovered a use.");
        }
        // A real upload under another owner supplies valid scoped parents for an isolated document-owner FK negative.
        var otherOwner = owner with { Id = Guid.NewGuid() };
        var otherUpload = await upload(otherOwner);
        Guid otherDocumentId, otherScanId;
        await using (var db = Context())
        {
            var version = await db.Versions.SingleAsync(x => x.Id == otherUpload.VersionId);
            otherDocumentId = version.DocumentId;
            otherScanId = await db.ScanEvidence.Where(x => x.VersionId == version.Id).Select(x => x.Id).SingleAsync();
        }
        baseline = await Footprint(connection);
        var pin = original.Pins[0];
        await RawDenial("23503", "fk_doc_pin_set_owner", "\"UseSetId\" = @missing", Guid.NewGuid());
        await RawDenial("23503", "fk_doc_pin_document_version", "\"DocumentId\" = @other", selections[1].DocumentId);
        await RawDenial("23503", "fk_doc_pin_version_scan", "\"ScanEvidenceId\" = @other", otherScanId);
        await RawDenial("23503", "fk_doc_pin_document_owner",
            "\"DocumentId\" = @other, \"VersionId\" = @otherVersion, \"ScanEvidenceId\" = @otherScan", otherDocumentId,
            otherUpload.VersionId, otherScanId);
        await RawDenial("23514", "ck_doc_use_pin_shape", "\"ScanVerdict\" = 2", Guid.NewGuid());
        await RawDenial("23514", "ck_doc_use_set_shape", "set-count", Guid.NewGuid());
        Console.WriteLine("PASS --use-pins: actual scoped Core persistence, exact-set original recovery, PostgreSQL timestamp/digest round-trip, separate review/publication uses, rollback/no orphans, managed context retirement denial, foreign scope, four named FK and two CHECK negatives. No module authorization, lifetime fence/race or publication activation claimed.");

        async Task RawDenial(string sqlState, string expectedConstraint, string change, Guid other, Guid? otherVersion = null, Guid? otherScan = null)
        {
            await using var db = await Open(connection);
            await using (var transaction = await db.BeginTransactionAsync())
            {
                var newSet = Guid.NewGuid();
                await using var clone = new NpgsqlCommand("""
                    INSERT INTO core_document.use_sets
                    ("Id","TenantId","LegalEntityId","UseKey","SourceModule","SourceRecordType","SourceRecordId","SourceRevision","Kind","OwnerModule","OwnerType","OwnerId","SelectionCount","SelectionDigest","CreatedBy","CreatedAt")
                    SELECT @newSet,"TenantId","LegalEntityId",@newKey,"SourceModule","SourceRecordType","SourceRecordId","SourceRevision","Kind","OwnerModule","OwnerType","OwnerId","SelectionCount","SelectionDigest","CreatedBy","CreatedAt"
                    FROM core_document.use_sets WHERE "Id"=@original
                    """, db, transaction);
                clone.Parameters.AddWithValue("newSet", newSet); clone.Parameters.AddWithValue("newKey", Guid.NewGuid());
                clone.Parameters.AddWithValue("original", original.Set.Id);
                Check(await clone.ExecuteNonQueryAsync() == 1, "Original use-set fixture missing.");
                string sql;
                if (change == "set-count") sql = "UPDATE core_document.use_sets SET \"SelectionCount\" = 0 WHERE \"Id\"=@newSet";
                else
                {
                    // Clone all immutable scalar facts into a NEW set to avoid an earlier unique(version/set) failure.
                    // Then change only the intended relationship in the transient candidate row.
                    sql = """
                        INSERT INTO core_document.use_pins
                        ("Id","TenantId","LegalEntityId","UseSetId","OwnerModule","OwnerType","OwnerId","DocumentId","VersionId","ScanEvidenceId","VersionNumber","DocumentRevision","VersionRevision","FileName","MediaType","SizeBytes","Sha256","ScanAttempt","ScanVerdict","ScanEngine","ScanDefinitions","ScanStartedAt","ScanFinishedAt","CreatedBy","CreatedAt")
                        SELECT @newPin,"TenantId","LegalEntityId",@newSet,"OwnerModule","OwnerType","OwnerId","DocumentId","VersionId","ScanEvidenceId","VersionNumber","DocumentRevision","VersionRevision","FileName","MediaType","SizeBytes","Sha256","ScanAttempt","ScanVerdict","ScanEngine","ScanDefinitions","ScanStartedAt","ScanFinishedAt","CreatedBy","CreatedAt"
                        FROM core_document.use_pins WHERE "Id"=@originalPin;
                        """;
                    await using var insert = new NpgsqlCommand(sql, db, transaction);
                    insert.Parameters.AddWithValue("newPin", Guid.NewGuid()); insert.Parameters.AddWithValue("newSet", newSet);
                    insert.Parameters.AddWithValue("originalPin", pin.Id);
                    Check(await insert.ExecuteNonQueryAsync() == 1, "Original use-pin fixture missing.");
                    sql = "UPDATE core_document.use_pins SET " + change + " WHERE \"UseSetId\"=@newSet";
                }
                try
                {
                    await using var invalid = new NpgsqlCommand(sql, db, transaction);
                    invalid.Parameters.AddWithValue("newSet", newSet); invalid.Parameters.AddWithValue("other", other);
                    invalid.Parameters.AddWithValue("missing", other);
                    if (otherVersion is { } version) invalid.Parameters.AddWithValue("otherVersion", version);
                    if (otherScan is { } scan) invalid.Parameters.AddWithValue("otherScan", scan);
                    await invalid.ExecuteNonQueryAsync();
                    throw new InvalidOperationException("Expected named relational denial was not raised.");
                }
                catch (PostgresException ex) when (ex.SqlState == sqlState && ex.ConstraintName == expectedConstraint) { }
                await transaction.RollbackAsync();
            }
            Check(await Footprint(connection) == baseline, "Raw negative changed original schema/data.");
            await using var fresh = Context();
            var retained = await new DocumentUseRepository(fresh, scope).FindAsync(owner, source);
            Check(retained is not null && retained.Set.Id == original.Set.Id && retained.Pins.Count == original.Pins.Count, "Original recovery failed after raw denial.");
        }
    }

    private static async Task<string> Footprint(string connection)
    {
        await using var db = await Open(connection);
        var data = new StringBuilder();
        foreach (var table in new[] { "documents", "versions", "scan_evidence", "record_events", "use_sets", "use_pins" })
        {
            await using var command = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.\"Id\"),'[]'::jsonb)::text FROM core_document.{table} r", db);
            data.Append(table).Append(await command.ExecuteScalarAsync());
        }
        await using var schema = new NpgsqlCommand("""
            SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.relname,r.conname),'[]'::jsonb)::text
            FROM (SELECT c.relname,k.conname,k.contype,k.convalidated,pg_get_constraintdef(k.oid,false) AS definition
                  FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                  WHERE n.nspname='core_document') r
            """, db);
        data.Append(await schema.ExecuteScalarAsync());
        await using var columns = new NpgsqlCommand("""
            SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.relname,r.attnum),'[]'::jsonb)::text
            FROM (SELECT c.relname,a.attnum,a.attname,format_type(a.atttypid,a.atttypmod) AS type,a.attnotnull,
                         pg_get_expr(d.adbin,d.adrelid,false) AS default_expression
                  FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
                  LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
                  WHERE n.nspname='core_document' AND c.relkind='r' AND a.attnum>0 AND NOT a.attisdropped) r
            """, db);
        data.Append(await columns.ExecuteScalarAsync());
        await using var history = new NpgsqlCommand("""SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r."MigrationId"),'[]'::jsonb)::text FROM core_document."__EFMigrationsHistory" r""", db);
        data.Append(await history.ExecuteScalarAsync());
        await using var indexes = new NpgsqlCommand("SELECT COALESCE(jsonb_agg(to_jsonb(r) ORDER BY r.tablename,r.indexname),'[]'::jsonb)::text FROM pg_indexes r WHERE r.schemaname='core_document'", db);
        data.Append(await indexes.ExecuteScalarAsync());
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString())));
    }
    private static async Task<NpgsqlConnection> Open(string connection)
    {
        var result = new NpgsqlConnection(connection);
        try { await result.OpenAsync(); return result; }
        catch { await result.DisposeAsync(); throw; }
    }
    private static async Task ExpectInvalid(Func<Task> operation, string error)
    {
        try { await operation(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(error);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed record ProbeScope(string TenantId, Guid LegalEntityId, string SubjectId) : IDocumentScope;
}
