using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Core.Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P03DocumentUseRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_doc_scan_version_id",
                schema: "core_document",
                table: "scan_evidence",
                columns: new[] { "TenantId", "LegalEntityId", "VersionId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_doc_owner_id",
                schema: "core_document",
                table: "documents",
                columns: new[] { "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId", "Id" });

            migrationBuilder.CreateTable(
                name: "use_sets",
                schema: "core_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseKey = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SourceRecordType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRevision = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    OwnerModule = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectionCount = table.Column<int>(type: "integer", nullable: false),
                    SelectionDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doc_use_sets", x => x.Id);
                    table.UniqueConstraint("ak_doc_use_set_owner_id", x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.Id });
                    table.CheckConstraint("ck_doc_use_set_shape", "\"SourceRevision\" > 0 AND \"Kind\" IN (1,2) AND \"SelectionCount\" BETWEEN 1 AND 100 AND \"SelectionDigest\" ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "use_pins",
                schema: "core_document",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerModule = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanEvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    DocumentRevision = table.Column<long>(type: "bigint", nullable: false),
                    VersionRevision = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScanAttempt = table.Column<int>(type: "integer", nullable: false),
                    ScanVerdict = table.Column<int>(type: "integer", nullable: false),
                    ScanEngine = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ScanDefinitions = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ScanStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ScanFinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doc_use_pins", x => x.Id);
                    table.CheckConstraint("ck_doc_use_pin_shape", "\"DocumentRevision\" > 0 AND \"VersionRevision\" > 0 AND \"VersionNumber\" > 0 AND \"SizeBytes\" BETWEEN 1 AND 104857600 AND \"ScanAttempt\" > 0 AND \"ScanVerdict\" = 1 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"ScanFinishedAt\" >= \"ScanStartedAt\" AND \"CreatedAt\" >= \"ScanFinishedAt\"");
                    table.ForeignKey(
                        name: "fk_doc_pin_document_owner",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.DocumentId },
                        principalSchema: "core_document",
                        principalTable: "documents",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doc_pin_document_version",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.DocumentId, x.VersionId },
                        principalSchema: "core_document",
                        principalTable: "versions",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "DocumentId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doc_pin_set_owner",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.OwnerModule, x.OwnerType, x.OwnerId, x.UseSetId },
                        principalSchema: "core_document",
                        principalTable: "use_sets",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_doc_pin_version_scan",
                        columns: x => new { x.TenantId, x.LegalEntityId, x.VersionId, x.ScanEvidenceId },
                        principalSchema: "core_document",
                        principalTable: "scan_evidence",
                        principalColumns: new[] { "TenantId", "LegalEntityId", "VersionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doc_use_document",
                schema: "core_document",
                table: "use_pins",
                columns: new[] { "TenantId", "LegalEntityId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_use_pins_TenantId_LegalEntityId_DocumentId_VersionId",
                schema: "core_document",
                table: "use_pins",
                columns: new[] { "TenantId", "LegalEntityId", "DocumentId", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_use_pins_TenantId_LegalEntityId_OwnerModule_OwnerType_Owne~1",
                schema: "core_document",
                table: "use_pins",
                columns: new[] { "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId", "UseSetId" });

            migrationBuilder.CreateIndex(
                name: "IX_use_pins_TenantId_LegalEntityId_OwnerModule_OwnerType_Owner~",
                schema: "core_document",
                table: "use_pins",
                columns: new[] { "TenantId", "LegalEntityId", "OwnerModule", "OwnerType", "OwnerId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_use_pins_TenantId_LegalEntityId_VersionId_ScanEvidenceId",
                schema: "core_document",
                table: "use_pins",
                columns: new[] { "TenantId", "LegalEntityId", "VersionId", "ScanEvidenceId" });

            migrationBuilder.CreateIndex(
                name: "ux_doc_use_set_version",
                schema: "core_document",
                table: "use_pins",
                columns: new[] { "TenantId", "LegalEntityId", "UseSetId", "VersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_doc_use_key",
                schema: "core_document",
                table: "use_sets",
                columns: new[] { "TenantId", "LegalEntityId", "UseKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retained use evidence cannot be removed by a code rollback. The locks are held
            // by EF's migration transaction through the subsequent drops, preventing a pin
            // writer from committing between this check and removal of the owning tables.
            migrationBuilder.Sql("""
                LOCK TABLE core_document.use_sets, core_document.use_pins IN ACCESS EXCLUSIVE MODE;
                DO $document_use_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM core_document.use_sets)
                        OR EXISTS (SELECT 1 FROM core_document.use_pins) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '55000',
                            MESSAGE = 'CoreDocumentUseRollbackWouldLoseEvidence';
                    END IF;
                END
                $document_use_rollback$;
                """);

            migrationBuilder.DropTable(
                name: "use_pins",
                schema: "core_document");

            migrationBuilder.DropTable(
                name: "use_sets",
                schema: "core_document");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_doc_scan_version_id",
                schema: "core_document",
                table: "scan_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_doc_owner_id",
                schema: "core_document",
                table: "documents");
        }
    }
}
