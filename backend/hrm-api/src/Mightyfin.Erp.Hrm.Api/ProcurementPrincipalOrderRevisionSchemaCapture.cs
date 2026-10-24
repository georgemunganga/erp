using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// The migration ID is genuine. Every catalog value below must come from its
// actual PostgreSQL capture; empty input keeps every P33 path unavailable.
internal static class ProcurementPrincipalOrderRevisionSchemaCapture
{
    internal const string MigrationId = "20261024221344_P33PrincipalOrderDraftRevisions";
    internal static readonly Type[] Types = [typeof(PrincipalOrderCommercialOrigin),
        typeof(PrincipalOrderDraftCommercialRevision), typeof(PrincipalOrderEffectiveSourceSlice)];
    internal static readonly string[] Tables = ["procurement.principal_order_commercial_origins",
        "procurement.principal_order_draft_commercial_revisions", "procurement.principal_order_effective_source_slices"];
    internal static readonly string[] CheckNames = [
        "procurement.principal_order_draft_receipts.ck_po_principal_receipt",
        "procurement.principal_order_commercial_origins.ck_po_draft_origin",
        "procurement.principal_order_draft_commercial_revisions.ck_po_draft_revision",
        "procurement.principal_order_effective_source_slices.ck_po_draft_slice"];
    internal static readonly string[] ParentKeyNames = [
        "procurement.purchase_order_lines.AK_purchase_order_lines_tenant_id_legal_entity_id_purchase_ord~",
        "procurement.principal_order_source_bindings.AK_principal_order_source_bindings_tenant_id_legal_entity_id_p~"];
    internal static readonly IReadOnlyDictionary<string, string> Checks = new Dictionary<string, string>();
    internal static readonly IReadOnlyDictionary<string, string> ParentKeys = new Dictionary<string, string>();
    internal static readonly string? HistoryCatalogSha256 = null;

    internal static bool Complete(IReadOnlyDictionary<string, string> checks,
        IReadOnlyDictionary<string, string> parents, string? historySha)
        => new HashSet<string>(checks.Keys, StringComparer.Ordinal).SetEquals(CheckNames)
            && new HashSet<string>(parents.Keys, StringComparer.Ordinal).SetEquals(ParentKeyNames)
            && checks.Values.All(x => !string.IsNullOrWhiteSpace(x))
            && parents.Values.All(x => !string.IsNullOrWhiteSpace(x))
            && historySha is { Length: 64 } && historySha.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Populated => Complete(Checks, ParentKeys, HistoryCatalogSha256);

    internal static ProcurementPrincipalCommercialSchemaInspection.Capture? Select(bool p33Applied)
    {
        if (!p33Applied) return new(ProcurementPrincipalCommercialSchemaCapture.MigrationId,
            ProcurementPrincipalCommercialSchemaCapture.Checks, ProcurementPrincipalCommercialSchemaCapture.OwnershipConstraints);
        if (!Populated) return null;
        var checks = ProcurementPrincipalCommercialSchemaCapture.Checks.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (var pair in Checks) checks[pair.Key] = pair.Value;
        return new(MigrationId, checks, ProcurementPrincipalCommercialSchemaCapture.OwnershipConstraints, ParentKeys);
    }
}
