using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6dInvoiceAmountCalculations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_amount_calculations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    core_currency_reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_currency_reference_revision = table.Column<int>(type: "integer", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    minor_units = table.Column<int>(type: "integer", nullable: false),
                    rounding_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    core_source_payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    core_published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    invoice_original_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    calculation_json = table.Column<string>(type: "text", nullable: false),
                    calculation_policy_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    calculation_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_invoice_amount_calculations", x => x.id);
                    table.UniqueConstraint("AK_invoice_amount_calculations_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_amount_calculation", "schema_version=2 AND version=1 AND supplier_invoice_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_currency_reference_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_currency_reference_revision>0 AND currency_code ~ '^[A-Z]{3}$' AND minor_units BETWEEN 0 AND 4 AND rounding_mode IN ('ToEven','AwayFromZero') AND core_source_payload_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND calculation_policy_sha256 ~ '^[0-9a-f]{64}$' AND calculation_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(calculation_json) BETWEEN 1 AND 262144 AND isfinite(core_published_at_utc) AND isfinite(observed_at_utc) AND core_published_at_utc<=observed_at_utc AND created_at>=observed_at_utc AND updated_at IS NULL AND updated_by IS NULL");
                    table.ForeignKey(
                        name: "FK_invoice_amount_calculations_supplier_invoices_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_amount_calculation_invoice",
                schema: "procurement",
                table: "invoice_amount_calculations",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE procurement.invoice_amount_calculations,
                  procurement.audit_events,
                  procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $m6d_invoice_amount_down$
                BEGIN
                  IF EXISTS (SELECT 1 FROM procurement.invoice_amount_calculations)
                    OR EXISTS (SELECT 1 FROM procurement.audit_events
                               WHERE record_type='InvoiceAmountCalculation'
                                  OR action='InvoiceAmountCalculationCaptured')
                    OR EXISTS (SELECT 1 FROM procurement.outbox_events
                               WHERE event_type='supplier_invoice.amount_calculated.v2')
                  THEN
                    RAISE EXCEPTION USING ERRCODE='55000',
                      MESSAGE='Retained invoice amount calculation evidence prevents downgrade; use compatible code or fix forward.';
                  END IF;
                END
                $m6d_invoice_amount_down$;
                """);

            migrationBuilder.DropTable(
                name: "invoice_amount_calculations",
                schema: "procurement");
        }
    }
}
