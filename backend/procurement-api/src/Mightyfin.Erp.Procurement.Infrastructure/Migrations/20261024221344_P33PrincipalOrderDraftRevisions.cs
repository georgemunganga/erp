using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P33PrincipalOrderDraftRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_purchase_ord~",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_principal_order_source_bindings_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_order_source_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "id" });

            migrationBuilder.CreateTable(
                name: "principal_order_commercial_origins",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    captured_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capture_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_principal_order_commercial_origins", x => x.id);
                    table.UniqueConstraint("ak_po_draft_origin_owner", x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id, x.id });
                    table.UniqueConstraint("AK_principal_order_commercial_origins_tenant_id_legal_entity_i~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_po_draft_origin", "schema_version=1 AND capture_mode IS NOT NULL AND capture_mode IN ('Create','VerifiedFirstEdit') AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_json IS NOT NULL AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND captured_by_principal_id IS NOT NULL AND captured_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND captured_by_principal_id<>'00000000-0000-0000-0000-000000000000' AND captured_at IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_po_draft_origin_order",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_draft_commercial_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    previous_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_order_version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_draft_commercial_revisions", x => x.id);
                    table.UniqueConstraint("ak_po_draft_revision_owner", x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id, x.id });
                    table.UniqueConstraint("AK_principal_order_draft_commercial_revisions_tenant_id_legal_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_po_draft_revision", "schema_version=1 AND revision_number>0 AND result_order_version=revision_number+1 AND reason IS NOT NULL AND length(reason)>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND command_sha256 IS NOT NULL AND command_sha256 ~ '^[0-9a-f]{64}$' AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_json IS NOT NULL AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND ((revision_number=1 AND previous_revision_id IS NULL AND previous_snapshot_sha256 IS NULL) OR (revision_number>1 AND previous_revision_id IS NOT NULL AND previous_snapshot_sha256 IS NOT NULL AND previous_snapshot_sha256 ~ '^[0-9a-f]{64}$')) AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND recorded_at IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_po_draft_revision_order",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_po_draft_revision_origin_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id, x.origin_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_commercial_origins",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_po_draft_revision_previous_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id, x.previous_revision_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_draft_commercial_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_effective_source_slices",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_binding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estimated_value = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
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
                    table.PrimaryKey("PK_principal_order_effective_source_slices", x => x.id);
                    table.UniqueConstraint("AK_principal_order_effective_source_slices_tenant_id_legal_ent~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_po_draft_slice", "schema_version=1 AND quantity>0 AND unit_price>=0 AND tax_amount>=0 AND estimated_value=quantity*unit_price AND currency_code IS NOT NULL AND currency_code ~ '^[A-Z]{3}$'");
                    table.ForeignKey(
                        name: "fk_po_draft_slice_allocation",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.allocation_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_allocations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_po_draft_slice_binding_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id, x.source_binding_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_source_bindings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_po_draft_slice_line_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_po_draft_slice_order",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_po_draft_slice_revision_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id, x.revision_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_draft_commercial_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                sql: "schema_version=2 AND operation IN ('Create','Edit','Cancel','Funding','Submit','Decide','Issue','Ack','ReleaseRequest','ReleaseConfirm') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND ((operation IN ('Create','Submit','Issue') AND response_status=201) OR (operation IN ('Edit','Cancel','Funding','Decide','Ack','ReleaseRequest','ReleaseConfirm') AND response_status=200))");

            migrationBuilder.CreateIndex(
                name: "ux_po_draft_origin_order",
                schema: "procurement",
                table: "principal_order_commercial_origins",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_draft_commercial_revisions_tenant_id_legal_~",
                schema: "procurement",
                table: "principal_order_draft_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "origin_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_draft_commercial_revisions_tenant_id_legal~1",
                schema: "procurement",
                table: "principal_order_draft_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "previous_revision_id" });

            migrationBuilder.CreateIndex(
                name: "ux_po_draft_revision_command",
                schema: "procurement",
                table: "principal_order_draft_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_po_draft_revision_number",
                schema: "procurement",
                table: "principal_order_draft_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_effective_source_slices_tenant_id_legal_en~1",
                schema: "procurement",
                table: "principal_order_effective_source_slices",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_effective_source_slices_tenant_id_legal_en~2",
                schema: "procurement",
                table: "principal_order_effective_source_slices",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "source_binding_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_effective_source_slices_tenant_id_legal_ent~",
                schema: "procurement",
                table: "principal_order_effective_source_slices",
                columns: new[] { "tenant_id", "legal_entity_id", "allocation_id" });

            migrationBuilder.CreateIndex(
                name: "ux_po_draft_slice_binding",
                schema: "procurement",
                table: "principal_order_effective_source_slices",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "revision_id", "source_binding_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_po_draft_slice_line",
                schema: "procurement",
                table: "principal_order_effective_source_slices",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "revision_id", "purchase_order_line_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P33PrincipalOrderDraftRevisionGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "principal_order_effective_source_slices",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_draft_commercial_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_commercial_origins",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_purchase_ord~",
                schema: "procurement",
                table: "purchase_order_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_principal_order_source_bindings_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_order_source_bindings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                sql: "schema_version=2 AND operation IN ('Create','Cancel','Funding','Submit','Decide','Issue','Ack','ReleaseRequest','ReleaseConfirm') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND ((operation IN ('Create','Submit','Issue') AND response_status=201) OR (operation IN ('Cancel','Funding','Decide','Ack','ReleaseRequest','ReleaseConfirm') AND response_status=200))");
        }
    }
}
