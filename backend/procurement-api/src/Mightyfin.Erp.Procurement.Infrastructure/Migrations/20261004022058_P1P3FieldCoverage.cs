using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P1P3FieldCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_line_nonnegative",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.AddColumn<string>(
                name: "company_id_type",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_currency_code",
                schema: "procurement",
                table: "suppliers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "eligible_from",
                schema: "procurement",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "eligible_until",
                schema: "procurement",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hold_reason",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_local",
                schema: "procurement",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_orderable",
                schema: "procurement",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_preferred",
                schema: "procurement",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_strategic",
                schema: "procurement",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "language_code",
                schema: "procurement",
                table: "suppliers",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_term_ref",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_verification_callback_ref",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_verification_status",
                schema: "procurement",
                table: "suppliers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_verification_token",
                schema: "procurement",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portal_access_state",
                schema: "procurement",
                table: "suppliers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remarks",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "risk_tier",
                schema: "procurement",
                table: "suppliers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "status_decided_at",
                schema: "procurement",
                table: "suppliers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_decided_by",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "status_policy_version_id",
                schema: "procurement",
                table: "suppliers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_type",
                schema: "procurement",
                table: "suppliers",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_category_ref",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_identifier_ref",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trading_name",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                schema: "procurement",
                table: "suppliers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "procurement",
                table: "supplier_contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language_code",
                schema: "procurement",
                table: "supplier_contacts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "procurement",
                table: "supplier_contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mobile_phone",
                schema: "procurement",
                table: "supplier_contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "salutation",
                schema: "procurement",
                table: "supplier_contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "work_phone",
                schema: "procurement",
                table: "supplier_contacts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_instance_ref",
                schema: "procurement",
                table: "purchase_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval_state",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "branch_ref",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_address_text",
                schema: "procurement",
                table: "purchase_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_location_ref",
                schema: "procurement",
                table: "purchase_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "funding_source_ref",
                schema: "procurement",
                table: "purchase_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes_to_approver",
                schema: "procurement",
                table: "purchase_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "on_behalf_of_subject_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plan_ref",
                schema: "procurement",
                table: "purchase_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "site_ref",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_channel",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                schema: "procurement",
                table: "purchase_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "submitted_version",
                schema: "procurement",
                table: "purchase_requests",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category_code",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cost_center_ref",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_percent",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "estimated_currency_code",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "needed_by",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "preferred_supplier_id",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "project_ref",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purchase_type",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "specification_document_ref_id",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_code_ref",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "brand",
                schema: "procurement",
                table: "catalog_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "expense_account_ref",
                schema: "procurement",
                table: "catalog_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "image_document_ref_id",
                schema: "procurement",
                table: "catalog_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "inspection_required",
                schema: "procurement",
                table: "catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_asset",
                schema: "procurement",
                table: "catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_purchasable",
                schema: "procurement",
                table: "catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_stock_item",
                schema: "procurement",
                table: "catalog_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "lead_time_days",
                schema: "procurement",
                table: "catalog_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manufacturer",
                schema: "procurement",
                table: "catalog_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manufacturer_part_number",
                schema: "procurement",
                table: "catalog_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "minimum_order_quantity",
                schema: "procurement",
                table: "catalog_items",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purchase_unit_code",
                schema: "procurement",
                table: "catalog_items",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "purchase_unit_conversion",
                schema: "procurement",
                table: "catalog_items",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_version",
                schema: "procurement",
                table: "catalog_items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "specification_document_ref_id",
                schema: "procurement",
                table: "catalog_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_request_lines_tenant_id_legal_entity_id_purchase_r~",
                schema: "procurement",
                table: "purchase_request_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "id" });

            migrationBuilder.CreateTable(
                name: "attachment_refs",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_service_id = table.Column<string>(type: "text", nullable: false),
                    document_version = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    classification = table.Column<string>(type: "text", nullable: true),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    scan_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_attachment_refs", x => x.id);
                    table.UniqueConstraint("AK_attachment_refs_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "budget_check_snapshots",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finance_check_ref = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    checked_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reservation_ref = table.Column<string>(type: "text", nullable: true),
                    reservation_status = table.Column<string>(type: "text", nullable: true),
                    exchange_rate_source = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_budget_check_snapshots", x => x.id);
                    table.UniqueConstraint("AK_budget_check_snapshots_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_budget_check_snapshots_purchase_requests_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_entries",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    site_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    contract_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_basis = table.Column<string>(type: "text", nullable: true),
                    minimum_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    maximum_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    published_version = table.Column<int>(type: "integer", nullable: false),
                    audience_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_catalog_entries", x => x.id);
                    table.UniqueConstraint("AK_catalog_entries_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_catalog_entry_values", "unit_price >= 0 AND (minimum_quantity IS NULL OR minimum_quantity >= 0) AND (maximum_quantity IS NULL OR maximum_quantity >= 0) AND (minimum_quantity IS NULL OR maximum_quantity IS NULL OR maximum_quantity >= minimum_quantity)");
                    table.ForeignKey(
                        name: "FK_catalog_entries_catalog_items_tenant_id_legal_entity_id_cat~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.catalog_item_id },
                        principalSchema: "procurement",
                        principalTable: "catalog_items",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_entries_suppliers_tenant_id_legal_entity_id_supplie~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "policy_evaluations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    result_code = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_policy_evaluations", x => x.id);
                    table.UniqueConstraint("AK_policy_evaluations_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_policy_evaluations_policy_versions_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_policy_evaluations_purchase_requests_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_request_allocations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estimated_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    decided_by = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_purchase_request_allocations", x => x.id);
                    table.UniqueConstraint("AK_purchase_request_allocations_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr_allocation_nonnegative", "quantity > 0 AND estimated_value >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_request_allocations_purchase_request_lines_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id, x.purchase_request_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_request_allocations_purchase_requests_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_attributes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    field_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    field_value = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_supplier_attributes", x => x.id);
                    table.UniqueConstraint("AK_supplier_attributes_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_attributes_suppliers_tenant_id_legal_entity_id_sup~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_categories",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_supplier_categories", x => x.id);
                    table.UniqueConstraint("AK_supplier_categories_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_categories_suppliers_tenant_id_legal_entity_id_sup~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_supplier_decisions", x => x.id);
                    table.UniqueConstraint("AK_supplier_decisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_decisions_suppliers_tenant_id_legal_entity_id_supp~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_qualifications",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    issuer = table.Column<string>(type: "text", nullable: true),
                    reference_number = table.Column<string>(type: "text", nullable: true),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    review_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reviewed_by = table.Column<string>(type: "text", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_document_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_supplier_qualifications", x => x.id);
                    table.UniqueConstraint("AK_supplier_qualifications_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_qualifications_suppliers_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_sites",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    address_line1 = table.Column<string>(type: "text", nullable: false),
                    address_line2 = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    region = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_service_site = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_supplier_sites", x => x.id);
                    table.UniqueConstraint("AK_supplier_sites_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_sites_suppliers_tenant_id_legal_entity_id_supplier~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_tenant_id_legal_entity_id_is_orderable_status",
                schema: "procurement",
                table: "suppliers",
                columns: new[] { "tenant_id", "legal_entity_id", "is_orderable", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contacts_tenant_id_legal_entity_id_supplier_id_is_~",
                schema: "procurement",
                table: "supplier_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "is_primary" },
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_lines_tenant_id_legal_entity_id_catalog_it~",
                schema: "procurement",
                table: "purchase_request_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_lines_tenant_id_legal_entity_id_preferred_~",
                schema: "procurement",
                table: "purchase_request_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "preferred_supplier_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_line_nonnegative",
                schema: "procurement",
                table: "purchase_request_lines",
                sql: "requested_quantity > 0 AND estimated_unit_price >= 0 AND approved_quantity >= 0 AND allocated_quantity >= 0 AND ordered_quantity >= 0 AND accepted_quantity >= 0 AND cancelled_quantity >= 0 AND (discount_percent IS NULL OR (discount_percent >= 0 AND discount_percent <= 100))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_catalog_item_buying_values",
                schema: "procurement",
                table: "catalog_items",
                sql: "(purchase_unit_conversion IS NULL OR purchase_unit_conversion > 0) AND (minimum_order_quantity IS NULL OR minimum_order_quantity >= 0) AND (lead_time_days IS NULL OR lead_time_days >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_attachment_refs_tenant_id_legal_entity_id_document_service_~",
                schema: "procurement",
                table: "attachment_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "document_service_id", "document_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachment_refs_tenant_id_legal_entity_id_owner_type_owner_~",
                schema: "procurement",
                table: "attachment_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "owner_type", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_check_snapshots_tenant_id_legal_entity_id_finance_ch~",
                schema: "procurement",
                table: "budget_check_snapshots",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_check_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_check_snapshots_tenant_id_legal_entity_id_purchase_r~",
                schema: "procurement",
                table: "budget_check_snapshots",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_entries_tenant_id_legal_entity_id_catalog_item_id_s~",
                schema: "procurement",
                table: "catalog_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_item_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_entries_tenant_id_legal_entity_id_supplier_id",
                schema: "procurement",
                table: "catalog_entries",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_evaluations_tenant_id_legal_entity_id_policy_version~",
                schema: "procurement",
                table: "policy_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_policy_evaluations_tenant_id_legal_entity_id_purchase_reque~",
                schema: "procurement",
                table: "policy_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_allocations_tenant_id_legal_entity_id_idem~",
                schema: "procurement",
                table: "purchase_request_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_allocations_tenant_id_legal_entity_id_purc~",
                schema: "procurement",
                table: "purchase_request_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "purchase_request_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_attributes_tenant_id_legal_entity_id_supplier_id_k~",
                schema: "procurement",
                table: "supplier_attributes",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "kind", "field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_categories_tenant_id_legal_entity_id_supplier_id_c~",
                schema: "procurement",
                table: "supplier_categories",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_decisions_tenant_id_legal_entity_id_supplier_id_de~",
                schema: "procurement",
                table: "supplier_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "decided_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_qualifications_tenant_id_legal_entity_id_supplier_~",
                schema: "procurement",
                table: "supplier_qualifications",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_sites_tenant_id_legal_entity_id_supplier_id_kind",
                schema: "procurement",
                table: "supplier_sites",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_sites_tenant_id_legal_entity_id_supplier_id_kind_i~",
                schema: "procurement",
                table: "supplier_sites",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "kind", "is_primary" },
                unique: true,
                filter: "is_primary = true");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_request_lines_catalog_items_tenant_id_legal_entity~",
                schema: "procurement",
                table: "purchase_request_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_item_id" },
                principalSchema: "procurement",
                principalTable: "catalog_items",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_request_lines_suppliers_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "purchase_request_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "preferred_supplier_id" },
                principalSchema: "procurement",
                principalTable: "suppliers",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_request_lines_catalog_items_tenant_id_legal_entity~",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_request_lines_suppliers_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropTable(
                name: "attachment_refs",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "budget_check_snapshots",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "catalog_entries",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "policy_evaluations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_request_allocations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_attributes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_categories",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_qualifications",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_sites",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "IX_suppliers_tenant_id_legal_entity_id_is_orderable_status",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropIndex(
                name: "IX_supplier_contacts_tenant_id_legal_entity_id_supplier_id_is_~",
                schema: "procurement",
                table: "supplier_contacts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_request_lines_tenant_id_legal_entity_id_purchase_r~",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropIndex(
                name: "IX_purchase_request_lines_tenant_id_legal_entity_id_catalog_it~",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropIndex(
                name: "IX_purchase_request_lines_tenant_id_legal_entity_id_preferred_~",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_line_nonnegative",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_catalog_item_buying_values",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "company_id_type",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "default_currency_code",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "eligible_from",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "eligible_until",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "hold_reason",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "is_local",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "is_orderable",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "is_preferred",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "is_strategic",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "language_code",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "payment_term_ref",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "payment_verification_callback_ref",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "payment_verification_status",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "payment_verification_token",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "portal_access_state",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "remarks",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "risk_tier",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "status_decided_at",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "status_decided_by",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "status_policy_version_id",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "status_reason",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "supplier_type",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "tax_category_ref",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "tax_identifier_ref",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "trading_name",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "website",
                schema: "procurement",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "procurement",
                table: "supplier_contacts");

            migrationBuilder.DropColumn(
                name: "language_code",
                schema: "procurement",
                table: "supplier_contacts");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "procurement",
                table: "supplier_contacts");

            migrationBuilder.DropColumn(
                name: "mobile_phone",
                schema: "procurement",
                table: "supplier_contacts");

            migrationBuilder.DropColumn(
                name: "salutation",
                schema: "procurement",
                table: "supplier_contacts");

            migrationBuilder.DropColumn(
                name: "work_phone",
                schema: "procurement",
                table: "supplier_contacts");

            migrationBuilder.DropColumn(
                name: "approval_instance_ref",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "approval_state",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "branch_ref",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "delivery_address_text",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "delivery_location_ref",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "funding_source_ref",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "notes_to_approver",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "on_behalf_of_subject_id",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "plan_ref",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "site_ref",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "source_channel",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "submitted_version",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "category_code",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "cost_center_ref",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "discount_percent",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "estimated_currency_code",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "needed_by",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "preferred_supplier_id",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "project_ref",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "purchase_type",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "specification_document_ref_id",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "tax_code_ref",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "brand",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "expense_account_ref",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "image_document_ref_id",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "inspection_required",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "is_asset",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "is_purchasable",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "is_stock_item",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "lead_time_days",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "manufacturer",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "manufacturer_part_number",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "minimum_order_quantity",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "purchase_unit_code",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "purchase_unit_conversion",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "source_version",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.DropColumn(
                name: "specification_document_ref_id",
                schema: "procurement",
                table: "catalog_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_line_nonnegative",
                schema: "procurement",
                table: "purchase_request_lines",
                sql: "requested_quantity > 0 AND estimated_unit_price >= 0 AND approved_quantity >= 0 AND allocated_quantity >= 0 AND ordered_quantity >= 0 AND accepted_quantity >= 0 AND cancelled_quantity >= 0");
        }
    }
}
