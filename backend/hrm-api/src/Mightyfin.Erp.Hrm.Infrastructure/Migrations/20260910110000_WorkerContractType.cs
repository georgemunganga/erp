using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260910110000_WorkerContractType")]
public partial class WorkerContractType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "contract_type", schema: "hrm", table: "workers", type: "text", nullable: true);
        migrationBuilder.Sql("UPDATE hrm.workers w SET contract_type = a.contract_type FROM hrm.assignments a WHERE a.worker_id = w.id AND a.status = 'current';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "contract_type", schema: "hrm", table: "workers");
}
