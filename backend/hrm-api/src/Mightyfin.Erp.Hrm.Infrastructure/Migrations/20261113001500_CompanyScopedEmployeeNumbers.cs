using Microsoft.EntityFrameworkCore.Migrations;

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

public partial class CompanyScopedEmployeeNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Install the company constraint before removing the legacy global one.
        migrationBuilder.CreateIndex(name: "ix_workers_tenant_id_employee_no", schema: "hrm",
            table: "workers", columns: new[] { "tenant_id", "employee_no" }, unique: true);
        migrationBuilder.DropIndex(name: "IX_workers_employee_no", schema: "hrm", table: "workers");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This fails safely if companies already reuse numbers. Never renumber or
        // delete employee records to make a historical global index fit again.
        migrationBuilder.CreateIndex(name: "IX_workers_employee_no", schema: "hrm",
            table: "workers", column: "employee_no", unique: true);
        migrationBuilder.DropIndex(name: "ix_workers_tenant_id_employee_no", schema: "hrm", table: "workers");
    }
}
