using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Core.Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P01CoreDocumentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core_document");

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "core_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerModule = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Classification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRetired = table.Column<bool>(type: "boolean", nullable: false),
                    RetiredBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RetirementReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LegalHold = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.UniqueConstraint("AK_documents_TenantId_LegalEntityId_Id", x => new { x.TenantId, x.LegalEntityId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "versions",
                schema: "core_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScanState = table.Column<int>(type: "integer", nullable: false),
                    ScanAttempts = table.Column<int>(type: "integer", nullable: false),
                    ScanLeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScanStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScanLeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_versions", x => x.Id);
                    table.UniqueConstraint("AK_versions_TenantId_LegalEntityId_Id", x => new { x.TenantId, x.LegalEntityId, x.Id });
                    table.ForeignKey(
                        name: "FK_versions_documents_TenantId_LegalEntityId_DocumentId",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.DocumentId },
                        principalSchema: "core_document",
                        principalTable: "documents",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scan_evidence",
                schema: "core_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Verdict = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Engine = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Definitions = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scan_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scan_evidence_versions_TenantId_LegalEntityId_VersionId",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.VersionId },
                        principalSchema: "core_document",
                        principalTable: "versions",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_LegalEntityId_OwnerModule_OwnerType_Owne~",
                schema: "core_document",
                table: "documents",
                columns: new[] { "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_scan_evidence_TenantId_LegalEntityId_VersionId_Attempt",
                schema: "core_document",
                table: "scan_evidence",
                columns: new[] { "TenantId", "LegalEntityId", "VersionId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_versions_StorageKey",
                schema: "core_document",
                table: "versions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_versions_TenantId_LegalEntityId_DocumentId_Number",
                schema: "core_document",
                table: "versions",
                columns: new[] { "TenantId", "LegalEntityId", "DocumentId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scan_evidence",
                schema: "core_document");

            migrationBuilder.DropTable(
                name: "versions",
                schema: "core_document");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "core_document");
        }
    }
}
