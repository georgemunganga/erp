using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P30PrincipalCommercialWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.AddColumn<int>(
                name: "draft_revision",
                schema: "procurement",
                table: "supplier_bids",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "original_direct_source_sha256",
                schema: "procurement",
                table: "purchase_orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "principal_draft_schema_version",
                schema: "procurement",
                table: "purchase_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "principal_order_draft_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_draft_receipts", x => x.id);
                    table.UniqueConstraint("AK_principal_order_draft_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_po_principal_receipt", "schema_version=2 AND operation IN ('Create','Cancel','Funding','Submit','Decide','Issue','Ack','ReleaseRequest','ReleaseConfirm') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND ((operation IN ('Create','Submit','Issue') AND response_status=201) OR (operation IN ('Cancel','Funding','Decide','Ack','ReleaseRequest','ReleaseConfirm') AND response_status=200))");
                    table.ForeignKey(
                        name: "FK_principal_order_draft_receipts_purchase_orders_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_funding_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manual_commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approved_manual_policy_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_reservations_json = table.Column<string>(type: "text", nullable: false),
                    attested_gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    buyer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    recorded_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by_organisation_wide = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_funding_evidence", x => x.id);
                    table.UniqueConstraint("AK_principal_order_funding_evidence_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_funding", "schema_version=2 AND commercial_revision=1 AND attested_gross_amount>=0 AND length(btrim(manual_commitment_reference))>0 AND length(btrim(external_evidence_reference))>0 AND length(btrim(approved_manual_policy_reference))>0 AND currency_code ~ '^[A-Z]{3}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND recorded_by_principal_id IS NOT NULL AND recorded_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND recorded_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (recorded_by_worker_alias_id IS NULL OR recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_funding_evidence_purchase_orders_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_source_bindings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_version = table.Column<long>(type: "bigint", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_business_version = table.Column<long>(type: "bigint", nullable: false),
                    approval_outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finance_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method_revision = table.Column<int>(type: "integer", nullable: false),
                    method_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    request_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_decision_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finance_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    principal_identity_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    buyer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    buyer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requester_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_estimate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    proposed_unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estimated_value = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_principal_order_source_bindings", x => x.id);
                    table.UniqueConstraint("AK_principal_order_source_bindings_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_po_principal_binding", "schema_version=2 AND quantity>0 AND unit_estimate>=0 AND proposed_unit_price>=0 AND estimated_value=quantity*proposed_unit_price AND request_version>=submitted_business_version AND submitted_business_version>0 AND method_revision>0 AND buyer_principal_id IS NOT NULL AND buyer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_principal_id <> '00000000-0000-0000-0000-000000000000' AND requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_worker_alias_id IS NULL OR requester_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_budget_check_snapshots_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.finance_snapshot_id },
                        principalSchema: "procurement",
                        principalTable: "budget_check_snapshots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_procurement_methods_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.method_id },
                        principalSchema: "procurement",
                        principalTable: "procurement_methods",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_purchase_order_lines_tenant~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_purchase_orders_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_purchase_request_allocation~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.allocation_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_allocations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_purchase_request_approval_o~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approval_outcome_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_outcomes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_purchase_request_approval_s~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_source_bindings_purchase_request_lines_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id, x.purchase_request_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_manual_contacts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reported_contact_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_manual_contacts", x => x.id);
                    table.UniqueConstraint("AK_sourcing_manual_contacts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_manual_contact_v2", "schema_version=2 AND channel IN ('EmailOutsideSystem','Physical','Telephone','Other') AND reference IS NOT NULL AND length(btrim(reference)) BETWEEN 1 AND 200 AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(reported_contact_at) AND isfinite(recorded_at) AND reported_contact_at <= recorded_at");
                    table.ForeignKey(
                        name: "FK_sourcing_manual_contacts_sourcing_events_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_manual_contacts_sourcing_invitations_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.invitation_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_invitations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_manual_contacts_suppliers_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_manual_quote_versions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bid_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    bid_version = table.Column<long>(type: "bigint", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_sourcing_manual_quote_versions", x => x.id);
                    table.UniqueConstraint("AK_sourcing_manual_quote_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_manual_quote_version_v2", "schema_version=2 AND operation IN ('Create','Revise','Cancel') AND revision>0 AND bid_version>0 AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND command_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(recorded_at)");
                    table.ForeignKey(
                        name: "FK_sourcing_manual_quote_versions_sourcing_events_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_manual_quote_versions_supplier_bids_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bid_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_bids",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_publication_openings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_version_at_opening = table.Column<long>(type: "bigint", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    published_policy_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_draft_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_publication_openings", x => x.id);
                    table.UniqueConstraint("AK_sourcing_publication_openings_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_publication_opening_v2", "schema_version=2 AND event_version_at_opening>0 AND policy_version_number>0 AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND published_policy_evidence_id <> '00000000-0000-0000-0000-000000000000'::uuid AND maker_principal_id IS NOT NULL AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (maker_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(opened_at)");
                    table.ForeignKey(
                        name: "FK_sourcing_publication_openings_policy_versions_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_openings_sourcing_events_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_openings_sourcing_policy_identity_evid~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.published_policy_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_release_requests",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    requested_order_version = table.Column<long>(type: "bigint", nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    requested_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_order_release_requests", x => x.id);
                    table.UniqueConstraint("AK_principal_order_release_requests_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_release_request", "schema_version=2 AND ((original_status='Draft' AND commercial_revision=0) OR (original_status IN ('Returned','Rejected') AND commercial_revision=1)) AND requested_order_version>0 AND length(btrim(reason))>0 AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND requested_by_principal_id IS NOT NULL AND requested_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND requested_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (requested_by_worker_alias_id IS NULL OR requested_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_release_requests_principal_order_funding_ev~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_release_requests_purchase_orders_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    submitted_row_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_decision_json = table.Column<string>(type: "text", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    buyer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    line_base_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_principal_order_submissions", x => x.id);
                    table.UniqueConstraint("AK_principal_order_submissions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_submission", "schema_version=2 AND commercial_revision=1 AND submitted_row_version>1 AND policy_version_number>0 AND line_base_amount>0 AND gross_amount>=line_base_amount AND currency_code ~ '^[A-Z]{3}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND buyer_principal_id IS NOT NULL AND buyer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_submissions_policy_versions_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_submissions_principal_order_funding_evidenc~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_submissions_purchase_orders_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_publication_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    case_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_version_at_decision = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_sourcing_publication_decisions", x => x.id);
                    table.UniqueConstraint("AK_sourcing_publication_decisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_publication_decision_v2", "schema_version=2 AND action IN ('Approve','Return','Reject') AND case_state IN ('Pending','Approved','Returned','Rejected') AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_approval_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND event_version_at_decision>0 AND isfinite(decided_at)");
                    table.ForeignKey(
                        name: "FK_sourcing_publication_decisions_sourcing_events_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_decisions_sourcing_publication_opening~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_release_confirmations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_release_evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approved_manual_policy_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    restored_source_reservations_json = table.Column<string>(type: "text", nullable: false),
                    released_gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    confirmed_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    confirmed_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_order_release_confirmations", x => x.id);
                    table.UniqueConstraint("AK_principal_order_release_confirmations_tenant_id_legal_entit~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_release_confirmation", "schema_version=2 AND released_gross_amount>=0 AND currency_code ~ '^[A-Z]{3}$' AND length(btrim(manual_commitment_reference))>0 AND length(btrim(external_release_evidence_reference))>0 AND length(btrim(approved_manual_policy_reference))>0 AND confirmed_by_principal_id IS NOT NULL AND confirmed_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND confirmed_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (confirmed_by_worker_alias_id IS NULL OR confirmed_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_release_confirmations_principal_order_fundi~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_release_confirmations_principal_order_relea~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.release_request_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_release_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_release_confirmations_purchase_orders_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_assignments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_revision = table.Column<long>(type: "bigint", nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_order_assignments", x => x.id);
                    table.UniqueConstraint("AK_principal_order_assignments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_assignment", "schema_version=2 AND grant_revision>0 AND authority_version>0 AND authority_evidence_sha256 ~ '^[0-9a-f]{64}$' AND principal_id IS NOT NULL AND principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND principal_id <> '00000000-0000-0000-0000-000000000000' AND (worker_alias_id IS NULL OR worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_assignments_principal_order_submissions_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_assignments_spending_authority_grants_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_principal_order_decisions", x => x.id);
                    table.UniqueConstraint("AK_principal_order_decisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_decision", "schema_version=2 AND action IN ('Approve','Return','Reject') AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (action='Approve' OR (reason IS NOT NULL AND length(btrim(reason))>0)) AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_decisions_principal_order_submissions_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_issued_versions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manual_commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    authorized_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    authorized_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_order_issued_versions", x => x.id);
                    table.UniqueConstraint("AK_principal_order_issued_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_issued", "schema_version=2 AND commercial_revision=1 AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND length(btrim(manual_commitment_reference))>0 AND authorized_by_principal_id IS NOT NULL AND authorized_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND authorized_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (authorized_by_worker_alias_id IS NULL OR authorized_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_issued_versions_principal_order_funding_evi~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_issued_versions_principal_order_submissions~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_issued_versions_purchase_orders_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_publication_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_sourcing_publication_receipts", x => x.id);
                    table.UniqueConstraint("AK_sourcing_publication_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_publication_receipt_v2", "schema_version=2 AND operation IN ('Open','Decide') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND isfinite(completed_at) AND ((operation='Open' AND decision_id IS NULL AND response_status_code=201) OR (operation='Decide' AND decision_id IS NOT NULL AND response_status_code=200))");
                    table.ForeignKey(
                        name: "FK_sourcing_publication_receipts_sourcing_events_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_receipts_sourcing_publication_decision~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.decision_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_receipts_sourcing_publication_openings~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_acknowledgments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    channel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    supplier_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recorded_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    recorded_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_principal_order_acknowledgments", x => x.id);
                    table.UniqueConstraint("AK_principal_order_acknowledgments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_acknowledgment", "schema_version=2 AND outcome IN ('Accepted','Rejected','Unknown') AND length(btrim(channel))>0 AND length(btrim(supplier_reference))>0 AND length(btrim(evidence_reference))>0 AND recorded_by_principal_id IS NOT NULL AND recorded_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND recorded_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (recorded_by_worker_alias_id IS NULL OR recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_acknowledgments_principal_order_issued_vers~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_acknowledgments_purchase_orders_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_receiving_drafts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    creator_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    creator_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_principal_receiving_drafts", x => x.id);
                    table.UniqueConstraint("ak_prcv_draft_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_prcv_draft", "schema_version=2 AND revision>0 AND kind IN ('Goods','Service') AND state IN ('Draft','Recorded','Certified','Cancelled') AND (kind='Goods' OR state<>'Recorded') AND (kind='Service' OR state<>'Certified') AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND creator_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND creator_principal_id<>'00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_prcv_draft_issued",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_draft_order",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_receiving_sources",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acknowledgment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_binding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    purchase_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    source_json = table.Column<string>(type: "text", nullable: false),
                    source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    accepted_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    accepted_net_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
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
                    table.PrimaryKey("PK_principal_receiving_sources", x => x.id);
                    table.UniqueConstraint("ak_prcv_source_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_prcv_source", "schema_version=2 AND kind IN ('Goods','Service') AND length(category_code)>0 AND length(purchase_type)>0 AND currency_code ~ '^[A-Z]{3}$' AND original_source_sha256 ~ '^[0-9a-f]{64}$' AND issued_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND sequence>=0 AND accepted_quantity>=0 AND accepted_net_amount>=0 AND source_sha256 ~ '^[0-9a-f]{64}$' AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "fk_prcv_source_ack",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.acknowledgment_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_acknowledgments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_source_funding",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_source_issued",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_source_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_source_order",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_source_original",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_binding_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_source_bindings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_source_request_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_source_submission",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_receiving_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    result_revision = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_principal_receiving_receipts", x => x.id);
                    table.UniqueConstraint("ak_prcv_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_prcv_receipt", "schema_version=2 AND operation IN ('Create','Revise','Cancel','Record','Inspect','Accept','Certify') AND response_status IN (200,201) AND result_revision>0 AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "fk_prcv_receipt_draft",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_receiving_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_principal_receiving_revisions", x => x.id);
                    table.UniqueConstraint("ak_prcv_revision_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_prcv_revision", "schema_version=2 AND revision>0 AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "fk_prcv_revision_draft",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_receiving_line_bindings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    rejected_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_principal_receiving_line_bindings", x => x.id);
                    table.UniqueConstraint("ak_prcv_line_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_prcv_line", "schema_version=2 AND quantity>0 AND rejected_quantity>=0 AND rejected_quantity<=quantity AND line_id<>'00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "fk_prcv_line_draft",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_line_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_revision_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_line_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_state_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_receiving_facts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    source_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_line_binding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
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
                    table.PrimaryKey("PK_principal_receiving_facts", x => x.id);
                    table.UniqueConstraint("ak_prcv_fact_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_prcv_fact", "schema_version=2 AND sequence>0 AND kind IN ('Observation','Inspection','Acceptance') AND ((kind='Observation' AND operation='Record') OR (kind='Inspection' AND operation='Inspect') OR (kind='Acceptance' AND operation IN ('Accept','Certify'))) AND quantity>=0 AND net_amount>=0 AND (kind='Inspection' OR quantity>0) AND (kind='Acceptance' OR net_amount=0) AND payload_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "fk_prcv_fact_draft",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_fact_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_line_binding_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_line_bindings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_fact_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prcv_fact_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_state_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_record_version_at_action>0 AND actor_principal_id IS NOT NULL AND ((schema_version=1 AND actor_worker_id IS NOT NULL AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0) OR (schema_version=2 AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')) AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(action_at) AND ((stage='Created' AND policy_record_version_at_action=1 AND created_evidence_id IS NULL AND created_evidence_stage IS NULL AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Published' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NOT NULL AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1,2) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')) OR (execution_version=2 AND source_evidence_id IS NOT NULL AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','PublicationReview','Published','Cancelled')))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders",
                sql: "(principal_draft_schema_version=0 AND original_direct_source_sha256 IS NULL) OR (principal_draft_schema_version=2 AND branch_ref IS NOT NULL AND buyer_subject_id IS NOT NULL AND buyer_subject_id IS NOT NULL AND buyer_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_subject_id <> '00000000-0000-0000-0000-000000000000' AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND finance_commitment_ref IS NULL AND ((status IN ('Draft','Cancelled','ReleasePending') AND approval_instance_ref IS NULL AND submitted_at IS NULL AND current_revision=0) OR (status IN ('PendingApproval','Approved','Returned','Rejected','ReleasePending','Cancelled','IssuePending','Issued','Disputed') AND approval_instance_ref IS NOT NULL AND submitted_at IS NOT NULL AND current_revision=1)) AND ((status='Issued' AND issued_at IS NOT NULL) OR (status<>'Issued' AND issued_at IS NULL)) AND ((status='Cancelled' AND closed_at IS NOT NULL) OR (status<>'Cancelled' AND closed_at IS NULL)))");

            migrationBuilder.CreateIndex(
                name: "ux_prcv_po_line_parent",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "purchase_request_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_acknowledgments_tenant_id_legal_entity_id_i~",
                schema: "procurement",
                table: "principal_order_acknowledgments",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_acknowledgments_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_order_acknowledgments",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_ack_parent",
                schema: "procurement",
                table: "principal_order_acknowledgments",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "issued_version_id", "purchase_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_assignments_tenant_id_legal_entity_id_core_~",
                schema: "procurement",
                table: "principal_order_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_assignments_tenant_id_legal_entity_id_grant~",
                schema: "procurement",
                table: "principal_order_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_assignments_tenant_id_legal_entity_id_submi~",
                schema: "procurement",
                table: "principal_order_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_decisions_tenant_id_legal_entity_id_core_de~",
                schema: "procurement",
                table: "principal_order_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_decisions_tenant_id_legal_entity_id_submiss~",
                schema: "procurement",
                table: "principal_order_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_draft_receipts_tenant_id_legal_entity_id_op~",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_draft_receipts_tenant_id_legal_entity_id_pu~",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_funding_evidence_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "principal_order_funding_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_issued_versions_tenant_id_legal_entity_id_f~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_issued_versions_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_issued_versions_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_issued_order",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_issued_parent",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "submission_id", "funding_evidence_id", "commercial_snapshot_sha256", "original_direct_source_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_confirmations_tenant_id_legal_enti~1",
                schema: "procurement",
                table: "principal_order_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_confirmations_tenant_id_legal_enti~2",
                schema: "procurement",
                table: "principal_order_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "release_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_confirmations_tenant_id_legal_entit~",
                schema: "procurement",
                table: "principal_order_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_requests_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "principal_order_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_requests_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "principal_order_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_~1",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "approval_outcome_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_~2",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "purchase_request_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_~3",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "purchase_request_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_a~",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "allocation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_f~",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_m~",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "method_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_source_bindings_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_original_parent",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "purchase_order_line_id", "purchase_request_line_id", "branch_id", "original_direct_source_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_core_~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_fundi~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_polic~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_purch~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_drafts_tenant_id_legal_entity_id_issued~",
                schema: "procurement",
                table: "principal_receiving_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_drafts_tenant_id_legal_entity_id_purcha~",
                schema: "procurement",
                table: "principal_receiving_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_draft_branch",
                schema: "procurement",
                table: "principal_receiving_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_draft_parent",
                schema: "procurement",
                table: "principal_receiving_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "branch_id", "purchase_order_id", "issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_facts_tenant_id_legal_entity_id_draft_id",
                schema: "procurement",
                table: "principal_receiving_facts",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_facts_tenant_id_legal_entity_id_draft_l~",
                schema: "procurement",
                table: "principal_receiving_facts",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_line_binding_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_facts_tenant_id_legal_entity_id_policy_~",
                schema: "procurement",
                table: "principal_receiving_facts",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_fact_command",
                schema: "procurement",
                table: "principal_receiving_facts",
                columns: new[] { "tenant_id", "legal_entity_id", "source_state_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_fact_sequence",
                schema: "procurement",
                table: "principal_receiving_facts",
                columns: new[] { "tenant_id", "legal_entity_id", "source_state_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_line_bindings_tenant_id_legal_entity_i~1",
                schema: "procurement",
                table: "principal_receiving_line_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "source_state_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_line_bindings_tenant_id_legal_entity_id~",
                schema: "procurement",
                table: "principal_receiving_line_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_line_parent",
                schema: "procurement",
                table: "principal_receiving_line_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_id", "draft_id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_revision_line",
                schema: "procurement",
                table: "principal_receiving_line_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_revision_id", "line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_revision_source",
                schema: "procurement",
                table: "principal_receiving_line_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_revision_id", "source_state_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_receipts_tenant_id_legal_entity_id_draf~",
                schema: "procurement",
                table: "principal_receiving_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_receipt_command",
                schema: "procurement",
                table: "principal_receiving_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_receipt_fact_parent",
                schema: "procurement",
                table: "principal_receiving_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key", "draft_id", "actor_principal_id", "branch_id", "operation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_draft_revision",
                schema: "procurement",
                table: "principal_receiving_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_revision_parent",
                schema: "procurement",
                table: "principal_receiving_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_sources_tenant_id_legal_entity_id_ackno~",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "acknowledgment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_sources_tenant_id_legal_entity_id_fundi~",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_sources_tenant_id_legal_entity_id_purc~1",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_sources_tenant_id_legal_entity_id_purc~2",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_sources_tenant_id_legal_entity_id_purch~",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_sources_tenant_id_legal_entity_id_sourc~",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "source_binding_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_receiving_sources_tenant_id_legal_entity_id_submi~",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "ux_prcv_source_line",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_source_parent",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "branch_id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_contacts_tenant_id_legal_entity_id_command_~",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_contacts_tenant_id_legal_entity_id_invitati~",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "invitation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_contacts_tenant_id_legal_entity_id_sourcing~",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_contacts_tenant_id_legal_entity_id_supplier~",
                schema: "procurement",
                table: "sourcing_manual_contacts",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_quote_versions_tenant_id_legal_entity_id_co~",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_quote_versions_tenant_id_legal_entity_id_so~",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_manual_quote_versions_tenant_id_legal_entity_id_su~",
                schema: "procurement",
                table: "sourcing_manual_quote_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_co~",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_op~",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_so~",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_cor~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_pol~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_pub~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "published_policy_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_sou~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "event_version_at_opening" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_com~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_dec~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "decision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_ope~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_sou~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });
            migrationBuilder.Sql("""
                ALTER TABLE procurement.principal_receiving_facts ADD CONSTRAINT fk_prcv_exact_fact_receipt
                  FOREIGN KEY (tenant_id,legal_entity_id,command_key,draft_id,actor_principal_id,branch_id,operation)
                  REFERENCES procurement.principal_receiving_receipts (tenant_id,legal_entity_id,command_key,draft_id,actor_principal_id,branch_id,operation)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_issued
                  FOREIGN KEY (tenant_id,legal_entity_id,issued_version_id,purchase_order_id,submission_id,funding_evidence_id,issued_snapshot_sha256,original_source_sha256)
                  REFERENCES procurement.principal_order_issued_versions (tenant_id,legal_entity_id,id,purchase_order_id,submission_id,funding_evidence_id,commercial_snapshot_sha256,original_direct_source_sha256)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_ack
                  FOREIGN KEY (tenant_id,legal_entity_id,acknowledgment_id,issued_version_id,purchase_order_id)
                  REFERENCES procurement.principal_order_acknowledgments (tenant_id,legal_entity_id,id,issued_version_id,purchase_order_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_original
                  FOREIGN KEY (tenant_id,legal_entity_id,source_binding_id,purchase_order_id,purchase_order_line_id,purchase_request_line_id,branch_id,original_source_sha256)
                  REFERENCES procurement.principal_order_source_bindings (tenant_id,legal_entity_id,id,purchase_order_id,purchase_order_line_id,purchase_request_line_id,branch_id,original_direct_source_sha256)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_sources ADD CONSTRAINT fk_prcv_exact_po_line
                  FOREIGN KEY (tenant_id,legal_entity_id,purchase_order_line_id,purchase_order_id,purchase_request_line_id)
                  REFERENCES procurement.purchase_order_lines (tenant_id,legal_entity_id,id,purchase_order_id,purchase_request_line_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_drafts ADD CONSTRAINT fk_prcv_exact_draft_issued
                  FOREIGN KEY (tenant_id,legal_entity_id,issued_version_id,purchase_order_id)
                  REFERENCES procurement.principal_order_issued_versions (tenant_id,legal_entity_id,id,purchase_order_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_line_bindings ADD CONSTRAINT fk_prcv_exact_line_source
                  FOREIGN KEY (tenant_id,legal_entity_id,source_state_id,branch_id,purchase_order_id,issued_version_id,purchase_order_line_id)
                  REFERENCES procurement.principal_receiving_sources (tenant_id,legal_entity_id,id,branch_id,purchase_order_id,issued_version_id,purchase_order_line_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_line_bindings ADD CONSTRAINT fk_prcv_exact_line_revision
                  FOREIGN KEY (tenant_id,legal_entity_id,draft_revision_id,draft_id)
                  REFERENCES procurement.principal_receiving_revisions (tenant_id,legal_entity_id,id,draft_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_facts ADD CONSTRAINT fk_prcv_exact_fact_line
                  FOREIGN KEY (tenant_id,legal_entity_id,draft_line_binding_id,source_state_id,draft_id,branch_id)
                  REFERENCES procurement.principal_receiving_line_bindings (tenant_id,legal_entity_id,id,source_state_id,draft_id,branch_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_receipts ADD CONSTRAINT fk_prcv_exact_receipt_draft
                  FOREIGN KEY (tenant_id,legal_entity_id,draft_id,branch_id)
                  REFERENCES procurement.principal_receiving_drafts (tenant_id,legal_entity_id,id,branch_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.principal_receiving_line_bindings ADD CONSTRAINT fk_prcv_exact_line_draft
                  FOREIGN KEY (tenant_id,legal_entity_id,draft_id,branch_id,purchase_order_id,issued_version_id)
                  REFERENCES procurement.principal_receiving_drafts (tenant_id,legal_entity_id,id,branch_id,purchase_order_id,issued_version_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE procurement.purchase_orders,
                  procurement.sourcing_events,
                  procurement.supplier_bids,
                  procurement.policy_versions,
                  procurement.sourcing_policy_identity_evidence,
                  procurement.principal_order_acknowledgments,
                  procurement.principal_order_assignments,
                  procurement.principal_order_decisions,
                  procurement.principal_order_draft_receipts,
                  procurement.principal_order_funding_evidence,
                  procurement.principal_order_issued_versions,
                  procurement.principal_order_release_confirmations,
                  procurement.principal_order_release_requests,
                  procurement.principal_order_source_bindings,
                  procurement.principal_order_submissions,
                  procurement.principal_receiving_drafts,
                  procurement.principal_receiving_facts,
                  procurement.principal_receiving_line_bindings,
                  procurement.principal_receiving_receipts,
                  procurement.principal_receiving_revisions,
                  procurement.principal_receiving_sources,
                  procurement.sourcing_manual_contacts,
                  procurement.sourcing_manual_quote_versions,
                  procurement.sourcing_publication_decisions,
                  procurement.sourcing_publication_openings,
                  procurement.sourcing_publication_receipts IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM procurement.principal_order_acknowledgments)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_assignments)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_decisions)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_draft_receipts)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_funding_evidence)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_issued_versions)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_release_confirmations)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_release_requests)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_source_bindings)
                    OR EXISTS (SELECT 1 FROM procurement.principal_order_submissions)
                    OR EXISTS (SELECT 1 FROM procurement.principal_receiving_drafts)
                    OR EXISTS (SELECT 1 FROM procurement.principal_receiving_facts)
                    OR EXISTS (SELECT 1 FROM procurement.principal_receiving_line_bindings)
                    OR EXISTS (SELECT 1 FROM procurement.principal_receiving_receipts)
                    OR EXISTS (SELECT 1 FROM procurement.principal_receiving_revisions)
                    OR EXISTS (SELECT 1 FROM procurement.principal_receiving_sources)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_manual_contacts)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_manual_quote_versions)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_decisions)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_openings)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_publication_receipts)
                    OR EXISTS (SELECT 1 FROM procurement.purchase_orders WHERE principal_draft_schema_version <> 0 OR original_direct_source_sha256 IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_events WHERE execution_version = 2 AND status NOT IN ('Draft','Cancelled'))
                    OR EXISTS (SELECT 1 FROM procurement.supplier_bids WHERE draft_revision <> 0)
                    OR EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence WHERE schema_version <> 1 OR actor_worker_id IS NULL)
                    OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE policy_key IN ('RECEIVING','SOURCING') AND CASE WHEN pg_input_is_valid(rules_json, 'jsonb') THEN rules_json::jsonb->>'schemaVersion' = '2' ELSE false END) THEN
                    RAISE EXCEPTION 'P30 principal commercial evidence prevents downgrade' USING ERRCODE = '55000';
                  END IF;
                END $$;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE procurement.principal_receiving_facts DROP CONSTRAINT fk_prcv_exact_fact_receipt;
                ALTER TABLE procurement.principal_receiving_line_bindings DROP CONSTRAINT fk_prcv_exact_line_draft;
                ALTER TABLE procurement.principal_receiving_receipts DROP CONSTRAINT fk_prcv_exact_receipt_draft;
                ALTER TABLE procurement.principal_receiving_facts DROP CONSTRAINT fk_prcv_exact_fact_line;
                ALTER TABLE procurement.principal_receiving_line_bindings DROP CONSTRAINT fk_prcv_exact_line_revision;
                ALTER TABLE procurement.principal_receiving_line_bindings DROP CONSTRAINT fk_prcv_exact_line_source;
                ALTER TABLE procurement.principal_receiving_drafts DROP CONSTRAINT fk_prcv_exact_draft_issued;
                ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_po_line;
                ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_original;
                ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_ack;
                ALTER TABLE procurement.principal_receiving_sources DROP CONSTRAINT fk_prcv_exact_issued;
                """);
            migrationBuilder.DropTable(
                name: "principal_order_assignments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_draft_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_release_confirmations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_facts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_manual_contacts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_manual_quote_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_publication_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_release_requests",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_line_bindings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_publication_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_sources",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_publication_openings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_drafts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_acknowledgments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_source_bindings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_issued_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_submissions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_funding_evidence",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "ux_prcv_po_line_parent",
                schema: "procurement",
                table: "purchase_order_lines");

            migrationBuilder.DropColumn(
                name: "draft_revision",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropColumn(
                name: "original_direct_source_sha256",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "principal_draft_schema_version",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                sql: "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_record_version_at_action>0 AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0 AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(action_at) AND ((stage='Created' AND policy_record_version_at_action=1 AND created_evidence_id IS NULL AND created_evidence_stage IS NULL AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Published' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NOT NULL AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1,2) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')) OR (execution_version=2 AND source_evidence_id IS NOT NULL AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')))");
        }
    }
}
