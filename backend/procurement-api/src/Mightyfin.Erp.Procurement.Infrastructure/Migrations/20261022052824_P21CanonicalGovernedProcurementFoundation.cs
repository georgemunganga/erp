using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P21CanonicalGovernedProcurementFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.AddColumn<Guid>(
                name: "active_match_claim_result_id",
                schema: "procurement",
                table: "supplier_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_mapping_version_id",
                schema: "procurement",
                table: "supplier_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_match_result_id",
                schema: "procurement",
                table: "supplier_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mapping_revision",
                schema: "procurement",
                table: "supplier_invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "match_revision",
                schema: "procurement",
                table: "supplier_invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "matching_state",
                schema: "procurement",
                table: "supplier_invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotCompared");

            migrationBuilder.AddColumn<Guid>(
                name: "branch_ref",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "sourcing_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "method_id",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "method_revision",
                schema: "procurement",
                table: "sourcing_events",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_draft_fingerprint",
                schema: "procurement",
                table: "sourcing_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_evidence_id",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "accepted_amount",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(38,8)",
                precision: 38,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<Guid>(
                name: "certification_effect_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "current_accepted_net_amount",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(38,8)",
                precision: 38,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "current_accepted_quantity",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "evidence_ref",
                schema: "procurement",
                table: "service_acceptances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "service_acceptances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_binding_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "procurement",
                table: "service_acceptances",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_worker_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "policy_version_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "effect_key",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_ref",
                schema: "procurement",
                table: "receipt_inspections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "receipt_inspections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "inspector_worker_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ledger_sequence",
                schema: "procurement",
                table: "receipt_inspections",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "physical_binding_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "physical_observation_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "policy_version_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "qualified_quantity",
                schema: "procurement",
                table: "receipt_inspections",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.AlterColumn<decimal>(
                name: "tax_variance",
                schema: "procurement",
                table: "invoice_match_results",
                type: "numeric(38,8)",
                precision: 38,
                scale: 8,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_variance",
                schema: "procurement",
                table: "invoice_match_results",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_variance",
                schema: "procurement",
                table: "invoice_match_results",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "actor_principal_id",
                schema: "procurement",
                table: "invoice_match_results",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "invoice_match_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "command_key",
                schema: "procurement",
                table: "invoice_match_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_json",
                schema: "procurement",
                table: "invoice_match_results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_sha256",
                schema: "procurement",
                table: "invoice_match_results",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "invoice_match_results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "input_fingerprint",
                schema: "procurement",
                table: "invoice_match_results",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_original_sha256",
                schema: "procurement",
                table: "invoice_match_results",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "invoice_version",
                schema: "procurement",
                table: "invoice_match_results",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_version_id",
                schema: "procurement",
                table: "invoice_match_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "mapping_version_id",
                schema: "procurement",
                table: "invoice_match_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "policy_rules_sha256",
                schema: "procurement",
                table: "invoice_match_results",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "purchase_order_id",
                schema: "procurement",
                table: "invoice_match_results",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_watermarks_json",
                schema: "procurement",
                table: "invoice_match_results",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discrepancy_key",
                schema: "procurement",
                table: "invoice_exceptions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "match_result_id",
                schema: "procurement",
                table: "invoice_exceptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_worker_id",
                schema: "procurement",
                table: "invoice_exceptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resolving_match_result_id",
                schema: "procurement",
                table: "invoice_exceptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supplier_invoice_line_id",
                schema: "procurement",
                table: "invoice_exceptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_ref",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "issued_version_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "procurement",
                table: "goods_receipts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receiver_worker_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recorded_at",
                schema: "procurement",
                table: "goods_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submission_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "initially_rejected_quantity",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "physical_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "physical_effect_key",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "physical_evidence_ref",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "physical_observation_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "physical_policy_version_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "physical_recorded_at",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "physical_sequence",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "checked_by_worker_id",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "checker_identity_schema_version",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "checker_identity_sha256",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_supplier_invoice_lines_tenant_id_legal_entity_id_id_supplie~",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_receipt_inspections_tenant_id_legal_entity_id_id_goods_rece~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "goods_receipt_line_id", "source_state_binding_id", "physical_binding_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_id_purchase_~",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_operational_policy_identity_owner",
                schema: "procurement",
                table: "policy_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "policy_key", "policy_version_number" });

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
                name: "AK_invoice_match_results_tenant_id_legal_entity_id_id_supplier~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_goods_receipts_tenant_id_legal_entity_id_id_purchase_order_~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_binding_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_budget_check_snapshots_tenant_id_legal_entity_id_id_purchas~",
                schema: "procurement",
                table: "budget_check_snapshots",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "request_version" });

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
                name: "operational_policy_identity_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_record_version_at_action = table.Column<long>(type: "bigint", nullable: false),
                    rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    submitted_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_operational_policy_identity_evidence", x => x.id);
                    table.UniqueConstraint("AK_operational_policy_identity_evidence_tenant_id_legal_entity~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_operational_policy_identity_lineage", x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.id, x.stage });
                    table.CheckConstraint("ck_operational_policy_identity", "schema_version=1 AND policy_key IN ('RECEIVING','INVOICE_MATCHING')\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
                    table.ForeignKey(
                        name: "fk_operational_policy_identity_created",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.created_evidence_id, x.created_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "operational_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operational_policy_identity_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_key", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operational_policy_identity_submitted",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.submitted_evidence_id, x.submitted_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "operational_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "id", "stage" },
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
                    maker_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requester_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    worker_identity_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    maker_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    request_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_decision_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finance_identity_schema_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.UniqueConstraint("AK_purchase_request_approval_submissions_tenant_id_legal_enti~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.core_approval_case_id });
                    table.UniqueConstraint("AK_purchase_request_approval_submissions_tenant_id_legal_enti~2", x => new { x.tenant_id, x.legal_entity_id, x.id, x.finance_snapshot_id });
                    table.UniqueConstraint("AK_purchase_request_approval_submissions_tenant_id_legal_enti~3", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_request_id, x.submitted_business_version, x.core_approval_case_id });
                    table.UniqueConstraint("AK_purchase_request_approval_submissions_tenant_id_legal_entit~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr_approval_submission_amount", "estimated_amount >= 0 AND amount_basis = 'EstimatedLineBase' AND currency_code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_pr_approval_submission_digest", "request_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND finance_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_decision_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_pr_approval_submission_finance_mode", "(finance_evidence_mode = 'AutomatedFinance' AND manual_evidence_authority_ref IS NULL) OR (finance_evidence_mode = 'ApprovedManualEvidence' AND manual_evidence_authority_ref IS NOT NULL AND length(btrim(manual_evidence_authority_ref)) > 0)");
                    table.CheckConstraint("ck_pr_approval_submission_identity", "snapshot_schema_version IN (1,2) AND submitted_business_version > 0 AND policy_version_number > 0 AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_pr_approval_worker_identity", "(snapshot_schema_version = 1 AND maker_worker_id IS NULL AND requester_worker_id IS NULL AND worker_identity_sha256 IS NULL) OR (snapshot_schema_version = 2 AND maker_worker_id IS NOT NULL AND requester_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_identity_sha256 IS NOT NULL AND worker_identity_sha256 ~ '^[a-f0-9]{64}$')");
                    table.CheckConstraint("ck_pr_finance_identity_version", "finance_identity_schema_version = 0 OR (finance_identity_schema_version = 1 AND snapshot_schema_version = 2)");
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

            migrationBuilder.CreateTable(
                name: "sourcing_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_schema_version = table.Column<int>(type: "integer", nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
                    command_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_command_receipts", x => x.id);
                    table.UniqueConstraint("ak_rfq_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_rfq_receipt_identity", "operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND response_schema_version=1 AND result_version>0 AND command_fingerprint ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND ((operation='Create' AND response_status_code=201) OR (operation='Cancel' AND response_status_code=200))");
                    table.ForeignKey(
                        name: "FK_sourcing_command_receipts_sourcing_events_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_draft_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    maker_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    maker_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    original_draft_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_draft_evidence", x => x.id);
                    table.UniqueConstraint("ak_rfq_evidence_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_rfq_original_envelope", "schema_version=1 AND payload_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND create_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_sourcing_draft_evidence_sourcing_events_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_policy_identity_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_record_version_at_action = table.Column<long>(type: "bigint", nullable: false),
                    rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    submitted_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_sourcing_policy_identity_evidence", x => x.id);
                    table.UniqueConstraint("ak_source_policy_identity_parent", x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.id, x.stage });
                    table.UniqueConstraint("AK_sourcing_policy_identity_evidence_tenant_id_legal_entity_id~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_source_policy_identity", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_record_version_at_action>0 AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0 AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(action_at) AND ((stage='Created' AND policy_record_version_at_action=1 AND created_evidence_id IS NULL AND created_evidence_stage IS NULL AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Published' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NOT NULL AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
                    table.ForeignKey(
                        name: "fk_source_policy_identity_created",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.created_evidence_id, x.created_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "sourcing_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_policy_identity_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_policy_identity_submitted",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.submitted_evidence_id, x.submitted_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "sourcing_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "spending_authority_grants",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    configuration_owner_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    submitted_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_business_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    review_return_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    activated_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    published_business_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revoked_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    superseded_by_grant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_basis = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    maximum_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    all_branches = table.Column<bool>(type: "boolean", nullable: false),
                    branch_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    all_departments = table.Column<bool>(type: "boolean", nullable: false),
                    department_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    all_categories = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_spending_authority_grants", x => x.id);
                    table.UniqueConstraint("AK_spending_authority_grants_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_spending_authority_grants_tenant_id_legal_entity_id_id_prin~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.principal_id, x.authority_version });
                    table.ForeignKey(
                        name: "FK_spending_authority_grants_spending_authority_grants_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.superseded_by_grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
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
                name: "spending_authority_categories",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_spending_authority_categories", x => x.id);
                    table.UniqueConstraint("AK_spending_authority_categories_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_spending_authority_categories_spending_authority_grants_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "spending_authority_identity_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    configuration_owner_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_record_version_at_action = table.Column<long>(type: "bigint", nullable: false),
                    business_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    submitted_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_spending_authority_identity_evidence", x => x.id);
                    table.UniqueConstraint("ak_authority_identity_lineage", x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.id, x.stage });
                    table.UniqueConstraint("AK_spending_authority_identity_evidence_tenant_id_legal_entity~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_authority_identity_evidence", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND authority_version>0 AND grant_record_version_at_action>0\nAND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
                    table.ForeignKey(
                        name: "fk_authority_identity_created",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.created_evidence_id, x.created_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "grant_id", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_authority_identity_grant",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_authority_identity_submitted",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.submitted_evidence_id, x.submitted_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "grant_id", "id", "stage" },
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
                name: "sourcing_demand_bindings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_version = table.Column<long>(type: "bigint", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_decision_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_rules_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finance_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finance_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method_revision = table.Column<long>(type: "bigint", nullable: false),
                    method_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    minimum_quote_count = table.Column<int>(type: "integer", nullable: false),
                    maker_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_estimate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estimated_value = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    category_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    department_ref = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_sourcing_demand_bindings", x => x.id);
                    table.UniqueConstraint("ak_rfq_binding_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_rfq_binding_value", "target_type='SourcingEvent' AND quantity>0 AND unit_estimate>=0 AND estimated_value=quantity*unit_estimate AND request_version>=submitted_version AND submitted_version>0 AND method_revision>0 AND minimum_quote_count BETWEEN 0 AND 20 AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_budget_check_snapshots_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.finance_snapshot_id },
                        principalSchema: "procurement",
                        principalTable: "budget_check_snapshots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_procurement_methods_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.method_id },
                        principalSchema: "procurement",
                        principalTable: "procurement_methods",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_allocations_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.allocation_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_allocations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_approval_outcomes~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approval_outcome_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_outcomes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_approval_submissi~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_lines_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id, x.purchase_request_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_sourcing_draft_evidence_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_draft_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_sourcing_events_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_sourcing_lines_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_line_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_lines",
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
                    table.UniqueConstraint("ak_invoice_issued_order_binding", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id });
                    table.UniqueConstraint("AK_purchase_order_issued_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_purchase_order_issued_versions_tenant_id_legal_entity_id_id~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.submission_id });
                    table.ForeignKey(
                        name: "FK_purchase_order_issued_versions_purchase_order_submissions_t~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.purchase_order_id, x.revision_number },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "commercial_revision" },
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

            migrationBuilder.CreateTable(
                name: "invoice_match_exposure_states",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    coverage_state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    owner_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    owner_watermark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    owner_evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    owner_valid_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_invoice_match_exposure_states", x => x.id);
                    table.UniqueConstraint("AK_invoice_match_exposure_states_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_invoice_match_exposure_states_tenant_id_legal_entity_id_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.issued_version_id });
                    table.CheckConstraint("ck_invoice_exposure_state", "sequence >= 0 AND coverage_state IN ('Unknown','Verified','Stale') AND currency_code ~ '^[A-Z]{3}$' AND (coverage_state <> 'Verified' OR (owner_reference IS NOT NULL AND owner_watermark IS NOT NULL AND owner_evidence_sha256 IS NOT NULL AND owner_evidence_sha256 ~ '^[0-9a-f]{64}$' AND owner_valid_until IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_invoice_match_exposure_states_purchase_order_issued_version~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_exposure_states_suppliers_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_match_mapping_versions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_revision = table.Column<int>(type: "integer", nullable: false),
                    invoice_original_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mapping_json = table.Column<string>(type: "text", nullable: false),
                    mapping_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    verification_state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    verification_evidence_json = table.Column<string>(type: "text", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_invoice_match_mapping_versions", x => x.id);
                    table.UniqueConstraint("AK_invoice_match_mapping_versions_tenant_id_legal_entity_id_i~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id, x.purchase_order_id, x.issued_version_id });
                    table.UniqueConstraint("AK_invoice_match_mapping_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_invoice_match_mapping_versions_tenant_id_legal_entity_id_id~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id });
                    table.CheckConstraint("ck_invoice_mapping_evidence", "mapping_revision > 0 AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_invoice_match_mapping_versions_purchase_order_issued_versio~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_mapping_versions_supplier_invoices_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
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
                    table.UniqueConstraint("AK_purchase_order_delivery_evidence_tenant_id_legal_entity_id~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.issued_version_id });
                    table.ForeignKey(
                        name: "FK_purchase_order_delivery_evidence_purchase_order_issued_vers~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receiving_source_states",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    classification_json = table.Column<string>(type: "text", nullable: false),
                    classification_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    current_accepted_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    current_accepted_net_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
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
                    table.PrimaryKey("PK_receiving_source_states", x => x.id);
                    table.UniqueConstraint("AK_receiving_source_states_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_receiving_source_states_tenant_id_legal_entity_id_id_purcha~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.issued_version_id, x.purchase_order_line_id });
                    table.CheckConstraint("ck_receiving_source_state", "execution_version = 1 AND sequence >= 0 AND current_accepted_quantity >= 0 AND current_accepted_net_amount >= 0 AND source_sha256 ~ '^[a-f0-9]{64}$'");
                    table.ForeignKey(
                        name: "FK_receiving_source_states_policy_versions_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.classification_policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_source_states_purchase_order_delivery_evidence_te~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.delivery_evidence_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_delivery_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_source_states_purchase_order_issued_versions_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id, x.purchase_order_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "submission_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_source_states_purchase_order_lines_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_match_allocations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exposure_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_source_state_id = table.Column<Guid>(type: "uuid", nullable: true),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_acceptance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_debit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    exposure_sequence = table.Column<long>(type: "bigint", nullable: false),
                    validation_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    owner_coverage_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_invoice_match_allocations", x => x.id);
                    table.UniqueConstraint("AK_invoice_match_allocations_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_match_allocation", "kind = 'Debit' AND original_debit_id IS NULL AND quantity > 0 AND net_amount >= 0 AND tax_amount >= 0 AND exposure_sequence > 0 AND (goods_receipt_line_id IS NULL OR service_acceptance_id IS NULL) AND ((goods_receipt_line_id IS NULL AND service_acceptance_id IS NULL AND receiving_source_state_id IS NULL) OR (receiving_source_state_id IS NOT NULL AND (goods_receipt_line_id IS NOT NULL OR service_acceptance_id IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_goods_receipt_lines_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_invoice_match_allocations_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.original_debit_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_match_allocations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_invoice_match_exposure_states_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.exposure_state_id, x.purchase_order_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_match_exposure_states",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_invoice_match_results_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.match_result_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_match_results",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_purchase_order_lines_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_receiving_source_states_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receiving_source_state_id, x.purchase_order_id, x.issued_version_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "receiving_source_states",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_service_acceptances_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.service_acceptance_id },
                        principalSchema: "procurement",
                        principalTable: "service_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_supplier_invoice_lines_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_line_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoice_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_allocations_suppliers_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receiving_acceptance_effects",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    acceptance_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    physical_observation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inspection_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_acceptance_effect_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correction_evidence = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    net_amount_delta = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deliverable_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    period_from = table.Column<DateOnly>(type: "date", nullable: true),
                    period_to = table.Column<DateOnly>(type: "date", nullable: true),
                    ledger_sequence = table.Column<long>(type: "bigint", nullable: false),
                    effect_key = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    receiving_source_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_acceptance_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_receiving_acceptance_effects", x => x.id);
                    table.UniqueConstraint("AK_receiving_acceptance_effects_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_receiving_positive_effect", "kind = 1 AND quantity_delta > 0 AND net_amount_delta >= 0 AND ledger_sequence > 0 AND original_acceptance_effect_id IS NULL AND correction_evidence IS NULL AND ((goods_receipt_line_id IS NOT NULL AND service_acceptance_id IS NULL AND acceptance_record_id = goods_receipt_line_id AND physical_observation_id IS NOT NULL) OR (service_acceptance_id IS NOT NULL AND goods_receipt_line_id IS NULL AND acceptance_record_id = service_acceptance_id AND physical_observation_id IS NULL AND inspection_evidence_id IS NULL))");
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_goods_receipt_lines_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_policy_versions_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receiving_policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_receipt_inspections_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.inspection_evidence_id, x.goods_receipt_line_id, x.receiving_source_state_id, x.physical_observation_id },
                        principalSchema: "procurement",
                        principalTable: "receipt_inspections",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "goods_receipt_line_id", "source_state_binding_id", "physical_binding_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_receiving_source_states_tenant~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receiving_source_state_id },
                        principalSchema: "procurement",
                        principalTable: "receiving_source_states",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_service_acceptances_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.service_acceptance_id },
                        principalSchema: "procurement",
                        principalTable: "service_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_active_match_cl~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "active_match_claim_result_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_current_mapping~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "current_mapping_version_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_last_match_resu~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "last_match_result_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_rfq_line_exact_source",
                schema: "procurement",
                table: "sourcing_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "purchase_request_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_events_tenant_id_legal_entity_id_method_id",
                schema: "procurement",
                table: "sourcing_events",
                columns: new[] { "tenant_id", "legal_entity_id", "method_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')))");

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "deliverable_ref" },
                unique: true,
                filter: "certified_at IS NOT NULL AND execution_version = 1");

            migrationBuilder.CreateIndex(
                name: "ux_invoice_service_exact_source_binding",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_line_id", "source_state_binding_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_receiving_service_state_binding",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_execution",
                schema: "procurement",
                table: "service_acceptances",
                sql: "execution_version = 0 OR (execution_version = 1 AND source_purchase_order_id IS NOT NULL AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (receiving_source_state_id IS NULL OR source_state_binding_id = receiving_source_state_id) AND (certified_at IS NULL OR (receiving_source_state_id IS NOT NULL AND certification_effect_id IS NOT NULL AND owner_worker_id IS NOT NULL AND policy_version_id IS NOT NULL AND accepted_quantity > 0 AND accepted_amount >= 0)))");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_observation_id" },
                unique: true,
                filter: "physical_observation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "ledger_sequence" },
                unique: true,
                filter: "ledger_sequence IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_inspection_execution",
                schema: "procurement",
                table: "receipt_inspections",
                sql: "execution_version = 0 OR (execution_version = 1 AND receiving_source_state_id IS NOT NULL AND physical_observation_id IS NOT NULL AND physical_binding_id = physical_observation_id AND source_state_binding_id = receiving_source_state_id AND inspector_worker_id IS NOT NULL AND policy_version_id IS NOT NULL AND ledger_sequence IS NOT NULL AND ledger_sequence > 0 AND effect_key IS NOT NULL AND inspected_at IS NOT NULL AND qualified_quantity >= 0 AND qualified_quantity <= inspected_quantity)");

            migrationBuilder.CreateIndex(
                name: "ux_rfq_allocation_exact_target",
                schema: "procurement",
                table: "purchase_request_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "purchase_request_line_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_procurement_method_execution_route",
                schema: "procurement",
                table: "procurement_methods",
                sql: "execution_route IN ('Unconfigured','DirectPurchase','CompetitiveSourcing','ContractRelease')");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_command_key",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true,
                filter: "command_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_issued_vers~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_mapping_ver~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "issued_version_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results",
                sql: "execution_version = 0 OR (execution_version = 1 AND mapping_version_id IS NOT NULL AND purchase_order_id IS NOT NULL AND issued_version_id IS NOT NULL AND invoice_version > 0 AND command_key IS NOT NULL AND actor_worker_id IS NOT NULL AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND invoice_original_sha256 IS NOT NULL AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND input_fingerprint IS NOT NULL AND input_fingerprint ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_json IS NOT NULL AND source_watermarks_json IS NOT NULL AND outcome IN ('Compared','Blocked'))");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_match_result_i~",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "match_result_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_resolving_matc~",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "resolving_match_result_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_supplier_invo~1",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_line_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_issued_version_id_~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id", "submission_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_header_execution",
                schema: "procurement",
                table: "goods_receipts",
                sql: "execution_version = 0 OR (execution_version = 1 AND issued_version_id IS NOT NULL AND submission_id IS NOT NULL AND issued_binding_id = issued_version_id AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (recorded_at IS NULL OR (receiver_worker_id IS NOT NULL AND receiver_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)))");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "source_purchase_order_id", "issued_binding_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_observation_id" },
                unique: true,
                filter: "physical_observation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_poli~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "physical_sequence" },
                unique: true,
                filter: "physical_sequence IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_invoice_goods_exact_source_binding",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_line_id", "source_state_binding_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_receiving_goods_physical_binding",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id", "physical_binding_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_line_execution",
                schema: "procurement",
                table: "goods_receipt_lines",
                sql: "execution_version = 0 OR (execution_version = 1 AND source_purchase_order_id IS NOT NULL AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (receiving_source_state_id IS NULL OR source_state_binding_id = receiving_source_state_id) AND (physical_observation_id IS NULL OR (receiving_source_state_id IS NOT NULL AND physical_binding_id = physical_observation_id AND physical_sequence IS NOT NULL AND physical_sequence > 0 AND physical_policy_version_id IS NOT NULL AND physical_effect_key IS NOT NULL AND physical_recorded_at IS NOT NULL AND received_quantity > 0 AND initially_rejected_quantity >= 0 AND initially_rejected_quantity <= received_quantity)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_budget_checker_identity",
                schema: "procurement",
                table: "budget_check_snapshots",
                sql: "(checker_identity_schema_version = 0 AND checked_by_worker_id IS NULL AND checker_identity_sha256 IS NULL) OR (checker_identity_schema_version = 1 AND checked_by_worker_id IS NOT NULL AND checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND checker_identity_sha256 IS NOT NULL AND checker_identity_sha256 ~ '^[a-f0-9]{64}$')");

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
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_exposu~1",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "exposure_state_id", "purchase_order_id", "issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_exposur~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "exposure_state_id", "exposure_sequence", "supplier_invoice_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_goods_r~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_match_~1",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "match_result_id", "supplier_invoice_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_match_r~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "match_result_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_origina~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "original_debit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_purchas~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_receivi~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_service~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_suppli~1",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_line_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_supplie~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_iss~",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_pur~",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_sup~",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_is~",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_su~",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id", "mapping_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_operational_policy_identity_evidence_tenant_id_legal_entit~1",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "created_evidence_id", "created_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_policy_identity_evidence_tenant_id_legal_entit~2",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "submitted_evidence_id", "submitted_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_policy_identity_evidence_tenant_id_legal_entity~",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_operational_policy_identity_created",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key" },
                unique: true,
                filter: "stage = 'Created'");

            migrationBuilder.CreateIndex(
                name: "ux_operational_policy_identity_published",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key" },
                unique: true,
                filter: "stage = 'Published'");

            migrationBuilder.CreateIndex(
                name: "ux_operational_policy_identity_revision",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "stage", "policy_record_version_at_action" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_effe~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "effect_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_good~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_insp~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "inspection_evidence_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_rec~1",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "ledger_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_rece~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_serv~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_classific~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "classification_policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_delivery_~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "delivery_evidence_id", "issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_issued_v~1",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_issued_ve~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_purchase_~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_command_receipts_tenant_id_legal_entity_id_sourcin~",
                schema: "procurement",
                table: "sourcing_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "ux_rfq_receipt_command",
                schema: "procurement",
                table: "sourcing_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_approval~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "approval_outcome_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_finance_~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_method_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "method_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_purchase~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "purchase_request_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_source_e~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "source_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_submissi~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "ux_rfq_binding_allocation",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "allocation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_binding_line",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_binding_source",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "purchase_request_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_evidence_create_key",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "create_command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_evidence_event",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_evidence_exact_event",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_policy_identity_evidence_tenant_id_legal_entity_i~1",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "created_evidence_id", "created_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_policy_identity_evidence_tenant_id_legal_entity_i~2",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "submitted_evidence_id", "submitted_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_policy_identity_evidence_tenant_id_legal_entity_id~",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_source_policy_identity_created",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                unique: true,
                filter: "stage = 'Created'");

            migrationBuilder.CreateIndex(
                name: "ux_source_policy_identity_published",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                unique: true,
                filter: "stage = 'Published'");

            migrationBuilder.CreateIndex(
                name: "ux_source_policy_identity_revision",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "stage", "policy_record_version_at_action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_categories_tenant_id_legal_entity_id_gra~",
                schema: "procurement",
                table: "spending_authority_categories",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_branch_~",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "branch_ref", "department_ref", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_princi~1",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_id", "permission", "action", "authority_reference", "authority_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_princip~",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_id", "permission", "action", "authority_reference" },
                unique: true,
                filter: "state = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_superse~",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "superseded_by_grant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_identity_evidence_tenant_id_legal_entit~1",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "submitted_evidence_id", "submitted_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_identity_evidence_tenant_id_legal_entity~",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "created_evidence_id", "created_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "ux_authority_identity_action_revision",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "stage", "grant_record_version_at_action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_authority_identity_activation",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id" },
                unique: true,
                filter: "stage = 'Activated'");

            migrationBuilder.CreateIndex(
                name: "ux_authority_identity_origin",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id" },
                unique: true,
                filter: "stage = 'Created'");

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_tenant_id_legal_entity_~1",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "source_purchase_order_id", "issued_binding_id" },
                principalSchema: "procurement",
                principalTable: "goods_receipts",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_binding_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_policy_version_id" },
                principalSchema: "procurement",
                principalTable: "policy_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" },
                principalSchema: "procurement",
                principalTable: "receiving_source_states",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipts_purchase_order_issued_versions_tenant_id_leg~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id", "submission_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "submission_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_exceptions_invoice_match_results_tenant_id_legal_en~",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "match_result_id", "supplier_invoice_id" },
                principalSchema: "procurement",
                principalTable: "invoice_match_results",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_exceptions_invoice_match_results_tenant_id_legal_e~1",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "resolving_match_result_id", "supplier_invoice_id" },
                principalSchema: "procurement",
                principalTable: "invoice_match_results",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_exceptions_supplier_invoice_lines_tenant_id_legal_e~",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_line_id", "supplier_invoice_id" },
                principalSchema: "procurement",
                principalTable: "supplier_invoice_lines",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_match_results_invoice_match_mapping_versions_tenant~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "issued_version_id" },
                principalSchema: "procurement",
                principalTable: "invoice_match_mapping_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "purchase_order_id", "issued_version_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_match_results_purchase_order_issued_versions_tenant~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receipt_inspections_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                principalSchema: "procurement",
                principalTable: "policy_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_acceptances_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                principalSchema: "procurement",
                principalTable: "policy_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_acceptances_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" },
                principalSchema: "procurement",
                principalTable: "receiving_source_states",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sourcing_events_procurement_methods_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "sourcing_events",
                columns: new[] { "tenant_id", "legal_entity_id", "method_id" },
                principalSchema: "procurement",
                principalTable: "procurement_methods",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_invoices_invoice_match_mapping_versions_tenant_id_~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "current_mapping_version_id", "id" },
                principalSchema: "procurement",
                principalTable: "invoice_match_mapping_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_invoices_invoice_match_results_tenant_id_legal_ent~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "active_match_claim_result_id", "id" },
                principalSchema: "procurement",
                principalTable: "invoice_match_results",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_invoices_invoice_match_results_tenant_id_legal_en~1",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "last_match_result_id", "id" },
                principalSchema: "procurement",
                principalTable: "invoice_match_results",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                onDelete: ReferentialAction.Restrict);
            // Frozen reviewed exact ownership overlay; supporting unique indexes exist above.
            migrationBuilder.Sql("""
                ALTER TABLE procurement.receipt_inspections
                  ADD CONSTRAINT fk_receiving_inspection_exact_physical
                  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
                  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.receiving_acceptance_effects
                  ADD CONSTRAINT fk_receiving_effect_exact_physical
                  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
                  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.receiving_acceptance_effects
                  ADD CONSTRAINT fk_receiving_effect_exact_service
                  FOREIGN KEY (tenant_id, legal_entity_id, service_acceptance_id, receiving_source_state_id)
                  REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, source_state_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.invoice_match_allocations
                  ADD CONSTRAINT fk_invoice_allocation_exact_goods
                  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, purchase_order_line_id, receiving_source_state_id)
                  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, purchase_order_line_id, source_state_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.invoice_match_allocations
                  ADD CONSTRAINT fk_invoice_allocation_exact_service
                  FOREIGN KEY (tenant_id, legal_entity_id, service_acceptance_id, purchase_order_line_id, receiving_source_state_id)
                  REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, purchase_order_line_id, source_state_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.sourcing_events ADD CONSTRAINT fk_rfq_event_original
                  FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,id)
                  REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_original
                  FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,sourcing_event_id)
                  REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_line
                  FOREIGN KEY (tenant_id,legal_entity_id,sourcing_line_id,sourcing_event_id,purchase_request_line_id)
                  REFERENCES procurement.sourcing_lines (tenant_id,legal_entity_id,id,sourcing_event_id,purchase_request_line_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_hold
                  FOREIGN KEY (tenant_id,legal_entity_id,allocation_id,purchase_request_id,purchase_request_line_id,target_type,sourcing_event_id)
                  REFERENCES procurement.purchase_request_allocations (tenant_id,legal_entity_id,id,purchase_request_id,purchase_request_line_id,target_type,target_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // First operation: locked evidence/unknown/numeric safety veto, then manual FK removal.
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                LOCK TABLE procurement.approval_command_receipts,
                    procurement.assignment_authority_evidence,
                    procurement.audit_events,
                    procurement.budget_check_snapshots,
                    procurement.commercial_command_receipts,
                    procurement.goods_receipt_lines,
                    procurement.goods_receipts,
                    procurement.invoice_exceptions,
                    procurement.invoice_match_allocations,
                    procurement.invoice_match_exposure_states,
                    procurement.invoice_match_mapping_versions,
                    procurement.invoice_match_results,
                    procurement.operational_policy_identity_evidence,
                    procurement.outbox_events,
                    procurement.policy_versions,
                    procurement.procurement_methods,
                    procurement.purchase_order_assignment_authority,
                    procurement.purchase_order_commitment_ownership,
                    procurement.purchase_order_delivery_evidence,
                    procurement.purchase_order_finance_evidence,
                    procurement.purchase_order_finance_resolutions,
                    procurement.purchase_order_issued_versions,
                    procurement.purchase_order_lines,
                    procurement.purchase_order_submissions,
                    procurement.purchase_orders,
                    procurement.purchase_request_allocations,
                    procurement.purchase_request_approval_outcomes,
                    procurement.purchase_request_approval_submissions,
                    procurement.purchase_request_finance_release_confirmations,
                    procurement.purchase_request_finance_release_requests,
                    procurement.purchase_request_lines,
                    procurement.purchase_requests,
                    procurement.receipt_inspections,
                    procurement.receiving_acceptance_effects,
                    procurement.receiving_source_states,
                    procurement.service_acceptances,
                    procurement.sourcing_command_receipts,
                    procurement.sourcing_demand_bindings,
                    procurement.sourcing_draft_evidence,
                    procurement.sourcing_events,
                    procurement.sourcing_policy_identity_evidence,
                    procurement.spending_authority_categories,
                    procurement.spending_authority_grants,
                    procurement.spending_authority_identity_evidence,
                    procurement.supplier_invoices IN ACCESS EXCLUSIVE MODE;
                DO $canonical_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.approval_command_receipts)
                      OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts)
                      OR EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_order_commitment_ownership)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_submissions)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_command_receipts)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_draft_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.spending_authority_grants)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_outcomes)
                      OR EXISTS (SELECT 1 FROM procurement.assignment_authority_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.spending_authority_categories)
                      OR EXISTS (SELECT 1 FROM procurement.spending_authority_identity_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_order_submissions)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_request_finance_release_requests)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_demand_bindings)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_order_assignment_authority)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_order_finance_resolutions)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_order_issued_versions)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_request_finance_release_confirmations)
                      OR EXISTS (SELECT 1 FROM procurement.invoice_match_exposure_states)
                      OR EXISTS (SELECT 1 FROM procurement.invoice_match_mapping_versions)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_order_delivery_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.receiving_source_states)
                      OR EXISTS (SELECT 1 FROM procurement.invoice_match_allocations)
                      OR EXISTS (SELECT 1 FROM procurement.receiving_acceptance_effects)
                      OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE record_type IN ('InvoiceMatchAllocation','InvoiceMatchExposureState','InvoiceMatchMappingVersion','OperationalPolicyIdentityRecord','ProcurementApprovalCommandReceipt','ProcurementCommercialCommandReceipt','ProcurementSpendingAuthorityCategory','ProcurementSpendingAuthorityGrant','PurchaseOrderAssignmentAuthorityEvidence','PurchaseOrderCommitmentOwnership','PurchaseOrderDeliveryEvidence','PurchaseOrderFinanceEvidence','PurchaseOrderFinanceResolution','PurchaseOrderIssuedVersion','PurchaseOrderSubmission','PurchaseRequestApprovalOutcome','PurchaseRequestApprovalSubmission','PurchaseRequestAssignmentAuthorityEvidence','PurchaseRequestFinanceReleaseConfirmation','PurchaseRequestFinanceReleaseRequest','ReceivingAcceptanceEffect','ReceivingSourceState','SourcingCommandReceipt','SourcingDemandBinding','SourcingDraftEvidence','SourcingPolicyIdentityEvidence','SpendingAuthorityIdentityRecord')
                        OR (record_type='PurchaseRequest' AND action IN ('ApprovalCaseOpened','ApprovalDecisionRecorded','FinanceReleaseEvidenceRecorded')) OR (record_type='PurchaseOrder' AND action IN ('CommercialApprovalOpened','CommercialApprovalDecision','UnfundedDraftCancelled','UnsubmittedFinanceReleaseConfirmed','FinanceCommitmentConfirmed','FinanceCommitmentReleaseConfirmed','IssueAuthorized','SupplierDeliveryObserved','ApprovedSourcesConverted','SourceDraftUpdated')))
                      OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE event_type IN
                        ('purchase_request.approval_opened.v1','purchase_request.approval_decision_recorded.v1',
                         'purchase_request.approved.v1','purchase_request.returned.v1','purchase_request.rejected.v1',
                         'procurement.finance_release.requested.v1','procurement.finance_release.confirmed.v1','purchase_order.approval_opened.v1','purchase_order.approval_decided.v1','purchase_order.draft_cancelled.v1','purchase_order.unsubmitted_finance_released.v1','purchase_order.finance_release_confirmed.v1','purchase_order.dispatch_requested.v1','purchase_order.delivery_observed.v1','purchase_order.draft_created.v1'))
                    THEN RAISE EXCEPTION 'CanonicalGovernedRollbackWouldLoseEvidence: retained governed rows/audit/outbox require a forward fix or protected restore.';
                    END IF;
                END $canonical_guard$;

                -- Retained 20261020114243_P24PurchaseOrderCommercialExecution downgrade invariant.
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

                -- Retained 20261020133820_P25PositiveReceivingExecution downgrade invariant.
                DO $p25_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.receiving_source_states)
                        OR EXISTS (SELECT 1 FROM procurement.receiving_acceptance_effects)
                        OR EXISTS (SELECT 1 FROM procurement.goods_receipts WHERE execution_version <> 0
                            OR issued_version_id IS NOT NULL OR submission_id IS NOT NULL
                            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR branch_ref IS NOT NULL OR receiver_worker_id IS NOT NULL OR recorded_at IS NOT NULL OR notes IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.goods_receipt_lines WHERE execution_version <> 0
                            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
                            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR initially_rejected_quantity <> 0 OR physical_observation_id IS NOT NULL
                            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR physical_policy_version_id IS NOT NULL OR physical_evidence_ref IS NOT NULL
                            OR physical_sequence IS NOT NULL OR physical_effect_key IS NOT NULL OR physical_recorded_at IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.receipt_inspections WHERE execution_version <> 0
                            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR physical_observation_id IS NOT NULL OR receiving_source_state_id IS NOT NULL
                            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR qualified_quantity <> 0 OR inspector_worker_id IS NOT NULL OR policy_version_id IS NOT NULL
                            OR evidence_ref IS NOT NULL OR ledger_sequence IS NOT NULL OR effect_key IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.service_acceptances WHERE execution_version <> 0
                            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
                            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR owner_worker_id IS NOT NULL OR policy_version_id IS NOT NULL OR evidence_ref IS NOT NULL
                            OR certification_effect_id IS NOT NULL OR current_accepted_quantity <> 0 OR current_accepted_net_amount <> 0 OR notes IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE upper(btrim(policy_key)) = 'RECEIVING')
                        OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts WHERE operation IN
                            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
                        OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE action IN
                            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
                        OR EXISTS (SELECT 1 FROM procurement.audit_events
                            WHERE record_type IN ('ReceivingSourceState','ReceivingAcceptanceEffect'))
                        OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE aggregate_type = 'Receiving'
                            OR event_type IN ('procurement.goods.recorded.v1','procurement.goods.inspected.v1','procurement.fulfilment.accepted.v1')) THEN
                        RAISE EXCEPTION 'P25RollbackRequiresEvidencePreservingRecovery: receiving has been configured or used; use a reviewed forward fix or protected restore, preserving records and evidence.';
                    END IF;
                    -- P25 widens an EXISTING service amount. Even a v0 owner-written value must not
                    -- silently round or overflow on restoration of the prior numeric(18,4) column.
                    IF EXISTS (SELECT 1 FROM procurement.service_acceptances
                        WHERE accepted_amount <> trunc(accepted_amount, 4) OR abs(accepted_amount) >= 100000000000000) THEN
                        RAISE EXCEPTION 'P25RollbackAmountWouldLosePrecision: retained service amount does not fit numeric(18,4) exactly.';
                    END IF;
                END $p25_guard$;

                -- Retained 20261020164745_P26InvoiceMatchingExecution downgrade invariant.
                DO $p26_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.invoice_match_allocations)
                       OR EXISTS (SELECT 1 FROM procurement.invoice_match_exposure_states)
                       OR EXISTS (SELECT 1 FROM procurement.invoice_match_mapping_versions)
                       OR EXISTS (SELECT 1 FROM procurement.supplier_invoices
                          WHERE mapping_revision IS DISTINCT FROM 0 OR match_revision IS DISTINCT FROM 0
                             OR current_mapping_version_id IS NOT NULL OR last_match_result_id IS NOT NULL
                             OR active_match_claim_result_id IS NOT NULL OR matching_state IS DISTINCT FROM 'NotCompared')
                       OR EXISTS (SELECT 1 FROM procurement.invoice_match_results
                          WHERE execution_version IS DISTINCT FROM 0 OR invoice_version IS DISTINCT FROM 0
                             OR mapping_version_id IS NOT NULL OR purchase_order_id IS NOT NULL OR issued_version_id IS NOT NULL
                             OR invoice_original_sha256 IS NOT NULL OR actor_principal_id IS NOT NULL OR actor_worker_id IS NOT NULL
                             OR command_key IS NOT NULL OR input_fingerprint IS NOT NULL OR evidence_json IS NOT NULL
                             OR evidence_sha256 IS NOT NULL OR policy_rules_sha256 IS NOT NULL OR source_watermarks_json IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM procurement.invoice_exceptions
                          WHERE match_result_id IS NOT NULL OR supplier_invoice_line_id IS NOT NULL OR discrepancy_key IS NOT NULL
                             OR owner_worker_id IS NOT NULL OR resolving_match_result_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts WHERE operation IN ('InvoiceMapping','InvoiceCompare'))
                       OR EXISTS (SELECT 1 FROM procurement.audit_events
                          WHERE action IN ('InvoiceMapping','InvoiceCompare')
                             OR record_type IN ('InvoiceMatchMappingVersion','InvoiceMatchExposureState','InvoiceMatchAllocation'))
                       OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE event_type = 'supplier_invoice.compared.v1') THEN
                        RAISE EXCEPTION 'P26RollbackRequiresEvidencePreservingRecovery: invoice matching has retained evidence; use a reviewed forward fix or protected restore without deleting evidence.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM procurement.invoice_match_results
                       WHERE quantity_variance IS NULL OR price_variance IS NULL OR tax_variance IS NULL) THEN
                        RAISE EXCEPTION 'P26RollbackWouldLoseUnknownVariance: an unknown variance cannot be replaced with zero; preserve the nullable schema.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM procurement.invoice_match_results
                       WHERE tax_variance IS NOT NULL AND
                          (abs(tax_variance) > 99999999999999.9999 OR tax_variance <> round(tax_variance, 4))) THEN
                        RAISE EXCEPTION 'P26RollbackWouldLoseTaxPrecision: retained tax variance cannot fit numeric(18,4) exactly; preserve the expanded schema.';
                    END IF;
                END $p26_guard$;


                -- Retained 20261020190554_P27FinanceCheckerIdentity downgrade invariant.
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.budget_check_snapshots
                        WHERE checker_identity_schema_version <> 0
                           OR checked_by_worker_id IS NOT NULL OR checker_identity_sha256 IS NOT NULL)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_submissions
                        WHERE finance_identity_schema_version <> 0)
                      OR EXISTS (SELECT 1 FROM procurement.audit_events
                        WHERE COALESCE(changed_fields_json::text,'') ~
                            '"(CheckerIdentitySchemaVersion|CheckedByWorkerId|CheckerIdentitySha256|FinanceIdentitySchemaVersion|checkerIdentitySchemaVersion|checkedByWorkerId|checkerIdentitySha256|financeIdentitySchemaVersion)"')
                      OR EXISTS (SELECT 1 FROM procurement.outbox_events
                        WHERE COALESCE(payload_json::text,'') ~
                            '"(CheckerIdentitySchemaVersion|CheckedByWorkerId|CheckerIdentitySha256|FinanceIdentitySchemaVersion|checkerIdentitySchemaVersion|checkedByWorkerId|checkerIdentitySha256|financeIdentitySchemaVersion)"')
                    THEN
                        RAISE EXCEPTION 'FinanceCheckerIdentityRollbackWouldLoseEvidence';
                    END IF;
                END $guard$;

                -- Retained 20261021065049_P28GovernedSourcingDrafts downgrade invariant.
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.sourcing_events
                        WHERE execution_version <> 0 OR branch_ref IS NOT NULL
                           OR maker_worker_id IS NOT NULL OR method_id IS NOT NULL
                           OR method_revision IS NOT NULL OR source_evidence_id IS NOT NULL
                           OR original_draft_fingerprint IS NOT NULL)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_draft_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_demand_bindings)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_command_receipts)
                      OR EXISTS (SELECT 1 FROM procurement.audit_events
                        WHERE record_type IN ('SourcingDraftEvidence','SourcingDemandBinding','SourcingCommandReceipt')
                           OR (record_type='SourcingEvent' AND action='DraftReserved')
                           OR COALESCE(changed_fields_json::text,'') ~
                            '"(SourceEvidenceId|OriginalDraftFingerprint|sourceEvidenceId|originalDraftFingerprint)"')
                      OR EXISTS (SELECT 1 FROM procurement.outbox_events
                        WHERE event_type IN ('sourcing.draft_reserved.v1','sourcing.draft_cancelled.v1')
                           OR COALESCE(payload_json::text,'') ~
                            '"(SourceEvidenceId|OriginalDraftFingerprint|sourceEvidenceId|originalDraftFingerprint)"')
                    THEN
                        RAISE EXCEPTION 'GovernedSourcingRollbackWouldLoseEvidence';
                    END IF;
                END $guard$;


                -- Retained 20261021084158_P29SourcingPolicyIdentity downgrade invariant.
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence)
                    THEN
                        RAISE EXCEPTION 'SourcingPolicyIdentityRollbackWouldLoseEvidence'
                            USING ERRCODE = '55000';
                    END IF;
                END $guard$;

                -- Retained 20261021114959_P30SpendingAuthorityWorkerIdentity downgrade invariant.
                DO $authority_identity_down$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.spending_authority_identity_evidence)
                        OR EXISTS (SELECT 1 FROM procurement.audit_events
                            WHERE record_type = 'SpendingAuthorityIdentityRecord') THEN
                        RAISE EXCEPTION USING ERRCODE = '55000',
                            MESSAGE = 'Retained spending authority identity prevents destructive downgrade.';
                    END IF;
                END
                $authority_identity_down$;

                -- Retained 20261022041534_P31OperationalPolicyWorkerIdentity downgrade invariant.
                DO $operational_policy_identity_down$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence)
                        OR EXISTS (SELECT 1 FROM procurement.audit_events
                            WHERE record_type = 'OperationalPolicyIdentityRecord')
                    THEN
                        RAISE EXCEPTION USING ERRCODE = '55000',
                            MESSAGE = 'Retained operational policy identity prevents destructive downgrade.';
                    END IF;
                END
                $operational_policy_identity_down$;

                -- Drop DB-only overlays before EF removes their supporting indexes/tables.
                ALTER TABLE procurement.receipt_inspections DROP CONSTRAINT fk_receiving_inspection_exact_physical;
                ALTER TABLE procurement.receiving_acceptance_effects DROP CONSTRAINT fk_receiving_effect_exact_physical;
                ALTER TABLE procurement.receiving_acceptance_effects DROP CONSTRAINT fk_receiving_effect_exact_service;
                ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_goods;
                ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_service;
                ALTER TABLE procurement.sourcing_events DROP CONSTRAINT fk_rfq_event_original;
                ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_original;
                ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_exact_line;
                ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_exact_hold;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_tenant_id_legal_entity_~1",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipts_purchase_order_issued_versions_tenant_id_leg~",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_exceptions_invoice_match_results_tenant_id_legal_en~",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_exceptions_invoice_match_results_tenant_id_legal_e~1",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_exceptions_supplier_invoice_lines_tenant_id_legal_e~",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_match_results_invoice_match_mapping_versions_tenant~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_match_results_purchase_order_issued_versions_tenant~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropForeignKey(
                name: "FK_receipt_inspections_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropForeignKey(
                name: "FK_service_acceptances_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropForeignKey(
                name: "FK_service_acceptances_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropForeignKey(
                name: "FK_sourcing_events_procurement_methods_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_invoices_invoice_match_mapping_versions_tenant_id_~",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_invoices_invoice_match_results_tenant_id_legal_ent~",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_invoices_invoice_match_results_tenant_id_legal_en~1",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropTable(
                name: "approval_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "assignment_authority_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "commercial_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_match_allocations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_match_mapping_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "operational_policy_identity_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_assignment_authority",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_finance_resolutions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_finance_release_confirmations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "receiving_acceptance_effects",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_demand_bindings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_policy_identity_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "spending_authority_categories",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "spending_authority_identity_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_match_exposure_states",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_finance_release_requests",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "receiving_source_states",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_draft_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "spending_authority_grants",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_approval_outcomes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_delivery_evidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_approval_submissions",
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

            migrationBuilder.DropIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_active_match_cl~",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_current_mapping~",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_last_match_resu~",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_supplier_invoice_lines_tenant_id_legal_entity_id_id_supplie~",
                schema: "procurement",
                table: "supplier_invoice_lines");

            migrationBuilder.DropIndex(
                name: "ux_rfq_line_exact_source",
                schema: "procurement",
                table: "sourcing_lines");

            migrationBuilder.DropIndex(
                name: "IX_sourcing_events_tenant_id_legal_entity_id_method_id",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "ux_invoice_service_exact_source_binding",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "ux_receiving_service_state_binding",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_execution",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_receipt_inspections_tenant_id_legal_entity_id_id_goods_rece~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_inspection_execution",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "ux_rfq_allocation_exact_target",
                schema: "procurement",
                table: "purchase_request_allocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_id_purchase_~",
                schema: "procurement",
                table: "purchase_order_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_procurement_method_execution_route",
                schema: "procurement",
                table: "procurement_methods");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_operational_policy_identity_owner",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_policy_versions_tenant_id_legal_entity_id_id_policy_version~",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_policy_evaluations_tenant_id_legal_entity_id_id_purchase_re~",
                schema: "procurement",
                table: "policy_evaluations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_invoice_match_results_tenant_id_legal_entity_id_id_supplier~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_command_key",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_issued_vers~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_mapping_ver~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_match_result_i~",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_resolving_matc~",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_supplier_invo~1",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_goods_receipts_tenant_id_legal_entity_id_id_purchase_order_~",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_issued_version_id_~",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_header_execution",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_poli~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "ux_invoice_goods_exact_source_binding",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "ux_receiving_goods_physical_binding",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_line_execution",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_budget_check_snapshots_tenant_id_legal_entity_id_id_purchas~",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_budget_checker_identity",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "active_match_claim_result_id",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "current_mapping_version_id",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "last_match_result_id",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "mapping_revision",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "match_revision",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "matching_state",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "branch_ref",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "method_id",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "method_revision",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "original_draft_fingerprint",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "source_evidence_id",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "certification_effect_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "current_accepted_net_amount",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "current_accepted_quantity",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "evidence_ref",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "issued_binding_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "owner_worker_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "policy_version_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "effect_key",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "evidence_ref",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "inspector_worker_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "ledger_sequence",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "physical_binding_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "physical_observation_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "policy_version_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "qualified_quantity",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "receipt_inspections");

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

            migrationBuilder.DropColumn(
                name: "actor_principal_id",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "actor_worker_id",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "command_key",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "evidence_json",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "evidence_sha256",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "input_fingerprint",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "invoice_original_sha256",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "invoice_version",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "issued_version_id",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "mapping_version_id",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "policy_rules_sha256",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "purchase_order_id",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "source_watermarks_json",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "discrepancy_key",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropColumn(
                name: "match_result_id",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropColumn(
                name: "owner_worker_id",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropColumn(
                name: "resolving_match_result_id",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropColumn(
                name: "supplier_invoice_line_id",
                schema: "procurement",
                table: "invoice_exceptions");

            migrationBuilder.DropColumn(
                name: "branch_ref",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "issued_version_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "receiver_worker_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "recorded_at",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "submission_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "initially_rejected_quantity",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_effect_key",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_evidence_ref",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_observation_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_policy_version_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_recorded_at",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_sequence",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "checked_by_worker_id",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "checker_identity_schema_version",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "checker_identity_sha256",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.AlterColumn<decimal>(
                name: "accepted_amount",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,8)",
                oldPrecision: 38,
                oldScale: 8);

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

            migrationBuilder.AlterColumn<decimal>(
                name: "tax_variance",
                schema: "procurement",
                table: "invoice_match_results",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,8)",
                oldPrecision: 38,
                oldScale: 8,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_variance",
                schema: "procurement",
                table: "invoice_match_results",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "price_variance",
                schema: "procurement",
                table: "invoice_match_results",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" });
        }
    }
}
