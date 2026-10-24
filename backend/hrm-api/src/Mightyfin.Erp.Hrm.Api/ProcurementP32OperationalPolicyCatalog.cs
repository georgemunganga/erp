namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Exact PostgreSQL 17 P32 policy catalog capture. Physical shape only:
/// current actor, scope, finite privileges, and business policy remain separate checks.</summary>
internal static class ProcurementP32OperationalPolicyCatalog
{
    internal const string MigrationId = "20261024182504_P32PrincipalCommercialEvidence";
    // Private read-only repeatable-read capture after genuine P32 Up/Down/Up:
    // proc_p32_schema_20261024_02, result SHA 5a5496a90a50e76cba5349065a89706cf1b4e95f304debe48b5c8a242de42801.
    // CHECK SHA 5c5567961eda3b19ddc9f27a322ccb49946b4d058b9968504869a55edeb75afe.
    internal const string CheckDefinition = "CHECK (((schema_version = ANY (ARRAY[1, 2])) AND ((policy_key)::text = ANY ((ARRAY['RECEIVING'::character varying, 'INVOICE_MATCHING'::character varying, 'INVOICE_UNIT_REVIEW'::character varying, 'MANUAL_PAYMENT_REVIEW'::character varying, 'SOURCING_COMMERCIAL_ROUND'::character varying])::text[])) AND (((policy_key)::text <> ALL ((ARRAY['MANUAL_PAYMENT_REVIEW'::character varying, 'SOURCING_COMMERCIAL_ROUND'::character varying])::text[])) OR (schema_version = 2)) AND (id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (policy_version_number > 0) AND (policy_record_version_at_action > 0) AND ((actor_worker_id IS NULL) OR (actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)) AND ((schema_version = 2) OR (actor_worker_id IS NOT NULL)) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((actor_principal_id)::text <> '00000000-0000-0000-0000-000000000000'::text) AND (rules_sha256 IS NOT NULL) AND ((rules_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND (evidence_sha256 IS NOT NULL) AND ((evidence_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND isfinite(action_at) AND (created_at = action_at) AND ((created_by)::text = (actor_principal_id)::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL) AND ((((stage)::text = 'Created'::text) AND (policy_record_version_at_action = 1) AND (created_evidence_id IS NULL) AND (created_evidence_stage IS NULL) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Submitted'::text) AND (policy_record_version_at_action > 1) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NULL) AND (submitted_evidence_stage IS NULL)) OR (((stage)::text = 'Published'::text) AND (policy_record_version_at_action > 2) AND (created_evidence_id IS NOT NULL) AND (created_evidence_id <> id) AND (created_evidence_stage IS NOT NULL) AND ((created_evidence_stage)::text = 'Created'::text) AND (submitted_evidence_id IS NOT NULL) AND (submitted_evidence_id <> id) AND (submitted_evidence_id <> created_evidence_id) AND (submitted_evidence_stage IS NOT NULL) AND ((submitted_evidence_stage)::text = 'Submitted'::text)))))";
    // Exact existing FingerprintSql against that same physical catalog; older P28
    // fingerprint remains valid only when P32 is not applied.
    internal const string ManualPaymentFingerprint = "c77d6d40a5cbd3b981e43338d56a8d337546a546c195656c969d3c96d148d129";

    internal static string? SelectCheck(IReadOnlyCollection<string> applied)
    {
        var p21 = applied.Contains(ProcurementOperationalPolicyIdentitySchemaReadiness.MigrationId);
        var p23 = applied.Contains(ProcurementInvoiceUnitReviewSchemaReadiness.MigrationId);
        var p28 = applied.Contains(ProcurementManualPaymentGovernanceReadiness.MigrationId);
        var p32 = applied.Contains(MigrationId);
        if (!p21 || (p28 && !p23) || (p32 && !p28)) return null;
        if (p32) return CheckDefinition;
        if (p28) return ProcurementManualPaymentGovernanceReadiness.OperationalCheckDefinition;
        if (p23) return ProcurementInvoiceUnitReviewSchemaReadiness.OperationalCheckDefinition;
        return ProcurementOperationalPolicyIdentitySchemaReadiness.CheckDefinition;
    }

    internal static string? SelectManualPaymentFingerprint(IReadOnlyCollection<string> applied)
    {
        if (!applied.Contains(ProcurementOperationalPolicyIdentitySchemaReadiness.MigrationId)
            || !applied.Contains(ProcurementInvoiceUnitReviewSchemaReadiness.MigrationId)
            || !applied.Contains(ProcurementManualPaymentGovernanceReadiness.MigrationId)) return null;
        return applied.Contains(MigrationId) ? ManualPaymentFingerprint
            : ProcurementManualPaymentGovernanceReadiness.ExpectedFingerprint;
    }
}
