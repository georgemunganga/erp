using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P9DraftCommercialLineLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "procurement",
                table: "sourcing_lines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                schema: "procurement",
                table: "sourcing_lines",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "procurement",
                table: "contract_rates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                schema: "procurement",
                table: "contract_rates",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                schema: "procurement",
                table: "sourcing_lines");

            migrationBuilder.DropColumn(
                name: "status_reason",
                schema: "procurement",
                table: "sourcing_lines");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "procurement",
                table: "contract_rates");

            migrationBuilder.DropColumn(
                name: "status_reason",
                schema: "procurement",
                table: "contract_rates");
        }
    }
}
