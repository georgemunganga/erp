using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreCompanyMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_company_memberships",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_company_memberships", x => x.id);
                    table.CheckConstraint("CK_company_membership_role", "role IN ('owner','administrator','member')");
                    table.ForeignKey(
                        name: "FK_core_company_memberships_legal_entities_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_company_memberships_local_users_tenant_id_local_user_id",
                        columns: x => new { x.tenant_id, x.local_user_id },
                        principalSchema: "hrm",
                        principalTable: "local_users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_memberships_tenant_id_legal_entity_id_local_us~",
                schema: "hrm",
                table: "core_company_memberships",
                columns: new[] { "tenant_id", "legal_entity_id", "local_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_company_memberships_tenant_id_local_user_id",
                schema: "hrm",
                table: "core_company_memberships",
                columns: new[] { "tenant_id", "local_user_id" });

            // Only immutable allocation evidence can establish an initial owner.
            // Classic local administrators and commercial memberships are not sources.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (
                    SELECT 1 FROM hrm.core_workspace_bindings b
                    WHERE NOT EXISTS (
                      SELECT 1 FROM hrm.local_users u
                      JOIN hrm.external_identity_links l
                        ON l.tenant_id=u.tenant_id AND l.local_user_id=u.id
                      WHERE u.tenant_id=b.tenant_id AND u.id=b.owner_local_user_id
                        AND u.credential_mode='federated'
                        AND l.issuer=b.issuer AND l.subject=b.subject
                    )
                  ) THEN
                    RAISE EXCEPTION 'Company membership migration requires valid explicit allocation owner links'
                      USING ERRCODE='23514';
                  END IF;
                END $$;
                INSERT INTO hrm.core_company_memberships
                  (id,legal_entity_id,local_user_id,role,is_active,tenant_id,created_at,created_by,is_archived)
                SELECT b.id,b.legal_entity_id,b.owner_local_user_id,'owner',
                  (u.is_active AND l.is_active AND NOT u.is_archived AND NOT l.is_archived AND NOT b.is_archived),
                  b.tenant_id,b.created_at,b.created_by,FALSE
                FROM hrm.core_workspace_bindings b
                JOIN hrm.local_users u ON u.tenant_id=b.tenant_id AND u.id=b.owner_local_user_id
                JOIN hrm.external_identity_links l ON l.tenant_id=b.tenant_id AND l.local_user_id=u.id
                  AND l.issuer=b.issuer AND l.subject=b.subject;

                CREATE FUNCTION hrm.core_company_membership_binding_guard() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN
                  IF TG_OP='DELETE' THEN
                    RAISE EXCEPTION 'Company membership binding is retained; revoke instead' USING ERRCODE='23514';
                  END IF;
                  IF NEW.id IS DISTINCT FROM OLD.id OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                    OR NEW.legal_entity_id IS DISTINCT FROM OLD.legal_entity_id
                    OR NEW.local_user_id IS DISTINCT FROM OLD.local_user_id THEN
                    RAISE EXCEPTION 'Company membership binding is immutable' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER core_company_membership_binding_guard
                  BEFORE UPDATE OR DELETE ON hrm.core_company_memberships
                  FOR EACH ROW EXECUTE FUNCTION hrm.core_company_membership_binding_guard();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM hrm.core_company_memberships) THEN
                    RAISE EXCEPTION 'Cannot discard populated company memberships' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP TRIGGER core_company_membership_binding_guard ON hrm.core_company_memberships;
                DROP FUNCTION hrm.core_company_membership_binding_guard();
                """);
            migrationBuilder.DropTable(
                name: "core_company_memberships",
                schema: "hrm");
        }
    }
}
