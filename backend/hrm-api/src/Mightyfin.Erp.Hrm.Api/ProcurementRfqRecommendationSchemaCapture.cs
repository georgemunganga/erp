using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// Canonical CHECK, FK and parent-key definitions from the actual joint
// PostgreSQL owner Up/Down/Up rehearsal; finite-role/runtime gates remain separate.
// Physical definitions imported verbatim from the accepted private PostgreSQL owner roundtrip.
internal static class ProcurementRfqRecommendationSchemaCapture
{
    internal const string MigrationId = "20261025014631_P36HumanRecommendationDraft";
    internal static readonly Type[] Types =
    [
        typeof(PrincipalRfqRecommendationDraftHead),
        typeof(PrincipalRfqRecommendationDraftRevision),
        typeof(PrincipalRfqRecommendationDraftLine),
        typeof(PrincipalRfqRecommendationDraftReceipt)
    ];
    internal static readonly IReadOnlySet<string> TableNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_rfq_recommendation_drafts",
        "procurement.principal_rfq_recommendation_revisions",
        "procurement.principal_rfq_recommendation_lines",
        "procurement.principal_rfq_recommendation_receipts"
    };
    internal static readonly IReadOnlySet<string> CheckNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_rfq_recommendation_drafts.ck_p36_rec_head",
        "procurement.principal_rfq_recommendation_revisions.ck_p36_rec_revision",
        "procurement.principal_rfq_recommendation_lines.ck_p36_rec_line",
        "procurement.principal_rfq_recommendation_receipts.ck_p36_rec_ack"
    };
    internal static readonly IReadOnlySet<string> ForeignNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_opened",
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_plan",
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_round",
        "procurement.principal_rfq_recommendation_drafts.fk_p36_rec_seal",
        "procurement.principal_rfq_recommendation_revisions.fk_p36_rec_prev",
        "procurement.principal_rfq_recommendation_revisions.fk_p36_rec_revision_head",
        "procurement.principal_rfq_recommendation_lines.fk_p36_rec_line_offer",
        "procurement.principal_rfq_recommendation_lines.fk_p36_rec_line_revision",
        "procurement.principal_rfq_recommendation_lines.fk_p36_rec_source_line",
        "procurement.principal_rfq_recommendation_receipts.fk_p36_rec_ack_revision"
    };
    internal static readonly IReadOnlySet<string> ParentNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.sourcing_lines.ak_p36_rec_source_line",
        "procurement.principal_rfq_commercial_round_seals.ak_p36_rec_seal",
        "procurement.principal_rfq_commercial_offer_submissions.ak_p36_rec_offer"
    };
    internal static readonly IReadOnlyDictionary<string,string> Checks =
        new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.principal_rfq_recommendation_drafts.ck_p36_rec_head"] = "CHECK (((schema_version = 1) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (round_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (unsealing_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (seal_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (branch_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (prepared_plan_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (evaluation_plan_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (current_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (maker_principal_id IS NOT NULL) AND ((maker_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((maker_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((maker_worker_alias_id IS NULL) OR (maker_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((current_revision >= 1) AND (current_revision <= 100)) AND ((approved_round_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((opening_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((offer_set_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((evaluation_definition_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((current_snapshot_sha256)::text ~ '^[a-f0-9]{64}$'::text)))",
        ["procurement.principal_rfq_recommendation_lines.ck_p36_rec_line"] = "CHECK (((id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (draft_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (revision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (offer_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (sourcing_line_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (quantity_exact IS NOT NULL) AND ((quantity_exact)::text ~ '^(0|[1-9][0-9]{0,13})(\\.[0-9]{1,4})?$'::text) AND ((quantity_exact)::text !~ '^0(\\.0+)?$'::text) AND (proposed_value_exact IS NOT NULL) AND ((proposed_value_exact)::text ~ '^(0|[1-9][0-9]{0,29})(\\.[0-9]{1,8})?$'::text) AND ((proposed_value_exact)::text !~ '^0(\\.0+)?$'::text) AND ((offer_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((currency_code)::text ~ '^[A-Z]{3}$'::text) AND (rationale IS NOT NULL) AND (length(btrim((rationale)::text)) > 0)))",
        ["procurement.principal_rfq_recommendation_receipts.ck_p36_rec_ack"] = "CHECK (((id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (draft_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (revision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (round_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((request_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((response_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((response_sha256)::text = encode(sha256(convert_to(response_json, 'UTF8'::name)), 'hex'::text))))",
        ["procurement.principal_rfq_recommendation_revisions.ck_p36_rec_revision"] = "CHECK (((schema_version = 1) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (draft_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (round_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (unsealing_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (branch_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((actor_worker_alias_id IS NULL) OR (actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((revision >= 1) AND (revision <= 100)) AND ((request_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((snapshot_sha256)::text ~ '^[a-f0-9]{64}$'::text) AND ((snapshot_sha256)::text = encode(sha256(convert_to(snapshot_json, 'UTF8'::name)), 'hex'::text)) AND (((revision = 1) AND (previous_revision_id IS NULL) AND (previous_snapshot_sha256 IS NULL)) OR ((revision > 1) AND (previous_revision_id IS NOT NULL) AND (previous_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (previous_revision_id <> id) AND (previous_snapshot_sha256 IS NOT NULL) AND ((previous_snapshot_sha256)::text ~ '^[a-f0-9]{64}$'::text)))))",
    };
    internal static readonly IReadOnlyDictionary<string,string> ForeignKeys =
        new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.principal_rfq_recommendation_drafts.fk_p36_rec_opened"] = "FOREIGN KEY (tenant_id, legal_entity_id, unsealing_id, round_id, sourcing_event_id, branch_id, opening_sha256) REFERENCES procurement.principal_rfq_commercial_offer_openings(tenant_id, legal_entity_id, id, opening_id, sourcing_event_id, branch_id, snapshot_sha256) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_drafts.fk_p36_rec_plan"] = "FOREIGN KEY (tenant_id, legal_entity_id, prepared_plan_id, sourcing_event_id, branch_id, evaluation_plan_revision_id, evaluation_definition_sha256) REFERENCES procurement.rfq_prepared_plans(tenant_id, legal_entity_id, id, sourcing_event_id, branch_id, draft_revision_id, evaluation_rules_sha256) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_drafts.fk_p36_rec_round"] = "FOREIGN KEY (tenant_id, legal_entity_id, round_id, prepared_plan_id, sourcing_event_id, branch_id, approved_round_sha256) REFERENCES procurement.principal_rfq_commercial_round_openings(tenant_id, legal_entity_id, id, prepared_plan_id, sourcing_event_id, branch_id, snapshot_sha256) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_drafts.fk_p36_rec_seal"] = "FOREIGN KEY (tenant_id, legal_entity_id, seal_id, round_id, sourcing_event_id, branch_id, offer_set_sha256) REFERENCES procurement.principal_rfq_commercial_round_seals(tenant_id, legal_entity_id, id, opening_id, sourcing_event_id, branch_id, submission_set_sha256) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_lines.fk_p36_rec_line_offer"] = "FOREIGN KEY (tenant_id, legal_entity_id, offer_id, round_id, sourcing_event_id, branch_id, supplier_id, offer_sha256) REFERENCES procurement.principal_rfq_commercial_offer_submissions(tenant_id, legal_entity_id, id, opening_id, sourcing_event_id, branch_id, supplier_id, snapshot_sha256) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_lines.fk_p36_rec_line_revision"] = "FOREIGN KEY (tenant_id, legal_entity_id, revision_id, draft_id, round_id, unsealing_id, sourcing_event_id, branch_id) REFERENCES procurement.principal_rfq_recommendation_revisions(tenant_id, legal_entity_id, id, draft_id, round_id, unsealing_id, sourcing_event_id, branch_id) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_lines.fk_p36_rec_source_line"] = "FOREIGN KEY (tenant_id, legal_entity_id, sourcing_line_id, sourcing_event_id) REFERENCES procurement.sourcing_lines(tenant_id, legal_entity_id, id, sourcing_event_id) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_receipts.fk_p36_rec_ack_revision"] = "FOREIGN KEY (tenant_id, legal_entity_id, revision_id, draft_id, round_id, unsealing_id, sourcing_event_id, branch_id, actor_principal_id, command_key, request_sha256) REFERENCES procurement.principal_rfq_recommendation_revisions(tenant_id, legal_entity_id, id, draft_id, round_id, unsealing_id, sourcing_event_id, branch_id, actor_principal_id, command_key, request_sha256) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_revisions.fk_p36_rec_prev"] = "FOREIGN KEY (tenant_id, legal_entity_id, previous_revision_id, draft_id, previous_snapshot_sha256) REFERENCES procurement.principal_rfq_recommendation_revisions(tenant_id, legal_entity_id, id, draft_id, snapshot_sha256) ON DELETE RESTRICT",
        ["procurement.principal_rfq_recommendation_revisions.fk_p36_rec_revision_head"] = "FOREIGN KEY (tenant_id, legal_entity_id, draft_id, round_id, unsealing_id, sourcing_event_id, branch_id) REFERENCES procurement.principal_rfq_recommendation_drafts(tenant_id, legal_entity_id, id, round_id, unsealing_id, sourcing_event_id, branch_id) ON DELETE RESTRICT",
    };
    internal static readonly IReadOnlyDictionary<string,string> ParentKeys =
        new Dictionary<string,string>(StringComparer.Ordinal)
    {
        ["procurement.principal_rfq_commercial_offer_submissions.ak_p36_rec_offer"] = "UNIQUE (tenant_id, legal_entity_id, id, opening_id, sourcing_event_id, branch_id, supplier_id, snapshot_sha256)",
        ["procurement.principal_rfq_commercial_round_seals.ak_p36_rec_seal"] = "UNIQUE (tenant_id, legal_entity_id, id, opening_id, sourcing_event_id, branch_id, submission_set_sha256)",
        ["procurement.sourcing_lines.ak_p36_rec_source_line"] = "UNIQUE (tenant_id, legal_entity_id, id, sourcing_event_id)",
    };

    internal static bool Complete(IReadOnlyDictionary<string,string> checks,
        IReadOnlyDictionary<string,string> foreignKeys,
        IReadOnlyDictionary<string,string> parentKeys) =>
        checks.Count == CheckNames.Count && foreignKeys.Count == ForeignNames.Count
        && parentKeys.Count == ParentNames.Count
        && checks.All(x => CheckNames.Contains(x.Key) && x.Value.StartsWith("CHECK (", StringComparison.Ordinal))
        && foreignKeys.All(x => ForeignNames.Contains(x.Key) && x.Value.StartsWith("FOREIGN KEY (", StringComparison.Ordinal))
        && parentKeys.All(x => ParentNames.Contains(x.Key) && x.Value.StartsWith("UNIQUE (", StringComparison.Ordinal));

    internal static bool Populated => Complete(Checks,ForeignKeys,ParentKeys);
    internal static readonly ProcurementPrincipalCommercialSchemaInspection.Capture Physical =
        new(MigrationId,Checks,ForeignKeys,ParentKeys);
}
