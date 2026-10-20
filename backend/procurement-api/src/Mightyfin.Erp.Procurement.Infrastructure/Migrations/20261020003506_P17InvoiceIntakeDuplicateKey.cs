using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P17InvoiceIntakeDuplicateKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_invoice_number",
                schema: "procurement",
                table: "supplier_invoices",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE procurement.supplier_invoices SET normalized_invoice_number = upper(btrim(invoice_number));");

            migrationBuilder.AlterColumn<string>(
                name: "normalized_invoice_number",
                schema: "procurement",
                table: "supplier_invoices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_supplier_id_nor~",
                schema: "procurement",
                table: "supplier_invoices",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "normalized_invoice_number", "currency_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_invoices_tenant_id_legal_entity_id_supplier_id_nor~",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "normalized_invoice_number",
                schema: "procurement",
                table: "supplier_invoices");
        }
    }
}
