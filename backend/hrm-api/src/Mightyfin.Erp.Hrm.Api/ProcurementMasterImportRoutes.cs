using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Company-scoped create-only Vendor/Item CSV import. Disabled until genuine schema and finite-role admission.</summary>
public static class ProcurementMasterImportRoutes
{
    internal sealed record MappedRow(int RowNumber, int StartLine, int EndLine,
        SortedDictionary<string, string> Fields);
    internal sealed record VendorCandidate(Guid Id, string CountryCode, string? NormalizedRegistration,
        string DisplayName, string LegalName);
    internal sealed record ItemCandidate(Guid Id, string Code);
    internal sealed record CandidateSet(VendorCandidate[] Vendors, ItemCandidate[] Items);

    public static void MapRoutes(WebApplication app)
    {
        MapKind(app, "vendors", "Vendor", "procurement-vendor-import", "procurement-vendor-create");
        MapKind(app, "items", "Item", "procurement-item-import", "procurement-item-manage");
    }

    private static void MapKind(WebApplication app, string path, string kind, string importAction, string nativeAction)
    {
        var group = app.MapGroup($"/api/procurement/v1/{path}/import");
        if (kind == "Item") group.AddEndpointFilter(ProcurementCatalogFencing.Write);
        group.MapGet("/schema", async (ProcurementDbContext db, HrmDbContext core,
            IConfiguration config, HttpContext http, CancellationToken ct) =>
            await Schema(db, core, config, http, kind, ct))
            .RequireAuthorization(importAction).RequireAuthorization(nativeAction);
        group.MapGet("/template", async (ProcurementDbContext db, HrmDbContext core,
            IConfiguration config, HttpContext http, CancellationToken ct) =>
            await Template(db, core, config, http, kind, ct))
            .RequireAuthorization(importAction).RequireAuthorization(nativeAction);
        group.MapPost("/preview", async (ProcurementDbContext db, HrmDbContext core, IConfiguration config,
            ProcurementRequestScope scope, HttpContext http, CancellationToken ct) =>
            await Preview(db, core, config, scope, http, kind, ct))
            .RequireAuthorization(importAction).RequireAuthorization(nativeAction);
        group.MapPost("/commit", async (ProcurementDbContext db, HrmDbContext core, IConfiguration config,
            ProcurementRequestScope scope, HttpContext http, CancellationToken ct) =>
            await Commit(db, core, config, scope, http, kind, ct))
            .RequireAuthorization(importAction).RequireAuthorization(nativeAction);
        group.MapGet("/{batchId:guid}", async (Guid batchId, ProcurementDbContext db,
            HrmDbContext core, IConfiguration config,
            ProcurementRequestScope scope, HttpContext http, CancellationToken ct) =>
            await Recover(batchId, db, core, config, scope, http, kind, ct))
            .RequireAuthorization(importAction).RequireAuthorization(nativeAction);
    }

