using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P26InvoiceMatchingExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddUniqueConstraint(
                name: "AK_supplier_invoice_lines_tenant_id_legal_entity_id_id_supplie~",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_invoice_issued_order_binding",
                schema: "procurement",
                table: "purchase_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_invoice_match_results_tenant_id_legal_entity_id_id_supplier~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" });

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
                name: "ux_invoice_service_exact_source_binding",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_line_id", "source_state_binding_id" },
                unique: true);

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
                name: "ux_invoice_goods_exact_source_binding",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_line_id", "source_state_binding_id" },
                unique: true);

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
            // Frozen migration-owned ownership overlay; independent of future helper changes.
            migrationBuilder.Sql(
                """
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lock and refuse loss of retained evidence before any destructive operation.
            migrationBuilder.Sql(
                """
                LOCK TABLE procurement.invoice_match_allocations, procurement.invoice_match_exposure_states,
                    procurement.invoice_match_mapping_versions, procurement.supplier_invoices,
                    procurement.invoice_match_results, procurement.invoice_exceptions,
                    procurement.commercial_command_receipts, procurement.audit_events,
                    procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
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
                ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_goods;
                ALTER TABLE procurement.invoice_match_allocations DROP CONSTRAINT fk_invoice_allocation_exact_service;
                """);
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
                name: "invoice_match_allocations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_match_mapping_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_match_exposure_states",
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
                name: "ux_invoice_service_exact_source_binding",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_invoice_issued_order_binding",
                schema: "procurement",
                table: "purchase_order_issued_versions");

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

            migrationBuilder.DropIndex(
                name: "ux_invoice_goods_exact_source_binding",
                schema: "procurement",
                table: "goods_receipt_lines");

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
        }
    }
}
