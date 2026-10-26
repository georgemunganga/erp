using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M2aMasterImportReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actor_subject_id",
                schema: "procurement",
                table: "import_batches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "commit_command_key",
                schema: "procurement",
                table: "import_batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "commit_request_sha256",
                schema: "procurement",
                table: "import_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "duplicate_policy",
                schema: "procurement",
                table: "import_batches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_sha256",
                schema: "procurement",
                table: "import_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_snapshot_text",
                schema: "procurement",
                table: "import_batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_sha256",
                schema: "procurement",
                table: "import_batches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_text",
                schema: "procurement",
                table: "import_batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "schema_version",
                schema: "procurement",
                table: "import_batches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "import_row_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    start_line = table.Column<int>(type: "integer", nullable: false),
                    end_line = table.Column<int>(type: "integer", nullable: false),
                    disposition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_row_receipts", x => x.id);
                    table.UniqueConstraint("AK_import_row_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_import_row_receipts_bounds", "row_number >= 2 AND start_line >= 2 AND end_line >= start_line");
                    table.ForeignKey(
                        name: "FK_import_row_receipts_import_batches_tenant_id_legal_entity_i~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.import_batch_id },
                        principalSchema: "procurement",
                        principalTable: "import_batches",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_scope_command",
                schema: "procurement",
                table: "import_batches",
                columns: new[] { "tenant_id", "legal_entity_id", "commit_command_key" },
                unique: true,
                filter: "commit_command_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_import_row_receipts_batch_row",
                schema: "procurement",
                table: "import_row_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "import_batch_id", "row_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // New migration only: retained import facts require compatible code/fix-forward.
            migrationBuilder.Sql("""
                DO $m2a_import_down$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.import_row_receipts)
                       OR EXISTS (
                           SELECT 1 FROM procurement.import_batches
                           WHERE actor_subject_id IS NOT NULL
                              OR commit_command_key IS NOT NULL
                              OR commit_request_sha256 IS NOT NULL
                              OR duplicate_policy IS NOT NULL
                              OR preview_sha256 IS NOT NULL
                              OR preview_snapshot_text IS NOT NULL
                              OR result_sha256 IS NOT NULL
                              OR result_text IS NOT NULL
                              OR schema_version <> 0)
                       OR EXISTS (
                           SELECT 1 FROM procurement.audit_events
                           WHERE record_type IN ('ImportBatch', 'ImportRowReceipt'))
                       OR EXISTS (
                           SELECT 1 FROM procurement.outbox_events
                           WHERE aggregate_type IN ('ImportBatch', 'ImportRowReceipt')
                              OR event_type LIKE 'procurement.master_import.%')
                    THEN
                        RAISE EXCEPTION USING ERRCODE = '55000',
                            MESSAGE = 'Master import data or retained evidence exists. Use compatible code or fix forward.';
                    END IF;
                END
                $m2a_import_down$;
                """);

            migrationBuilder.DropTable(
                name: "import_row_receipts",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "ix_import_batches_scope_command",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "actor_subject_id",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "commit_command_key",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "commit_request_sha256",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "duplicate_policy",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "preview_sha256",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "preview_snapshot_text",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "result_sha256",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "result_text",
                schema: "procurement",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "schema_version",
                schema: "procurement",
                table: "import_batches");
        }
    }
}
