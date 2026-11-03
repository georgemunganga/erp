using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7b1CommercialRoundDraftPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_round_original_source",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "id" });

            migrationBuilder.CreateTable(
                name: "commercial_round_enrollments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_version = table.Column<long>(type: "bigint", nullable: false),
                    source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    enrolled_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    enrolled_by_worker_alias = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_round_enrollments", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_enrollments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_round_enrollment_source", x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.branch_id, x.id });
                    table.CheckConstraint("ck_round_enrollment_v1", "schema_version=1 AND source_event_version>0 AND source_sha256 ~ '^[0-9a-f]{64}$' AND original_maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND enrolled_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND isfinite(recorded_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.CheckConstraint("ck_round_nonzero_commercial_round_enrollments", "\"id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"branch_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND (\"enrolled_by_worker_alias\" IS NULL OR \"enrolled_by_worker_alias\" <> '00000000-0000-0000-0000-000000000000'::uuid) AND \"legal_entity_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"source_evidence_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"sourcing_event_id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_round_principals_commercial_round_enrollments", "\"enrolled_by_principal_id\" <> '00000000-0000-0000-0000-000000000000' AND \"original_maker_principal_id\" <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_commercial_round_enrollments_sourcing_events_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_round_original_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.source_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_draft_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_round_plans",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_round_plans", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_plans_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_round_plan_source", x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.branch_id, x.id });
                    table.CheckConstraint("ck_round_nonzero_commercial_round_plans", "\"id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"branch_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"enrollment_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"legal_entity_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"sourcing_event_id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_round_plan_draft", "version>0 AND status IN ('Draft','Cancelled') AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'");
                    table.CheckConstraint("ck_round_principals_commercial_round_plans", "\"maker_principal_id\" <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_round_plan_enrollment",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.branch_id, x.enrollment_id },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_enrollments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "branch_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_round_plan_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expected_version = table.Column<long>(type: "bigint", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias = table.Column<Guid>(type: "uuid", nullable: true),
                    company_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requirements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    proposed_policy_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    disclosure_proposal = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    document_requirements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    technical_release_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    commercial_release_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criterion_count = table.Column<int>(type: "integer", nullable: false),
                    participant_count = table.Column<int>(type: "integer", nullable: false),
                    content_json = table.Column<string>(type: "text", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_round_plan_revisions", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_plan_revisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_round_revision_plan", x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.id });
                    table.UniqueConstraint("ak_round_revision_result", x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.id, x.result_version });
                    table.CheckConstraint("ck_round_nonzero_commercial_round_plan_revisions", "\"id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND (\"actor_worker_alias\" IS NULL OR \"actor_worker_alias\" <> '00000000-0000-0000-0000-000000000000'::uuid) AND \"branch_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"legal_entity_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"plan_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND (\"previous_revision_id\" IS NULL OR \"previous_revision_id\" <> '00000000-0000-0000-0000-000000000000'::uuid) AND \"sourcing_event_id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_round_principals_commercial_round_plan_revisions", "\"actor_principal_id\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_round_revision_v1", "schema_version=1 AND expected_version>=0 AND result_version=expected_version+1 AND operation IN ('Create','Revise','Cancel') AND ((operation='Create' AND expected_version=0 AND previous_revision_id IS NULL) OR (operation<>'Create' AND expected_version>0 AND previous_revision_id IS NOT NULL)) AND result_status=CASE WHEN operation='Cancel' THEN 'Cancelled' ELSE 'Draft' END AND (operation<>'Cancel' OR char_length(btrim(reason)) BETWEEN 1 AND 2000) AND criterion_count BETWEEN 0 AND 100 AND participant_count BETWEEN 0 AND 100 AND octet_length(content_json) BETWEEN 2 AND 2000000 AND content_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND isfinite(recorded_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_round_revision_previous",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.previous_revision_id, x.expected_version },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plan_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "plan_id", "id", "result_version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_round_revision_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.branch_id, x.plan_id },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "branch_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_round_criteria",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rubric = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    maximum_score = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    minimum_score = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_round_criteria", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_criteria_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_round_criterion_v1", "\"order\" BETWEEN 1 AND 100 AND stage IN ('Mandatory','Technical','Commercial') AND kind IN ('PassFail','Weighted') AND char_length(btrim(code)) BETWEEN 1 AND 80 AND (weight IS NULL OR weight>=0) AND (maximum_score IS NULL OR maximum_score>=0) AND (minimum_score IS NULL OR minimum_score>=0) AND (minimum_score IS NULL OR maximum_score IS NULL OR minimum_score<=maximum_score) AND (kind<>'PassFail' OR (weight IS NULL AND maximum_score IS NULL AND minimum_score IS NULL)) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.CheckConstraint("ck_round_nonzero_commercial_round_criteria", "\"id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"legal_entity_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"logical_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"plan_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"revision_id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "fk_round_criterion_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.revision_id },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plan_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "plan_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_round_participants",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    duty = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    conflict_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_round_participants", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_participants_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_round_nonzero_commercial_round_participants", "\"id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"legal_entity_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"logical_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"plan_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"revision_id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_round_participant_v1", "\"order\" BETWEEN 1 AND 100 AND principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND duty IN ('Planner','Reviewer','OpeningOfficer','TechnicalEvaluator','CommercialEvaluator','ConflictReviewer') AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.CheckConstraint("ck_round_principals_commercial_round_participants", "\"principal_id\" <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_round_participant_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.revision_id },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plan_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "plan_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_round_plan_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_round_plan_receipts", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_plan_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_round_nonzero_commercial_round_plan_receipts", "\"id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"branch_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"command_key\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"legal_entity_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"plan_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"revision_id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"sourcing_event_id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_round_principals_commercial_round_plan_receipts", "\"actor_principal_id\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("ck_round_receipt_v1", "result_version>0 AND operation IN ('Create','Revise','Cancel') AND response_status_code=CASE WHEN operation='Create' THEN 201 ELSE 200 END AND octet_length(response_json) BETWEEN 2 AND 65536 AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_round_receipt_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.revision_id, x.result_version },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plan_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "plan_id", "id", "result_version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_criteria_tenant_id_legal_entity_id_plan_id~",
                schema: "procurement",
                table: "commercial_round_criteria",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "revision_id" });

            migrationBuilder.CreateIndex(
                name: "ux_round_criterion_code",
                schema: "procurement",
                table: "commercial_round_criteria",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_round_criterion_logical",
                schema: "procurement",
                table: "commercial_round_criteria",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "logical_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_round_criterion_order",
                schema: "procurement",
                table: "commercial_round_criteria",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_enrollments_tenant_id_legal_entity_id_sour~",
                schema: "procurement",
                table: "commercial_round_enrollments",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "source_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "ux_round_enrollment_event",
                schema: "procurement",
                table: "commercial_round_enrollments",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_participants_tenant_id_legal_entity_id_pla~",
                schema: "procurement",
                table: "commercial_round_participants",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "revision_id" });

            migrationBuilder.CreateIndex(
                name: "ux_round_participant_duty",
                schema: "procurement",
                table: "commercial_round_participants",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "principal_id", "duty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_round_participant_logical",
                schema: "procurement",
                table: "commercial_round_participants",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "logical_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_round_participant_order",
                schema: "procurement",
                table: "commercial_round_participants",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_plan_receipts_tenant_id_legal_entity_id_pl~",
                schema: "procurement",
                table: "commercial_round_plan_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "revision_id", "result_version" });

            migrationBuilder.CreateIndex(
                name: "ux_round_actor_command",
                schema: "procurement",
                table: "commercial_round_plan_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_round_result_receipt",
                schema: "procurement",
                table: "commercial_round_plan_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "result_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_plan_revisions_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "commercial_round_plan_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "previous_revision_id", "expected_version" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_plan_revisions_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "commercial_round_plan_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "branch_id", "plan_id" });

            migrationBuilder.CreateIndex(
                name: "ux_round_revision_version",
                schema: "procurement",
                table: "commercial_round_plan_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "result_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_plans_tenant_id_legal_entity_id_sourcing_e~",
                schema: "procurement",
                table: "commercial_round_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "branch_id", "enrollment_id" });

            migrationBuilder.CreateIndex(
                name: "ux_round_plan_enrollment",
                schema: "procurement",
                table: "commercial_round_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "enrollment_id" },
                unique: true);
            migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.Install);
            migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.AppendOnlyTriggers);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.RetainedDownGuard);
            migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundPlanRetentionSql.UninstallEmpty);
            migrationBuilder.DropTable(
                name: "commercial_round_criteria",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "commercial_round_participants",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "commercial_round_plan_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "commercial_round_plan_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "commercial_round_plans",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "commercial_round_enrollments",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_round_original_source",
                schema: "procurement",
                table: "sourcing_draft_evidence");
        }
    }
}
