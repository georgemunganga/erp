using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProcurementPlanGovernedReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.AddColumn<Guid>(
                name: "branch_ref",
                schema: "procurement",
                table: "procurement_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "plan_review_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    procurement_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_plan_review_command_receipts", x => x.id);
                    table.UniqueConstraint("AK_plan_review_command_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_plan_review_receipt", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND operation IN ('ClaimScope','Submit','Decide','Withdraw') AND (decision_id IS NULL OR decision_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND ((operation IN ('Decide','Withdraw') AND decision_id IS NOT NULL) OR (operation IN ('ClaimScope','Submit') AND decision_id IS NULL)) AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_status BETWEEN 200 AND 299");
                    table.ForeignKey(
                        name: "FK_plan_review_command_receipts_procurement_plans_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.procurement_plan_id },
                        principalSchema: "procurement",
                        principalTable: "procurement_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_review_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    procurement_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_ref = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_plan_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    maker_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    maker_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    estimated_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_plan_review_submissions", x => x.id);
                    table.UniqueConstraint("AK_plan_review_submissions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_plan_review_submission", "submitted_plan_version > 0 AND estimated_amount >= 0 AND branch_ref <> '00000000-0000-0000-0000-000000000000'::uuid AND (maker_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND state IN ('Pending','Returned','Rejected','Approved','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_plan_review_submissions_policy_versions_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_review_submissions_procurement_plans_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.procurement_plan_id },
                        principalSchema: "procurement",
                        principalTable: "procurement_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_plans_tenant_id_legal_entity_id_branch_ref_stat~",
                schema: "procurement",
                table: "procurement_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "branch_ref", "status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW','PROCUREMENT_PLAN_REVIEW') AND (policy_key NOT IN ('MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW','PROCUREMENT_PLAN_REVIEW') OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.CreateIndex(
                name: "IX_plan_review_command_receipts_tenant_id_legal_entity_id_comm~",
                schema: "procurement",
                table: "plan_review_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_review_command_receipts_tenant_id_legal_entity_id_deci~",
                schema: "procurement",
                table: "plan_review_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "decision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_review_command_receipts_tenant_id_legal_entity_id_proc~",
                schema: "procurement",
                table: "plan_review_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "procurement_plan_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_review_submissions_tenant_id_legal_entity_id_core_appr~",
                schema: "procurement",
                table: "plan_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plan_review_submissions_tenant_id_legal_entity_id_policy_ve~",
                schema: "procurement",
                table: "plan_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_review_submissions_tenant_id_legal_entity_id_procureme~",
                schema: "procurement",
                table: "plan_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "procurement_plan_id", "submitted_plan_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retain every review receipt/round, Plan policy identity lineage and
            // claimed Draft branch. The default EF migration transaction holds
            // these locks until every subsequent Down operation commits or rolls back.
            migrationBuilder.Sql("""
                DO $plan_review_retention$
                BEGIN
                    -- Acquire the eventual destructive locks before inspecting retained data.
                    -- This blocks a concurrent writer from adding evidence after the checks.
                    LOCK TABLE procurement.operational_policy_identity_evidence,
                        procurement.plan_review_command_receipts,
                        procurement.plan_review_submissions,
                        procurement.procurement_plans IN ACCESS EXCLUSIVE MODE;
                
                    IF EXISTS (SELECT 1 FROM procurement.plan_review_command_receipts)
                        OR EXISTS (SELECT 1 FROM procurement.plan_review_submissions)
                        OR EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence
                            WHERE policy_key = 'PROCUREMENT_PLAN_REVIEW')
                        OR EXISTS (SELECT 1 FROM procurement.procurement_plans
                            WHERE branch_ref IS NOT NULL)
                    THEN
                        RAISE EXCEPTION USING ERRCODE = '55000',
                            MESSAGE = 'Retain Plan review evidence and claimed plan branch scope; use a reviewed forward recovery.';
                    END IF;
                END;
                $plan_review_retention$;
                """);

            migrationBuilder.DropTable(
                name: "plan_review_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "plan_review_submissions",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_procurement_plans_tenant_id_legal_entity_id_branch_ref_stat~",
                schema: "procurement",
                table: "procurement_plans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropColumn(
                name: "branch_ref",
                schema: "procurement",
                table: "procurement_plans");

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW') AND (policy_key NOT IN ('MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW') OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
        }
    }
}
