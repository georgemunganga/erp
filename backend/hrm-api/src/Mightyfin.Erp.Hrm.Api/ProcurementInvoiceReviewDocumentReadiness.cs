using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Core.Documents.Infrastructure;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;
namespace Mightyfin.Erp.Hrm.Api;
internal static class ProcurementInvoiceReviewDocumentReadiness
{
    // Actual PG17 capture, not guessed pg_get_constraintdef formatting. Source evidence:
    // core-document-p03-migration-20261021_0812/p03-checks.json SHA 0adcf6565c0d6bb8807433d3b26e8f104f49b253b92121f88ab5a99fd2655413
    internal const string MigrationId = "20261021080548_P03DocumentUseRetention";
    internal static readonly IReadOnlyDictionary<string,string> Checks = new Dictionary<string,string>
    {
        ["ck_doc_use_pin_shape"] = "CHECK (((\"DocumentRevision\" > 0) AND (\"VersionRevision\" > 0) AND (\"VersionNumber\" > 0) AND ((\"SizeBytes\" >= 1) AND (\"SizeBytes\" <= 104857600)) AND (\"ScanAttempt\" > 0) AND (\"ScanVerdict\" = 1) AND ((\"Sha256\")::text ~ '^[0-9a-f]{64}$'::text) AND (\"ScanFinishedAt\" >= \"ScanStartedAt\") AND (\"CreatedAt\" >= \"ScanFinishedAt\")))",
        ["ck_doc_use_set_shape"] = "CHECK (((\"SourceRevision\" > 0) AND (\"Kind\" = ANY (ARRAY[1, 2])) AND ((\"SelectionCount\" >= 1) AND (\"SelectionCount\" <= 100)) AND ((\"SelectionDigest\")::text ~ '^[0-9a-f]{64}$'::text)))",
    };
    internal static async Task<bool> ReadyAsync(ProcurementDbContext transaction, ProcurementRequestScope scope,
        bool write, bool retire, CancellationToken ct, bool download = false)
    {
        if (!transaction.Database.IsNpgsql() || transaction.Database.CurrentTransaction is null) return false;
        try
        {
            await using var documents = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
                .UseNpgsql(transaction.Database.GetDbConnection(), p => p.MigrationsHistoryTable("__EFMigrationsHistory", "core_document")).Options, scope);
            await documents.Database.UseTransactionAsync(transaction.Database.CurrentTransaction.GetDbTransaction(), ct);
            if (documents.Database.HasPendingModelChanges() || (await documents.Database.GetPendingMigrationsAsync(ct)).Any()
                || !(await documents.Database.GetAppliedMigrationsAsync(ct)).Contains(MigrationId)) return false;
            await using var command = transaction.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.Database.CurrentTransaction.GetDbTransaction(); command.CommandTimeout = 10;
            async Task<bool> True(string sql) { command.CommandText = sql; return await command.ExecuteScalarAsync(ct) is true; }
            foreach (var entity in documents.GetService<IDesignTimeModel>().Model.GetEntityTypes())
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                if (table.Schema != "core_document" || !await True(ColumnsSql(entity, table))) return false;
                command.CommandText = SelectAllColumnsSql(entity, table); await using (var reader = await command.ExecuteReaderAsync(ct)) { }
                foreach (var key in entity.GetKeys()) if (!await True(KeySql(table,key.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),key.GetName()!,key.IsPrimaryKey()))) return false;
                foreach (var fk in entity.GetForeignKeys())
                {
                    var parent = StoreObjectIdentifier.Table(fk.PrincipalEntityType.GetTableName()!,fk.PrincipalEntityType.GetSchema());
                    if (!await True(ForeignKeySql(table,parent,fk.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),fk.PrincipalKey.Properties.Select(x=>x.GetColumnName(parent)!).ToArray(),fk.GetConstraintName()))) return false;
                }
                foreach (var index in entity.GetIndexes().Where(x=>x.IsUnique))
                    if (!await True(UniqueIndexSql(table,index.Properties.Select(x=>x.GetColumnName(table)!).ToArray(),index.GetDatabaseName()!,index.GetFilter()))) return false;
                foreach (var check in entity.GetCheckConstraints())
                {
                    if (!Checks.TryGetValue(check.Name!,out var expected)) return false;
                    command.CommandText = CheckSql(table,check.Name!); if (await command.ExecuteScalarAsync(ct) is not string actual || actual!=expected) return false;
                }
                var relation = "core_document."+table.Name;
                if (!await True($"SELECT has_table_privilege(current_user,{Literal(relation)},'SELECT') AND NOT has_table_privilege(current_user,{Literal(relation)},'DELETE,TRUNCATE') AND NOT EXISTS(SELECT 1 FROM pg_class c WHERE c.oid={Literal(relation)}::regclass AND (pg_has_role(current_user,c.relowner,'MEMBER') OR pg_has_role(current_user,c.relowner,'SET')) )")) return false;
                if (table.Name is "use_sets" or "use_pins" or "scan_evidence" or "record_events")
                    if (!await True($"SELECT NOT has_any_column_privilege(current_user,{Literal(relation)},'UPDATE')")) return false;
                if (write && table.Name is ("use_sets" or "use_pins"))
                    if (!await True($"SELECT has_table_privilege(current_user,{Literal(relation)},'INSERT')")) return false;
            }
            if (!await True("SELECT NOT has_schema_privilege(current_user,'core_document','CREATE') AND NOT EXISTS(SELECT 1 FROM pg_namespace n WHERE n.nspname='core_document' AND (pg_has_role(current_user,n.nspowner,'MEMBER') OR pg_has_role(current_user,n.nspowner,'SET')))")) return false;
            // A purpose-scoped download appends DownloadAuthorized to the Core
            // document event log. Ordinary read routes do not need this grant.
            if (download && !await True("SELECT has_table_privilege(current_user,'core_document.record_events','INSERT')")) return false;
            if (retire)
            {
                var entity = documents.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Mightyfin.Erp.Core.Documents.Domain.CoreDocument))!;
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
                foreach (var name in new[] { "IsRetired", "RetiredAt", "RetiredBy", "RetirementReason", "Version" })
                    if (!await True($"SELECT has_column_privilege(current_user,'core_document.documents',{Literal(entity.FindProperty(name)!.GetColumnName(table)!)},'UPDATE')")) return false;
                if (!await True("SELECT has_table_privilege(current_user,'core_document.record_events','INSERT')")) return false;
            }
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) { return false; }
    }
}
