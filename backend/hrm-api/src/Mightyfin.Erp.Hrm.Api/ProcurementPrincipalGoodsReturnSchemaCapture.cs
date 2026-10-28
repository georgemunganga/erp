using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// Expected shapes retained from the held P37 owner contract. The new M5d
// migration must receive its own genuine PostgreSQL capture and owner rehearsal.
internal static class ProcurementPrincipalGoodsReturnSchemaCapture
{
    internal const string MigrationId = "20261028180000_AddPrincipalGoodsReturnDrafts";
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
    internal static readonly IReadOnlyDictionary<string,string> Checks = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.principal_goods_return_drafts.ck_pgret_draft"] = "CHECK (((schema_version = 1) AND (revision > 0) AND ((state)::text = ANY ((ARRAY['Draft'::character varying, 'Cancelled'::character varying])::text[])) AND (branch_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (current_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (source_sha256 IS NOT NULL) AND ((source_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (acceptance_sha256 IS NOT NULL) AND ((acceptance_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (accepted_fact_quantity > (0)::numeric) AND (creator_principal_id IS NOT NULL) AND ((creator_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((creator_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text)))",
        ["procurement.principal_goods_return_receipts.ck_pgret_receipt"] = "CHECK (((schema_version = 1) AND ((operation)::text = ANY ((ARRAY['Create'::character varying, 'Revise'::character varying, 'Cancel'::character varying])::text[])) AND ((((operation)::text = 'Create'::text) AND (response_status = 201) AND (result_revision = 1)) OR (((operation)::text = ANY ((ARRAY['Revise'::character varying, 'Cancel'::character varying])::text[])) AND (response_status = 200) AND (result_revision > 1))) AND (result_revision > 0) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (request_sha256 IS NOT NULL) AND ((request_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (response_sha256 IS NOT NULL) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text)))",
        ["procurement.principal_goods_return_revisions.ck_pgret_revision"] = "CHECK (((schema_version = 1) AND (revision > 0) AND ((state)::text = ANY ((ARRAY['Draft'::character varying, 'Cancelled'::character varying])::text[])) AND (quantity > (0)::numeric) AND (reason IS NOT NULL) AND (length(btrim((reason)::text)) > 0) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (proposal_sha256 IS NOT NULL) AND ((proposal_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text)))",
    };
    internal static readonly IReadOnlyDictionary<string,string> ForeignDefinitions = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.principal_goods_return_drafts.fk_pgret_acceptance"] = "FOREIGN KEY (tenant_id, legal_entity_id, acceptance_fact_id) REFERENCES procurement.principal_receiving_facts(tenant_id, legal_entity_id, id) ON DELETE RESTRICT",
        ["procurement.principal_goods_return_drafts.fk_pgret_current_revision"] = "FOREIGN KEY (tenant_id, legal_entity_id, id, current_revision_id, revision, state) REFERENCES procurement.principal_goods_return_revisions(tenant_id, legal_entity_id, draft_id, id, revision, state) DEFERRABLE INITIALLY DEFERRED",
        ["procurement.principal_goods_return_drafts.fk_pgret_exact_fact"] = "FOREIGN KEY (tenant_id, legal_entity_id, acceptance_fact_id, source_state_id, receipt_id, branch_id) REFERENCES procurement.principal_receiving_facts(tenant_id, legal_entity_id, id, source_state_id, draft_id, branch_id)",
        ["procurement.principal_goods_return_drafts.fk_pgret_exact_receipt"] = "FOREIGN KEY (tenant_id, legal_entity_id, receipt_id, branch_id, purchase_order_id, issued_version_id) REFERENCES procurement.principal_receiving_drafts(tenant_id, legal_entity_id, id, branch_id, purchase_order_id, issued_version_id)",
        ["procurement.principal_goods_return_drafts.fk_pgret_exact_source"] = "FOREIGN KEY (tenant_id, legal_entity_id, source_state_id, branch_id, purchase_order_id, purchase_order_line_id, issued_version_id) REFERENCES procurement.principal_receiving_sources(tenant_id, legal_entity_id, id, branch_id, purchase_order_id, purchase_order_line_id, issued_version_id)",
        ["procurement.principal_goods_return_drafts.fk_pgret_issued"] = "FOREIGN KEY (tenant_id, legal_entity_id, issued_version_id) REFERENCES procurement.principal_order_issued_versions(tenant_id, legal_entity_id, id) ON DELETE RESTRICT",
        ["procurement.principal_goods_return_drafts.fk_pgret_order_line"] = "FOREIGN KEY (tenant_id, legal_entity_id, purchase_order_line_id) REFERENCES procurement.purchase_order_lines(tenant_id, legal_entity_id, id) ON DELETE RESTRICT",
        ["procurement.principal_goods_return_drafts.fk_pgret_receipt"] = "FOREIGN KEY (tenant_id, legal_entity_id, receipt_id) REFERENCES procurement.principal_receiving_drafts(tenant_id, legal_entity_id, id) ON DELETE RESTRICT",
        ["procurement.principal_goods_return_drafts.fk_pgret_source"] = "FOREIGN KEY (tenant_id, legal_entity_id, source_state_id) REFERENCES procurement.principal_receiving_sources(tenant_id, legal_entity_id, id) ON DELETE RESTRICT",
        ["procurement.principal_goods_return_receipts.fk_pgret_receipt_draft"] = "FOREIGN KEY (tenant_id, legal_entity_id, draft_id, branch_id) REFERENCES procurement.principal_goods_return_drafts(tenant_id, legal_entity_id, id, branch_id) ON DELETE RESTRICT",
        ["procurement.principal_goods_return_receipts.fk_pgret_receipt_revision"] = "FOREIGN KEY (tenant_id, legal_entity_id, draft_id, result_revision, actor_principal_id, command_key) REFERENCES procurement.principal_goods_return_revisions(tenant_id, legal_entity_id, draft_id, revision, actor_principal_id, command_key) ON DELETE RESTRICT",
        ["procurement.principal_goods_return_revisions.fk_pgret_revision_draft"] = "FOREIGN KEY (tenant_id, legal_entity_id, draft_id) REFERENCES procurement.principal_goods_return_drafts(tenant_id, legal_entity_id, id) ON DELETE RESTRICT",
    };
    internal static readonly IReadOnlyDictionary<string,string> OverlayKeys = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.principal_goods_return_revisions.ak_pgret_current_revision"] = "UNIQUE (tenant_id, legal_entity_id, draft_id, id, revision, state)",
        ["procurement.principal_receiving_drafts.ak_pgret_receipt_owner"] = "UNIQUE (tenant_id, legal_entity_id, id, branch_id, purchase_order_id, issued_version_id)",
        ["procurement.principal_receiving_facts.ak_pgret_fact_owner"] = "UNIQUE (tenant_id, legal_entity_id, id, source_state_id, draft_id, branch_id)",
        ["procurement.principal_receiving_sources.ak_pgret_source_owner"] = "UNIQUE (tenant_id, legal_entity_id, id, branch_id, purchase_order_id, purchase_order_line_id, issued_version_id)",
    };
    internal static readonly IReadOnlyDictionary<string,string> IndexDefinitions = new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["IX_principal_goods_return_drafts_tenant_id_legal_entity_id_acc~"] = "CREATE INDEX \"IX_principal_goods_return_drafts_tenant_id_legal_entity_id_acc~\" ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, acceptance_fact_id)",
        ["IX_principal_goods_return_drafts_tenant_id_legal_entity_id_iss~"] = "CREATE INDEX \"IX_principal_goods_return_drafts_tenant_id_legal_entity_id_iss~\" ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, issued_version_id)",
        ["IX_principal_goods_return_drafts_tenant_id_legal_entity_id_pur~"] = "CREATE INDEX \"IX_principal_goods_return_drafts_tenant_id_legal_entity_id_pur~\" ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, purchase_order_line_id)",
        ["IX_principal_goods_return_drafts_tenant_id_legal_entity_id_rec~"] = "CREATE INDEX \"IX_principal_goods_return_drafts_tenant_id_legal_entity_id_rec~\" ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, receipt_id)",
        ["IX_principal_goods_return_drafts_tenant_id_legal_entity_id_sou~"] = "CREATE INDEX \"IX_principal_goods_return_drafts_tenant_id_legal_entity_id_sou~\" ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, source_state_id)",
        ["IX_principal_goods_return_receipts_tenant_id_legal_entity_id_d~"] = "CREATE INDEX \"IX_principal_goods_return_receipts_tenant_id_legal_entity_id_d~\" ON procurement.principal_goods_return_receipts USING btree (tenant_id, legal_entity_id, draft_id, branch_id)",
        ["IX_principal_goods_return_receipts_tenant_id_legal_entity_id_~1"] = "CREATE INDEX \"IX_principal_goods_return_receipts_tenant_id_legal_entity_id_~1\" ON procurement.principal_goods_return_receipts USING btree (tenant_id, legal_entity_id, draft_id, result_revision, actor_principal_id, command_key)",
        ["PK_principal_goods_return_drafts"] = "CREATE UNIQUE INDEX \"PK_principal_goods_return_drafts\" ON procurement.principal_goods_return_drafts USING btree (id)",
        ["PK_principal_goods_return_receipts"] = "CREATE UNIQUE INDEX \"PK_principal_goods_return_receipts\" ON procurement.principal_goods_return_receipts USING btree (id)",
        ["PK_principal_goods_return_revisions"] = "CREATE UNIQUE INDEX \"PK_principal_goods_return_revisions\" ON procurement.principal_goods_return_revisions USING btree (id)",
        ["ak_pgret_current_revision"] = "CREATE UNIQUE INDEX ak_pgret_current_revision ON procurement.principal_goods_return_revisions USING btree (tenant_id, legal_entity_id, draft_id, id, revision, state)",
        ["ak_pgret_draft_branch"] = "CREATE UNIQUE INDEX ak_pgret_draft_branch ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, id, branch_id)",
        ["ak_pgret_draft_scope"] = "CREATE UNIQUE INDEX ak_pgret_draft_scope ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, id)",
        ["ak_pgret_receipt_scope"] = "CREATE UNIQUE INDEX ak_pgret_receipt_scope ON procurement.principal_goods_return_receipts USING btree (tenant_id, legal_entity_id, id)",
        ["ak_pgret_revision_command"] = "CREATE UNIQUE INDEX ak_pgret_revision_command ON procurement.principal_goods_return_revisions USING btree (tenant_id, legal_entity_id, draft_id, revision, actor_principal_id, command_key)",
        ["ak_pgret_revision_scope"] = "CREATE UNIQUE INDEX ak_pgret_revision_scope ON procurement.principal_goods_return_revisions USING btree (tenant_id, legal_entity_id, id)",
        ["ix_pgret_branch_created"] = "CREATE INDEX ix_pgret_branch_created ON procurement.principal_goods_return_drafts USING btree (tenant_id, legal_entity_id, branch_id, created_at)",
        ["ux_pgret_command"] = "CREATE UNIQUE INDEX ux_pgret_command ON procurement.principal_goods_return_receipts USING btree (tenant_id, legal_entity_id, command_key)",
        ["ux_pgret_revision"] = "CREATE UNIQUE INDEX ux_pgret_revision ON procurement.principal_goods_return_revisions USING btree (tenant_id, legal_entity_id, draft_id, revision)",
    };

    internal static bool Complete(IReadOnlyDictionary<string,string> checks,
        IReadOnlyDictionary<string,string> foreign, IReadOnlyDictionary<string,string> keys,
        IReadOnlyDictionary<string,string> indexes) => Exact(checks,CheckNames)
        && Exact(foreign,ForeignNames) && Exact(keys,OverlayKeyNames) && Exact(indexes,IndexNames);
    private static bool Exact(IReadOnlyDictionary<string,string> values,IReadOnlySet<string> names)
        => values.Count==names.Count && names.SetEquals(values.Keys)
            && values.Values.All(v=>!string.IsNullOrWhiteSpace(v));
    internal static bool Populated => Complete(Checks,ForeignDefinitions,OverlayKeys,IndexDefinitions);

}
