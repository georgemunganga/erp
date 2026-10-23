using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreCompanyTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                schema: "hrm",
                table: "legal_entities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM hrm.legal_entities) THEN
                        RAISE EXCEPTION 'Company date configuration is retained; use code rollback without dropping it' USING ERRCODE = '23514';
                    END IF;
                END; $$;
                """);
            migrationBuilder.DropColumn(
                name: "time_zone_id",
                schema: "hrm",
                table: "legal_entities");
        }
    }
}
