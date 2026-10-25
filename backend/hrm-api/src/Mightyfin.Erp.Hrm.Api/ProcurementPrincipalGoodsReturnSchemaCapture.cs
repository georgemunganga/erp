using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// Names derive from genuine P37 EF output and its reviewed owner overlay.
// Definitions MUST come from actual PostgreSQL capture; empty is unavailable.
internal static class ProcurementPrincipalGoodsReturnSchemaCapture
{
    internal const string MigrationId = "20261025020252_P37PrincipalGoodsReturnDrafts";
    internal static readonly Type[] Types = [typeof(PrincipalGoodsReturnDraft),
        typeof(PrincipalGoodsReturnRevision),typeof(PrincipalGoodsReturnCommandReceipt)];
    internal static readonly string[] Tables = ["procurement.principal_goods_return_drafts",
        "procurement.principal_goods_return_revisions","procurement.principal_goods_return_receipts"];
    internal static readonly IReadOnlySet<string> CheckNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_goods_return_drafts.ck_pgret_draft",
        "procurement.principal_goods_return_revisions.ck_pgret_revision",
        "procurement.principal_goods_return_receipts.ck_pgret_receipt",
    };
    internal static readonly IReadOnlySet<string> ForeignNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_goods_return_drafts.fk_pgret_acceptance",
        "procurement.principal_goods_return_drafts.fk_pgret_issued",
        "procurement.principal_goods_return_drafts.fk_pgret_order_line",
        "procurement.principal_goods_return_drafts.fk_pgret_receipt",
        "procurement.principal_goods_return_drafts.fk_pgret_source",
        "procurement.principal_goods_return_revisions.fk_pgret_revision_draft",
        "procurement.principal_goods_return_receipts.fk_pgret_receipt_draft",
        "procurement.principal_goods_return_receipts.fk_pgret_receipt_revision",
        "procurement.principal_goods_return_drafts.fk_pgret_exact_source",
        "procurement.principal_goods_return_drafts.fk_pgret_exact_fact",
        "procurement.principal_goods_return_drafts.fk_pgret_exact_receipt",
        "procurement.principal_goods_return_drafts.fk_pgret_current_revision",
    };
    internal static readonly IReadOnlySet<string> OverlayKeyNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_receiving_sources.ak_pgret_source_owner",
        "procurement.principal_receiving_facts.ak_pgret_fact_owner",
        "procurement.principal_receiving_drafts.ak_pgret_receipt_owner",
        "procurement.principal_goods_return_revisions.ak_pgret_current_revision",
    };
    internal static readonly IReadOnlySet<string> IndexNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "ix_pgret_branch_created",
        "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_acc~",
        "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_iss~",
        "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_pur~",
        "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_rec~",
        "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_sou~",
        "IX_principal_goods_return_receipts_tenant_id_legal_entity_id_~1",
        "IX_principal_goods_return_receipts_tenant_id_legal_entity_id_d~",
        "ux_pgret_command",
        "ux_pgret_revision",
        "PK_principal_goods_return_drafts",
        "PK_principal_goods_return_revisions",
        "PK_principal_goods_return_receipts",
        "ak_pgret_draft_branch",
        "ak_pgret_draft_scope",
        "ak_pgret_revision_command",
        "ak_pgret_revision_scope",
        "ak_pgret_receipt_scope",
        "ak_pgret_current_revision",
    };
    internal static readonly IReadOnlyDictionary<string,string> Checks = new Dictionary<string,string>();
    internal static readonly IReadOnlyDictionary<string,string> ForeignDefinitions = new Dictionary<string,string>();
    internal static readonly IReadOnlyDictionary<string,string> OverlayKeys = new Dictionary<string,string>();
    internal static readonly IReadOnlyDictionary<string,string> IndexDefinitions = new Dictionary<string,string>();

    internal static bool Complete(IReadOnlyDictionary<string,string> checks,
        IReadOnlyDictionary<string,string> foreign, IReadOnlyDictionary<string,string> keys,
        IReadOnlyDictionary<string,string> indexes) => Exact(checks,CheckNames)
        && Exact(foreign,ForeignNames) && Exact(keys,OverlayKeyNames) && Exact(indexes,IndexNames);
    private static bool Exact(IReadOnlyDictionary<string,string> values,IReadOnlySet<string> names)
        => values.Count==names.Count && names.SetEquals(values.Keys)
            && values.Values.All(v=>!string.IsNullOrWhiteSpace(v));
    internal static bool Populated => Complete(Checks,ForeignDefinitions,OverlayKeys,IndexDefinitions);
    internal static ProcurementPrincipalCommercialSchemaInspection.Capture Physical =>
        new(MigrationId,Checks,ForeignDefinitions,OverlayKeys);
}
