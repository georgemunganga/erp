using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;
namespace Mightyfin.Erp.Hrm.Api;
public sealed class ProcurementInvoiceUnitReviewOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}
public interface IProcurementInvoiceUnitReviewSchemaReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct);
}
public sealed class ProcurementInvoiceUnitReviewSchemaReadiness : IProcurementInvoiceUnitReviewSchemaReadiness
{
    // Central generation + actual PG17 capture fills these exact constants. No candidate
    // model-derived CHECK strings, caller manifest knobs or static readiness true.
    internal const string MigrationId = "";
    internal const string OperationalCheckDefinition = "";
    internal static readonly IReadOnlyDictionary<string, string> CheckDefinitions = new Dictionary<string, string>();
    // Exact PostgreSQL-rendered P23 CHECK definitions for the two altered legacy
    // tables. No model SQL string or EF migration history alone grants readiness.
    internal static readonly IReadOnlyDictionary<string, string> PrincipalCheckDefinitions = new Dictionary<string, string>();
    private static readonly Type[] Types = [typeof(InvoiceUnitReviewSource), typeof(InvoiceUnitReviewAssignmentEvidence), typeof(InvoiceUnitReviewOutcome), typeof(InvoiceUnitReviewCommandReceipt)];
    internal static bool ManifestCaptured => MigrationId.Length > 0 && OperationalCheckDefinition.Length > 0 && CheckDefinitions.Count == 4 && PrincipalCheckDefinitions.Count == 2;
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) => Check(owner, false, ct);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => Check(owner, true, ct);
    internal const string CoreDecisionPrivilegesSql = "SELECT has_table_privilege(current_user,'core_approval.decisions','SELECT') AND has_table_privilege(current_user,'core_approval.decisions','INSERT') AND NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE') AND NOT has_table_privilege(current_user,'core_approval.decisions','DELETE,TRUNCATE')";
    internal const string CoreReadBoundarySql = "SELECT has_table_privilege(current_user,'core_approval.decisions','SELECT') AND NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE') AND NOT has_table_privilege(current_user,'core_approval.decisions','DELETE,TRUNCATE') AND NOT has_schema_privilege(current_user,'core_approval','CREATE') AND NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='core_approval' AND c.relname IN ('cases','stages','assignments','decisions') AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))";
    internal static string AppendPrivilegesSql(string relation, bool write)
        => $"SELECT NOT has_any_column_privilege(current_user,{Literal(relation)},'UPDATE') AND NOT has_table_privilege(current_user,{Literal(relation)},'DELETE,TRUNCATE') AND NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid={Literal(relation)}::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')))"
            + (write ? $" AND has_table_privilege(current_user,{Literal(relation)},'INSERT')" : "");
    private static async Task<bool> Check(ProcurementDbContext owner, bool write, CancellationToken ct)
    {
        if (!ManifestCaptured || !owner.Database.IsNpgsql()) return false;
        var opened = false;
        try
        {
            if (owner.Database.HasPendingModelChanges() || (await owner.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await owner.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId, StringComparer.Ordinal)) return false;
            if (owner.Database.GetDbConnection().State != ConnectionState.Open) { await owner.Database.OpenConnectionAsync(ct); opened = true; }
            await using var command = owner.Database.GetDbConnection().CreateCommand(); command.Transaction = owner.Database.CurrentTransaction?.GetDbTransaction(); command.CommandTimeout = 10;
            async Task<bool> True(string sql) { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            var model = owner.GetService<IDesignTimeModel>().Model;
            foreach (var type in Types)
            {
                var entity = model.FindEntityType(type); if (entity is null) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                if (table.Schema != "procurement") return false;
                command.CommandText = SelectAllColumnsSql(entity, table); await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                if (!await True(ColumnsSql(entity, table))) return false;
                var checks = entity.GetCheckConstraints().ToArray();
                if (checks.Length != 1 || !CheckDefinitions.TryGetValue(checks[0].Name!, out var expected)) return false;
                command.CommandText = CheckSql(table, checks[0].Name!); if (await command.ExecuteScalarAsync(ct) is not string actual || actual != expected) return false;
                foreach (var foreign in entity.GetForeignKeys())
                {
                    var parent = StoreObjectIdentifier.Table(foreign.PrincipalEntityType.GetTableName()!, foreign.PrincipalEntityType.GetSchema());
                    if (!await True(ForeignKeySql(table, parent, foreign.Properties.Select(x => x.GetColumnName(table)!).ToArray(), foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(), foreign.GetConstraintName()))) return false;
                    if (!await True(KeySql(parent, foreign.PrincipalKey.Properties.Select(x => x.GetColumnName(parent)!).ToArray(), foreign.PrincipalKey.GetName()!, foreign.PrincipalKey.IsPrimaryKey()))) return false;
                }
                foreach (var key in entity.GetKeys()) if (!await True(KeySql(table, key.Properties.Select(x => x.GetColumnName(table)!).ToArray(), key.GetName()!, key.IsPrimaryKey()))) return false;
                foreach (var index in entity.GetIndexes().Where(x => x.IsUnique))
                    if (index.GetFilter() is not null || !await True(UniqueIndexSql(table, index.Properties.Select(x => x.GetColumnName(table)!).ToArray(), index.GetDatabaseName()!, null))) return false;
                var relation = "procurement." + table.Name;
                if (!await True($"SELECT has_table_privilege(current_user,'{relation}','SELECT')")) return false;
                // Eligibility reads trust retained append-only facts too: an overprivileged
                // reader cannot use evidence it could have rewritten itself.
                if (!await True(AppendPrivilegesSql(relation, write))) return false;
            }
            foreach (var (type, name) in new[] { (typeof(InvoiceMatchMappingVersion), "ck_invoice_mapping_evidence"), (typeof(InvoiceMatchResult), "ck_invoice_match_execution") })
            {
                var entity = model.FindEntityType(type); if (entity is null || !PrincipalCheckDefinitions.TryGetValue(name, out var expected)) return false;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                if (table.Schema != "procurement" || !await True(ColumnsSql(entity, table))) return false;
                command.CommandText = CheckSql(table, name);
                if (await command.ExecuteScalarAsync(ct) is not string actual || actual != expected) return false;
            }
            command.CommandText = CheckSql(StoreObjectIdentifier.Table("operational_policy_identity_evidence", "procurement"), "ck_operational_policy_identity");
            if (await command.ExecuteScalarAsync(ct) is not string operational || operational != OperationalCheckDefinition) return false;
            if (!await True(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql)
                || !await True(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql)
                || !await True(CoreReadBoundarySql)) return false;
            if (write)
            {
                foreach (var table in new[] { "cases", "stages", "assignments" })
                    if (!await True($"SELECT has_table_privilege(current_user,'core_approval.{table}','SELECT') AND has_table_privilege(current_user,'core_approval.{table}','INSERT') AND has_table_privilege(current_user,'core_approval.{table}','UPDATE') AND NOT has_table_privilege(current_user,'core_approval.{table}','DELETE,TRUNCATE')")) return false;
                if (!await True(CoreDecisionPrivilegesSql)) return false;
                foreach (var table in new[] { "audit_events", "outbox_events" })
                    if (!await True($"SELECT has_table_privilege(current_user,'procurement.{table}','SELECT') AND has_table_privilege(current_user,'procurement.{table}','INSERT') AND NOT has_any_column_privilege(current_user,'procurement.{table}','UPDATE') AND NOT has_table_privilege(current_user,'procurement.{table}','DELETE,TRUNCATE')")) return false;
            }
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
        finally { if (opened) await owner.Database.CloseConnectionAsync(); }
    }
}
