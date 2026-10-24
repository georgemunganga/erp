using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P31PrincipalInvoiceMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~",
                schema: "procurement",
                table: "principal_receiving_facts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_id", "sequence" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_principal_order_issued_versions_tenant_id_legal_entity_id_i~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" });

            migrationBuilder.CreateTable(
                name: "principal_invoice_exposure_states",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    coverage_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_exposure_states", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_exposure_states_tenant_id_legal_entity_i~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.principal_issued_version_id });
                    table.UniqueConstraint("AK_principal_invoice_exposure_states_tenant_id_legal_entity_id~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_exposure", "purchase_order_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_issued_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND currency_code ~ '^[A-Z]{3}$' AND sequence >= 0 AND coverage_state IN ('Unknown','Verified','Stale') AND (coverage_state <> 'Verified' OR owner_reference IS NOT NULL AND owner_watermark IS NOT NULL AND owner_evidence_sha256 IS NOT NULL AND owner_evidence_sha256 ~ '^[0-9a-f]{64}$' AND owner_valid_until IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_principal_invoice_exposure_states_purchase_orders_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_invoice_exposure_states_suppliers_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_exposure_exact_issued_order",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.principal_issued_version_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_mappings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    invoice_version = table.Column<long>(type: "bigint", nullable: false),
                    mapping_revision = table.Column<int>(type: "integer", nullable: false),
                    original_invoice_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mapping_json = table.Column<string>(type: "text", nullable: false),
                    mapping_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mapped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_mappings", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_mappings_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_principal_invoice_mappings_tenant_id_legal_entity_id_id_su~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id, x.principal_issued_version_id });
                    table.UniqueConstraint("AK_principal_invoice_mappings_tenant_id_legal_entity_id_id_su~2", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id });
                    table.UniqueConstraint("AK_principal_invoice_mappings_tenant_id_legal_entity_id_id_sup~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id });
                    table.CheckConstraint("ck_pi_mapping", "supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND purchase_order_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_issued_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND invoice_version > 0 AND mapping_revision > 0 AND currency_code ~ '^[A-Z]{3}$' AND original_invoice_sha256 ~ '^[0-9a-f]{64}$' AND issued_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND (maker_worker_alias_id IS NULL OR maker_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND octet_length(mapping_json) BETWEEN 1 AND 262144 AND isfinite(mapped_at)");
                    table.ForeignKey(
                        name: "FK_principal_invoice_mappings_purchase_orders_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_invoice_mappings_supplier_invoices_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_invoice_mappings_suppliers_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_mapping_exact_issued_order",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.principal_issued_version_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_receipts", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_receipt", "supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND operation IN ('Map','OpenReview','DecideReview','Compare','IssueAction') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND response_status BETWEEN 200 AND 299 AND octet_length(response_json) BETWEEN 1 AND 262144 AND isfinite(completed_at)");
                    table.ForeignKey(
                        name: "FK_principal_invoice_receipts_supplier_invoices_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_discrepancies",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    original_invoice_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issue_key_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    owner_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    owner_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolution_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_principal_invoice_discrepancies", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_discrepancies_tenant_id_legal_entity_id_i~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id, x.mapping_version_id });
                    table.UniqueConstraint("AK_principal_invoice_discrepancies_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_discrepancy", "supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND mapping_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND purchase_order_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_issued_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND (supplier_invoice_line_id IS NULL OR supplier_invoice_line_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND original_invoice_sha256 ~ '^[0-9a-f]{64}$' AND issue_key_sha256 ~ '^[0-9a-f]{64}$' AND code <> '' AND owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND owner_principal_id <> '00000000-0000-0000-0000-000000000000' AND (owner_worker_alias_id IS NULL OR owner_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND state IN ('Open','InReview','AwaitingCorrection','ReadyToRetry','Resolved','CancelledWithReason') AND isfinite(opened_at) AND (due_at IS NULL OR isfinite(due_at)) AND (resolved_at IS NULL OR isfinite(resolved_at)) AND (state IN ('Resolved','CancelledWithReason'))=(resolved_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_pi_discrepancy_exact_mapping",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.mapping_version_id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_mappings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_discrepancy_invoice_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_line_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoice_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_mapping_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_receiving_source_state_id = table.Column<Guid>(type: "uuid", nullable: true),
                    principal_acceptance_fact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acceptance_sequence = table.Column<long>(type: "bigint", nullable: true),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_line_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    invoice_unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_mapping_lines", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_mapping_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_principal_invoice_mapping_lines_tenant_id_legal_entity_id_m~", x => new { x.tenant_id, x.legal_entity_id, x.mapping_version_id, x.supplier_invoice_line_id, x.purchase_order_line_id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id });
                    table.CheckConstraint("ck_pi_map_line", "mapping_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_invoice_line_id <> '00000000-0000-0000-0000-000000000000'::uuid AND purchase_order_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_issued_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND purchase_order_line_id <> '00000000-0000-0000-0000-000000000000'::uuid AND document_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND invoice_unit_code <> '' AND document_line_reference <> '' AND (principal_acceptance_fact_id IS NULL AND principal_receiving_source_state_id IS NULL AND acceptance_sequence IS NULL OR principal_acceptance_fact_id IS NOT NULL AND principal_acceptance_fact_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_receiving_source_state_id IS NOT NULL AND principal_receiving_source_state_id <> '00000000-0000-0000-0000-000000000000'::uuid AND acceptance_sequence IS NOT NULL AND acceptance_sequence > 0)");
                    table.ForeignKey(
                        name: "fk_pi_line_acceptance",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.principal_acceptance_fact_id, x.principal_receiving_source_state_id, x.acceptance_sequence },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_facts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_id", "sequence" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_line_exact_invoice_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_line_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoice_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_line_exact_mapping",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.mapping_version_id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_mappings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_line_exact_order_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_line_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.principal_receiving_source_state_id, x.purchase_order_id, x.principal_issued_version_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_unit_reviews",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matching_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_invoice_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_units_json = table.Column<string>(type: "text", nullable: false),
                    original_units_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_invoice_unit_reviews", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_unit_reviews_tenant_id_legal_entity_id_i~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.mapping_version_id, x.supplier_invoice_id, x.principal_issued_version_id });
                    table.UniqueConstraint("AK_principal_invoice_unit_reviews_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_principal_invoice_unit_reviews_tenant_id_legal_entity_id_id~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.core_case_id });
                    table.CheckConstraint("ck_pi_review", "supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND mapping_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_issued_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND matching_policy_id <> review_policy_id AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND original_invoice_sha256 ~ '^[0-9a-f]{64}$' AND original_units_sha256 ~ '^[0-9a-f]{64}$' AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND (maker_worker_alias_id IS NULL OR maker_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND octet_length(original_units_json) BETWEEN 1 AND 262144 AND isfinite(opened_at)");
                    table.ForeignKey(
                        name: "FK_principal_invoice_unit_reviews_policy_versions_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.matching_policy_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_invoice_unit_reviews_policy_versions_tenant_id_l~1",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_policy_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_review_exact_mapping",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.mapping_version_id, x.supplier_invoice_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_mappings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_review_assignments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    reviewer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_invoice_review_assignments", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_review_assignments_tenant_id_legal_entit~1", x => new { x.tenant_id, x.legal_entity_id, x.core_assignment_id, x.review_source_id, x.core_case_id });
                    table.UniqueConstraint("AK_principal_invoice_review_assignments_tenant_id_legal_entity~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_assignment", "review_source_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND reviewer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (reviewer_worker_alias_id IS NULL OR reviewer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "fk_pi_review_assignment_exact_case",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_unit_reviews",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_review_outcomes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evidence_json = table.Column<string>(type: "text", nullable: false),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reviewer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    reviewer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_invoice_review_outcomes", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_review_outcomes_tenant_id_legal_entity_i~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.review_source_id });
                    table.UniqueConstraint("AK_principal_invoice_review_outcomes_tenant_id_legal_entity_id~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_outcome", "review_source_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND final_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND state IN ('Verified','Returned','Rejected') AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND reviewer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (reviewer_worker_alias_id IS NULL OR reviewer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(completed_at)");
                    table.ForeignKey(
                        name: "fk_pi_review_outcome_exact_case",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_unit_reviews",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_review_attestations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    reviewer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_units_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    statement = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    attested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_review_attestations", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_review_attestations_tenant_id_legal_entit~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_attestation", "review_source_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND reviewer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (reviewer_worker_alias_id IS NULL OR reviewer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND original_units_sha256 ~ '^[0-9a-f]{64}$' AND statement = 'OriginalDocumentUnitsConfirmed' AND isfinite(attested_at)");
                    table.ForeignKey(
                        name: "fk_pi_attestation_exact_assignment",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.core_assignment_id, x.review_source_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_review_assignments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id", "review_source_id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_attestation_exact_case",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_unit_reviews",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_comparisons",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matching_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exposure_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_version = table.Column<long>(type: "bigint", nullable: false),
                    original_invoice_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evidence_json = table.Column<string>(type: "text", nullable: false),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    owner_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    owner_watermark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    compared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_comparisons", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_comparisons_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_principal_invoice_comparisons_tenant_id_legal_entity_id_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id });
                    table.UniqueConstraint("AK_principal_invoice_comparisons_tenant_id_legal_entity_id_id~1", x => new { x.tenant_id, x.legal_entity_id, x.id, x.supplier_invoice_id, x.mapping_version_id });
                    table.UniqueConstraint("AK_principal_invoice_comparisons_tenant_id_legal_entity_id_id~2", x => new { x.tenant_id, x.legal_entity_id, x.id, x.mapping_version_id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id });
                    table.CheckConstraint("ck_pi_comparison", "supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND mapping_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND purchase_order_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_issued_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND review_source_id <> '00000000-0000-0000-0000-000000000000'::uuid AND review_outcome_id <> '00000000-0000-0000-0000-000000000000'::uuid AND matching_policy_id <> '00000000-0000-0000-0000-000000000000'::uuid AND exposure_state_id <> '00000000-0000-0000-0000-000000000000'::uuid AND invoice_version > 0 AND original_invoice_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND outcome = 'Compared' AND owner_reference <> '' AND owner_watermark <> '' AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(compared_at)");
                    table.ForeignKey(
                        name: "FK_principal_invoice_comparisons_policy_versions_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.matching_policy_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_compare_exact_exposure",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.exposure_state_id, x.purchase_order_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_exposure_states",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_compare_exact_mapping",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.mapping_version_id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_mappings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_compare_exact_review_outcome",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_outcome_id, x.review_source_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_review_outcomes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "review_source_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_compare_exact_review_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id, x.mapping_version_id, x.supplier_invoice_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_unit_reviews",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "mapping_version_id", "supplier_invoice_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_claims",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comparison_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exposure_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_receiving_source_state_id = table.Column<Guid>(type: "uuid", nullable: true),
                    principal_acceptance_fact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acceptance_sequence = table.Column<long>(type: "bigint", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    exposure_sequence = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_claims", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_claims_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_claim", "supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_invoice_line_id <> '00000000-0000-0000-0000-000000000000'::uuid AND mapping_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND comparison_id <> '00000000-0000-0000-0000-000000000000'::uuid AND exposure_state_id <> '00000000-0000-0000-0000-000000000000'::uuid AND purchase_order_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_issued_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND purchase_order_line_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND currency_code ~ '^[A-Z]{3}$' AND unit_code <> '' AND quantity > 0 AND net_amount >= 0 AND tax_amount >= 0 AND exposure_sequence > 0 AND owner_coverage_reference <> '' AND (principal_receiving_source_state_id IS NULL AND principal_acceptance_fact_id IS NULL AND acceptance_sequence IS NULL OR principal_receiving_source_state_id IS NOT NULL AND principal_receiving_source_state_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_acceptance_fact_id IS NOT NULL AND principal_acceptance_fact_id <> '00000000-0000-0000-0000-000000000000'::uuid AND acceptance_sequence IS NOT NULL AND acceptance_sequence > 0)");
                    table.ForeignKey(
                        name: "fk_pi_claim_acceptance",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.principal_acceptance_fact_id, x.principal_receiving_source_state_id, x.acceptance_sequence },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_facts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_id", "sequence" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_claim_exact_comparison",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.comparison_id, x.mapping_version_id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_comparisons",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_claim_exact_exposure",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.exposure_state_id, x.purchase_order_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_exposure_states",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_claim_exact_invoice_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_line_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoice_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_claim_exact_mapping_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.mapping_version_id, x.supplier_invoice_line_id, x.purchase_order_line_id, x.supplier_invoice_id, x.purchase_order_id, x.principal_issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_mapping_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_line_id", "purchase_order_line_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_claim_exact_order_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_claim_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.principal_receiving_source_state_id, x.purchase_order_id, x.principal_issued_version_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_discrepancy_actions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discrepancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    resolving_comparison_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_discrepancy_actions", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_discrepancy_actions_tenant_id_legal_entit~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_discrepancy_action", "discrepancy_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND mapping_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND (resolving_comparison_id IS NULL OR resolving_comparison_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND action IN ('Opened','Assigned','Commented','CorrectionRequested','ReadyToRetry','Resolved','CancelledWithReason') AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND reason <> '' AND isfinite(at)");
                    table.ForeignKey(
                        name: "fk_pi_discrepancy_action_exact_comparison",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.resolving_comparison_id, x.supplier_invoice_id, x.mapping_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_comparisons",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "mapping_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_discrepancy_action_exact_issue",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.discrepancy_id, x.supplier_invoice_id, x.mapping_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_discrepancies",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "mapping_version_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_invoice_states",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    invoice_version = table.Column<long>(type: "bigint", nullable: false),
                    mapping_revision = table.Column<int>(type: "integer", nullable: false),
                    current_mapping_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_comparison_id = table.Column<Guid>(type: "uuid", nullable: true),
                    active_claim_comparison_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_principal_invoice_states", x => x.id);
                    table.UniqueConstraint("AK_principal_invoice_states_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pi_state", "supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND invoice_version > 0 AND mapping_revision >= 0 AND currency_code ~ '^[A-Z]{3}$' AND state IN ('Unmapped','Mapped','ReviewPending','Reviewed','ComparisonBlocked','Compared') AND (mapping_revision=0)=(current_mapping_id IS NULL) AND (active_claim_comparison_id IS NULL OR last_comparison_id=active_claim_comparison_id)");
                    table.ForeignKey(
                        name: "FK_principal_invoice_states_supplier_invoices_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_invoice_states_suppliers_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_state_active_claim",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.active_claim_comparison_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_comparisons",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_state_current_mapping",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.current_mapping_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_mappings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pi_state_last_comparison",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.last_comparison_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "principal_invoice_comparisons",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_claims_tenant_id_legal_entity_id_comparis~",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "comparison_id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_claims_tenant_id_legal_entity_id_exposure~",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "exposure_state_id", "purchase_order_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_claims_tenant_id_legal_entity_id_mapping_~",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_line_id", "purchase_order_line_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_claims_tenant_id_legal_entity_id_princip~1",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_receiving_source_state_id", "purchase_order_id", "principal_issued_version_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_claims_tenant_id_legal_entity_id_principa~",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_acceptance_fact_id", "principal_receiving_source_state_id", "acceptance_sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_claims_tenant_id_legal_entity_id_purchase~",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_claims_tenant_id_legal_entity_id_supplier~",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_line_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_claim_line",
                schema: "procurement",
                table: "principal_invoice_claims",
                columns: new[] { "tenant_id", "legal_entity_id", "comparison_id", "supplier_invoice_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_comparisons_tenant_id_legal_entity_id_exp~",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "exposure_state_id", "purchase_order_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_comparisons_tenant_id_legal_entity_id_map~",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_comparisons_tenant_id_legal_entity_id_mat~",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "matching_policy_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_comparisons_tenant_id_legal_entity_id_re~1",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id", "mapping_version_id", "supplier_invoice_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_comparisons_tenant_id_legal_entity_id_rev~",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "review_outcome_id", "review_source_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_compare_claim_owner",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_compare_command",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_compare_invoice_owner",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_compare_mapping_claim_owner",
                schema: "procurement",
                table: "principal_invoice_comparisons",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_discrepancies_tenant_id_legal_entity_id_m~",
                schema: "procurement",
                table: "principal_invoice_discrepancies",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_discrepancies_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "principal_invoice_discrepancies",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_line_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_discrepancy_attempt_issue",
                schema: "procurement",
                table: "principal_invoice_discrepancies",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id", "mapping_version_id", "command_key", "issue_key_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_discrepancy_owner",
                schema: "procurement",
                table: "principal_invoice_discrepancies",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "mapping_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_discrepancy_actions_tenant_id_legal_enti~1",
                schema: "procurement",
                table: "principal_invoice_discrepancy_actions",
                columns: new[] { "tenant_id", "legal_entity_id", "resolving_comparison_id", "supplier_invoice_id", "mapping_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_discrepancy_actions_tenant_id_legal_entit~",
                schema: "procurement",
                table: "principal_invoice_discrepancy_actions",
                columns: new[] { "tenant_id", "legal_entity_id", "discrepancy_id", "supplier_invoice_id", "mapping_version_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_discrepancy_action_command",
                schema: "procurement",
                table: "principal_invoice_discrepancy_actions",
                columns: new[] { "tenant_id", "legal_entity_id", "discrepancy_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_exposure_states_tenant_id_legal_entity_i~1",
                schema: "procurement",
                table: "principal_invoice_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_exposure_states_tenant_id_legal_entity_id~",
                schema: "procurement",
                table: "principal_invoice_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_exposure_source",
                schema: "procurement",
                table: "principal_invoice_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "principal_issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mapping_lines_tenant_id_legal_entity_id_~1",
                schema: "procurement",
                table: "principal_invoice_mapping_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_acceptance_fact_id", "principal_receiving_source_state_id", "acceptance_sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mapping_lines_tenant_id_legal_entity_id_~2",
                schema: "procurement",
                table: "principal_invoice_mapping_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_receiving_source_state_id", "purchase_order_id", "principal_issued_version_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mapping_lines_tenant_id_legal_entity_id_m~",
                schema: "procurement",
                table: "principal_invoice_mapping_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mapping_lines_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_invoice_mapping_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mapping_lines_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "principal_invoice_mapping_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_line_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_map_line",
                schema: "procurement",
                table: "principal_invoice_mapping_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_map_line_claim_owner",
                schema: "procurement",
                table: "principal_invoice_mapping_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_line_id", "purchase_order_line_id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mappings_tenant_id_legal_entity_id_princi~",
                schema: "procurement",
                table: "principal_invoice_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mappings_tenant_id_legal_entity_id_purcha~",
                schema: "procurement",
                table: "principal_invoice_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_mappings_tenant_id_legal_entity_id_suppli~",
                schema: "procurement",
                table: "principal_invoice_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_map_invoice_owner",
                schema: "procurement",
                table: "principal_invoice_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_map_owner",
                schema: "procurement",
                table: "principal_invoice_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "purchase_order_id", "principal_issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_map_review_owner",
                schema: "procurement",
                table: "principal_invoice_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "principal_issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_map_revision",
                schema: "procurement",
                table: "principal_invoice_mappings",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id", "mapping_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_receipts_tenant_id_legal_entity_id_suppli~",
                schema: "procurement",
                table: "principal_invoice_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_receipt_command",
                schema: "procurement",
                table: "principal_invoice_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_review_assignments_tenant_id_legal_entity~",
                schema: "procurement",
                table: "principal_invoice_review_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_assignment",
                schema: "procurement",
                table: "principal_invoice_review_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_review_attestations_tenant_id_legal_enti~1",
                schema: "procurement",
                table: "principal_invoice_review_attestations",
                columns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id", "review_source_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_review_attestations_tenant_id_legal_entit~",
                schema: "procurement",
                table: "principal_invoice_review_attestations",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_attestation_decision",
                schema: "procurement",
                table: "principal_invoice_review_attestations",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_review_outcomes_tenant_id_legal_entity_id~",
                schema: "procurement",
                table: "principal_invoice_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_outcome",
                schema: "procurement",
                table: "principal_invoice_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_outcome_owner",
                schema: "procurement",
                table: "principal_invoice_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "review_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_states_tenant_id_legal_entity_id_active_c~",
                schema: "procurement",
                table: "principal_invoice_states",
                columns: new[] { "tenant_id", "legal_entity_id", "active_claim_comparison_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_states_tenant_id_legal_entity_id_current_~",
                schema: "procurement",
                table: "principal_invoice_states",
                columns: new[] { "tenant_id", "legal_entity_id", "current_mapping_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_states_tenant_id_legal_entity_id_last_com~",
                schema: "procurement",
                table: "principal_invoice_states",
                columns: new[] { "tenant_id", "legal_entity_id", "last_comparison_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_states_tenant_id_legal_entity_id_supplier~",
                schema: "procurement",
                table: "principal_invoice_states",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_state_invoice",
                schema: "procurement",
                table: "principal_invoice_states",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_unit_reviews_tenant_id_legal_entity_id_m~1",
                schema: "procurement",
                table: "principal_invoice_unit_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_id", "principal_issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_unit_reviews_tenant_id_legal_entity_id_ma~",
                schema: "procurement",
                table: "principal_invoice_unit_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "matching_policy_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_invoice_unit_reviews_tenant_id_legal_entity_id_re~",
                schema: "procurement",
                table: "principal_invoice_unit_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "review_policy_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_case",
                schema: "procurement",
                table: "principal_invoice_unit_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_case_owner",
                schema: "procurement",
                table: "principal_invoice_unit_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_mapping",
                schema: "procurement",
                table: "principal_invoice_unit_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pi_review_source_owner",
                schema: "procurement",
                table: "principal_invoice_unit_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "mapping_version_id", "supplier_invoice_id", "principal_issued_version_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P31PrincipalInvoiceOwnershipGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "principal_invoice_claims",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_discrepancy_actions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_review_attestations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_states",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_mapping_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_discrepancies",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_review_assignments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_comparisons",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_exposure_states",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_review_outcomes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_unit_reviews",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_invoice_mappings",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~",
                schema: "procurement",
                table: "principal_receiving_sources");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~",
                schema: "procurement",
                table: "principal_receiving_facts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_principal_order_issued_versions_tenant_id_legal_entity_id_i~",
                schema: "procurement",
                table: "principal_order_issued_versions");
        }
    }
}
