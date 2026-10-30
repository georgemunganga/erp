using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6dCoreCurrencyPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currency_precision_versions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    minor_units = table.Column<int>(type: "integer", nullable: false),
                    rounding = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    publication_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retirement_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_precision_versions", x => x.id);
                    table.UniqueConstraint("AK_currency_precision_versions_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_core_currency_precision_version", "revision > 0 AND minor_units BETWEEN 0 AND 4 AND rounding IN ('ToEven','AwayFromZero') AND currency_code ~ '^[A-Z]{3}$' AND payload_sha256 ~ '^[0-9a-f]{64}$' AND (retired_at_utc IS NULL OR retired_at_utc >= published_at_utc)");
                });

            migrationBuilder.CreateTable(
                name: "currency_precision_command_receipts",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_precision_command_receipts", x => x.id);
                    table.CheckConstraint("ck_core_currency_precision_receipt", "operation IN ('Publish','Retire') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND response_status IN (200,201) AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_currency_precision_command_receipts_currency_precision_vers~",
                        columns: x => new { x.tenant_id, x.version_id },
                        principalSchema: "hrm",
                        principalTable: "currency_precision_versions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_currency_precision_command_receipts_tenant_id_actor_princip~",
                schema: "hrm",
                table: "currency_precision_command_receipts",
                columns: new[] { "tenant_id", "actor_principal_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_currency_precision_command_receipts_tenant_id_version_id",
                schema: "hrm",
                table: "currency_precision_command_receipts",
                columns: new[] { "tenant_id", "version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_currency_precision_versions_tenant_id_currency_code",
                schema: "hrm",
                table: "currency_precision_versions",
                columns: new[] { "tenant_id", "currency_code" },
                unique: true,
                filter: "retired_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_currency_precision_versions_tenant_id_currency_code_revision",
                schema: "hrm",
                table: "currency_precision_versions",
                columns: new[] { "tenant_id", "currency_code", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE hrm.currency_precision_versions,
                  hrm.currency_precision_command_receipts,
                  hrm.audit_entries,
                  hrm.outbox_messages IN ACCESS EXCLUSIVE MODE;
                DO $m6d_core_currency_down$
                BEGIN
                  IF EXISTS (SELECT 1 FROM hrm.currency_precision_versions)
                    OR EXISTS (SELECT 1 FROM hrm.currency_precision_command_receipts)
                    OR EXISTS (SELECT 1 FROM hrm.audit_entries
                               WHERE entity_type IN ('CoreCurrencyPrecisionVersion',
                                 'CoreCurrencyPrecisionCommandReceipt'))
                    OR EXISTS (SELECT 1 FROM hrm.outbox_messages
                               WHERE event_type LIKE 'core.currency_precision.%')
                  THEN
                    RAISE EXCEPTION USING ERRCODE='55000',
                      MESSAGE='Retained Core currency precision evidence prevents downgrade; use compatible code or fix forward.';
                  END IF;
                END
                $m6d_core_currency_down$;
                """);

            migrationBuilder.DropTable(
                name: "currency_precision_command_receipts",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "currency_precision_versions",
                schema: "hrm");
        }
    }
}
