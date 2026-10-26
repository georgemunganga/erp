using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M3bPrincipalDirectOrderDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
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
                    table.CheckConstraint("ck_po_principal_receipt", "schema_version=2 AND operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND ((operation='Create' AND response_status=201) OR (operation='Cancel' AND response_status=200))");
                    table.ForeignKey(
                        name: "FK_principal_order_draft_receipts_purchase_orders_tenant_id_le~",
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

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders",
                sql: "(principal_draft_schema_version=0 AND original_direct_source_sha256 IS NULL) OR (principal_draft_schema_version=2 AND branch_ref IS NOT NULL AND buyer_subject_id IS NOT NULL AND buyer_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_subject_id <> '00000000-0000-0000-0000-000000000000' AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled') AND finance_commitment_ref IS NULL AND approval_instance_ref IS NULL AND submitted_at IS NULL AND issued_at IS NULL)");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This migration adds retained principal source and command evidence.
            // Check it under locks before EF drops any column or table.
            migrationBuilder.Sql(M3bPrincipalOrderDraftGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "principal_order_draft_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_source_bindings",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "original_direct_source_sha256",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "principal_draft_schema_version",
                schema: "procurement",
                table: "purchase_orders");
        }
    }
}
