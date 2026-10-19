using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P13PolicyEvaluationSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "amount_basis",
                schema: "procurement",
                table: "policy_evaluations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "decision_json",
                schema: "procurement",
                table: "policy_evaluations",
                type: "character varying(65536)",
                maxLength: 65536,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "policy_version_number",
                schema: "procurement",
                table: "policy_evaluations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "request_version",
                schema: "procurement",
                table: "policy_evaluations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_policy_evaluations_tenant_id_legal_entity_id_purchase_requ~1",
                schema: "procurement",
                table: "policy_evaluations",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "request_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_policy_evaluations_tenant_id_legal_entity_id_purchase_requ~1",
                schema: "procurement",
                table: "policy_evaluations");

            migrationBuilder.DropColumn(
                name: "amount_basis",
                schema: "procurement",
                table: "policy_evaluations");

            migrationBuilder.DropColumn(
                name: "decision_json",
                schema: "procurement",
                table: "policy_evaluations");

            migrationBuilder.DropColumn(
                name: "policy_version_number",
                schema: "procurement",
                table: "policy_evaluations");

            migrationBuilder.DropColumn(
                name: "request_version",
                schema: "procurement",
                table: "policy_evaluations");
        }
    }
}