    private static IResult Error(int status, string code) => Results.Json(new { code }, statusCode: status);
    private static IResult Unavailable(HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        return Results.Json(new { code = "import-unavailable", traceId = http.TraceIdentifier },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<bool> Ready(ProcurementDbContext db, HrmDbContext core,
        IConfiguration config, HttpContext http, CancellationToken ct)
    {
        // Default off. HasPendingModelChanges holds the route closed until the
        // genuine migration exists and is installed on the selected database.
        try
        {
            if (!config.GetValue<bool>("Procurement:MasterImport:Enabled")
                || ProcurementImportPreviewLimits.Read(config) is null
                || !config.GetValue<bool>("Procurement:MasterImport:Maintenance:Enabled")
                || !ProcurementImportPreviewMaintenance.ConnectionBoundary(
                    config.GetConnectionString("ProcurementImportMaintenance"),
                    config.GetConnectionString("Procurement"), config.GetConnectionString("Hrm"))
                || http.RequestServices.GetService<ProcurementImportMaintenanceHealth>()
                    ?.IsHealthy(DateTimeOffset.UtcNow) != true) return false;
            if (!await ProcurementImportPreviewMaintenance.RoleReadyAsync(config, ct)) return false;
            if (!await ProcurementHumanCsvExport.NativeReaderReadyAsync(db, ct)) return false;
            var procurement = await db.Database.SqlQueryRaw<bool>("""
                SELECT has_schema_privilege(current_user,'procurement','USAGE')
                   AND has_table_privilege(current_user,'procurement.import_batches','SELECT')
                   AND has_table_privilege(current_user,'procurement.import_batches','INSERT')
                   AND NOT has_table_privilege(current_user,'procurement.import_batches','DELETE')
                   AND NOT has_table_privilege(current_user,'procurement.import_batches','TRUNCATE')
                   AND COALESCE((SELECT bool_and(
                       (a.attname = ANY(ARRAY['state','applied_rows','commit_command_key',
                           'commit_request_sha256','result_text','result_sha256',
                           'version','updated_at','updated_by']::text[]))
                       = has_column_privilege(current_user,'procurement.import_batches',a.attname,'UPDATE'))
                       AND count(*) FILTER (WHERE a.attname = ANY(ARRAY[
                           'state','applied_rows','commit_command_key','commit_request_sha256',
                           'result_text','result_sha256','version','updated_at','updated_by']::text[])) = 9
                       FROM pg_attribute a WHERE a.attrelid='procurement.import_batches'::regclass
                           AND a.attnum > 0 AND NOT a.attisdropped),false)
                   AND has_table_privilege(current_user,'procurement.import_row_receipts','SELECT')
                   AND has_table_privilege(current_user,'procurement.import_row_receipts','INSERT')
                   AND NOT has_table_privilege(current_user,'procurement.import_row_receipts','UPDATE')
                   AND NOT has_table_privilege(current_user,'procurement.import_row_receipts','DELETE')
                   AND NOT has_table_privilege(current_user,'procurement.import_row_receipts','TRUNCATE')
                   AND NOT EXISTS (SELECT 1 FROM pg_attribute a
                       WHERE a.attrelid='procurement.import_row_receipts'::regclass
                           AND a.attnum > 0 AND NOT a.attisdropped
                           AND has_column_privilege(current_user,'procurement.import_row_receipts',a.attname,'UPDATE'))
                   AND has_table_privilege(current_user,'procurement.audit_events','INSERT')
                   AND has_table_privilege(current_user,'procurement.suppliers','SELECT')
                   AND has_table_privilege(current_user,'procurement.suppliers','INSERT')
                   AND has_table_privilege(current_user,'procurement.catalog_items','SELECT')
                   AND has_table_privilege(current_user,'procurement.catalog_items','INSERT') AS "Value"
                """).SingleAsync(ct);
            if (!procurement) return false;
            if (!SameDatabase(db, core)) return false;
            var coreIdentity = await core.Database.SqlQueryRaw<bool>("""
                SELECT has_schema_privilege(current_user,'hrm','USAGE')
                   AND has_column_privilege(current_user,'hrm.local_users','tenant_id','SELECT')
                   AND has_column_privilege(current_user,'hrm.local_users','id','SELECT')
                   AND has_column_privilege(current_user,'hrm.local_users','is_active','SELECT')
                   AND has_column_privilege(current_user,'hrm.local_users','is_archived','SELECT')
                   AND has_column_privilege(current_user,'hrm.local_users','roles_csv','SELECT')
                   AND has_column_privilege(current_user,'hrm.local_users','worker_id','SELECT')
                   AND has_column_privilege(current_user,'hrm.local_users','locked_until','SELECT')
                   AND EXISTS (SELECT 1 FROM pg_attribute a
                       WHERE a.attrelid='hrm.local_users'::regclass AND a.attnum > 0 AND NOT a.attisdropped
                           AND has_column_privilege(current_user,'hrm.local_users',a.attname,'UPDATE'))
                   AND has_column_privilege(current_user,'hrm.tenant_role_assignments','tenant_id','SELECT')
                   AND has_column_privilege(current_user,'hrm.tenant_role_assignments','role_key','SELECT')
                   AND has_column_privilege(current_user,'hrm.tenant_role_assignments','active','SELECT')
                   AND has_column_privilege(current_user,'hrm.tenant_role_assignments','is_archived','SELECT')
                   AND has_column_privilege(current_user,'hrm.tenant_role_assignments','permissions_csv','SELECT')
                   AND EXISTS (SELECT 1 FROM pg_attribute a
                       WHERE a.attrelid='hrm.tenant_role_assignments'::regclass
                           AND a.attnum > 0 AND NOT a.attisdropped
                           AND has_column_privilege(current_user,'hrm.tenant_role_assignments',a.attname,'UPDATE'))
                   AND has_table_privilege(current_user,'hrm.legal_entities','SELECT')
                   AND has_table_privilege(current_user,'hrm.hr_user_branch_assignments','SELECT')
                   AND has_table_privilege(current_user,'hrm.work_locations','SELECT')
                   AND has_table_privilege(current_user,'hrm.org_units','SELECT')
                   AS "Value"
                """).SingleAsync(ct);
            if (!coreIdentity) return false;
            // Both master kinds pin the selected Core company row. Vendor also
            // compares its currency; the Record role receives no HRM grant.
            return await core.Database.SqlQueryRaw<bool>("""
                SELECT has_column_privilege(current_user,'hrm.legal_entities','tenant_id','SELECT')
                   AND has_column_privilege(current_user,'hrm.legal_entities','id','SELECT')
                   AND has_column_privilege(current_user,'hrm.legal_entities','is_archived','SELECT')
                   AND has_column_privilege(current_user,'hrm.legal_entities','currency','SELECT')
                   AND EXISTS (SELECT 1 FROM pg_attribute a
                       WHERE a.attrelid='hrm.legal_entities'::regclass
                           AND a.attnum > 0 AND NOT a.attisdropped
                           AND has_column_privilege(current_user,'hrm.legal_entities',a.attname,'UPDATE'))
                   AS "Value"
                """).SingleAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }

    private static bool SameDatabase(ProcurementDbContext db, HrmDbContext core) =>
        db.Database.GetDbConnection() is NpgsqlConnection procurementConnection
        && core.Database.GetDbConnection() is NpgsqlConnection identity
        && string.Equals(procurementConnection.Host, identity.Host, StringComparison.OrdinalIgnoreCase)
        && procurementConnection.Port == identity.Port
        && string.Equals(procurementConnection.Database, identity.Database, StringComparison.Ordinal);

    // Read-only result recovery survives a paused/new-work-disabled import service.
    // Current import and native create/manage policies and Core scope middleware
    // still run before this method; no stored result is an authorization token.
    private static async Task<bool> RecoveryReady(ProcurementDbContext db, HrmDbContext core,
        CancellationToken ct)
    {
        try
        {
            if (!SameDatabase(db, core)
                || !await ProcurementHumanCsvExport.NativeReaderReadyAsync(db, ct)) return false;
            return await db.Database.SqlQueryRaw<bool>("""
                SELECT has_schema_privilege(current_user,'procurement','USAGE')
                   AND has_table_privilege(current_user,'procurement.import_batches','SELECT')
                   AND has_table_privilege(current_user,'procurement.import_row_receipts','SELECT')
                   AS "Value"
                """).SingleAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }

    private static async Task<IResult> Schema(ProcurementDbContext db, HrmDbContext core, IConfiguration config,
        HttpContext http, string kind, CancellationToken ct)
    {
        if (!await Ready(db, core, config, http, ct)) return Unavailable(http);
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { schemaVersion = ProcurementMasterImportContracts.SchemaVersion,
            kind = kind == "Vendor" ? "vendors" : "items",
            fields = ProcurementMasterImportContracts.Fields(kind).Select(key => new
            { key, label = key, required = ProcurementMasterImportContracts.Required(kind).Contains(key) }).ToArray() });
    }

    private static async Task<IResult> Template(ProcurementDbContext db, HrmDbContext core, IConfiguration config,
        HttpContext http, string kind, CancellationToken ct)
    {
        if (!await Ready(db, core, config, http, ct)) return Unavailable(http);
        http.Response.Headers.CacheControl = "no-store";
        var columns = ProcurementMasterImportContracts.Fields(kind);
        var content = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes(string.Join(',', columns) + "\r\n")).ToArray();
        return Results.File(content, "text/csv; charset=utf-8",
            kind == "Vendor" ? "vendors-import-template.csv" : "items-import-template.csv");
    }

    internal static async Task<(T? Value, IResult? Error)> ReadJson<T>(HttpContext http, CancellationToken ct)
    {
        if (http.Request.ContentType is null || !http.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            return (default, Error(StatusCodes.Status415UnsupportedMediaType, "invalid-import-content-type"));
        if (http.Request.ContentLength > ProcurementMasterImportContracts.MaxJsonBytes)
            return (default, Error(StatusCodes.Status413PayloadTooLarge, "import-limit-exceeded"));
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        while (true)
        {
            var count = await http.Request.Body.ReadAsync(block, ct);
            if (count == 0) break;
            if (buffer.Length + count > ProcurementMasterImportContracts.MaxJsonBytes)
                return (default, Error(StatusCodes.Status413PayloadTooLarge, "import-limit-exceeded"));
            buffer.Write(block, 0, count);
        }
        try
        {
            var value = JsonSerializer.Deserialize<T>(buffer.ToArray(), ProcurementMasterImportContracts.Json);
            return value is null ? (default, Error(422, "validation-failed")) : (value, null);
        }
        catch (JsonException) { return (default, Error(422, "validation-failed")); }
    }

    private static async Task<IResult> Preview(ProcurementDbContext db, HrmDbContext core,
        IConfiguration config, ProcurementRequestScope scope, HttpContext http, string kind, CancellationToken ct)
    {
        if (!await Ready(db, core, config, http, ct)) return Unavailable(http);
        var (request, bodyError) = await ReadJson<MasterImportPreviewRequest>(http, ct);
        if (bodyError is not null) return bodyError;
        var (document, bytes, parseCode) = ProcurementMasterImportContracts.Parse(request, kind);
        if (parseCode is not null) return Error(parseCode == "import-limit-exceeded" ? 413 : 422, parseCode);
        var source = request!;
        var csv = document!;
        var fields = csv.Rows.Select((row, index) => new MappedRow(index + 2, row.StartLine,
            row.EndLine, ProcurementMasterImportContracts.MapRow(csv, row, source.Mapping))).ToArray();
        var currency = kind == "Vendor"
            ? await ProcurementCurrencyDefaults.ForSelectedCompanyAsync(core, scope, ct) : null;
        MasterImportRow[] rows;
        try { rows = await EvaluateRows(db, kind, fields, source.DuplicatePolicy,
            source.AcknowledgedSimilarityRows ?? [], currency, ct); }
        catch (InvalidOperationException) { return Error(422, "validation-failed"); }
        var snapshot = new MasterImportSnapshot(ProcurementMasterImportContracts.SchemaVersion,
            scope.TenantId, scope.LegalEntityId, scope.SubjectId, kind,
            ProcurementMasterImportContracts.Hash(bytes!), source.FileName, source.DuplicatePolicy,
            ProcurementMasterImportContracts.SortMapping(source.Mapping),
            (source.AcknowledgedSimilarityRows ?? []).Order().ToArray(), currency, rows);
        var snapshotText = ProcurementMasterImportContracts.SnapshotText(snapshot);
        var digest = ProcurementMasterImportContracts.Hash(snapshotText);
        var batch = new ImportBatch { RecordType = kind, FileName = source.FileName,
            FileSha256 = snapshot.FileSha256, ActorSubjectId = scope.SubjectId,
            SchemaVersion = snapshot.SchemaVersion, DuplicatePolicy = source.DuplicatePolicy,
            PreviewSnapshotText = snapshotText, PreviewSha256 = digest,
            TotalRows = rows.Length, AppliedRows = 0, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            State = "Previewed" };
        // One tenant/company advisory transaction lock serializes quota admission
        // across actors and API instances. Count expired-but-unswept mapped input;
        // the quota cannot be evaded by a stalled maintenance worker.
        var limits = ProcurementImportPreviewLimits.Read(config)!;
        var snapshotBytes = Encoding.UTF8.GetByteCount(snapshotText);
        try
        {
            await using var quotaTx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using (var gate = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(1734702991,hashtext(@scope))", connection,
                (NpgsqlTransaction)quotaTx.GetDbTransaction()))
            {
                gate.Parameters.AddWithValue("scope", scope.TenantId + "|" + scope.LegalEntityId.ToString("D"));
                await gate.ExecuteNonQueryAsync(ct);
            }
            // The quota wait can outlive the middleware's Core decision. Pin
            // current company, actor and role rows until this preview commits.
            await using var coreTx = await core.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await LockedCurrency(core, scope, ct);
            if (!await LockedCurrentGrant(core, scope, kind, ct)
                || !await CurrentScopeAllowed(core, scope, ct))
                return Error(403, "forbidden");
            long actorCount;
            long companyBytes;
            await using (var count = new NpgsqlCommand("""
                SELECT count(*) FILTER (WHERE actor_subject_id=@actor),
                       COALESCE(sum(octet_length(preview_snapshot_text)),0)
                  FROM procurement.import_batches
                 WHERE tenant_id=@tenant AND legal_entity_id=@company
                   AND preview_snapshot_text IS NOT NULL AND state<>'Committed'
                """, connection, (NpgsqlTransaction)quotaTx.GetDbTransaction()))
            {
                count.Parameters.AddWithValue("actor", scope.SubjectId);
                count.Parameters.AddWithValue("tenant", scope.TenantId);
                count.Parameters.AddWithValue("company", scope.LegalEntityId);
                await using var result = await count.ExecuteReaderAsync(ct);
                await result.ReadAsync(ct);
                actorCount = result.GetInt64(0);
                companyBytes = result.GetInt64(1);
            }
            if (!ProcurementImportPreviewLimits.Admits(actorCount,
                    companyBytes, snapshotBytes, limits))
            {
                http.Response.Headers.CacheControl = "no-store";
                return Error(StatusCodes.Status429TooManyRequests, "import-staging-full");
            }
            db.AddScoped(batch);
            await db.SaveChangesAsync(ct);
            await quotaTx.CommitAsync(ct);
            await coreTx.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(http); }
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(ProcurementMasterImportContracts.PreviewView(batch.Id, digest, batch.ExpiresAt, rows));
    }

    private static async Task<MasterImportRow[]> EvaluateRows(ProcurementDbContext db, string kind,
        IReadOnlyList<MappedRow> source, string policy, int[] acknowledgements,
        string? companyCurrency, CancellationToken ct)
    {
        var candidates = await LoadCandidates(db, kind, source, ct);
        return EvaluateRowsAgainstCandidates(kind, source, policy, acknowledgements, companyCurrency, candidates);
    }

    internal static MasterImportRow[] EvaluateRowsAgainstCandidates(string kind,
        IReadOnlyList<MappedRow> source, string policy, int[] acknowledgements,
        string? companyCurrency, CandidateSet candidates)
    {
        var acceptedAcks = new HashSet<int>(acknowledgements);
        var usedAcks = new HashSet<int>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var seenLegalNames = new HashSet<string>(StringComparer.Ordinal);
        var seenDisplayNames = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<MasterImportRow>(source.Count);
        foreach (var row in source)
        {
            var fields = row.Fields;
            string? error = kind == "Vendor"
                ? ProcurementMasterDraftRules.ValidateVendor(ProcurementMasterDraftRules.ImportVendor(fields, companyCurrency))
                : ProcurementMasterDraftRules.ValidateItem(ProcurementMasterDraftRules.ImportItem(fields));
            var key = "";
            var naturalMatches = Array.Empty<Guid>();
            var similarMatches = Array.Empty<Guid>();
            var similarWithinFile = false;
            if (kind == "Vendor")
            {
                var supplied = fields.GetValueOrDefault("registrationNumber");
                var registration = ProcurementMasterDraftRules.NormalizeRegistration(supplied);
                if (!string.IsNullOrWhiteSpace(supplied) && registration is null)
                    error ??= "A supplied company ID must contain a letter or digit.";
                var country = fields.GetValueOrDefault("countryCode")?.ToUpperInvariant() ?? "";
                if (registration is not null)
                {
                    key = country + "|" + registration;
                    naturalMatches = candidates.Vendors.Where(x => x.CountryCode == country
                        && x.NormalizedRegistration == registration).Select(x => x.Id).ToArray();
                }
                var legal = fields.GetValueOrDefault("legalName")?.ToLowerInvariant() ?? "";
                var display = fields.GetValueOrDefault("displayName")?.ToLowerInvariant() ?? "";
                similarMatches = candidates.Vendors.Where(x => x.LegalName.ToLowerInvariant() == legal
                    || x.DisplayName.ToLowerInvariant() == display).Select(x => x.Id).ToArray();
                var repeatedLegal = !seenLegalNames.Add(legal);
                var repeatedDisplay = !seenDisplayNames.Add(display);
                similarWithinFile = repeatedLegal || repeatedDisplay;
            }
            else
            {
                key = fields.GetValueOrDefault("code")?.ToUpperInvariant() ?? "";
                naturalMatches = candidates.Items.Where(x => x.Code == key).Select(x => x.Id).ToArray();
            }
            var repeated = key.Length > 0 && !seenKeys.Add(key);
            var similarity = kind == "Vendor" && naturalMatches.Length == 0
                && (similarMatches.Length > 0 || similarWithinFile);
            var matchHash = ProcurementMasterImportContracts.Hash(string.Join(',',
                naturalMatches.Concat(similarMatches).Distinct().Order().Select(x => x.ToString("N"))));
            string disposition = "create";
            string? code = null;
            string? message = null;
            if (error is not null)
            {
                disposition = "error"; code = "validation-failed"; message = error;
            }
            else if (naturalMatches.Length > 0 || repeated)
            {
                disposition = policy == "skip" ? "skip" : "error";
                code = "duplicate-master";
                message = "A record with this exact identity already exists or occurs earlier in the file.";
            }
            else if (similarity)
            {
                if (acceptedAcks.Contains(row.RowNumber)) usedAcks.Add(row.RowNumber);
                else
                {
                    disposition = "error"; code = "similar-vendor";
                    message = "A vendor may already exist. Review and acknowledge the similarity or use individual intake.";
                }
            }
            rows.Add(new MasterImportRow(row.RowNumber, row.StartLine, row.EndLine,
                fields, disposition, code, message, matchHash));
        }
        if (!acceptedAcks.SetEquals(usedAcks)) throw new InvalidOperationException("Similarity acknowledgement changed.");
        return rows.ToArray();
    }

    private static async Task<CandidateSet> LoadCandidates(ProcurementDbContext db, string kind,
        IReadOnlyList<MappedRow> rows, CancellationToken ct)
    {
        if (kind == "Item")
        {
            var codes = rows.Select(x => x.Fields.GetValueOrDefault("code")?.ToUpperInvariant() ?? "")
                .Where(x => x.Length > 0).Distinct().ToArray();
            var items = await db.CatalogItems.AsNoTracking().Where(x => codes.Contains(x.Code))
                .Select(x => new ItemCandidate(x.Id, x.Code)).ToArrayAsync(ct);
            return new CandidateSet([], items);
        }
        var registrations = rows.Select(x => ProcurementMasterDraftRules.NormalizeRegistration(
                x.Fields.GetValueOrDefault("registrationNumber")))
            .Where(x => x is not null).Select(x => x!).Distinct().ToArray();
        var names = rows.SelectMany(x => new[] { x.Fields.GetValueOrDefault("legalName"),
                x.Fields.GetValueOrDefault("displayName") })
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.ToLowerInvariant()).Distinct().ToArray();
        var vendors = await db.Suppliers.AsNoTracking()
            .Where(x => (x.NormalizedRegistration != null && registrations.Contains(x.NormalizedRegistration))
                || names.Contains(x.LegalName.ToLower()) || names.Contains(x.DisplayName.ToLower()))
            .Select(x => new VendorCandidate(x.Id, x.CountryCode, x.NormalizedRegistration,
                x.DisplayName, x.LegalName)).Take(10001).ToArrayAsync(ct);
        if (vendors.Length == 10001) throw new InvalidOperationException("Candidate lookup exceeds the finite limit.");
        return new CandidateSet(vendors, []);
    }

    private static async Task<IResult> Commit(ProcurementDbContext db, HrmDbContext core, IConfiguration config,
        ProcurementRequestScope scope, HttpContext http, string kind, CancellationToken ct)
    {
        if (!await Ready(db, core, config, http, ct)) return Unavailable(http);
        var (request, bodyError) = await ReadJson<MasterImportCommitRequest>(http, ct);
        if (bodyError is not null) return bodyError;
        if (request!.BatchId == Guid.Empty || request.CommandKey == Guid.Empty
            || !ProcurementMasterImportContracts.IsHash(request.PreviewSha256))
            return Error(422, "validation-failed");
        var commandDigest = ProcurementMasterImportContracts.Hash(
            $"{request.BatchId:D}|{request.PreviewSha256}|{request.CommandKey:D}");
        try
        {
            // Core owns currency and holds its company row until the Procurement
            // serializable write commits. Both connections must address one PG DB.
            await using var coreTx = await core.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var selectedCurrency = await LockedCurrency(core, scope, ct);
            var currency = kind == "Vendor" ? selectedCurrency : null;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await LockBatch(db, scope, kind, request.BatchId, ct)) return Error(404, "import-not-found");
            if (!await LockedCurrentGrant(core, scope, kind, ct)
                || !await CurrentScopeAllowed(core, scope, ct))
                return Error(403, "forbidden");
            var batch = await db.ImportBatches.SingleOrDefaultAsync(x => x.Id == request.BatchId, ct);
            if (batch is null || batch.RecordType != kind || batch.ActorSubjectId != scope.SubjectId)
                return Error(404, "import-not-found");
            var reused = await db.ImportBatches.AsNoTracking().SingleOrDefaultAsync(
                x => x.CommitCommandKey == request.CommandKey, ct);
            if (reused is not null && reused.Id != batch.Id) return Error(409, "import-conflict");
            if (batch.State == "Committed")
            {
                if (batch.CommitCommandKey != request.CommandKey || batch.CommitRequestSha256 != commandDigest
                    || batch.PreviewSha256 != request.PreviewSha256
                    || !await ValidSavedResult(db, batch, ct))
                    return Error(409, "import-conflict");
                http.Response.Headers.CacheControl = "no-store";
                return Results.Bytes(Encoding.UTF8.GetBytes(batch.ResultText!), "application/json");
            }
            if (batch.State != "Previewed" || batch.ExpiresAt <= DateTimeOffset.UtcNow)
                return Error(409, "import-preview-expired");
            if (batch.PreviewSha256 != request.PreviewSha256
                || !ProcurementMasterImportContracts.IsHash(batch.PreviewSha256)
                || batch.PreviewSnapshotText is null
                || ProcurementMasterImportContracts.Hash(batch.PreviewSnapshotText) != batch.PreviewSha256)
                return Error(409, "import-conflict");
            var snapshot = ProcurementMasterImportContracts.ReadSnapshot(batch.PreviewSnapshotText);
            if (snapshot is null || snapshot.ActorSubjectId != scope.SubjectId || snapshot.TenantId != scope.TenantId
                || snapshot.CompanyId != scope.LegalEntityId || snapshot.Kind != kind
                || snapshot.SchemaVersion != ProcurementMasterImportContracts.SchemaVersion
                || snapshot.FileSha256 != batch.FileSha256 || snapshot.Rows.Length != batch.TotalRows
                || snapshot.Rows.Any(x => x.Disposition == "error"))
                return Error(409, "import-conflict");

            if (currency != snapshot.CompanyCurrencyCode) return Error(409, "import-preview-stale");
            var mapped = snapshot.Rows.Select(x => new MappedRow(x.RowNumber, x.StartLine, x.EndLine,
                x.Fields)).ToArray();
            var current = await EvaluateRows(db, kind, mapped, snapshot.DuplicatePolicy,
                snapshot.AcknowledgedSimilarityRows, currency, ct);
            if (current.Length != snapshot.Rows.Length || current.Where((x, i) =>
                x.Disposition != snapshot.Rows[i].Disposition || x.Code != snapshot.Rows[i].Code
                || x.MatchSetSha256 != snapshot.Rows[i].MatchSetSha256).Any())
                return Error(409, "import-preview-stale");

            var committed = new List<object>(current.Length);
            var created = 0;
            var skipped = 0;
            foreach (var row in current)
            {
                Guid? recordId = null;
                if (row.Disposition == "create")
                {
                    if (kind == "Vendor")
                    {
                        var input = ProcurementMasterDraftRules.ImportVendor(row.Fields, currency);
                        var supplier = ProcurementMasterDraftRules.NewVendor(input, scope);
                        db.AddScoped(supplier);
                        recordId = supplier.Id;
                    }
                    else
                    {
                        var input = ProcurementMasterDraftRules.ImportItem(row.Fields);
                        var item = ProcurementMasterDraftRules.NewItem(input);
                        db.AddScoped(item);
                        recordId = item.Id;
                    }
                    created++;
                }
                else skipped++;
                db.AddScoped(new ImportRowReceipt { ImportBatchId = batch.Id, RowNumber = row.RowNumber,
                    StartLine = row.StartLine, EndLine = row.EndLine, Disposition = row.Disposition,
                    CreatedRecordId = recordId,
                    RowSha256 = ProcurementMasterImportContracts.Hash(
                        JsonSerializer.Serialize(row.Fields, ProcurementMasterImportContracts.Json)) });
                committed.Add(new { rowNumber = row.RowNumber, disposition = row.Disposition, recordId });
            }
            var resultText = JsonSerializer.Serialize(new { batchId = batch.Id, created, skipped,
                rows = committed }, ProcurementMasterImportContracts.Json);
            batch.State = "Committed";
            batch.AppliedRows = created;
            batch.CommitCommandKey = request.CommandKey;
            batch.CommitRequestSha256 = commandDigest;
            batch.ResultText = resultText;
            batch.ResultSha256 = ProcurementMasterImportContracts.Hash(resultText);
            db.AddScoped(new AuditEvent { RecordType = "ImportBatch", RecordId = batch.Id,
                Action = "ImportCommitted", ActorSubjectId = scope.SubjectId,
                ChangedFieldsJson = JsonSerializer.Serialize(new { kind, created, skipped, batch.TotalRows }),
                CorrelationId = scope.CorrelationId });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await coreTx.CommitAsync(ct);
            http.Response.Headers.CacheControl = "no-store";
            return Results.Bytes(Encoding.UTF8.GetBytes(resultText), "application/json");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (DbUpdateException error) when (error.InnerException is PostgresException pg
            && (pg.SqlState is "42501" or "42P01" or "42703")) { return Unavailable(http); }
        catch (DbUpdateException) { return Error(409, "import-conflict"); }
        catch (PostgresException error) when (error.SqlState is "40001" or "40P01" or "23505")
        { return Error(409, "import-conflict"); }
        catch (PostgresException error) when (error.SqlState is "42501" or "42P01" or "42703")
        { return Unavailable(http); }
        catch (InvalidOperationException) { return Error(409, "import-preview-stale"); }
    }

    private static async Task<bool> ValidSavedResult(ProcurementDbContext db,
        ImportBatch batch, CancellationToken ct)
    {
        var receipts = await db.ImportRowReceipts.AsNoTracking()
            .Where(x => x.ImportBatchId == batch.Id).ToArrayAsync(ct);
        return ProcurementDbContext.ValidMasterImportResult(batch, receipts);
    }

    private static async Task<bool> LockBatch(ProcurementDbContext db, ProcurementRequestScope scope,
        string kind, Guid batchId, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            SELECT id FROM procurement.import_batches
            WHERE id=@id AND tenant_id=@tenant AND legal_entity_id=@company
              AND actor_subject_id=@actor AND record_type=@kind FOR UPDATE
            """;
        void Parameter(string name, object value)
        {
            var p = command.CreateParameter(); p.ParameterName = name; p.Value = value; command.Parameters.Add(p);
        }
        Parameter("id", batchId); Parameter("tenant", scope.TenantId);
        Parameter("company", scope.LegalEntityId); Parameter("actor", scope.SubjectId); Parameter("kind", kind);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<string?> LockedCurrency(HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        await using var command = core.Database.GetDbConnection().CreateCommand();
        command.Transaction = core.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            SELECT currency FROM hrm.legal_entities
            WHERE tenant_id=@tenant AND id=@company AND is_archived=false FOR SHARE
            """;
        var tenant = command.CreateParameter(); tenant.ParameterName = "tenant";
        tenant.Value = scope.TenantId; command.Parameters.Add(tenant);
        var company = command.CreateParameter(); company.ParameterName = "company";
        company.Value = scope.LegalEntityId; command.Parameters.Add(company);
        var value = await command.ExecuteScalarAsync(ct);
        if (value is null or DBNull) throw new InvalidOperationException("Selected company is unavailable.");
        return string.IsNullOrWhiteSpace((string)value) ? null : ((string)value).Trim().ToUpperInvariant();
    }

    private static bool HasImportActions(ProcurementCorePrincipal principal, string kind) =>
        principal.Actions.Contains("procurement-access", StringComparer.Ordinal)
        && principal.Actions.Contains(kind == "Vendor" ? "procurement-vendor-import" : "procurement-item-import",
            StringComparer.Ordinal)
        && principal.Actions.Contains(kind == "Vendor" ? "procurement-vendor-create" : "procurement-item-manage",
            StringComparer.Ordinal);

    private static async Task<bool> LockedCurrentGrant(HrmDbContext core,
        ProcurementRequestScope scope, string kind, CancellationToken ct)
    {
        if (!Guid.TryParse(scope.SubjectId, out var subject) || subject == Guid.Empty) return false;
        // Hold Core actor and assigned role rows until the Procurement commit.
        // A revoked user or permission while waiting for the batch lock is denied.
        await using var user = core.Database.GetDbConnection().CreateCommand();
        user.Transaction = core.Database.CurrentTransaction!.GetDbTransaction();
        user.CommandText = """
            SELECT roles_csv FROM hrm.local_users
            WHERE tenant_id=@tenant AND id=@actor AND is_active=true AND is_archived=false FOR SHARE
            """;
        var tenant = user.CreateParameter(); tenant.ParameterName = "tenant";
        tenant.Value = scope.TenantId; user.Parameters.Add(tenant);
        var actor = user.CreateParameter(); actor.ParameterName = "actor";
        actor.Value = subject; user.Parameters.Add(actor);
        var rolesCsv = await user.ExecuteScalarAsync(ct) as string;
        if (rolesCsv is null) return false;
        var assigned = LocalIdentityRoutes.ParseRoles(rolesCsv);
        if (assigned.Length == 0) return false;
        await using var roles = core.Database.GetDbConnection().CreateCommand();
        roles.Transaction = core.Database.CurrentTransaction!.GetDbTransaction();
        roles.CommandText = """
            SELECT role_key FROM hrm.tenant_role_assignments
            WHERE tenant_id=@tenant AND role_key = ANY(@roles) FOR SHARE
            """;
        var roleTenant = roles.CreateParameter(); roleTenant.ParameterName = "tenant";
        roleTenant.Value = scope.TenantId; roles.Parameters.Add(roleTenant);
        var keys = roles.CreateParameter(); keys.ParameterName = "roles";
        keys.Value = assigned; roles.Parameters.Add(keys);
        var locked = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await roles.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) locked.Add(reader.GetString(0));
        var current = await ProcurementCorePrincipals.CurrentAsync(core, scope.TenantId, scope.SubjectId, ct);
        return current is not null && current.Roles.All(locked.Contains)
            && HasImportActions(current, kind);
    }

