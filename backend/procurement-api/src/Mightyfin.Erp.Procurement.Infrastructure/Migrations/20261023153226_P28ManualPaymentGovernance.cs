using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P28ManualPaymentGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "procurement",
                table: "manual_payment_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_manual_payment_decision_governance",
                schema: "procurement",
                table: "manual_payment_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "manual_payment_request_id", "branch_id", "to_version" });

            migrationBuilder.CreateTable(
                name: "manual_payment_date_observations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_payment_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_payment_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_version = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    company_business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    company_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_manual_payment_date_observations", x => x.id);
                    table.UniqueConstraint("AK_manual_payment_date_observations_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_manual_payment_company_date", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND version = 1 AND request_version > 0 AND action IN ('Record','Confirm') AND company_business_date > DATE '0001-01-01' AND length(btrim(company_time_zone_id)) BETWEEN 1 AND 100 AND isfinite(observed_at_utc)");
                    table.ForeignKey(
                        name: "FK_manual_payment_date_observations_manual_payment_decisions_t~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.manual_payment_decision_id, x.manual_payment_request_id, x.branch_id, x.request_version },
                        principalSchema: "procurement",
                        principalTable: "manual_payment_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "manual_payment_request_id", "branch_id", "to_version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manual_payment_workflow_cases",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_payment_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opened_request_version = table.Column<long>(type: "bigint", nullable: false),
                    opening_manual_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    assignee_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_version_at_opening = table.Column<long>(type: "bigint", nullable: false),
                    invoice_gross_amount_at_opening = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_manual_payment_workflow_cases", x => x.id);
                    table.UniqueConstraint("ak_manual_payment_case_source", x => new { x.tenant_id, x.legal_entity_id, x.id, x.manual_payment_request_id, x.branch_id });
                    table.UniqueConstraint("AK_manual_payment_workflow_cases_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_manual_payment_case_source", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND version = 1 AND purpose IN ('Approval','Confirmation') AND opened_request_version > 0 AND invoice_version_at_opening > 0 AND invoice_gross_amount_at_opening > 0 AND invoice_gross_amount_at_opening <> 'NaN'::numeric AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_approval_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_approval_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number > 0 AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND assignee_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND assignee_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id <> assignee_principal_id AND (maker_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (assignee_core_linked_worker_id IS NULL OR assignee_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (maker_core_linked_worker_id IS NULL OR assignee_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> assignee_core_linked_worker_id) AND length(policy_rules_sha256)=64 AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND length(snapshot_sha256)=64 AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(snapshot_json) BETWEEN 1 AND 65536 AND isfinite(opened_at)");
                    table.ForeignKey(
                        name: "FK_manual_payment_workflow_cases_manual_payment_decisions_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_manual_decision_id, x.manual_payment_request_id, x.branch_id, x.opened_request_version },
                        principalSchema: "procurement",
                        principalTable: "manual_payment_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "manual_payment_request_id", "branch_id", "to_version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_payment_workflow_cases_policy_versions_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manual_payment_core_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_payment_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_case_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_payment_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_request_version = table.Column<long>(type: "bigint", nullable: false),
                    to_request_version = table.Column<long>(type: "bigint", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_manual_payment_core_decisions", x => x.id);
                    table.UniqueConstraint("AK_manual_payment_core_decisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_manual_payment_core_decision", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND version = 1 AND core_approval_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND from_request_version > 0 AND to_request_version = from_request_version + 1 AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND manual_action IN ('Approve','Return','Reject','Confirm','Dispute','Cancel') AND isfinite(decided_at)");
                    table.ForeignKey(
                        name: "FK_manual_payment_core_decisions_manual_payment_decisions_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.manual_payment_decision_id, x.manual_payment_request_id, x.branch_id, x.to_request_version },
                        principalSchema: "procurement",
                        principalTable: "manual_payment_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "manual_payment_request_id", "branch_id", "to_version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_payment_core_decisions_manual_payment_workflow_cases~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.workflow_case_link_id, x.manual_payment_request_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "manual_payment_workflow_cases",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "manual_payment_request_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW') AND (policy_key <> 'MANUAL_PAYMENT_REVIEW' OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_core_decisions_tenant_id_legal_entity_id_cor~",
                schema: "procurement",
                table: "manual_payment_core_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_core_decisions_tenant_id_legal_entity_id_ma~1",
                schema: "procurement",
                table: "manual_payment_core_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_decision_id", "manual_payment_request_id", "branch_id", "to_request_version" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_core_decisions_tenant_id_legal_entity_id_man~",
                schema: "procurement",
                table: "manual_payment_core_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_core_decisions_tenant_id_legal_entity_id_wor~",
                schema: "procurement",
                table: "manual_payment_core_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "workflow_case_link_id", "manual_payment_request_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_date_observations_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "manual_payment_date_observations",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_date_observations_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "manual_payment_date_observations",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_decision_id", "manual_payment_request_id", "branch_id", "request_version" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_workflow_cases_tenant_id_legal_entity_id_cor~",
                schema: "procurement",
                table: "manual_payment_workflow_cases",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_workflow_cases_tenant_id_legal_entity_id_man~",
                schema: "procurement",
                table: "manual_payment_workflow_cases",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_request_id", "purpose", "opened_request_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_workflow_cases_tenant_id_legal_entity_id_op~1",
                schema: "procurement",
                table: "manual_payment_workflow_cases",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_manual_decision_id", "manual_payment_request_id", "branch_id", "opened_request_version" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_workflow_cases_tenant_id_legal_entity_id_ope~",
                schema: "procurement",
                table: "manual_payment_workflow_cases",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_manual_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_workflow_cases_tenant_id_legal_entity_id_pol~",
                schema: "procurement",
                table: "manual_payment_workflow_cases",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });
            InstallGovernanceGuards(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RemoveGovernanceGuards(migrationBuilder);
            migrationBuilder.DropTable(
                name: "manual_payment_core_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "manual_payment_date_observations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "manual_payment_workflow_cases",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_manual_payment_decision_governance",
                schema: "procurement",
                table: "manual_payment_decisions");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "procurement",
                table: "manual_payment_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW')\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
        }
    }
}
