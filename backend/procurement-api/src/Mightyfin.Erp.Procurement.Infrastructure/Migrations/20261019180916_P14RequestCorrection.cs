using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P14RequestCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "return_reason",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "returned_at",
                schema: "procurement",
                table: "purchase_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "returned_by_subject_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "return_reason",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "returned_at",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "returned_by_subject_id",
                schema: "procurement",
                table: "purchase_requests");
        }
    }
}
