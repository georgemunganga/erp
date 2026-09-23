using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

/// <summary>Kept deliberately narrow: earlier hand-authored migrations already
/// own the payroll/identity tables present in the model snapshot.</summary>
[DbContext(typeof(HrmDbContext))]
[Migration("20260904082000_M52CompanyBranding")]
public partial class M52CompanyBranding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "company_brandings",
            schema: "hrm",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                display_name = table.Column<string>(type: "text", nullable: false),
                primary_color = table.Column<string>(type: "text", nullable: false),
                secondary_color = table.Column<string>(type: "text", nullable: false),
                accent_color = table.Column<string>(type: "text", nullable: false),
                rail_color = table.Column<string>(type: "text", nullable: false),
                logo_light_data_uri = table.Column<string>(type: "text", nullable: true),
                logo_dark_data_uri = table.Column<string>(type: "text", nullable: true),
                favicon_data_uri = table.Column<string>(type: "text", nullable: true),
                tenant_id = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "text", nullable: true),
                is_archived = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_company_brandings", x => x.id));
        migrationBuilder.CreateIndex("IX_company_brandings_tenant_id", "company_brandings", "tenant_id", schema: "hrm", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "company_brandings", schema: "hrm");
}
