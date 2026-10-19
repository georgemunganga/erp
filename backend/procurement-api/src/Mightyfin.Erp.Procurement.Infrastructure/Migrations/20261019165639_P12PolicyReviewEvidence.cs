using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P12PolicyReviewEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "publication_reason",
                schema: "procurement",
                table: "policy_versions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at",
                schema: "procurement",
                table: "policy_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "published_by",
                schema: "procurement",
                table: "policy_versions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_reason",
                schema: "procurement",
                table: "policy_versions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "review_requested_at",
                schema: "procurement",
                table: "policy_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_requested_by",
                schema: "procurement",
                table: "policy_versions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "publication_reason",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropColumn(
                name: "published_at",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropColumn(
                name: "published_by",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropColumn(
                name: "review_reason",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropColumn(
                name: "review_requested_at",
                schema: "procurement",
                table: "policy_versions");

            migrationBuilder.DropColumn(
                name: "review_requested_by",
                schema: "procurement",
                table: "policy_versions");
        }
    }
}
