using Microsoft.EntityFrameworkCore.Migrations;

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

public partial class PayGroupPaydayRule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(name: "payday_rule", schema: "hrm", table: "pay_groups",
            type: "text", nullable: false, defaultValue: "calendar-day");
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "payday_rule", schema: "hrm", table: "pay_groups");
}
