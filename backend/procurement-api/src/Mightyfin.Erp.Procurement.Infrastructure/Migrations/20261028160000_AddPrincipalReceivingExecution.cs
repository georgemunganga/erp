using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrincipalReceivingExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "ux_prcv_po_line_parent",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "purchase_request_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_prcv_original_parent",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "purchase_order_line_id", "purchase_request_line_id", "branch_id", "original_direct_source_sha256" },
                unique: true);

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
                name: "ux_prcv_ack_parent",
                schema: "procurement",
                table: "principal_order_acknowledgments",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "issued_version_id", "purchase_order_id" },
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
            migrationBuilder.Sql(PrincipalReceivingOwnershipConstraintSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(M5bPrincipalReceivingGuards.BeforeDownSql);
            migrationBuilder.Sql(PrincipalReceivingOwnershipConstraintSql.Remove);
            migrationBuilder.DropTable(
                name: "principal_receiving_facts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_line_bindings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_sources",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_receiving_drafts",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "ux_prcv_po_line_parent",
                schema: "procurement",
                table: "purchase_order_lines");

            migrationBuilder.DropIndex(
                name: "ux_prcv_original_parent",
                schema: "procurement",
                table: "principal_order_source_bindings");

            migrationBuilder.DropIndex(
                name: "ux_prcv_issued_order",
                schema: "procurement",
                table: "principal_order_issued_versions");

            migrationBuilder.DropIndex(
                name: "ux_prcv_issued_parent",
                schema: "procurement",
                table: "principal_order_issued_versions");

            migrationBuilder.DropIndex(
                name: "ux_prcv_ack_parent",
                schema: "procurement",
                table: "principal_order_acknowledgments");
        }
    }
}
