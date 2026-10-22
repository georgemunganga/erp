using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Core.Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P02CoreDocumentUploadRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScanFailureCode",
                schema: "core_document",
                table: "versions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                schema: "core_document",
                table: "scan_evidence",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadKey",
                schema: "core_document",
                table: "documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_versions_TenantId_LegalEntityId_DocumentId_Id",
                schema: "core_document",
                table: "versions",
                columns: new[] { "TenantId", "LegalEntityId", "DocumentId", "Id" });

            migrationBuilder.CreateTable(
                name: "record_events",
                schema: "core_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_events_versions_TenantId_LegalEntityId_DocumentId_Ve~",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.VersionId },
                        principalSchema: "core_document",
                        principalTable: "versions",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "DocumentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_LegalEntityId_OwnerModule_OwnerType_Own~1",
                schema: "core_document",
                table: "documents",
                columns: new[] { "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId", "UploadKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_record_events_TenantId_LegalEntityId_DocumentId_At",
                schema: "core_document",
                table: "record_events",
                columns: new[] { "TenantId", "LegalEntityId", "DocumentId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_record_events_TenantId_LegalEntityId_DocumentId_VersionId",
                schema: "core_document",
                table: "record_events",
                columns: new[] { "TenantId", "LegalEntityId", "DocumentId", "VersionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "record_events",
                schema: "core_document");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_versions_TenantId_LegalEntityId_DocumentId_Id",
                schema: "core_document",
                table: "versions");

            migrationBuilder.DropIndex(
                name: "IX_documents_TenantId_LegalEntityId_OwnerModule_OwnerType_Own~1",
                schema: "core_document",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ScanFailureCode",
                schema: "core_document",
                table: "versions");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                schema: "core_document",
                table: "scan_evidence");

            migrationBuilder.DropColumn(
                name: "UploadKey",
                schema: "core_document",
                table: "documents");
        }
    }
}
