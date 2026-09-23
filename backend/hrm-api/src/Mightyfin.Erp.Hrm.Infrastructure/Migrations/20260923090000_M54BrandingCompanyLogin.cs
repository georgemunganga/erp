using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations;

[DbContext(typeof(HrmDbContext))]
[Migration("20260923090000_M54BrandingCompanyLogin")]
public partial class M54BrandingCompanyLogin : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Add("company_name", "Company");
        Add("company_domain", "");
        Add("login_heading", "");
        Add("login_description", "");
        Add("email_placeholder", "");
        Add("password_placeholder", "Enter your password");
        Add("support_email", "");
        migrationBuilder.Sql("UPDATE hrm.company_brandings SET company_name = display_name");

        void Add(string name, string value) => migrationBuilder.AddColumn<string>(
            name: name, schema: "hrm", table: "company_brandings", type: "text",
            nullable: false, defaultValue: value);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var name in new[] { "company_name", "company_domain", "login_heading", "login_description",
                     "email_placeholder", "password_placeholder", "support_email" })
            migrationBuilder.DropColumn(name: name, schema: "hrm", table: "company_brandings");
    }
}
