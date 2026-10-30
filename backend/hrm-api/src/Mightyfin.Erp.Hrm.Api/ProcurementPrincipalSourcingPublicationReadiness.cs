using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Hrm.Api.Routing;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

public sealed class ProcurementPrincipalSourcingPublicationOptions
{
    public bool Enabled { get; set; }
}

public interface IProcurementPrincipalSourcingPublicationReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct);
    Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(false);
    Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct) => IsReadyAsync(owner, ct);
}

/// <summary>Fail-closed inspection on the actual finite execution connection.
/// M4's three append-only tables supplement P25/P29; they confer no Core,
/// policy, Finance, supplier delivery or business approval authority.</summary>
public sealed class ProcurementPrincipalSourcingPublicationSchemaReadiness(
    IConfiguration configuration, IProcurementScope scope,
    ProcurementPrincipalApprovalStoreReadiness p25,
    IProcurementSourcingDraftSchemaReadiness p29,
    IProcurementSourcingPolicyIdentitySchemaReadiness policy) : IProcurementPrincipalSourcingPublicationReadiness
{
    internal static readonly Type[] OwnedTypes =
        [typeof(SourcingPublicationOpening), typeof(SourcingPublicationDecision), typeof(SourcingPublicationReceipt)];
    internal static readonly string[] OwnedChecks =
        ["ck_sourcing_publication_opening_v2", "ck_sourcing_publication_decision_v2", "ck_sourcing_publication_receipt_v2"];
    internal static bool ManifestCaptured => OwnedChecks.All(M4PrincipalRfqPhysicalChecks.Native.ContainsKey)
        && M4PrincipalRfqPhysicalChecks.Native.Count == 6;

    public async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!ManifestCaptured || !ConnectionBoundary(out _)) return false;
        try
        {
            var execution = configuration.GetConnectionString("ProcurementApprovalExecution")!;
            await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(execution, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement"))
                .Options, scope);
            return await IsReadyAsync(owner, false, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }

    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) =>
        IsReadyAsync(owner, true, ct);

    public async Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!ManifestCaptured || !ConnectionBoundary(out var expectedRole) || !owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (ProcurementCompiledModelParity.HasPendingModelChanges(owner)
                || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct))
                    .Contains(M4PrincipalRfqPhysicalChecks.MigrationId, StringComparer.Ordinal)
                || !await p25.CanExecuteAsync(owner, ct)
                || !await p29.IsReadyAsync(owner, false, ct)
                || !await policy.IsReadReadyAsync(owner, ct)) return false;

            var connection = owner.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = connection.CreateCommand();
            command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandTimeout = 10;
            async Task<bool> True(string sql)
            { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            if (!await True(PrivilegeSql(write, expectedRole)) || !await True(PolicyWorkerNullableSql)) return false;

            var model = owner.GetService<IDesignTimeModel>().Model;
            var entities = OwnedTypes.Select(model.FindEntityType).ToArray();
            if (entities.Any(x => x is null)
                || entities.Sum(x => x!.GetKeys().Count()) != 6
                || entities.Sum(x => x!.GetForeignKeys().Count()) != 8
                || entities.Sum(x => x!.GetIndexes().Count()) != 11) return false;
            var checks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in entities.Select(x => x!))
            {
                if (entity.GetSchema() != "procurement") return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                command.CommandText = SelectAllColumnsSql(entity, table);
                await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, table))) return false;
                foreach (var key in entity.GetKeys())
                    if (!await True(KeySql(table,
                        key.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        key.GetName()!, key.IsPrimaryKey()))) return false;
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!,
                        foreign.PrincipalEntityType.GetSchema());
                    if (!await True(ForeignKeySql(table, parent,
                        foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(),
                        foreign.GetConstraintName()))) return false;
                }
                foreach (var index in entity.GetIndexes())
                    if (index.GetFilter() is not null || !await True(IndexSql(table,
                        index.Properties.Select(x => x.GetColumnName(table)!).ToArray(),
                        index.GetDatabaseName()!, index.IsUnique))) return false;
                var tableChecks = entity.GetCheckConstraints().ToArray();
                if (tableChecks.Length != 1 || !OwnedChecks.Contains(tableChecks[0].Name, StringComparer.Ordinal)) return false;
                var check = tableChecks[0]; checks.Add(check.Name);
                command.CommandText = CheckSql(table, check.Name);
                if (await command.ExecuteScalarAsync(ct) is not string actual
                    || !M4PrincipalRfqPhysicalChecks.Matches(check.Name, actual)) return false;
                if (!await True(ExactTableShapeSql(table, entity.GetKeys().Count(),
                        entity.GetForeignKeys().Count(), entity.GetIndexes().Count()))) return false;
            }
            return checks.SetEquals(OwnedChecks);
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }

    private bool ConnectionBoundary(out string executionRole)
    {
        executionRole = string.Empty;
        var record = configuration.GetConnectionString("Procurement");
        var execution = configuration.GetConnectionString("ProcurementApprovalExecution");
        if (string.IsNullOrWhiteSpace(record) || string.IsNullOrWhiteSpace(execution)
            || !ProcurementMasterImportRoutes.SameDatabaseEndpoints(record, false, null, 0,
                execution, false, null, 0)) return false;
        try
        {
            static NpgsqlConnectionStringBuilder Settings(string value) => new(
                value.StartsWith("postgresql", StringComparison.OrdinalIgnoreCase) && value.Contains('?')
                    ? NpgsqlConnectionStringNormalizer.Normalize(value) : value);
            var r = Settings(record); var e = Settings(execution);
            if (string.IsNullOrWhiteSpace(r.Username) || string.IsNullOrWhiteSpace(e.Username)
                || string.Equals(r.Username, e.Username, StringComparison.Ordinal)) return false;
            executionRole = e.Username;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException) { return false; }
    }

    internal static string PrivilegeSql(bool write, string expectedRole)
    {
        string[] read = ["sourcing_events", "sourcing_lines", "sourcing_invitations",
            "sourcing_publication_openings", "sourcing_publication_decisions", "sourcing_publication_receipts",
            "sourcing_policy_identity_evidence", "spending_authority_identity_evidence",
            "policy_versions", "suppliers", "supplier_qualifications", "audit_events", "outbox_events"];
        string[] append = ["sourcing_publication_openings", "sourcing_publication_decisions",
            "sourcing_publication_receipts", "audit_events", "outbox_events"];
        var clauses = read.Select(x => "has_table_privilege(current_user," + Literal("procurement." + x) + ",'SELECT')").ToList();
        clauses.Add("session_user=current_user AND current_user=" + Literal(expectedRole));
        clauses.Add("NOT EXISTS(SELECT 1 FROM pg_roles WHERE rolname=current_user AND "
            + "(rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls))");
        clauses.Add("NOT has_schema_privilege(current_user,'procurement','CREATE')");
        clauses.Add("NOT has_schema_privilege(current_user,'core_approval','CREATE')");
        foreach (var table in append)
        {
            var name = Literal("procurement." + table);
            clauses.Add("NOT has_any_column_privilege(current_user," + name + ",'UPDATE')");
            clauses.Add("NOT has_table_privilege(current_user," + name + ",'DELETE')");
            clauses.Add("NOT has_table_privilege(current_user," + name + ",'TRUNCATE')");
            clauses.Add("NOT has_table_privilege(current_user," + name + ",'TRIGGER')");
            if (write) clauses.Add("has_table_privilege(current_user," + name + ",'INSERT')");
        }
        clauses.Add("NOT has_table_privilege(current_user,'procurement.sourcing_invitations','DELETE')");
        if (write)
        {
            clauses.Add("has_table_privilege(current_user,'procurement.sourcing_events','UPDATE')");
            clauses.Add("has_table_privilege(current_user,'procurement.sourcing_invitations','INSERT')");
        }
        return "SELECT " + string.Join(" AND ", clauses);
    }

    internal const string PolicyWorkerNullableSql = "SELECT EXISTS(SELECT 1 FROM pg_attribute a WHERE "
        + "a.attrelid='procurement.sourcing_policy_identity_evidence'::regclass AND a.attname='actor_worker_id' "
        + "AND NOT a.attisdropped AND NOT a.attnotnull AND format_type(a.atttypid,a.atttypmod)='uuid') "
        + "AND NOT EXISTS(SELECT 1 FROM pg_attrdef d JOIN pg_attribute a ON a.attrelid=d.adrelid "
        + "AND a.attnum=d.adnum WHERE a.attrelid='procurement.sourcing_policy_identity_evidence'::regclass "
        + "AND a.attname='actor_worker_id')";

    private static string ExactTableShapeSql(StoreObjectIdentifier table, int keys, int foreigns, int indexes) =>
        "SELECT (SELECT count(*) FROM pg_constraint WHERE conrelid=" + Literal(table.Schema + "." + table.Name)
        + "::regclass AND contype IN ('p','u','f','c'))=" + (keys + foreigns + 1)
        + " AND (SELECT count(*) FROM pg_index WHERE indrelid=" + Literal(table.Schema + "." + table.Name)
        + "::regclass)=" + (keys + indexes);
}
