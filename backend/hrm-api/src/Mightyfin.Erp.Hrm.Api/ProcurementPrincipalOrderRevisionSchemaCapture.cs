using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// Genuine PG17 owner capture from the exact P33 Up/Down/Up and separate
// read-only history query. Physical evidence never substitutes for finite roles,
// current authority, feature flags or business/runtime acceptance.
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
    internal static readonly IReadOnlyDictionary<string, string> Checks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["procurement.principal_order_commercial_origins.ck_po_draft_origin"] = "CHECK (((schema_version = 1) AND (capture_mode IS NOT NULL) AND ((capture_mode)::text = ANY ((ARRAY['Create'::character varying, 'VerifiedFirstEdit'::character varying])::text[])) AND (original_direct_source_sha256 IS NOT NULL) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (snapshot_json IS NOT NULL) AND (snapshot_sha256 IS NOT NULL) AND ((snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (captured_by_principal_id IS NOT NULL) AND ((captured_by_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((captured_by_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (captured_at IS NOT NULL)))",
        ["procurement.principal_order_draft_commercial_revisions.ck_po_draft_revision"] = "CHECK (((schema_version = 1) AND (revision_number > 0) AND (result_order_version = (revision_number + 1)) AND (reason IS NOT NULL) AND (length((reason)::text) > 0) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (command_sha256 IS NOT NULL) AND ((command_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (original_direct_source_sha256 IS NOT NULL) AND ((original_direct_source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (snapshot_json IS NOT NULL) AND (snapshot_sha256 IS NOT NULL) AND ((snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (((revision_number = 1) AND (previous_revision_id IS NULL) AND (previous_snapshot_sha256 IS NULL)) OR ((revision_number > 1) AND (previous_revision_id IS NOT NULL) AND (previous_snapshot_sha256 IS NOT NULL) AND ((previous_snapshot_sha256)::text ~ '^[0-9a-f]{64}$'::text))) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (recorded_at IS NOT NULL)))",
        ["procurement.principal_order_draft_receipts.ck_po_principal_receipt"] = "CHECK (((schema_version = 2) AND ((operation)::text = ANY ((ARRAY['Create'::character varying, 'Edit'::character varying, 'Cancel'::character varying, 'Funding'::character varying, 'Submit'::character varying, 'Decide'::character varying, 'Issue'::character varying, 'Ack'::character varying, 'ReleaseRequest'::character varying, 'ReleaseConfirm'::character varying])::text[])) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((command_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (result_version > 0) AND ((((operation)::text = ANY ((ARRAY['Create'::character varying, 'Submit'::character varying, 'Issue'::character varying])::text[])) AND (response_status = 201)) OR (((operation)::text = ANY ((ARRAY['Edit'::character varying, 'Cancel'::character varying, 'Funding'::character varying, 'Decide'::character varying, 'Ack'::character varying, 'ReleaseRequest'::character varying, 'ReleaseConfirm'::character varying])::text[])) AND (response_status = 200)))))",
        ["procurement.principal_order_effective_source_slices.ck_po_draft_slice"] = "CHECK (((schema_version = 1) AND (quantity > (0)::numeric) AND (unit_price >= (0)::numeric) AND (tax_amount >= (0)::numeric) AND (estimated_value = (quantity * unit_price)) AND (currency_code IS NOT NULL) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text)))",
    };
    internal static readonly IReadOnlyDictionary<string, string> ParentKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["procurement.principal_order_source_bindings.AK_principal_order_source_bindings_tenant_id_legal_entity_id_p~"] = "UNIQUE (tenant_id, legal_entity_id, purchase_order_id, id)",
        ["procurement.purchase_order_lines.AK_purchase_order_lines_tenant_id_legal_entity_id_purchase_ord~"] = "UNIQUE (tenant_id, legal_entity_id, purchase_order_id, id)",
    };
    internal const string HistoryCatalogSha256 = "6e41bd83ce4ed1b7a714db922f7d889387c0d91353655fb54413505dc28a820e";

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
