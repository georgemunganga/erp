using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6dInvoiceIntakeReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_intake_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculation_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_invoice_intake_command_receipts", x => x.id);
                    table.UniqueConstraint("AK_invoice_intake_command_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_intake_receipt_v2", "operation='InvoiceCaptureV2' AND version=1 AND response_status=201 AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND calculation_id <> '00000000-0000-0000-0000-000000000000'::uuid AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(response_json) BETWEEN 1 AND 262144 AND isfinite(completed_at_utc) AND created_at>=completed_at_utc AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "FK_invoice_intake_command_receipts_invoice_amount_calculations~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.calculation_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_amount_calculations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_intake_command_receipts_supplier_invoices_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_intake_command_receipts_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "invoice_intake_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_intake_receipt_actor_key",
                schema: "procurement",
                table: "invoice_intake_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_invoice_intake_receipt_calculation",
                schema: "procurement",
                table: "invoice_intake_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "calculation_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE procurement.invoice_intake_command_receipts,
                  procurement.invoice_amount_calculations,
                  procurement.audit_events,
                  procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $m6d_invoice_capture_receipt_down$
                BEGIN
                  IF EXISTS (SELECT 1 FROM procurement.invoice_intake_command_receipts)
                    OR EXISTS (SELECT 1 FROM procurement.invoice_amount_calculations)
                    OR EXISTS (SELECT 1 FROM procurement.audit_events
                               WHERE record_type='InvoiceIntakeCommandReceipt'
                                  OR action='InvoiceCaptureV2'
                                  OR record_type='InvoiceAmountCalculation'
                                  OR action='InvoiceAmountCalculationCaptured')
                    OR EXISTS (SELECT 1 FROM procurement.outbox_events
                               WHERE event_type IN ('supplier_invoice.captured.v2',
                                 'supplier_invoice.amount_calculated.v2'))
                  THEN
                    RAISE EXCEPTION USING ERRCODE='55000',
                      MESSAGE='Retained invoice capture or amount evidence prevents downgrade; use compatible code or fix forward.';
                  END IF;
                END
                $m6d_invoice_capture_receipt_down$;
                """);

            migrationBuilder.DropTable(
                name: "invoice_intake_command_receipts",
                schema: "procurement");
        }
    }
}
