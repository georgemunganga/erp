using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P24PurchaseOrderCommercialExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "estimated_value",
                schema: "procurement",
                table: "purchase_request_allocations",
                type: "numeric(38,8)",
                precision: 38,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "return_reason",
                schema: "procurement",
                table: "purchase_orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                schema: "procurement",
                table: "purchase_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "submitted_business_version",
                schema: "procurement",
                table: "purchase_orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "execution_route",
                schema: "procurement",
                table: "procurement_methods",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unconfigured");

            migrationBuilder.CreateTable(
                name: "commercial_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_commercial_command_receipts", x => x.id);
                    table.UniqueConstraint("AK_commercial_command_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_commercial_command_receipts_purchase_orders_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_commitment_ownership",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_purchase_order_commitment_ownership", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_commitment_ownership_tenant_id_legal_entity_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_purchase_order_commitment_ownership_tenant_id_legal_entity~1", x => new { x.tenant_id, x.legal_entity_id, x.commitment_reference, x.purchase_order_id });
                    table.CheckConstraint("ck_po_commitment_reference", "length(btrim(commitment_reference)) > 0");
                    table.ForeignKey(
                        name: "FK_purchase_order_commitment_ownership_purchase_orders_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_finance_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    evidence_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    approved_manual_policy_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    confirmed_gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    source_reservations_json = table.Column<string>(type: "text", nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    buyer_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_workers_json = table.Column<string>(type: "text", nullable: false),
                    worker_identity_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    verified_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    verified_by_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    replaces_commitment_reference = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_purchase_order_finance_evidence", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_finance_evidence_tenant_id_legal_entity_id_~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.commercial_revision, x.currency_code, x.commercial_snapshot_sha256 });
                    table.UniqueConstraint("AK_purchase_order_finance_evidence_tenant_id_legal_entity_id_i~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id });
                    table.UniqueConstraint("AK_purchase_order_finance_evidence_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_order_finance_evidence", "commercial_revision > 0 AND status IN ('Confirmed','Revoked') AND confirmed_gross_amount >= 0 AND currency_code ~ '^[A-Z]{3}$' AND buyer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_identity_sha256 ~ '^[0-9a-f]{64}$' AND length(btrim(commitment_reference)) > 0 AND length(btrim(evidence_reference)) > 0 AND evidence_mode = 'ApprovedManualEvidence' AND length(btrim(approved_manual_policy_reference)) > 0 AND length(btrim(verified_by_principal_id)) > 0 AND verified_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_purchase_order_finance_evidence_purchase_order_commitment_o~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.commitment_reference, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_commitment_ownership",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "commitment_reference", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_finance_evidence_purchase_orders_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_finance_evidence_suppliers_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    submitted_row_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    finance_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    buyer_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_workers_json = table.Column<string>(type: "text", nullable: false),
                    worker_identity_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_basis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    line_base_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_evidence_json = table.Column<string>(type: "text", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_decision_json = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_purchase_order_submissions", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_submissions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_purchase_order_submissions_tenant_id_legal_entity_id_id_cor~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.core_approval_case_id });
                    table.UniqueConstraint("AK_purchase_order_submissions_tenant_id_legal_entity_id_id_pu~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.finance_evidence_id });
                    table.UniqueConstraint("AK_purchase_order_submissions_tenant_id_legal_entity_id_id_pur~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.commercial_revision });
                    table.CheckConstraint("ck_order_submission_evidence", "commercial_revision > 0 AND submitted_row_version > 0 AND amount_basis = 'ApprovedLineBase' AND line_base_amount >= 0 AND gross_amount >= line_base_amount AND buyer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_identity_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_purchase_order_submissions_policy_versions_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_submissions_purchase_order_finance_evidence_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.finance_evidence_id, x.purchase_order_id, x.commercial_revision, x.currency_code, x.commercial_snapshot_sha256 },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_finance_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "commercial_revision", "currency_code", "commercial_snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_submissions_purchase_orders_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_assignment_authority",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_revision = table.Column<long>(type: "bigint", nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assignee_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    authority_evidence_json = table.Column<string>(type: "text", nullable: false),
                    authority_evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_purchase_order_assignment_authority", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_assignment_authority_tenant_id_legal_entity_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_po_assignment_identity", "core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND assignee_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND grant_revision > 0 AND authority_version > 0 AND assignee_kind = 'ProcurementManager' AND authority_evidence_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_purchase_order_assignment_authority_purchase_order_submissi~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.core_approval_case_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_approval_case_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_assignment_authority_spending_authority_gran~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.principal_id, x.authority_version },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "principal_id", "authority_version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_finance_resolutions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    finance_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approved_manual_policy_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_reservations_json = table.Column<string>(type: "text", nullable: false),
                    released_gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    verified_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    verified_by_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_purchase_order_finance_resolutions", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_finance_resolutions_tenant_id_legal_entity_i~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_po_finance_resolution", "released_gross_amount >= 0 AND currency_code ~ '^[A-Z]{3}$' AND verified_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(evidence_reference)) > 0 AND length(btrim(approved_manual_policy_reference)) > 0 AND ((outcome = 'CancelledUnsubmittedOrder' AND submission_id IS NULL) OR (outcome IN ('ReopenedDraft','CancelledRejectedOrder') AND submission_id IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_purchase_order_finance_resolutions_purchase_order_finance_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.finance_evidence_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_finance_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_finance_resolutions_purchase_order_submissio~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.purchase_order_id, x.finance_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "finance_evidence_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_issued_versions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finance_commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issue_effect_key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    issued_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    participant_workers_json = table.Column<string>(type: "text", nullable: false),
                    authorized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_purchase_order_issued_versions", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_issued_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_purchase_order_issued_versions_purchase_order_submissions_t~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.purchase_order_id, x.revision_number },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "commercial_revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_delivery_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    verified_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    verified_by_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_purchase_order_delivery_evidence", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_delivery_evidence_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_purchase_order_delivery_evidence_purchase_order_issued_vers~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_procurement_method_execution_route",
                schema: "procurement",
                table: "procurement_methods",
                sql: "execution_route IN ('Unconfigured','DirectPurchase','CompetitiveSourcing','ContractRelease')");

            migrationBuilder.CreateIndex(
                name: "IX_commercial_command_receipts_tenant_id_legal_entity_id_opera~",
                schema: "procurement",
                table: "commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_commercial_command_receipts_tenant_id_legal_entity_id_purch~",
                schema: "procurement",
                table: "commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_assignment_authority_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "purchase_order_assignment_authority",
                columns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_assignment_authority_tenant_id_legal_entity~1",
                schema: "procurement",
                table: "purchase_order_assignment_authority",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "core_approval_case_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_assignment_authority_tenant_id_legal_entity~2",
                schema: "procurement",
                table: "purchase_order_assignment_authority",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "principal_id", "authority_version" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_commitment_ownership_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "purchase_order_commitment_ownership",
                columns: new[] { "tenant_id", "legal_entity_id", "commitment_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_commitment_ownership_tenant_id_legal_entity~1",
                schema: "procurement",
                table: "purchase_order_commitment_ownership",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_delivery_evidence_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "purchase_order_delivery_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "delivery_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_evidence_tenant_id_legal_entity_id_c~",
                schema: "procurement",
                table: "purchase_order_finance_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "commitment_reference", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_evidence_tenant_id_legal_entity_id_e~",
                schema: "procurement",
                table: "purchase_order_finance_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "evidence_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_evidence_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "purchase_order_finance_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_evidence_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "purchase_order_finance_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_resolutions_tenant_id_legal_entity_~1",
                schema: "procurement",
                table: "purchase_order_finance_resolutions",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_evidence_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_resolutions_tenant_id_legal_entity_~2",
                schema: "procurement",
                table: "purchase_order_finance_resolutions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_resolutions_tenant_id_legal_entity_~3",
                schema: "procurement",
                table: "purchase_order_finance_resolutions",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_evidence_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_resolutions_tenant_id_legal_entity_~4",
                schema: "procurement",
                table: "purchase_order_finance_resolutions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "purchase_order_id", "finance_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_finance_resolutions_tenant_id_legal_entity_i~",
                schema: "procurement",
                table: "purchase_order_finance_resolutions",
                columns: new[] { "tenant_id", "legal_entity_id", "evidence_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_issued_versions_tenant_id_legal_entity_id_is~",
                schema: "procurement",
                table: "purchase_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "issue_effect_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_issued_versions_tenant_id_legal_entity_id_pu~",
                schema: "procurement",
                table: "purchase_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_issued_versions_tenant_id_legal_entity_id_s~1",
                schema: "procurement",
                table: "purchase_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "purchase_order_id", "revision_number" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_issued_versions_tenant_id_legal_entity_id_su~",
                schema: "procurement",
                table: "purchase_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_submissions_tenant_id_legal_entity_id_core_a~",
                schema: "procurement",
                table: "purchase_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_submissions_tenant_id_legal_entity_id_financ~",
                schema: "procurement",
                table: "purchase_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_evidence_id", "purchase_order_id", "commercial_revision", "currency_code", "commercial_snapshot_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_submissions_tenant_id_legal_entity_id_policy~",
                schema: "procurement",
                table: "purchase_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_submissions_tenant_id_legal_entity_id_purcha~",
                schema: "procurement",
                table: "purchase_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Downgrade only an unused execution schema. Never erase commercial evidence,
            // configured routing or decimal digits as an implicit code rollback.
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                LOCK TABLE procurement.commercial_command_receipts,
                    procurement.procurement_methods, procurement.purchase_order_assignment_authority,
                    procurement.purchase_order_commitment_ownership, procurement.purchase_order_delivery_evidence,
                    procurement.purchase_order_finance_evidence, procurement.purchase_order_finance_resolutions,
                    procurement.purchase_order_issued_versions, procurement.purchase_order_submissions,
                    procurement.purchase_orders, procurement.purchase_request_allocations
                    IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.commercial_command_receipts)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_order_assignment_authority)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_order_commitment_ownership)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_order_delivery_evidence)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_evidence)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_resolutions)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_order_issued_versions)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_order_submissions)
                        OR EXISTS (SELECT 1 FROM procurement.purchase_orders
                            WHERE return_reason IS NOT NULL OR submitted_at IS NOT NULL
                                OR submitted_business_version IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.procurement_methods
                            WHERE execution_route <> 'Unconfigured') THEN
                        RAISE EXCEPTION 'P24RollbackRequiresEvidencePreservingRecovery: order execution has been used or configured; use a reviewed forward fix or protected restore plan.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM procurement.purchase_request_allocations
                        WHERE estimated_value <> trunc(estimated_value, 4)
                            OR abs(estimated_value) >= 100000000000000) THEN
                        RAISE EXCEPTION 'P24RollbackAmountWouldLosePrecision: allocation amounts cannot fit numeric(18,4) exactly.';
                    END IF;
                END $guard$;
                """);

            migrationBuilder.DropTable(
                name: "commercial_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_assignment_authority",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_delivery_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_finance_resolutions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_issued_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_submissions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_finance_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_commitment_ownership",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_procurement_method_execution_route",
                schema: "procurement",
                table: "procurement_methods");

            migrationBuilder.DropColumn(
                name: "return_reason",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "submitted_business_version",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "execution_route",
                schema: "procurement",
                table: "procurement_methods");

            migrationBuilder.AlterColumn<decimal>(
                name: "estimated_value",
                schema: "procurement",
                table: "purchase_request_allocations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,8)",
                oldPrecision: 38,
                oldScale: 8);
        }
    }
}
