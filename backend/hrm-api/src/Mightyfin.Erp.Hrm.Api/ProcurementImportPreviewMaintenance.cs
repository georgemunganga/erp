using System.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Mightyfin.Erp.Hrm.Api.Routing;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Engineering limits for transient mapped input, not business-record retention.</summary>
public sealed record ProcurementImportPreviewLimits(int PerActor, long CompanyBytes)
{
    public const int DefaultPerActor = 3;
    public const long DefaultCompanyBytes = 64L * 1024 * 1024;

    public static ProcurementImportPreviewLimits? Read(IConfiguration config)
    {
        try
        {
            var actor = config.GetValue("Procurement:MasterImport:MaxStagedPerActor", DefaultPerActor);
            var bytes = config.GetValue("Procurement:MasterImport:MaxStagedCompanyBytes", DefaultCompanyBytes);
            return actor is >= 1 and <= 10 && bytes is >= 5_242_880 and <= 268_435_456
                ? new(actor, bytes) : null;
        }
        catch (InvalidOperationException) { return null; }
    }

    public static bool Admits(long actorCount, long companyBytes, int snapshotBytes,
        ProcurementImportPreviewLimits limits) =>
        actorCount >= 0 && companyBytes >= 0 && snapshotBytes > 0
        && actorCount < limits.PerActor
        && companyBytes <= limits.CompanyBytes - snapshotBytes;
}

