using static Mightyfin.Erp.Hrm.Api.ProcurementSchemaInspectionSql;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Only exact named CHECK spellings observed in a genuine PG17.11
/// pg_dump/pg_restore. Every other definition and all other physical fields stay
/// unchanged in the complete B1 fingerprint; no general SQL normalization.</summary>
internal static class CommercialRoundRestoredCheckForms
{
    internal sealed record CheckForm(string Table, string Name, string Direct, string Restored);

    internal static readonly IReadOnlyList<CheckForm> Observed = Array.AsReadOnly(new[]
    {
        new CheckForm("commercial_round_criteria", "ck_round_criterion_v1",
            """
CHECK (((("order" >= 1) AND ("order" <= 100)) AND ((stage)::text = ANY ((ARRAY['Mandatory'::character varying, 'Technical'::character varying, 'Commercial'::character varying])::text[])) AND ((kind)::text = ANY ((ARRAY['PassFail'::character varying, 'Weighted'::character varying])::text[])) AND ((char_length(btrim((code)::text)) >= 1) AND (char_length(btrim((code)::text)) <= 80)) AND ((weight IS NULL) OR (weight >= (0)::numeric)) AND ((maximum_score IS NULL) OR (maximum_score >= (0)::numeric)) AND ((minimum_score IS NULL) OR (minimum_score >= (0)::numeric)) AND ((minimum_score IS NULL) OR (maximum_score IS NULL) OR (minimum_score <= maximum_score)) AND (((kind)::text <> 'PassFail'::text) OR ((weight IS NULL) AND (maximum_score IS NULL) AND (minimum_score IS NULL))) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
""",
            """
CHECK ((("order" >= 1) AND ("order" <= 100) AND ((stage)::text = ANY (ARRAY[('Mandatory'::character varying)::text, ('Technical'::character varying)::text, ('Commercial'::character varying)::text])) AND ((kind)::text = ANY (ARRAY[('PassFail'::character varying)::text, ('Weighted'::character varying)::text])) AND ((char_length(btrim((code)::text)) >= 1) AND (char_length(btrim((code)::text)) <= 80)) AND ((weight IS NULL) OR (weight >= (0)::numeric)) AND ((maximum_score IS NULL) OR (maximum_score >= (0)::numeric)) AND ((minimum_score IS NULL) OR (minimum_score >= (0)::numeric)) AND ((minimum_score IS NULL) OR (maximum_score IS NULL) OR (minimum_score <= maximum_score)) AND (((kind)::text <> 'PassFail'::text) OR ((weight IS NULL) AND (maximum_score IS NULL) AND (minimum_score IS NULL))) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
"""),
        new CheckForm("commercial_round_participants", "ck_round_participant_v1",
            """
CHECK (((("order" >= 1) AND ("order" <= 100)) AND ((principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((duty)::text = ANY ((ARRAY['Planner'::character varying, 'Reviewer'::character varying, 'OpeningOfficer'::character varying, 'TechnicalEvaluator'::character varying, 'CommercialEvaluator'::character varying, 'ConflictReviewer'::character varying])::text[])) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
""",
            """
CHECK ((("order" >= 1) AND ("order" <= 100) AND ((principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND ((duty)::text = ANY (ARRAY[('Planner'::character varying)::text, ('Reviewer'::character varying)::text, ('OpeningOfficer'::character varying)::text, ('TechnicalEvaluator'::character varying)::text, ('CommercialEvaluator'::character varying)::text, ('ConflictReviewer'::character varying)::text])) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
"""),
        new CheckForm("commercial_round_plan_receipts", "ck_round_receipt_v1",
            """
CHECK (((result_version > 0) AND ((operation)::text = ANY ((ARRAY['Create'::character varying, 'Revise'::character varying, 'Cancel'::character varying])::text[])) AND (response_status_code =
CASE
    WHEN ((operation)::text = 'Create'::text) THEN 201
    ELSE 200
END) AND ((octet_length(response_json) >= 2) AND (octet_length(response_json) <= 65536)) AND ((request_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
""",
            """
CHECK (((result_version > 0) AND ((operation)::text = ANY (ARRAY[('Create'::character varying)::text, ('Revise'::character varying)::text, ('Cancel'::character varying)::text])) AND (response_status_code =
CASE
    WHEN ((operation)::text = 'Create'::text) THEN 201
    ELSE 200
END) AND ((octet_length(response_json) >= 2) AND (octet_length(response_json) <= 65536)) AND ((request_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((response_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
"""),
        new CheckForm("commercial_round_plan_revisions", "ck_round_revision_v1",
            """
CHECK (((schema_version = 1) AND (expected_version >= 0) AND (result_version = (expected_version + 1)) AND ((operation)::text = ANY ((ARRAY['Create'::character varying, 'Revise'::character varying, 'Cancel'::character varying])::text[])) AND ((((operation)::text = 'Create'::text) AND (expected_version = 0) AND (previous_revision_id IS NULL)) OR (((operation)::text <> 'Create'::text) AND (expected_version > 0) AND (previous_revision_id IS NOT NULL))) AND ((result_status)::text =
CASE
    WHEN ((operation)::text = 'Cancel'::text) THEN 'Cancelled'::text
    ELSE 'Draft'::text
END) AND (((operation)::text <> 'Cancel'::text) OR ((char_length(btrim((reason)::text)) >= 1) AND (char_length(btrim((reason)::text)) <= 2000))) AND ((criterion_count >= 0) AND (criterion_count <= 100)) AND ((participant_count >= 0) AND (participant_count <= 100)) AND ((octet_length(content_json) >= 2) AND (octet_length(content_json) <= 2000000)) AND ((content_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND isfinite(recorded_at) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
""",
            """
CHECK (((schema_version = 1) AND (expected_version >= 0) AND (result_version = (expected_version + 1)) AND ((operation)::text = ANY (ARRAY[('Create'::character varying)::text, ('Revise'::character varying)::text, ('Cancel'::character varying)::text])) AND ((((operation)::text = 'Create'::text) AND (expected_version = 0) AND (previous_revision_id IS NULL)) OR (((operation)::text <> 'Create'::text) AND (expected_version > 0) AND (previous_revision_id IS NOT NULL))) AND ((result_status)::text =
CASE
    WHEN ((operation)::text = 'Cancel'::text) THEN 'Cancelled'::text
    ELSE 'Draft'::text
END) AND (((operation)::text <> 'Cancel'::text) OR ((char_length(btrim((reason)::text)) >= 1) AND (char_length(btrim((reason)::text)) <= 2000))) AND ((criterion_count >= 0) AND (criterion_count <= 100)) AND ((participant_count >= 0) AND (participant_count <= 100)) AND ((octet_length(content_json) >= 2) AND (octet_length(content_json) <= 2000000)) AND ((content_sha256)::text ~ '^[0-9a-f]{64}$'::text) AND ((actor_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text) AND isfinite(recorded_at) AND (version = 1) AND (updated_at IS NULL) AND (updated_by IS NULL)))
"""),
        new CheckForm("commercial_round_plans", "ck_round_plan_draft",
            """
CHECK (((version > 0) AND ((status)::text = ANY ((ARRAY['Draft'::character varying, 'Cancelled'::character varying])::text[])) AND ((maker_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text)))
""",
            """
CHECK (((version > 0) AND ((status)::text = ANY (ARRAY[('Draft'::character varying)::text, ('Cancelled'::character varying)::text])) AND ((maker_principal_id)::text ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'::text)))
"""),
    });

    internal static string CanonicalDefinition(string table, string name, string definition)
        => Observed.FirstOrDefault(x => x.Table == table && x.Name == name && x.Restored == definition)
            ?.Direct ?? definition;

    // r/c are the existing captured relation/constraint aliases. Match the
    // complete observed spelling, schema-qualified by the outer B1 relation set.
    internal static readonly string Sql = "CASE " + string.Join(" ", Observed.Select(x =>
        "WHEN r.relname=" + Literal(x.Table) + " AND c.conname=" + Literal(x.Name)
        + " AND c.contype='c' AND pg_get_constraintdef(c.oid)=" + Literal(x.Restored)
        + " THEN " + Literal(x.Direct))) + " ELSE pg_get_constraintdef(c.oid) END";
}
