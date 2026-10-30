using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6bPrincipalInvoiceReceivingBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_match_allocation",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.AddColumn<Guid>(
                name: "principal_receiving_fact_id",
                schema: "procurement",
                table: "invoice_match_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "principal_receiving_source_state_id",
                schema: "procurement",
                table: "invoice_match_allocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~",
                schema: "procurement",
                table: "principal_receiving_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~",
                schema: "procurement",
                table: "principal_receiving_facts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_princi~1",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_receiving_source_state_id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_princip~",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_receiving_fact_id", "principal_receiving_source_state_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_match_allocation",
                schema: "procurement",
                table: "invoice_match_allocations",
                sql: "kind = 'Debit' AND original_debit_id IS NULL AND quantity > 0 AND net_amount >= 0 AND tax_amount >= 0 AND exposure_sequence > 0 AND ((principal_receiving_source_state_id IS NULL AND principal_receiving_fact_id IS NULL AND (goods_receipt_line_id IS NULL OR service_acceptance_id IS NULL) AND ((goods_receipt_line_id IS NULL AND service_acceptance_id IS NULL AND receiving_source_state_id IS NULL) OR (receiving_source_state_id IS NOT NULL AND (goods_receipt_line_id IS NOT NULL OR service_acceptance_id IS NOT NULL)))) OR (principal_receiving_source_state_id IS NOT NULL AND principal_receiving_source_state_id <> '00000000-0000-0000-0000-000000000000'::uuid AND principal_receiving_fact_id IS NOT NULL AND principal_receiving_fact_id <> '00000000-0000-0000-0000-000000000000'::uuid AND receiving_source_state_id IS NULL AND goods_receipt_line_id IS NULL AND service_acceptance_id IS NULL))");

            migrationBuilder.AddForeignKey(
                name: "fk_invoice_allocation_principal_fact_source",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_receiving_fact_id", "principal_receiving_source_state_id" },
                principalSchema: "procurement",
                principalTable: "principal_receiving_facts",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoice_allocation_principal_source_line",
                schema: "procurement",
                table: "invoice_match_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_receiving_source_state_id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                principalSchema: "procurement",
                principalTable: "principal_receiving_sources",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(M6bPrincipalInvoiceRollbackGuards.BeforeDownSql);

            migrationBuilder.DropForeignKey(
                name: "fk_invoice_allocation_principal_fact_source",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_invoice_allocation_principal_source_line",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~",
                schema: "procurement",
                table: "principal_receiving_sources");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~",
                schema: "procurement",
                table: "principal_receiving_facts");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_princi~1",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_allocations_tenant_id_legal_entity_id_princip~",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_match_allocation",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.DropColumn(
                name: "principal_receiving_fact_id",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.DropColumn(
                name: "principal_receiving_source_state_id",
                schema: "procurement",
                table: "invoice_match_allocations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_match_allocation",
                schema: "procurement",
                table: "invoice_match_allocations",
                sql: "kind = 'Debit' AND original_debit_id IS NULL AND quantity > 0 AND net_amount >= 0 AND tax_amount >= 0 AND exposure_sequence > 0 AND (goods_receipt_line_id IS NULL OR service_acceptance_id IS NULL) AND ((goods_receipt_line_id IS NULL AND service_acceptance_id IS NULL AND receiving_source_state_id IS NULL) OR (receiving_source_state_id IS NOT NULL AND (goods_receipt_line_id IS NOT NULL OR service_acceptance_id IS NOT NULL)))");
        }
    }
}
