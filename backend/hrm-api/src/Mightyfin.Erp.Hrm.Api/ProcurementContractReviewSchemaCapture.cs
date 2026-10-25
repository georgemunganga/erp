using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

// Literal PostgreSQL 17 capture from the accepted private P35 empty roundtrip.
// Provenance is recorded in P35-READINESS-CAPTURE-20261024.json; no normalization.
internal static class ProcurementContractReviewSchemaCapture
{
    internal const string MigrationId = "20261024224934_P35ContractInternalReview";
    internal static readonly Type[] Types = [typeof(ContractReviewPolicyIdentityRecord),
        typeof(ContractReviewSubmission), typeof(ContractReviewVote),
        typeof(ContractReviewCorrection), typeof(ContractReviewCommandReceipt)];
    internal static readonly IReadOnlySet<string> TableNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "procurement.contract_review_policy_identity", "procurement.contract_review_submissions",
        "procurement.contract_review_votes", "procurement.contract_review_corrections", "procurement.contract_review_command_receipts"
    };
    internal static readonly string[] CheckNames = [
        "procurement.contract_review_policy_identity.ck_p35_contract_policy_identity",
        "procurement.contract_review_submissions.ck_p35_submission", "procurement.contract_review_votes.ck_p35_vote",
        "procurement.contract_review_corrections.ck_p35_correction", "procurement.contract_review_command_receipts.ck_p35_receipt"];
    internal static readonly string[] ForeignNames = [
        "procurement.contract_review_policy_identity.fk_p35_contract_policy_created",
        "procurement.contract_review_policy_identity.fk_p35_contract_policy_owner",
        "procurement.contract_review_policy_identity.fk_p35_contract_policy_submitted",
        "procurement.contract_review_submissions.fk_p35_submission_contract",
        "procurement.contract_review_submissions.fk_p35_submission_policy",
        "procurement.contract_review_submissions.fk_p35_submission_version",
        "procurement.contract_review_corrections.fk_p35_correction_submission",
        "procurement.contract_review_corrections.fk_p35_correction_version",
        "procurement.contract_review_votes.fk_p35_vote_submission",
        "procurement.contract_review_command_receipts.fk_p35_receipt_correction",
        "procurement.contract_review_command_receipts.fk_p35_receipt_submission",
        "procurement.contract_review_command_receipts.fk_p35_receipt_vote"];
    internal static readonly string[] ParentNames = ["procurement.supplier_contract_versions.ak_p35_contract_version_owner"];
    internal static readonly IReadOnlyDictionary<string, string> ModelPredicates = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ux_p35_contract_policy_created"] = "stage = 'Created'",
        ["ux_p35_contract_policy_published"] = "stage = 'Published'"
    };
    internal static readonly IReadOnlyDictionary<string, string> Checks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["procurement.contract_review_command_receipts.ck_p35_receipt"] = "CHECK (((id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (command_key <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (command_sha256 IS NOT NULL) AND ((command_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (response_sha256 IS NOT NULL) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (response_json IS NOT NULL) AND (length(response_json) > 2) AND ((((operation)::text = 'Submit'::text) AND (vote_id IS NULL) AND (correction_id IS NULL) AND (response_status = 201)) OR (((operation)::text = 'Decide'::text) AND (vote_id IS NOT NULL) AND (correction_id IS NULL) AND (response_status = 200)) OR (((operation)::text = 'Correction'::text) AND (vote_id IS NULL) AND (correction_id IS NOT NULL) AND (response_status = 200))) AND (result_contract_version > 0) AND isfinite(completed_at) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))",
        ["procurement.contract_review_corrections.ck_p35_correction"] = "CHECK (((id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (contract_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (contract_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (submission_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (review_branch_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (from_contract_version > 0) AND (to_contract_version = (from_contract_version + 1)) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((actor_worker_alias_id IS NULL) OR (actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (reason IS NOT NULL) AND (length(TRIM(BOTH FROM reason)) > 0) AND (length((reason)::text) <= 2000) AND isfinite(corrected_at) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))",
        ["procurement.contract_review_policy_identity.ck_p35_contract_policy_identity"] = "CHECK (((schema_version = 2) AND ((policy_key)::text = 'SUPPLIER_CONTRACT_REVIEW'::text) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_number > 0) AND (policy_record_version_at_action > 0) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (rules_sha256 IS NOT NULL) AND ((rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (policy_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (policy_record_version_at_action > 1) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Published'::text) AND (policy_record_version_at_action > 2) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))",
        ["procurement.contract_review_submissions.ck_p35_submission"] = "CHECK (((schema_version = 1) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (contract_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (contract_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (review_branch_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((policy_key)::text = 'SUPPLIER_CONTRACT_REVIEW'::text) AND (contract_business_version > 0) AND (contract_version_business_version > 0) AND (submission_number > 0) AND (policy_version_number > 0) AND (maker_principal_id IS NOT NULL) AND ((maker_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((maker_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((maker_worker_alias_id IS NULL) OR (maker_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND (policy_rules_sha256 IS NOT NULL) AND ((policy_rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (assignment_pins_sha256 IS NOT NULL) AND ((assignment_pins_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (assignment_pins_json IS NOT NULL) AND (length(assignment_pins_json) > 2) AND (terms_sha256 IS NOT NULL) AND ((terms_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (terms_json IS NOT NULL) AND (length(terms_json) > 2) AND isfinite(submitted_at) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))",
        ["procurement.contract_review_votes.ck_p35_vote"] = "CHECK (((id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_principal_id IS NOT NULL) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND ((actor_worker_alias_id IS NULL) OR (actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((action)::text = ANY ((ARRAY['Approve'::character varying, 'Return'::character varying, 'Reject'::character varying])::text[])) AND isfinite(decided_at) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))",
    };
    internal static readonly IReadOnlyDictionary<string, string> ForeignKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["procurement.contract_review_command_receipts.fk_p35_receipt_correction"] = "FOREIGN KEY (tenant_id, legal_entity_id, submission_id, correction_id, contract_id, review_branch_id) REFERENCES procurement.contract_review_corrections(tenant_id, legal_entity_id, submission_id, id, contract_id, review_branch_id) ON DELETE RESTRICT",
        ["procurement.contract_review_command_receipts.fk_p35_receipt_submission"] = "FOREIGN KEY (tenant_id, legal_entity_id, submission_id, contract_id, review_branch_id) REFERENCES procurement.contract_review_submissions(tenant_id, legal_entity_id, id, contract_id, review_branch_id) ON DELETE RESTRICT",
        ["procurement.contract_review_command_receipts.fk_p35_receipt_vote"] = "FOREIGN KEY (tenant_id, legal_entity_id, submission_id, vote_id, contract_id, review_branch_id) REFERENCES procurement.contract_review_votes(tenant_id, legal_entity_id, submission_id, id, contract_id, review_branch_id) ON DELETE RESTRICT",
        ["procurement.contract_review_corrections.fk_p35_correction_submission"] = "FOREIGN KEY (tenant_id, legal_entity_id, submission_id, contract_id, review_branch_id, core_case_id) REFERENCES procurement.contract_review_submissions(tenant_id, legal_entity_id, id, contract_id, review_branch_id, core_case_id) ON DELETE RESTRICT",
        ["procurement.contract_review_corrections.fk_p35_correction_version"] = "FOREIGN KEY (tenant_id, legal_entity_id, contract_id, contract_version_id) REFERENCES procurement.supplier_contract_versions(tenant_id, legal_entity_id, supplier_contract_id, id) ON DELETE RESTRICT",
        ["procurement.contract_review_policy_identity.fk_p35_contract_policy_created"] = "FOREIGN KEY (tenant_id, legal_entity_id, policy_version_id, policy_key, created_evidence_id, created_evidence_stage) REFERENCES procurement.contract_review_policy_identity(tenant_id, legal_entity_id, policy_version_id, policy_key, id, stage) ON DELETE RESTRICT",
        ["procurement.contract_review_policy_identity.fk_p35_contract_policy_owner"] = "FOREIGN KEY (tenant_id, legal_entity_id, policy_version_id, policy_key, policy_version_number) REFERENCES procurement.policy_versions(tenant_id, legal_entity_id, id, policy_key, policy_version_number) ON DELETE RESTRICT",
        ["procurement.contract_review_policy_identity.fk_p35_contract_policy_submitted"] = "FOREIGN KEY (tenant_id, legal_entity_id, policy_version_id, policy_key, submitted_evidence_id, submitted_evidence_stage) REFERENCES procurement.contract_review_policy_identity(tenant_id, legal_entity_id, policy_version_id, policy_key, id, stage) ON DELETE RESTRICT",
        ["procurement.contract_review_submissions.fk_p35_submission_contract"] = "FOREIGN KEY (tenant_id, legal_entity_id, contract_id) REFERENCES procurement.supplier_contracts(tenant_id, legal_entity_id, id) ON DELETE RESTRICT",
        ["procurement.contract_review_submissions.fk_p35_submission_policy"] = "FOREIGN KEY (tenant_id, legal_entity_id, policy_version_id, policy_key, policy_version_number) REFERENCES procurement.policy_versions(tenant_id, legal_entity_id, id, policy_key, policy_version_number) ON DELETE RESTRICT",
        ["procurement.contract_review_submissions.fk_p35_submission_version"] = "FOREIGN KEY (tenant_id, legal_entity_id, contract_id, contract_version_id) REFERENCES procurement.supplier_contract_versions(tenant_id, legal_entity_id, supplier_contract_id, id) ON DELETE RESTRICT",
        ["procurement.contract_review_votes.fk_p35_vote_submission"] = "FOREIGN KEY (tenant_id, legal_entity_id, submission_id, contract_id, review_branch_id, core_case_id) REFERENCES procurement.contract_review_submissions(tenant_id, legal_entity_id, id, contract_id, review_branch_id, core_case_id) ON DELETE RESTRICT",
    };
    internal static readonly IReadOnlyDictionary<string, string> ParentKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["procurement.supplier_contract_versions.ak_p35_contract_version_owner"] = "UNIQUE (tenant_id, legal_entity_id, supplier_contract_id, id)",
    };
    internal static readonly IReadOnlyDictionary<string, string> PartialPredicates = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ux_p35_contract_policy_created"] = "((stage)::text = 'Created'::text)",
        ["ux_p35_contract_policy_published"] = "((stage)::text = 'Published'::text)",
    };
    internal static bool Complete(IReadOnlyDictionary<string, string> checks,
        IReadOnlyDictionary<string, string> foreigns, IReadOnlyDictionary<string, string> parents,
        IReadOnlyDictionary<string, string> predicates)
    {
        static bool Exact(IReadOnlyDictionary<string, string> values, IEnumerable<string> names)
            => values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(names)
                && values.Values.All(x => !string.IsNullOrWhiteSpace(x));
        return Exact(checks, CheckNames) && Exact(foreigns, ForeignNames)
            && Exact(parents, ParentNames) && Exact(predicates, ModelPredicates.Keys);
    }
    internal static bool Populated => Complete(Checks, ForeignKeys, ParentKeys, PartialPredicates);
    internal static readonly ProcurementPrincipalCommercialSchemaInspection.Capture Physical =
        new(MigrationId, Checks, ForeignKeys, ParentKeys);
}
