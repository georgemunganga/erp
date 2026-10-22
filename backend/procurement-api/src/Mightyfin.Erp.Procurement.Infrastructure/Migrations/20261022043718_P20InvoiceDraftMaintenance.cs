using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P20InvoiceDraftMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_ref",
                schema: "procurement",
                table: "supplier_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "procurement",
                table: "supplier_invoice_lines",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE procurement.supplier_invoices,
                  procurement.supplier_invoice_lines IN ACCESS EXCLUSIVE MODE;
                DO $invoice_crud_down$
                BEGIN
                  IF EXISTS (SELECT 1 FROM procurement.supplier_invoices WHERE branch_ref IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM procurement.supplier_invoice_lines WHERE NOT is_active)
                  THEN
                    RAISE EXCEPTION USING ERRCODE='55000',
                      MESSAGE='Retained invoice branch or removed-line history prevents destructive downgrade.';
                  END IF;
                END
                $invoice_crud_down$;
                """);
            migrationBuilder.DropColumn(
                name: "branch_ref",
                schema: "procurement",
                table: "supplier_invoices");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "procurement",
                table: "supplier_invoice_lines");
        }
    }
}
