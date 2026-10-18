using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P8PlanningDraftLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                schema: "procurement",
                table: "procurement_plans",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "procurement",
                table: "procurement_plan_lines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "status_reason",
                schema: "procurement",
                table: "procurement_plan_lines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status_reason",
                schema: "procurement",
                table: "procurement_plans");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "procurement",
                table: "procurement_plan_lines");

            migrationBuilder.DropColumn(
                name: "status_reason",
                schema: "procurement",
                table: "procurement_plan_lines");
        }
    }
}
