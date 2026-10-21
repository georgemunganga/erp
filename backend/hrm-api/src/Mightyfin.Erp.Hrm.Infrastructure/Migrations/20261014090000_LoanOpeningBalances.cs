using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20261014090000_LoanOpeningBalances")]
public partial class LoanOpeningBalances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "record_type", schema: "hrm", table: "salary_advances",
            type: "text", nullable: false, defaultValue: "salary-advance");
        migrationBuilder.AddColumn<decimal>(name: "opening_recovered_amount", schema: "hrm", table: "salary_advances",
            type: "numeric", nullable: false, defaultValue: 0m);
        migrationBuilder.AlterColumn<DateOnly>(name: "issue_date", schema: "hrm", table: "salary_advances",
            type: "date", nullable: true, oldClrType: typeof(DateOnly), oldType: "date");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "record_type", schema: "hrm", table: "salary_advances");
        migrationBuilder.DropColumn(name: "opening_recovered_amount", schema: "hrm", table: "salary_advances");
        migrationBuilder.AlterColumn<DateOnly>(name: "issue_date", schema: "hrm", table: "salary_advances",
            type: "date", nullable: false, defaultValue: new DateOnly(1, 1, 1), oldClrType: typeof(DateOnly), oldType: "date", oldNullable: true);
    }
}
