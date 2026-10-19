using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P11CatalogPriceProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "catalog_entry_id",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "catalog_published_version",
                schema: "procurement",
                table: "purchase_request_lines",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_request_lines_tenant_id_legal_entity_id_catalog_en~",
                schema: "procurement",
                table: "purchase_request_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_entry_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_line_catalog_ref",
                schema: "procurement",
                table: "purchase_request_lines",
                sql: "(catalog_entry_id IS NULL AND catalog_published_version IS NULL) OR (catalog_entry_id IS NOT NULL AND catalog_published_version > 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_request_lines_catalog_entries_tenant_id_legal_enti~",
                schema: "procurement",
                table: "purchase_request_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_entry_id" },
                principalSchema: "procurement",
                principalTable: "catalog_entries",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_request_lines_catalog_entries_tenant_id_legal_enti~",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropIndex(
                name: "IX_purchase_request_lines_tenant_id_legal_entity_id_catalog_en~",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_line_catalog_ref",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "catalog_entry_id",
                schema: "procurement",
                table: "purchase_request_lines");

            migrationBuilder.DropColumn(
                name: "catalog_published_version",
                schema: "procurement",
                table: "purchase_request_lines");
        }
    }
}
