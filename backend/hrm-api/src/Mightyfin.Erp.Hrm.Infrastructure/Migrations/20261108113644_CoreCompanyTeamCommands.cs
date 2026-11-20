using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreCompanyTeamCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "revision",
                schema: "hrm",
                table: "core_company_memberships",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_core_company_memberships_tenant_id_legal_entity_id_id",
                schema: "hrm",
                table: "core_company_memberships",
                columns: new[] { "tenant_id", "legal_entity_id", "id" });

            migrationBuilder.CreateTable(
                name: "core_company_team_command_receipts",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_subject = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    result_role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    result_active = table.Column<bool>(type: "boolean", nullable: false),
                    result_revision = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_company_team_command_receipts", x => x.id);
                    table.CheckConstraint("CK_company_team_receipt", "result_revision >= 1 AND length(request_hash) = 64 AND operation IN ('role-change','revoke') AND result_role IN ('owner','administrator','member')");
                    table.ForeignKey(
                        name: "FK_core_company_team_command_receipts_core_company_memberships~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.membership_id },
                        principalSchema: "hrm",
                        principalTable: "core_company_memberships",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_company_team_command_receipts_legal_entities_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_company_team_command_receipts_local_users_tenant_id_ac~",
                        columns: x => new { x.tenant_id, x.actor_local_user_id },
                        principalSchema: "hrm",
                        principalTable: "local_users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_company_membership_revision",
                schema: "hrm",
                table: "core_company_memberships",
                sql: "revision >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_core_company_team_command_receipts_actor_issuer_actor_subje~",
                schema: "hrm",
                table: "core_company_team_command_receipts",
                columns: new[] { "actor_issuer", "actor_subject", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_company_team_command_receipts_tenant_id_actor_local_us~",
                schema: "hrm",
                table: "core_company_team_command_receipts",
                columns: new[] { "tenant_id", "actor_local_user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_team_command_receipts_tenant_id_legal_entity_i~",
                schema: "hrm",
                table: "core_company_team_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "membership_id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION hrm.guard_core_company_team_receipt() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN
                  RAISE EXCEPTION 'Company team command receipts are immutable' USING ERRCODE='23514';
                END $$;
                CREATE TRIGGER guard_core_company_team_receipt
                  BEFORE UPDATE OR DELETE ON hrm.core_company_team_command_receipts
                  FOR EACH ROW EXECUTE FUNCTION hrm.guard_core_company_team_receipt();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM hrm.core_company_team_command_receipts)
                    OR EXISTS (SELECT 1 FROM hrm.core_company_memberships WHERE revision <> 1) THEN
                    RAISE EXCEPTION 'Cannot discard company team command history' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP TRIGGER guard_core_company_team_receipt ON hrm.core_company_team_command_receipts;
                DROP FUNCTION hrm.guard_core_company_team_receipt();
                """);
            migrationBuilder.DropTable(
                name: "core_company_team_command_receipts",
                schema: "hrm");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_core_company_memberships_tenant_id_legal_entity_id_id",
                schema: "hrm",
                table: "core_company_memberships");

            migrationBuilder.DropCheckConstraint(
                name: "CK_company_membership_revision",
                schema: "hrm",
                table: "core_company_memberships");

            migrationBuilder.DropColumn(
                name: "revision",
                schema: "hrm",
                table: "core_company_memberships");
        }
    }
}