    internal static async Task<bool> CurrentScopeAllowed(HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (scope.PrincipalId is not Guid principalId) return false;
        // Re-read the actual Core company/branch assignment after any wait for
        // the batch lock. A previous shell selection is never a lasting grant.
        if (!await core.LegalEntities.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
            && x.Id == scope.LegalEntityId && !x.IsArchived, ct)) return false;
        var assigned = await core.UserBranchAssignments.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.UserId == principalId && !x.IsArchived)
            .Select(x => x.LocationId).Distinct().ToArrayAsync(ct);
        var companyBranches = await core.WorkLocations.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId
                && !x.IsArchived && (assigned.Length == 0 || assigned.Contains(x.Id)))
            .Select(x => x.Id).ToArrayAsync(ct);
        if (assigned.Length > 0 && companyBranches.Length == 0) return false;
        if (scope.WorkLocationId is Guid branch && !companyBranches.Contains(branch)) return false;
        if (scope.OrgUnitId is Guid orgUnit && (assigned.Length > 0
            || !await core.OrgUnits.AsNoTracking().AnyAsync(x => x.TenantId == scope.TenantId
                && x.Id == orgUnit && x.LegalEntityId == scope.LegalEntityId
                && !x.IsArchived && x.Status == "active", ct))) return false;
        return true;
    }

    private static async Task<IResult> Recover(Guid batchId, ProcurementDbContext db,
        HrmDbContext core, IConfiguration config,
        ProcurementRequestScope scope, HttpContext http, string kind, CancellationToken ct)
    {
        if (!await RecoveryReady(db, core, ct)) return Unavailable(http);
        var batch = await db.ImportBatches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == batchId
            && x.ActorSubjectId == scope.SubjectId && x.RecordType == kind, ct);
        if (batch is null) return Error(404, "import-not-found");
        // Only the database's serialized sweep outcome, never the client clock,
        // permits the pending command journal to resolve as terminal Expired.
        if (IsTerminalExpired(batch))
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { state = "Expired", batchId });
        }
        if (batch.PreviewSnapshotText is null || batch.PreviewSha256 is null
            || ProcurementMasterImportContracts.Hash(batch.PreviewSnapshotText) != batch.PreviewSha256)
            return Unavailable(http);
        http.Response.Headers.CacheControl = "no-store";
        if (batch.State == "Committed")
        {
            if (!await ValidSavedResult(db, batch, ct)) return Unavailable(http);
            return Results.Ok(new { state = "Committed", batchId, result =
                JsonSerializer.Deserialize<JsonElement>(batch.ResultText!) });
        }
        // An expired Previewed row is not a terminal write outcome: a commit
        // may already hold its row lock and finish after this read. Retain the
        // typed preview with its past expiresAt so clients keep uncertain work
        // pending instead of clearing the command journal on a false Expired.
        if (batch.State != "Previewed") return Unavailable(http);
        var snapshot = ProcurementMasterImportContracts.ReadSnapshot(batch.PreviewSnapshotText);
        if (snapshot is null || snapshot.TenantId != scope.TenantId || snapshot.CompanyId != scope.LegalEntityId
            || snapshot.ActorSubjectId != scope.SubjectId || snapshot.Kind != kind)
            return Unavailable(http);
        return Results.Ok(ProcurementMasterImportContracts.PreviewView(batch.Id,
            batch.PreviewSha256, batch.ExpiresAt, snapshot.Rows));
    }

    internal static bool IsTerminalExpired(ImportBatch batch) =>
        batch.State == "Expired" && batch.SchemaVersion == ProcurementMasterImportContracts.SchemaVersion
        && batch.PreviewSnapshotText is null && batch.PreviewSha256 is not null
        && ProcurementMasterImportContracts.IsHash(batch.PreviewSha256)
        && batch.CommitCommandKey is null && batch.CommitRequestSha256 is null
        && batch.ResultText is null && batch.ResultSha256 is null && batch.ResultJson is null
        && batch.AppliedRows == 0 && batch.TotalRows > 0;
}
