using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P21PurchaseRequestApprovalSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_policy_versions_tenant_id_legal_entity_id_id_policy_version~",
                schema: "procurement",
                table: "policy_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_number" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_policy_evaluations_tenant_id_legal_entity_id_id_purchase_re~",
                schema: "procurement",
                table: "policy_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "request_version", "policy_version_id", "policy_version_number" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_budget_check_snapshots_tenant_id_legal_entity_id_id_purchas~",
                schema: "procurement",
                table: "budget_check_snapshots",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "request_version" });

            migrationBuilder.CreateTable(
                name: "purchase_request_approval_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_business_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    request_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_decision_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finance_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finance_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    finance_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finance_evidence_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    manual_evidence_authority_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    amount_basis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    estimated_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_purchase_request_approval_submissions", x => x.id);
                    table.UniqueConstraint("AK_purchase_request_approval_submissions_tenant_id_legal_entit~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr_approval_submission_amount", "estimated_amount >= 0 AND amount_basis = 'EstimatedLineBase' AND currency_code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_pr_approval_submission_digest", "request_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND finance_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_decision_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_pr_approval_submission_finance_mode", "(finance_evidence_mode = 'AutomatedFinance' AND manual_evidence_authority_ref IS NULL) OR (finance_evidence_mode = 'ApprovedManualEvidence' AND manual_evidence_authority_ref IS NOT NULL AND length(btrim(manual_evidence_authority_ref)) > 0)");
                    table.CheckConstraint("ck_pr_approval_submission_identity", "snapshot_schema_version = 1 AND submitted_business_version > 0 AND policy_version_number > 0 AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_purchase_request_approval_submissions_budget_check_snapshot~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.finance_snapshot_id, x.purchase_request_id, x.submitted_business_version },
                        principalSchema: "procurement",
                        principalTable: "budget_check_snapshots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "request_version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_request_approval_submissions_policy_evaluations_te~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_evaluation_id, x.purchase_request_id, x.submitted_business_version, x.policy_version_id, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_evaluations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "request_version", "policy_version_id", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_request_approval_submissions_policy_versions_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_request_approval_submissions_purchase_requests_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_submissions_tenant_id_legal_enti~1",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_submissions_tenant_id_legal_enti~2",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_submissions_tenant_id_legal_enti~3",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "submitted_business_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_submissions_tenant_id_legal_enti~4",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_snapshot_id", "purchase_request_id", "submitted_business_version" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_submissions_tenant_id_legal_enti~5",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_evaluation_id", "purchase_request_id", "submitted_business_version", "policy_version_id", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_submissions_tenant_id_legal_entit~",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_request_approval_submissions",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_policy_versions_tenant_id_legal_entity_id_id_policy_version~",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_policy_evaluations_tenant_id_legal_entity_id_id_purchase_re~",
                schema: "procurement",
                table: "policy_evaluations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_budget_check_snapshots_tenant_id_legal_entity_id_id_purchas~",
                schema: "procurement",
                table: "budget_check_snapshots");
        }
    }
}
