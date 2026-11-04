using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M7b1cCommercialRoundReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_round_plan_draft",
                schema: "procurement",
                table: "commercial_round_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_round_revision_v1",
                schema: "procurement",
                table: "commercial_round_plan_revisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_round_receipt_v1",
                schema: "procurement",
                table: "commercial_round_plan_receipts");

            migrationBuilder.CreateTable(
                name: "commercial_round_review_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_plan_version = table.Column<long>(type: "bigint", nullable: false),
                    submitted_content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<long>(type: "bigint", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_worker_alias = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_commercial_round_review_submissions", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_review_submissions_tenant_id_legal_entity_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_round_review_submission_plan", x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.id });
                    table.CheckConstraint("ck_round_review_submission", "submitted_plan_version>0 AND policy_version_number>0 AND submitted_content_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND submitted_revision_id<>'00000000-0000-0000-0000-000000000000'::uuid AND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid AND isfinite(submitted_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_round_review_plan",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id, x.branch_id, x.plan_id },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "branch_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_round_review_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_round_review_submitted_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.submitted_revision_id, x.submitted_plan_version },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plan_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "plan_id", "id", "result_version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_round_review_outcomes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_plan_version = table.Column<long>(type: "bigint", nullable: false),
                    result_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_commercial_round_review_outcomes", x => x.id);
                    table.UniqueConstraint("AK_commercial_round_review_outcomes_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_round_review_outcome", "result_plan_version>0 AND action IN ('Approve','Return','Reject') AND ((action='Approve' AND result_status IN ('InReview','Published') AND reason IS NULL) OR (action='Return' AND result_status='Draft' AND char_length(btrim(reason)) BETWEEN 1 AND 2000) OR (action='Reject' AND result_status='Rejected' AND char_length(btrim(reason)) BETWEEN 1 AND 2000)) AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND isfinite(recorded_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "fk_round_review_outcome_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.result_revision_id, x.result_plan_version },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_plan_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "plan_id", "id", "result_version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_round_review_outcome_submission",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.plan_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "commercial_round_review_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "plan_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW','PROCUREMENT_PLAN_REVIEW','COMMERCIAL_ROUND_PLAN_REVIEW') AND (policy_key NOT IN ('MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW','PROCUREMENT_PLAN_REVIEW','COMMERCIAL_ROUND_PLAN_REVIEW') OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_round_plan_draft",
                schema: "procurement",
                table: "commercial_round_plans",
                sql: "version>0 AND status IN ('Draft','InReview','Rejected','Published','Cancelled') AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_round_revision_v1",
                schema: "procurement",
                table: "commercial_round_plan_revisions",
                sql: "schema_version=1 AND expected_version>=0 AND result_version=expected_version+1 AND operation IN ('Create','Revise','Cancel','Submit','Return','Reject','Approve') AND ((operation='Create' AND expected_version=0 AND previous_revision_id IS NULL) OR (operation<>'Create' AND expected_version>0 AND previous_revision_id IS NOT NULL)) AND ((operation IN ('Create','Revise','Return') AND result_status='Draft') OR (operation='Cancel' AND result_status='Cancelled') OR (operation='Submit' AND result_status='InReview') OR (operation='Reject' AND result_status='Rejected') OR (operation='Approve' AND result_status IN ('InReview','Published'))) AND ((operation IN ('Cancel','Return','Reject') AND char_length(btrim(reason)) BETWEEN 1 AND 2000) OR (operation NOT IN ('Cancel','Return','Reject') AND reason IS NULL)) AND criterion_count BETWEEN 0 AND 100 AND participant_count BETWEEN 0 AND 100 AND octet_length(content_json) BETWEEN 2 AND 2000000 AND content_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND isfinite(recorded_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_round_receipt_v1",
                schema: "procurement",
                table: "commercial_round_plan_receipts",
                sql: "result_version>0 AND operation IN ('Create','Revise','Cancel','Submit','Return','Reject','Approve') AND response_status_code=CASE WHEN operation IN ('Create','Submit') THEN 201 ELSE 200 END AND octet_length(response_json) BETWEEN 2 AND 65536 AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND version=1 AND updated_at IS NULL AND updated_by IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_review_outcomes_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "commercial_round_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_review_outcomes_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "commercial_round_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "result_revision_id", "result_plan_version" });

            migrationBuilder.CreateIndex(
                name: "ux_round_review_core_decision",
                schema: "procurement",
                table: "commercial_round_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_round_review_result_version",
                schema: "procurement",
                table: "commercial_round_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "result_plan_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_review_submissions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "commercial_round_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_review_submissions_tenant_id_legal_entity~1",
                schema: "procurement",
                table: "commercial_round_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "submitted_revision_id", "submitted_plan_version" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_round_review_submissions_tenant_id_legal_entity~2",
                schema: "procurement",
                table: "commercial_round_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "branch_id", "plan_id" });

            migrationBuilder.CreateIndex(
                name: "ux_round_review_core_case",
                schema: "procurement",
                table: "commercial_round_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_round_review_submission_version",
                schema: "procurement",
                table: "commercial_round_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "plan_id", "submitted_plan_version" },
                unique: true);
            migrationBuilder.Sql(global::Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundReviewSql.Install);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(global::Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundReviewSql.RetainedDownGuard);
            migrationBuilder.Sql(global::Mightyfin.Erp.Procurement.Infrastructure.Data.CommercialRoundReviewSql.UninstallEmpty);
            migrationBuilder.DropTable(
                name: "commercial_round_review_outcomes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "commercial_round_review_submissions",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_round_plan_draft",
                schema: "procurement",
                table: "commercial_round_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_round_revision_v1",
                schema: "procurement",
                table: "commercial_round_plan_revisions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_round_receipt_v1",
                schema: "procurement",
                table: "commercial_round_plan_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW','PROCUREMENT_PLAN_REVIEW') AND (policy_key NOT IN ('MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW','PROCUREMENT_PLAN_REVIEW') OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_round_plan_draft",
                schema: "procurement",
                table: "commercial_round_plans",
                sql: "version>0 AND status IN ('Draft','Cancelled') AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_round_revision_v1",
                schema: "procurement",
                table: "commercial_round_plan_revisions",
                sql: "schema_version=1 AND expected_version>=0 AND result_version=expected_version+1 AND operation IN ('Create','Revise','Cancel') AND ((operation='Create' AND expected_version=0 AND previous_revision_id IS NULL) OR (operation<>'Create' AND expected_version>0 AND previous_revision_id IS NOT NULL)) AND result_status=CASE WHEN operation='Cancel' THEN 'Cancelled' ELSE 'Draft' END AND (operation<>'Cancel' OR char_length(btrim(reason)) BETWEEN 1 AND 2000) AND criterion_count BETWEEN 0 AND 100 AND participant_count BETWEEN 0 AND 100 AND octet_length(content_json) BETWEEN 2 AND 2000000 AND content_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND isfinite(recorded_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_round_receipt_v1",
                schema: "procurement",
                table: "commercial_round_plan_receipts",
                sql: "result_version>0 AND operation IN ('Create','Revise','Cancel') AND response_status_code=CASE WHEN operation='Create' THEN 201 ELSE 200 END AND octet_length(response_json) BETWEEN 2 AND 65536 AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND version=1 AND updated_at IS NULL AND updated_by IS NULL");
        }
    }
}
