using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P24CorePrincipalRecurringPurchasePlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_purchase_commitments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    work_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_cycle_on = table.Column<DateOnly>(type: "date", nullable: false),
                    last_cycle_on = table.Column<DateOnly>(type: "date", nullable: true),
                    next_cycle_on = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_recurring_purchase_commitments", x => x.id);
                    table.UniqueConstraint("AK_recurring_purchase_commitments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_recurring_purchase_schedule", "status IN ('Draft','Closed') AND frequency IN ('Weekly','Monthly','Quarterly','Yearly') AND expected_amount > 0 AND next_cycle_on >= first_cycle_on AND (last_cycle_on IS NULL OR (last_cycle_on >= first_cycle_on AND next_cycle_on <= last_cycle_on)) AND owner_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (owner_worker_id IS NULL OR owner_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND owner_subject_id = owner_principal_id::text AND ((status = 'Draft' AND closed_at IS NULL AND closed_reason IS NULL) OR (status = 'Closed' AND closed_at IS NOT NULL AND closed_reason IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_recurring_purchase_commitments_supplier_contracts_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_contract_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_contracts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_purchase_commitments_suppliers_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_purchase_commitments_tenant_id_legal_entity_id_cr~",
                schema: "procurement",
                table: "recurring_purchase_commitments",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_purchase_commitments_tenant_id_legal_entity_id_nu~",
                schema: "procurement",
                table: "recurring_purchase_commitments",
                columns: new[] { "tenant_id", "legal_entity_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_purchase_commitments_tenant_id_legal_entity_id_ow~",
                schema: "procurement",
                table: "recurring_purchase_commitments",
                columns: new[] { "tenant_id", "legal_entity_id", "owner_principal_id", "next_cycle_on" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_purchase_commitments_tenant_id_legal_entity_id_s~1",
                schema: "procurement",
                table: "recurring_purchase_commitments",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_purchase_commitments_tenant_id_legal_entity_id_su~",
                schema: "procurement",
                table: "recurring_purchase_commitments",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_contract_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retained plans include Closed history. These locks remain held by the
            // ordinary migration transaction until its guarded DDL completes.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.recurring_purchase_commitments,
                    procurement.audit_events, procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $p24_recurring_down$
                BEGIN
                    IF EXISTS(SELECT 1 FROM procurement.recurring_purchase_commitments)
                        OR EXISTS(SELECT 1 FROM procurement.audit_events
                            WHERE record_type = 'RecurringPurchaseCommitment')
                        OR EXISTS(SELECT 1 FROM procurement.outbox_events
                            WHERE aggregate_type = 'RecurringPurchaseCommitment')
                    THEN
                        RAISE EXCEPTION USING ERRCODE='55000',
                            MESSAGE='Retained recurring purchase planning evidence prevents destructive downgrade.';
                    END IF;
                END;
                $p24_recurring_down$;
                """);

            migrationBuilder.DropTable(
                name: "recurring_purchase_commitments",
                schema: "procurement");
        }
    }
}
