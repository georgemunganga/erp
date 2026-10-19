using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P10BudgetDecisionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "finance_check_ref",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "checked_by_subject_id",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "decision_reason",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_ref",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "request_version",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "checked_by_subject_id",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "decision_reason",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "evidence_ref",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "request_version",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.AlterColumn<string>(
                name: "finance_check_ref",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);
        }
    }
}
