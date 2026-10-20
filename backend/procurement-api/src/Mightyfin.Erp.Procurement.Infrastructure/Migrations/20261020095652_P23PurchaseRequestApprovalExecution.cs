using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P23PurchaseRequestApprovalExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.AddColumn<Guid>(
                name: "maker_worker_id",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "requester_worker_id",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "worker_identity_sha256",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_spending_authority_grants_tenant_id_legal_entity_id_id_prin~",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "principal_id", "authority_version" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_request_approval_submissions_tenant_id_legal_enti~1",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "core_approval_case_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_request_approval_submissions_tenant_id_legal_enti~2",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "finance_snapshot_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_request_approval_submissions_tenant_id_legal_enti~3",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "submitted_business_version", "core_approval_case_id" });

            migrationBuilder.CreateTable(
                name: "approval_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_approval_command_receipts", x => x.id);
                    table.UniqueConstraint("AK_approval_command_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_approval_command_receipts_purchase_requests_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_authority_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assignee_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_revision = table.Column<long>(type: "bigint", nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    evidence_json = table.Column<string>(type: "text", nullable: false),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_assignment_authority_evidence", x => x.id);
                    table.UniqueConstraint("AK_assignment_authority_evidence_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr_assignment_authority_identity", "core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND grant_revision > 0 AND authority_version > 0 AND length(btrim(principal_id)) > 0 AND assignee_kind IN ('LineManager','ProcurementManager') AND evidence_sha256 ~ '^[a-f0-9]{64}$' AND (assignee_worker_id IS NULL OR assignee_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_assignment_authority_evidence_purchase_request_approval_sub~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.core_approval_case_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_approval_case_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assignment_authority_evidence_spending_authority_grants_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.principal_id, x.authority_version },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "principal_id", "authority_version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_request_approval_outcomes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_business_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_command_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    effect_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    actor_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_purchase_request_approval_outcomes", x => x.id);
                    table.UniqueConstraint("AK_purchase_request_approval_outcomes_tenant_id_legal_entity_~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.submission_id, x.purchase_request_id, x.submitted_business_version, x.core_approval_case_id, x.outcome });
                    table.UniqueConstraint("AK_purchase_request_approval_outcomes_tenant_id_legal_entity_i~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr_approval_outcome_evidence", "outcome IN ('Approved','Rejected','Returned') AND submitted_business_version > 0 AND core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(decision_command_key)) > 0 AND length(btrim(effect_key)) > 0 AND length(btrim(actor_subject_id)) > 0");
                    table.ForeignKey(
                        name: "FK_purchase_request_approval_outcomes_purchase_request_approva~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.purchase_request_id, x.submitted_business_version, x.core_approval_case_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "submitted_business_version", "core_approval_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_request_finance_release_requests",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_business_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finance_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    reservation_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reserved_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    effect_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    owner_queue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_by_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_purchase_request_finance_release_requests", x => x.id);
                    table.UniqueConstraint("AK_purchase_request_finance_release_requests_tenant_id_legal_~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.reservation_ref, x.currency_code, x.reserved_amount });
                    table.UniqueConstraint("AK_purchase_request_finance_release_requests_tenant_id_legal_e~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr_finance_release_request", "outcome IN ('Rejected','Returned') AND reserved_amount >= 0 AND currency_code ~ '^[A-Z]{3}$' AND length(btrim(reservation_ref)) > 0 AND length(btrim(effect_key)) > 0 AND length(btrim(owner_queue)) > 0 AND length(btrim(requested_by_subject_id)) > 0");
                    table.ForeignKey(
                        name: "FK_purchase_request_finance_release_requests_budget_check_snap~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.finance_snapshot_id, x.purchase_request_id, x.submitted_business_version },
                        principalSchema: "procurement",
                        principalTable: "budget_check_snapshots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "request_version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_request_finance_release_requests_purchase_request_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.finance_snapshot_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "finance_snapshot_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_request_finance_release_requests_purchase_request~1",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approval_outcome_id, x.submission_id, x.purchase_request_id, x.submitted_business_version, x.core_approval_case_id, x.outcome },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_outcomes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "submission_id", "purchase_request_id", "submitted_business_version", "core_approval_case_id", "outcome" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_request_finance_release_confirmations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    released_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    evidence_ref = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    evidence_source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    verified_by_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    finance_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effect_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_purchase_request_finance_release_confirmations", x => x.id);
                    table.UniqueConstraint("AK_purchase_request_finance_release_confirmations_tenant_id_le~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr_finance_release_confirmation", "released_amount >= 0 AND currency_code ~ '^[A-Z]{3}$' AND finance_confirmed_at <= recorded_at AND length(btrim(evidence_ref)) > 0 AND length(btrim(evidence_source)) > 0 AND length(btrim(verified_by_subject_id)) > 0 AND length(btrim(effect_key)) > 0");
                    table.ForeignKey(
                        name: "FK_purchase_request_finance_release_confirmations_purchase_req~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.release_request_id, x.reservation_ref, x.currency_code, x.released_amount },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_finance_release_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "reservation_ref", "currency_code", "reserved_amount" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "snapshot_schema_version IN (1,2) AND submitted_business_version > 0 AND policy_version_number > 0 AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_worker_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "(snapshot_schema_version = 1 AND maker_worker_id IS NULL AND requester_worker_id IS NULL AND worker_identity_sha256 IS NULL) OR (snapshot_schema_version = 2 AND maker_worker_id IS NOT NULL AND requester_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_identity_sha256 IS NOT NULL AND worker_identity_sha256 ~ '^[a-f0-9]{64}$')");

            migrationBuilder.CreateIndex(
                name: "IX_approval_command_receipts_tenant_id_legal_entity_id_operati~",
                schema: "procurement",
                table: "approval_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_command_receipts_tenant_id_legal_entity_id_purchas~",
                schema: "procurement",
                table: "approval_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id" });

            migrationBuilder.CreateIndex(
                name: "IX_assignment_authority_evidence_tenant_id_legal_entity_id_cor~",
                schema: "procurement",
                table: "assignment_authority_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assignment_authority_evidence_tenant_id_legal_entity_id_gra~",
                schema: "procurement",
                table: "assignment_authority_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "principal_id", "authority_version" });

            migrationBuilder.CreateIndex(
                name: "IX_assignment_authority_evidence_tenant_id_legal_entity_id_sub~",
                schema: "procurement",
                table: "assignment_authority_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "core_approval_case_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_outcomes_tenant_id_legal_entity_~1",
                schema: "procurement",
                table: "purchase_request_approval_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_outcomes_tenant_id_legal_entity_~2",
                schema: "procurement",
                table: "purchase_request_approval_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "effect_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_outcomes_tenant_id_legal_entity_~3",
                schema: "procurement",
                table: "purchase_request_approval_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_outcomes_tenant_id_legal_entity_~4",
                schema: "procurement",
                table: "purchase_request_approval_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "purchase_request_id", "submitted_business_version", "core_approval_case_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_approval_outcomes_tenant_id_legal_entity_i~",
                schema: "procurement",
                table: "purchase_request_approval_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_confirmations_tenant_id_l~1",
                schema: "procurement",
                table: "purchase_request_finance_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "release_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_confirmations_tenant_id_l~2",
                schema: "procurement",
                table: "purchase_request_finance_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "release_request_id", "reservation_ref", "currency_code", "released_amount" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_confirmations_tenant_id_le~",
                schema: "procurement",
                table: "purchase_request_finance_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "effect_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_requests_tenant_id_legal_~1",
                schema: "procurement",
                table: "purchase_request_finance_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "effect_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_requests_tenant_id_legal_~2",
                schema: "procurement",
                table: "purchase_request_finance_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_requests_tenant_id_legal_~3",
                schema: "procurement",
                table: "purchase_request_finance_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "finance_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_requests_tenant_id_legal_~4",
                schema: "procurement",
                table: "purchase_request_finance_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_snapshot_id", "purchase_request_id", "submitted_business_version" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_requests_tenant_id_legal_~5",
                schema: "procurement",
                table: "purchase_request_finance_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "approval_outcome_id", "submission_id", "purchase_request_id", "submitted_business_version", "core_approval_case_id", "outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_finance_release_requests_tenant_id_legal_e~",
                schema: "procurement",
                table: "purchase_request_finance_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "approval_outcome_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "assignment_authority_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_finance_release_confirmations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_finance_release_requests",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_approval_outcomes",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_spending_authority_grants_tenant_id_legal_entity_id_id_prin~",
                schema: "procurement",
                table: "spending_authority_grants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_request_approval_submissions_tenant_id_legal_enti~1",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_request_approval_submissions_tenant_id_legal_enti~2",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_request_approval_submissions_tenant_id_legal_enti~3",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_worker_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropColumn(
                name: "maker_worker_id",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropColumn(
                name: "requester_worker_id",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropColumn(
                name: "worker_identity_sha256",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "snapshot_schema_version = 1 AND submitted_business_version > 0 AND policy_version_number > 0 AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
        }
    }
}
