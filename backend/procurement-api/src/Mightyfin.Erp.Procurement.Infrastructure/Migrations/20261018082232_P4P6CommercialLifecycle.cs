using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P4P6CommercialLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sourcing_events",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    owner_subject_id = table.Column<string>(type: "text", nullable: false),
                    opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_instance_ref = table.Column<string>(type: "text", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_sourcing_events", x => x.id);
                    table.UniqueConstraint("AK_sourcing_events_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "sourcing_awards",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: false),
                    approval_instance_ref = table.Column<string>(type: "text", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_sourcing_awards", x => x.id);
                    table.UniqueConstraint("AK_sourcing_awards_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_sourcing_awards_sourcing_events_tenant_id_legal_entity_id_s~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_awards_suppliers_tenant_id_legal_entity_id_supplie~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_invitations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    viewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivery_ref = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_sourcing_invitations", x => x.id);
                    table.UniqueConstraint("AK_sourcing_invitations_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_sourcing_invitations_sourcing_events_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_invitations_suppliers_tenant_id_legal_entity_id_su~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    purchase_request_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    unit_code = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    category_code = table.Column<string>(type: "text", nullable: true),
                    needed_by = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_sourcing_lines", x => x.id);
                    table.UniqueConstraint("AK_sourcing_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_sourcing_lines_purchase_request_lines_tenant_id_legal_entit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_lines_sourcing_events_tenant_id_legal_entity_id_so~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_contracts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_award_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    contract_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    owner_subject_id = table.Column<string>(type: "text", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ceiling_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    renewal_notice_on = table.Column<DateOnly>(type: "date", nullable: true),
                    termination_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_supplier_contracts", x => x.id);
                    table.UniqueConstraint("AK_supplier_contracts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_contracts_sourcing_awards_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_award_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_awards",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_contracts_suppliers_tenant_id_legal_entity_id_supp~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_bids",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_channel = table.Column<string>(type: "text", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    surrogate_entered_by = table.Column<string>(type: "text", nullable: true),
                    evidence_document_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correction_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_supplier_bids", x => x.id);
                    table.UniqueConstraint("AK_supplier_bids_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_bids_sourcing_events_tenant_id_legal_entity_id_sou~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bids_sourcing_invitations_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.invitation_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_invitations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bids_suppliers_tenant_id_legal_entity_id_supplier_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_award_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_award_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    awarded_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_award_lines", x => x.id);
                    table.UniqueConstraint("AK_sourcing_award_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_sourcing_award_lines_sourcing_awards_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_award_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_awards",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_award_lines_sourcing_lines_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_line_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_contract_versions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ceiling_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    approval_instance_ref = table.Column<string>(type: "text", nullable: true),
                    signed_document_service_id = table.Column<string>(type: "text", nullable: true),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    amendment_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_supplier_contract_versions", x => x.id);
                    table.UniqueConstraint("AK_supplier_contract_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_contract_versions_supplier_contracts_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_contract_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_contracts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bid_evaluations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bid_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_subject_id = table.Column<string>(type: "text", nullable: false),
                    stage = table.Column<string>(type: "text", nullable: false),
                    criterion_code = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    conflict_declared = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_bid_evaluations", x => x.id);
                    table.UniqueConstraint("AK_bid_evaluations_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_bid_evaluations_supplier_bids_tenant_id_legal_entity_id_sup~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bid_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_bids",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_bid_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bid_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    delivery_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    lead_time_text = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_supplier_bid_lines", x => x.id);
                    table.UniqueConstraint("AK_supplier_bid_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_bid_lines_sourcing_lines_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_line_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_bid_lines_supplier_bids_tenant_id_legal_entity_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bid_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_bids",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_rates",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_contract_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    unit_code = table.Column<string>(type: "text", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    maximum_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_contract_rates", x => x.id);
                    table.UniqueConstraint("AK_contract_rates_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_contract_rates_catalog_items_tenant_id_legal_entity_id_cata~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.catalog_item_id },
                        principalSchema: "procurement",
                        principalTable: "catalog_items",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_rates_supplier_contract_versions_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_contract_version_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_contract_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_award_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_contract_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    buyer_subject_id = table.Column<string>(type: "text", nullable: false),
                    ship_to_ref = table.Column<string>(type: "text", nullable: true),
                    bill_to_ref = table.Column<string>(type: "text", nullable: true),
                    payment_term_ref = table.Column<string>(type: "text", nullable: true),
                    finance_commitment_ref = table.Column<string>(type: "text", nullable: true),
                    approval_instance_ref = table.Column<string>(type: "text", nullable: true),
                    expected_delivery_on = table.Column<DateOnly>(type: "date", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closure_reason = table.Column<string>(type: "text", nullable: true),
                    current_revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.UniqueConstraint("AK_purchase_orders_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_purchase_orders_sourcing_awards_tenant_id_legal_entity_id_s~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_award_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_awards",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_supplier_contract_versions_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_contract_version_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_contract_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_orders_suppliers_tenant_id_legal_entity_id_supplie~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    site_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_note_ref = table.Column<string>(type: "text", nullable: true),
                    receiver_subject_id = table.Column<string>(type: "text", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    inventory_handoff_ref = table.Column<string>(type: "text", nullable: true),
                    asset_handoff_ref = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_goods_receipts", x => x.id);
                    table.UniqueConstraint("AK_goods_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_goods_receipts_purchase_orders_tenant_id_legal_entity_id_pu~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    purchase_request_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    unit_code = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    accepted_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    invoiced_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    cost_center_ref = table.Column<string>(type: "text", nullable: true),
                    project_ref = table.Column<string>(type: "text", nullable: true),
                    expected_delivery_on = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_purchase_order_lines", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_catalog_items_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.catalog_item_id },
                        principalSchema: "procurement",
                        principalTable: "catalog_items",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_request_lines_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    before_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    after_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    approval_instance_ref = table.Column<string>(type: "text", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_purchase_order_revisions", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_revisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_purchase_order_revisions_purchase_orders_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoices",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    intake_channel = table.Column<string>(type: "text", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    finance_posting_ref = table.Column<string>(type: "text", nullable: true),
                    evidence_document_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_for_invoice_ref = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_supplier_invoices", x => x.id);
                    table.UniqueConstraint("AK_supplier_invoices_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_invoices_purchase_orders_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoices_suppliers_tenant_id_legal_entity_id_suppl~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_order_acknowledgements",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    decision = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    supplier_actor_ref = table.Column<string>(type: "text", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_supplier_order_acknowledgements", x => x.id);
                    table.UniqueConstraint("AK_supplier_order_acknowledgements_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_order_acknowledgements_purchase_orders_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    accepted_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rejected_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    batch_ref = table.Column<string>(type: "text", nullable: true),
                    serial_ref = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_goods_receipt_lines", x => x.id);
                    table.UniqueConstraint("AK_goods_receipt_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_goods_receipts_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_receipt_lines_purchase_order_lines_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_acceptances",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    accepted_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    accepted_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    owner_subject_id = table.Column<string>(type: "text", nullable: false),
                    deliverable_ref = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    certified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_service_acceptances", x => x.id);
                    table.UniqueConstraint("AK_service_acceptances_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_service_acceptances_purchase_order_lines_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_invoice_handoffs",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    finance_posting_ref = table.Column<string>(type: "text", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_finance_invoice_handoffs", x => x.id);
                    table.UniqueConstraint("AK_finance_invoice_handoffs_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_finance_invoice_handoffs_supplier_invoices_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finance_payment_status_refs",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finance_payment_ref = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_finance_payment_status_refs", x => x.id);
                    table.UniqueConstraint("AK_finance_payment_status_refs_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_finance_payment_status_refs_supplier_invoices_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_exceptions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    owner_subject_id = table.Column<string>(type: "text", nullable: false),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_invoice_exceptions", x => x.id);
                    table.UniqueConstraint("AK_invoice_exceptions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_invoice_exceptions_supplier_invoices_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_match_results",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity_variance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    price_variance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_variance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    matched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_invoice_match_results", x => x.id);
                    table.UniqueConstraint("AK_invoice_match_results_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_invoice_match_results_policy_versions_tenant_id_legal_entit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_match_results_supplier_invoices_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_inspections",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    inspected_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    defect_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    inspector_subject_id = table.Column<string>(type: "text", nullable: false),
                    checklist_ref = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    inspected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_receipt_inspections", x => x.id);
                    table.UniqueConstraint("AK_receipt_inspections_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_receipt_inspections_goods_receipt_lines_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_returns",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    supplier_return_ref = table.Column<string>(type: "text", nullable: true),
                    inventory_adjustment_ref = table.Column<string>(type: "text", nullable: true),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_supplier_returns", x => x.id);
                    table.UniqueConstraint("AK_supplier_returns_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_returns_goods_receipt_lines_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_invoice_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_acceptance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_code_ref = table.Column<string>(type: "text", nullable: true),
                    cost_center_ref = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_supplier_invoice_lines", x => x.id);
                    table.UniqueConstraint("AK_supplier_invoice_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_goods_receipt_lines_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_purchase_order_lines_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_service_acceptances_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.service_acceptance_id },
                        principalSchema: "procurement",
                        principalTable: "service_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_invoice_lines_supplier_invoices_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_credit_note_refs",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_return_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credit_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    finance_credit_ref = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_supplier_credit_note_refs", x => x.id);
                    table.UniqueConstraint("AK_supplier_credit_note_refs_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_credit_note_refs_supplier_invoices_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_note_refs_supplier_returns_tenant_id_legal_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_return_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_returns",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_credit_note_refs_suppliers_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bid_evaluations_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "bid_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_bid_evaluations_tenant_id_legal_entity_id_supplier_bid_id_e~",
                schema: "procurement",
                table: "bid_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "evaluator_subject_id", "stage", "criterion_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_rates_tenant_id_legal_entity_id_catalog_item_id",
                schema: "procurement",
                table: "contract_rates",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_rates_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "contract_rates",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_rates_tenant_id_legal_entity_id_supplier_contract_~",
                schema: "procurement",
                table: "contract_rates",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_contract_version_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_handoffs_tenant_id_legal_entity_id_created_~",
                schema: "procurement",
                table: "finance_invoice_handoffs",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_handoffs_tenant_id_legal_entity_id_idempote~",
                schema: "procurement",
                table: "finance_invoice_handoffs",
                columns: new[] { "tenant_id", "legal_entity_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_invoice_handoffs_tenant_id_legal_entity_id_supplier~",
                schema: "procurement",
                table: "finance_invoice_handoffs",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_status_refs_tenant_id_legal_entity_id_creat~",
                schema: "procurement",
                table: "finance_payment_status_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_status_refs_tenant_id_legal_entity_id_finan~",
                schema: "procurement",
                table: "finance_payment_status_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_payment_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_payment_status_refs_tenant_id_legal_entity_id_suppl~",
                schema: "procurement",
                table: "finance_payment_status_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_purchase_orde~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_number",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_purchase_order_id",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_exceptions_tenant_id_legal_entity_id_supplier_invoi~",
                schema: "procurement",
                table: "invoice_exceptions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_policy_vers~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_supplier_in~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_legal_entity_id_catalog_item~",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_legal_entity_id_purchase_ord~",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_tenant_id_legal_entity_id_purchase_req~",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_revisions_tenant_id_legal_entity_id_created_~",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_revisions_tenant_id_legal_entity_id_purchase~",
                schema: "procurement",
                table: "purchase_order_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_legal_entity_id_number",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_legal_entity_id_sourcing_award_id",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_award_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_legal_entity_id_supplier_contract~",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_contract_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_legal_entity_id_supplier_id",
                schema: "procurement",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_purchase_orde~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_award_lines_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "sourcing_award_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_award_lines_tenant_id_legal_entity_id_sourcing_awa~",
                schema: "procurement",
                table: "sourcing_award_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_award_id", "sourcing_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_award_lines_tenant_id_legal_entity_id_sourcing_lin~",
                schema: "procurement",
                table: "sourcing_award_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_awards_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "sourcing_awards",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_awards_tenant_id_legal_entity_id_sourcing_event_id",
                schema: "procurement",
                table: "sourcing_awards",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_awards_tenant_id_legal_entity_id_supplier_id",
                schema: "procurement",
                table: "sourcing_awards",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_events_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "sourcing_events",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_events_tenant_id_legal_entity_id_number",
                schema: "procurement",
                table: "sourcing_events",
                columns: new[] { "tenant_id", "legal_entity_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_invitations_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "sourcing_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_invitations_tenant_id_legal_entity_id_sourcing_eve~",
                schema: "procurement",
                table: "sourcing_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "supplier_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_invitations_tenant_id_legal_entity_id_supplier_id",
                schema: "procurement",
                table: "sourcing_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_lines_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "sourcing_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_lines_tenant_id_legal_entity_id_purchase_request_l~",
                schema: "procurement",
                table: "sourcing_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_lines_tenant_id_legal_entity_id_sourcing_event_id_~",
                schema: "procurement",
                table: "sourcing_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bid_lines_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "supplier_bid_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bid_lines_tenant_id_legal_entity_id_sourcing_line_~",
                schema: "procurement",
                table: "supplier_bid_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bid_lines_tenant_id_legal_entity_id_supplier_bid_i~",
                schema: "procurement",
                table: "supplier_bid_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "sourcing_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bids_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "supplier_bids",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bids_tenant_id_legal_entity_id_invitation_id",
                schema: "procurement",
                table: "supplier_bids",
                columns: new[] { "tenant_id", "legal_entity_id", "invitation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bids_tenant_id_legal_entity_id_sourcing_event_id_s~",
                schema: "procurement",
                table: "supplier_bids",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "supplier_id", "submission_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_bids_tenant_id_legal_entity_id_supplier_id",
                schema: "procurement",
                table: "supplier_bids",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contract_versions_tenant_id_legal_entity_id_create~",
                schema: "procurement",
                table: "supplier_contract_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contract_versions_tenant_id_legal_entity_id_suppli~",
                schema: "procurement",
                table: "supplier_contract_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_contract_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contracts_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "supplier_contracts",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contracts_tenant_id_legal_entity_id_number",
                schema: "procurement",
                table: "supplier_contracts",
                columns: new[] { "tenant_id", "legal_entity_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contracts_tenant_id_legal_entity_id_sourcing_award~",
                schema: "procurement",
                table: "supplier_contracts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_award_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_contracts_tenant_id_legal_entity_id_supplier_id",
                schema: "procurement",
                table: "supplier_contracts",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_note_refs_tenant_id_legal_entity_id_created~",
                schema: "procurement",
                table: "supplier_credit_note_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_note_refs_tenant_id_legal_entity_id_suppli~1",
                schema: "procurement",
                table: "supplier_credit_note_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_return_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_note_refs_tenant_id_legal_entity_id_suppli~2",
                schema: "procurement",
                table: "supplier_credit_note_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "credit_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_note_refs_tenant_id_legal_entity_id_supplie~",
                schema: "procurement",
                table: "supplier_credit_note_refs",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_tenant_id_legal_entity_id_goods_rece~",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_tenant_id_legal_entity_id_purchase_o~",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_tenant_id_legal_entity_id_service_ac~",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoice_lines_tenant_id_legal_entity_id_supplier_i~",
                schema: "procurement",
                table: "supplier_invoice_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_purchase_order_~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_supplier_id_inv~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "invoice_number", "currency_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_order_acknowledgements_tenant_id_legal_entity_id_c~",
                schema: "procurement",
                table: "supplier_order_acknowledgements",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_order_acknowledgements_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "supplier_order_acknowledgements",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_tenant_id_legal_entity_id_goods_receipt_li~",
                schema: "procurement",
                table: "supplier_returns",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bid_evaluations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "contract_rates",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "finance_invoice_handoffs",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "finance_payment_status_refs",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_exceptions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_match_results",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "receipt_inspections",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_award_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_bid_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_credit_note_refs",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_invoice_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_order_acknowledgements",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_bids",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_returns",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "service_acceptances",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_invoices",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_invitations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "goods_receipt_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "goods_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_order_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_contract_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_contracts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_awards",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_events",
                schema: "procurement");
        }
    }
}
