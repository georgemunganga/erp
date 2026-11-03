using System.Collections.ObjectModel;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Procurement consumes controlled Core projections, never authentication secrets or a
/// general HR directory. Required reads stay minimal; the permitted union also
/// accommodates safe projections already used by the shared execution role.
/// Consumers retain their own minimum required projection; this union grants nothing.
/// </summary>
internal static class ProcurementCoreReadProjectionGuard
{
    internal static readonly IReadOnlyDictionary<string, string[]> RequiredReadColumns = Required();
    internal static readonly IReadOnlyDictionary<string, string[]> AllowedReadColumns = Allowed();

    // Examine effective column privileges, including table grants, PUBLIC and
    // inherited memberships. New columns and unlisted HR views fail closed.
    // A safe optional read is permitted here, not automatically granted/required.
    internal static readonly string Sql = "NOT EXISTS(SELECT 1 FROM pg_class c "
        + "JOIN pg_namespace n ON n.oid=c.relnamespace "
        + "JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped "
        + "WHERE n.nspname='hrm' AND c.relkind IN ('r','p','v','m','f') "
        + "AND has_column_privilege(current_user,c.oid,a.attnum,'SELECT') "
        + "AND NOT EXISTS(SELECT 1 FROM (VALUES "
        + string.Join(",", AllowedReadColumns.OrderBy(x => x.Key, StringComparer.Ordinal)
            .SelectMany(x => x.Value.OrderBy(c => c, StringComparer.Ordinal)
                .Select(c => "(" + Literal(x.Key[4..]) + "," + Literal(c) + ")")))
        + ") AS allowed(table_name,column_name) WHERE allowed.table_name=c.relname "
        + "AND allowed.column_name=a.attname))";

    internal static bool Allows(string table, string column)
        => AllowedReadColumns.TryGetValue(table, out var columns)
            && columns.Contains(column, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string[]> Required()
    {
        var values = Copy(ProcurementInvoiceReplacementSchemaReadiness.CoreReadColumns);
        Add(values, "hrm.local_users", "display_name");
        Add(values, "hrm.legal_entities", "time_zone_id");
        return Freeze(values);
    }

    private static IReadOnlyDictionary<string, string[]> Allowed()
    {
        var values = Copy(RequiredReadColumns);
        foreach (var projection in ProcurementManualPaymentReadSchemaReadiness.ReadColumns
            .Where(x => x.Key.StartsWith("hrm.", StringComparison.Ordinal)))
            Add(values, projection.Key, projection.Value);

        // Existing principal approval uses department effective dates and the
        // worker's manager relationship. These established shared reads remain
        // optional here; no consumer's minimum or private HR projection grows.
        Add(values, "hrm.org_units", "effective_from", "effective_to");
        Add(values, "hrm.workers", "manager_id");

        // The existing invoice currency consumer reads company currency and the
        // Core-owned reference/version metadata; it does not read Core command
        // receipts, employee pay, credentials or recovery/session material.
        Add(values, "hrm.legal_entities", "currency");
        Add(values, "hrm.__hrm_migrations", "ProductVersion");
        Add(values, "hrm.currency_precision_versions", "id", "currency_code", "revision", "minor_units",
            "rounding", "source_reference", "publication_reason", "published_at_utc", "retired_at_utc",
            "retirement_reason", "payload_sha256", "tenant_id", "created_at", "created_by",
            "updated_at", "updated_by", "is_archived");
        return Freeze(values);
    }

    private static Dictionary<string, HashSet<string>> Copy(IReadOnlyDictionary<string, string[]> source)
        => source.ToDictionary(x => x.Key, x => new HashSet<string>(x.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    private static void Add(Dictionary<string, HashSet<string>> values, string table, params string[] columns)
    {
        if (!values.TryGetValue(table, out var existing))
            values.Add(table, existing = new HashSet<string>(StringComparer.Ordinal));
        existing.UnionWith(columns);
    }

    private static IReadOnlyDictionary<string, string[]> Freeze(Dictionary<string, HashSet<string>> values)
        => new ReadOnlyDictionary<string, string[]>(values.ToDictionary(x => x.Key,
            x => x.Value.OrderBy(c => c, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal));

    private static string Literal(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
