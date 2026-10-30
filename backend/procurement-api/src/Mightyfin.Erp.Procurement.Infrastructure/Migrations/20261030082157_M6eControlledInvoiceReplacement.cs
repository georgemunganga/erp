using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6eControlledInvoiceReplacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_replacement_links",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_invoice_version = table.Column<long>(type: "bigint", nullable: false),
                    source_original_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_mapping_revision = table.Column<int>(type: "integer", nullable: false),
                    blocked_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                    no_claim_proof_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capture_command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_invoice_replacement_links", x => x.id);
                    table.UniqueConstraint("AK_invoice_replacement_links_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_replacement_link_v1", "version=1 AND source_invoice_id<>successor_invoice_id AND source_invoice_version>0 AND source_mapping_revision>0 AND source_original_sha256 ~ '^[0-9a-f]{64}$' AND no_claim_proof_sha256 ~ '^[0-9a-f]{64}$' AND currency_code ~ '^[A-Z]{3}$' AND octet_length(reason) BETWEEN 1 AND 1000 AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND capture_command_key <> command_key AND isfinite(recorded_at_utc) AND created_at>=recorded_at_utc AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_invoice_amount_calculations_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.calculation_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_amount_calculations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_invoice_intake_command_receipts_t~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.capture_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_intake_command_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_invoice_match_mapping_versions_te~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_mapping_version_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_match_mapping_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_invoice_match_results_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.blocked_result_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_match_results",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_invoice_unit_review_outcomes_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_outcome_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_unit_review_outcomes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_invoice_unit_review_sources_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_unit_review_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_supplier_invoices_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_links_supplier_invoices_tenant_id_lega~1",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.successor_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_replacement_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_invoice_replacement_command_receipts", x => x.id);
                    table.UniqueConstraint("AK_invoice_replacement_command_receipts_tenant_id_legal_entity~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_replacement_receipt_v1", "operation='InvoiceReplaceV2' AND version=1 AND response_status=201 AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(response_json) BETWEEN 1 AND 262144 AND isfinite(completed_at_utc) AND created_at>=completed_at_utc AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "FK_invoice_replacement_command_receipts_invoice_replacement_li~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.link_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_replacement_links",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_command_receipts_supplier_invoices_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_replacement_command_receipts_supplier_invoices_ten~1",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.successor_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_command_receipts_tenant_id_legal_entit~1",
                schema: "procurement",
                table: "invoice_replacement_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "successor_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_command_receipts_tenant_id_legal_entity~",
                schema: "procurement",
                table: "invoice_replacement_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "source_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_replacement_receipt_actor_key",
                schema: "procurement",
                table: "invoice_replacement_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_invoice_replacement_receipt_link",
                schema: "procurement",
                table: "invoice_replacement_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "link_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_links_tenant_id_legal_entity_id_blocked~",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "blocked_result_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_links_tenant_id_legal_entity_id_calcula~",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "calculation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_links_tenant_id_legal_entity_id_capture~",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "capture_receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_links_tenant_id_legal_entity_id_review_~",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "review_outcome_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_links_tenant_id_legal_entity_id_review~1",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_replacement_links_tenant_id_legal_entity_id_source_~",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "source_mapping_version_id" });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_replacement_source",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "source_invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_invoice_replacement_successor",
                schema: "procurement",
                table: "invoice_replacement_links",
                columns: new[] { "tenant_id", "legal_entity_id", "successor_invoice_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A populated correction is retained business evidence. Rollback
            // can remove this schema only before the first recorded correction.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.invoice_replacement_links,
                  procurement.invoice_replacement_command_receipts,
                  procurement.audit_events,
                  procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $m6e_invoice_replacement_down$
                BEGIN
                  IF EXISTS (SELECT 1 FROM procurement.invoice_replacement_links)
                    OR EXISTS (SELECT 1 FROM procurement.invoice_replacement_command_receipts)
                    OR EXISTS (SELECT 1 FROM procurement.audit_events
                               WHERE record_type IN ('InvoiceReplacementLink', 'InvoiceReplacementCommandReceipt')
                                  OR action='InvoiceReplacementRecorded')
                    OR EXISTS (SELECT 1 FROM procurement.outbox_events
                               WHERE event_type='supplier_invoice.replaced.v1')
                  THEN
                    RAISE EXCEPTION 'M6E invoice replacement evidence exists; retain this schema and use a forward correction.'
                      USING ERRCODE='55000';
                  END IF;
                END
                $m6e_invoice_replacement_down$;
                """);

            migrationBuilder.DropTable(
                name: "invoice_replacement_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_replacement_links",
                schema: "procurement");
        }
    }
}
