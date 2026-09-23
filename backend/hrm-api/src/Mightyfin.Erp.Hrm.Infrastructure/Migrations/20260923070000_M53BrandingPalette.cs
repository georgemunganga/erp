using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260923070000_M53BrandingPalette")]
public partial class M53BrandingPalette : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Add("primary_foreground_color", "#FFFFFF");
        Add("button_color", "#012642");
        Add("button_foreground_color", "#FFFFFF");
        Add("secondary_foreground_color", "#012642");
        Add("accent_foreground_color", "#012642");
        Add("rail_foreground_color", "#FFFFFF");
        Add("rail_muted_color", "#A7C7DA");
        Add("rail_active_color", "#0B3A5D");
        migrationBuilder.Sql("UPDATE hrm.company_brandings SET button_color = primary_color");

        void Add(string name, string value) => migrationBuilder.AddColumn<string>(
            name: name, schema: "hrm", table: "company_brandings", type: "text",
            nullable: false, defaultValue: value);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var name in new[] { "primary_foreground_color", "button_color", "button_foreground_color", "secondary_foreground_color",
                     "accent_foreground_color", "rail_foreground_color", "rail_muted_color", "rail_active_color" })
            migrationBuilder.DropColumn(name: name, schema: "hrm", table: "company_brandings");
    }
}