/// <summary>Only a recent successful finite-role sweep admits new preview writes.</summary>
public sealed class ProcurementImportMaintenanceHealth : IHealthCheck
{
    private long lastSuccessTicks;
    public void MarkSuccess(DateTimeOffset time) => Interlocked.Exchange(ref lastSuccessTicks, time.UtcTicks);
    public void MarkFailure() => Interlocked.Exchange(ref lastSuccessTicks, 0);
    public bool IsHealthy(DateTimeOffset now) =>
        Interlocked.Read(ref lastSuccessTicks) is var ticks && ticks > 0
        && now.UtcTicks >= ticks && now.UtcTicks - ticks <= TimeSpan.FromMinutes(3).Ticks;
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(IsHealthy(DateTimeOffset.UtcNow)
            ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Import preview maintenance unavailable."));
}

/// <summary>
/// Clears only expired, uncommitted mapped input using a separate finite database role.
/// Committed import history, receipts, masters, and original CSV are outside this worker.
/// </summary>
public sealed class ProcurementImportPreviewMaintenance(
    IConfiguration config, ProcurementImportMaintenanceHealth health,
    ILogger<ProcurementImportPreviewMaintenance> logger) : BackgroundService
{
    internal const string ServiceActor = "procurement-import-maintenance";
    internal const int SweepLimit = 50;

    internal const string AdmissionSql = """
        SELECT NOT r.rolsuper AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolbypassrls
           AND NOT has_database_privilege(current_user,current_database(),'CREATE')
           AND has_schema_privilege(current_user,'procurement','USAGE')
           AND NOT EXISTS (SELECT 1 FROM pg_namespace s
             WHERE left(s.nspname,3) <> 'pg_'
               AND s.nspname <> 'information_schema'
               AND has_schema_privilege(current_user,s.oid,'CREATE'))
           AND has_table_privilege(current_user,'procurement.import_batches','SELECT')
           AND COALESCE((SELECT bool_and(has_column_privilege(
                 current_user,'procurement.import_batches',required.column_name,'SELECT'))
                 FROM unnest(ARRAY['id','tenant_id','legal_entity_id','schema_version',
                   'record_type','state','expires_at','preview_snapshot_text',
                   'commit_command_key','commit_request_sha256','result_text',
                   'result_sha256','total_rows']::text[]) AS required(column_name)),false)
           AND NOT has_any_column_privilege(current_user,'procurement.import_batches','INSERT')
           AND NOT has_table_privilege(current_user,'procurement.import_batches','DELETE')
           AND NOT has_table_privilege(current_user,'procurement.import_batches','TRUNCATE')
           AND COALESCE((SELECT bool_and(
                 (a.attname = ANY(ARRAY['preview_snapshot_text','state','version','updated_at','updated_by']::text[]))
                 = has_column_privilege(current_user,'procurement.import_batches',a.attname,'UPDATE'))
                 AND count(*) FILTER (WHERE a.attname = ANY(ARRAY[
                   'preview_snapshot_text','state','version','updated_at','updated_by']::text[])) = 5
                 FROM pg_attribute a WHERE a.attrelid='procurement.import_batches'::regclass
                   AND a.attnum > 0 AND NOT a.attisdropped),false)
           AND has_table_privilege(current_user,'procurement.audit_events','INSERT')
           AND COALESCE((SELECT bool_and(has_column_privilege(
                 current_user,'procurement.audit_events',required.column_name,'INSERT'))
                 FROM unnest(ARRAY['id','record_type','record_id','action',
                   'actor_subject_id','changed_fields_json','correlation_id',
                   'tenant_id','legal_entity_id','created_at','created_by',
                   'version']::text[]) AS required(column_name)),false)
           AND NOT has_any_column_privilege(current_user,'procurement.audit_events','SELECT')
           AND NOT has_any_column_privilege(current_user,'procurement.audit_events','UPDATE')
           AND NOT EXISTS (SELECT 1 FROM pg_attribute a
             WHERE a.attrelid='procurement.audit_events'::regclass
               AND a.attnum>0 AND NOT a.attisdropped
               AND has_column_privilege(current_user,'procurement.audit_events',a.attname,'UPDATE'))
           AND NOT has_table_privilege(current_user,'procurement.audit_events','DELETE')
           AND NOT has_table_privilege(current_user,'procurement.audit_events','TRUNCATE')
           AND NOT EXISTS (
             SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
             WHERE n.nspname IN ('procurement','hrm','core_approval','core_document')
               AND c.relkind IN ('r','p','v','m','f')
               AND NOT (n.nspname='procurement' AND c.relname IN
                 ('import_batches','audit_events'))
               AND (has_any_column_privilege(current_user,c.oid,'SELECT')
                 OR has_any_column_privilege(current_user,c.oid,'INSERT')
                 OR has_any_column_privilege(current_user,c.oid,'UPDATE')
                 OR has_table_privilege(current_user,c.oid,'DELETE')
                 OR has_table_privilege(current_user,c.oid,'TRUNCATE')))
           AS admitted
        FROM pg_roles r WHERE r.rolname=current_user
        """;

    // One statement, one transaction: SKIP LOCKED cannot touch an in-flight commit.
    // If the audit insert fails the state/snapshot update rolls back with it.
    internal const string SweepSql = """
        WITH picked AS MATERIALIZED (
            SELECT id, tenant_id, legal_entity_id
            FROM procurement.import_batches
            WHERE schema_version=1 AND record_type IN ('Vendor','Item')
              AND state='Previewed' AND expires_at <= clock_timestamp()
              AND preview_snapshot_text IS NOT NULL
              AND commit_command_key IS NULL AND commit_request_sha256 IS NULL
              AND result_text IS NULL AND result_sha256 IS NULL AND result_json IS NULL
              AND applied_rows=0
            ORDER BY expires_at,id
            LIMIT @limit FOR UPDATE SKIP LOCKED
        ), changed AS (
            UPDATE procurement.import_batches b
               SET preview_snapshot_text=NULL, state='Expired',
                   version=b.version+1, updated_at=clock_timestamp(), updated_by=@actor
              FROM picked p
             WHERE b.id=p.id AND b.tenant_id=p.tenant_id AND b.legal_entity_id=p.legal_entity_id
            RETURNING b.id,b.tenant_id,b.legal_entity_id,b.total_rows
        ), audited AS (
            INSERT INTO procurement.audit_events
                (id,record_type,record_id,action,actor_subject_id,changed_fields_json,
                 correlation_id,tenant_id,legal_entity_id,created_at,created_by,version)
            SELECT gen_random_uuid(),'ImportBatch',id,'PreviewExpired',@actor,
                   jsonb_build_object('reason','technical-preview-ttl','rowCount',total_rows),
                   NULL,tenant_id,legal_entity_id,clock_timestamp(),@actor,1
            FROM changed RETURNING 1 AS inserted
        )
        SELECT (SELECT count(*) FROM changed) AS changed_count,
               (SELECT count(*) FROM audited) AS audited_count
        """;

    internal static bool ConnectionBoundary(string? maintenance, string? record, string? core)
    {
        if (string.IsNullOrWhiteSpace(maintenance) || string.IsNullOrWhiteSpace(record)
            || string.IsNullOrWhiteSpace(core)) return false;
        try
        {
            var m = new NpgsqlConnectionStringBuilder(Normalize(maintenance));
            var r = new NpgsqlConnectionStringBuilder(Normalize(record));
            var c = new NpgsqlConnectionStringBuilder(Normalize(core));
            return SameDatabase(m,r) && SameDatabase(m,c)
                && !string.IsNullOrWhiteSpace(m.Username)
                && !string.IsNullOrWhiteSpace(r.Username)
                && !string.IsNullOrWhiteSpace(c.Username)
                && !string.Equals(m.Username,r.Username,StringComparison.Ordinal)
                && !string.Equals(m.Username,c.Username,StringComparison.Ordinal);
        }
        catch (ArgumentException) { return false; }
        catch (FormatException) { return false; }
    }

    private static string Normalize(string value) =>
        value.StartsWith("postgresql",StringComparison.OrdinalIgnoreCase) && value.Contains('?')
            ? NpgsqlConnectionStringNormalizer.Normalize(value) : value;
    private static string TimedConnection(string value) =>
        new NpgsqlConnectionStringBuilder(Normalize(value))
        { Timeout = 5, CommandTimeout = 15 }.ConnectionString;
    private static bool SameDatabase(NpgsqlConnectionStringBuilder a, NpgsqlConnectionStringBuilder b) =>
        string.Equals(a.Host,b.Host,StringComparison.OrdinalIgnoreCase)
        && a.Port==b.Port && string.Equals(a.Database,b.Database,StringComparison.Ordinal);

    internal static async Task<bool> RoleReadyAsync(IConfiguration configuration, CancellationToken ct)
    {
        var maintenance = configuration.GetConnectionString("ProcurementImportMaintenance");
        if (!ConnectionBoundary(maintenance, configuration.GetConnectionString("Procurement"),
                configuration.GetConnectionString("Hrm"))) return false;
        await using var connection = new NpgsqlConnection(TimedConnection(maintenance!));
        await connection.OpenAsync(ct);
        await using var admission = new NpgsqlCommand(AdmissionSql, connection) { CommandTimeout = 15 };
        return await admission.ExecuteScalarAsync(ct) is true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!config.GetValue<bool>("Procurement:MasterImport:Maintenance:Enabled")
                    || !await RoleReadyAsync(config, stoppingToken))
                    throw new InvalidOperationException("Import preview maintenance connection unavailable.");
                var maintenance = config.GetConnectionString("ProcurementImportMaintenance")!;
                await using var connection = new NpgsqlConnection(TimedConnection(maintenance!));
                await connection.OpenAsync(stoppingToken);
                await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, stoppingToken);
                await using (var lockTimeout = new NpgsqlCommand(
                    "SET LOCAL lock_timeout='2s'", connection, tx))
                    await lockTimeout.ExecuteNonQueryAsync(stoppingToken);
                await using (var statementTimeout = new NpgsqlCommand(
                    "SET LOCAL statement_timeout='15s'", connection, tx))
                    await statementTimeout.ExecuteNonQueryAsync(stoppingToken);
                await using var sweep = new NpgsqlCommand(SweepSql, connection, tx) { CommandTimeout = 15 };
                sweep.Parameters.AddWithValue("limit", SweepLimit);
                sweep.Parameters.AddWithValue("actor", ServiceActor);
                await using var reader = await sweep.ExecuteReaderAsync(stoppingToken);
                if (!await reader.ReadAsync(stoppingToken) || reader.GetInt64(0) != reader.GetInt64(1))
                    throw new InvalidOperationException("Import preview expiry audit did not match changed rows.");
                await reader.DisposeAsync();
                await tx.CommitAsync(stoppingToken);
                health.MarkSuccess(DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                health.MarkFailure();
                logger.LogError("Import preview maintenance unavailable ({ErrorType}).", exception.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
        health.MarkFailure();
    }
}
