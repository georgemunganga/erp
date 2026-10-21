using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Owning-store read-only SQL shared by Procurement readiness providers.
/// Optional names/predicates are reviewed constants, never caller configuration.</summary>
internal static class ProcurementSchemaInspectionSql
{
    internal static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    internal static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    internal static string Columns(IEnumerable<string> values) => "ARRAY[" + string.Join(',', values.Select(Literal)) + "]::text[]";
    internal static string ForeignKeySql(StoreObjectIdentifier child, StoreObjectIdentifier parent, string[] from, string[] to, string? name = null) =>
        "SELECT EXISTS(SELECT 1 FROM pg_constraint c WHERE c.contype='f' AND c.convalidated AND NOT c.condeferrable AND NOT c.condeferred AND c.confdeltype='r' AND c.confupdtype='a' AND c.confmatchtype='s' AND c.conrelid="
        + Literal(child.Schema + "." + child.Name) + "::regclass AND c.confrelid=" + Literal(parent.Schema + "." + parent.Name)
        + "::regclass AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=k.n ORDER BY k.p)=" + Columns(from)
        + " AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=c.confrelid AND a.attnum=k.n ORDER BY k.p)=" + Columns(to) + (name is null ? "" : " AND c.conname=" + Literal(name)) + ")";
    internal static string UniqueIndexSql(StoreObjectIdentifier table, string[] columns, string? name = null, string? predicate = null) =>
        "SELECT EXISTS(SELECT 1 FROM pg_index i WHERE i.indrelid=" + Literal(table.Schema + "." + table.Name)
        + "::regclass AND i.indisunique AND i.indisvalid AND i.indimmediate AND i.indexprs IS NULL " + (predicate is null ? "AND i.indpred IS NULL" : "AND pg_get_expr(i.indpred,i.indrelid)=" + Literal(predicate)) + " AND i.indnkeyatts=i.indnatts AND ARRAY(SELECT a.attname::text FROM unnest(i.indkey::smallint[]) WITH ORDINALITY k(n,p) JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.n ORDER BY k.p)=" + Columns(columns) + (name is null ? "" : " AND i.indexrelid=" + Literal(table.Schema + "." + name) + "::regclass") + ")";

    internal static string SelectAllColumnsSql(IEntityType entity, StoreObjectIdentifier table) =>
        "SELECT " + string.Join(',', entity.GetProperties().Select(x => Quote(x.GetColumnName(table)!)))
        + " FROM " + Quote(table.Schema!) + "." + Quote(table.Name) + " LIMIT 0";
    internal static string ColumnsSql(IEntityType entity, StoreObjectIdentifier table) => "SELECT "
        + string.Join(" AND ", entity.GetProperties().Select(property =>
            "EXISTS(SELECT 1 FROM pg_attribute a WHERE a.attrelid="
            + Literal(table.Schema + "." + table.Name) + "::regclass AND a.attname="
            + Literal(property.GetColumnName(table)!) + " AND NOT a.attisdropped AND a.attidentity='' AND a.attgenerated='' AND a.attnotnull="
            + (property.IsNullable ? "false" : "true") + " AND format_type(a.atttypid,a.atttypmod)="
            + Literal(property.GetColumnType()!) + ")"));
    internal static string CheckSql(StoreObjectIdentifier table, string name) =>
        "SELECT pg_get_constraintdef(oid,false) FROM pg_constraint WHERE conrelid="
        + Literal(table.Schema + "." + table.Name) + "::regclass AND conname=" + Literal(name)
        + " AND contype='c' AND convalidated AND conislocal";

}
