using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreCompanyInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_external_identity_links_tenant_id_local_user_id",
                schema: "hrm",
                table: "external_identity_links");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_external_identity_links_tenant_id_local_user_id_id",
                schema: "hrm",
                table: "external_identity_links",
                columns: new[] { "tenant_id", "local_user_id", "id" });

            migrationBuilder.CreateTable(
                name: "core_company_invitations",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inviter_subject = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    inviter_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inviter_local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    protected_delivery = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    accepted_subject = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: true),
                    accepted_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_company_invitations", x => x.id);
                    table.UniqueConstraint("AK_core_company_invitations_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("CK_company_invitation", "role IN ('administrator','member') AND length(recipient_hash)=64 AND length(token_hash)=64 AND expires_at > created_at AND ((state='pending' AND revision=1 AND accepted_subject IS NULL AND accepted_membership_id IS NULL) OR (state='revoked' AND revision=2 AND accepted_subject IS NULL AND accepted_membership_id IS NULL) OR (state='accepted' AND revision=2 AND accepted_subject IS NOT NULL AND accepted_membership_id IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_core_company_invitations_core_company_memberships_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.accepted_membership_id },
                        principalSchema: "hrm",
                        principalTable: "core_company_memberships",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_company_invitations_external_identity_links_tenant_id_~",
                        columns: x => new { x.tenant_id, x.inviter_local_user_id, x.inviter_link_id },
                        principalSchema: "hrm",
                        principalTable: "external_identity_links",
                        principalColumns: new[] { "tenant_id", "local_user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_company_invitations_legal_entities_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_company_invitations_local_users_tenant_id_inviter_loca~",
                        columns: x => new { x.tenant_id, x.inviter_local_user_id },
                        principalSchema: "hrm",
                        principalTable: "local_users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "core_company_invitation_receipts",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_subject = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    result_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    result_revision = table.Column<int>(type: "integer", nullable: false),
                    result_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_company_invitation_receipts", x => x.id);
                    table.CheckConstraint("CK_company_invitation_receipt", "length(request_hash)=64 AND ((operation='issue' AND result_state='pending' AND result_revision=1 AND result_membership_id IS NULL) OR (operation='revoke' AND result_state='revoked' AND result_revision=2 AND result_membership_id IS NULL) OR (operation='accept' AND result_state='accepted' AND result_revision=2 AND result_membership_id IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_core_company_invitation_receipts_core_company_invitations_t~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.invitation_id },
                        principalSchema: "hrm",
                        principalTable: "core_company_invitations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_company_invitation_receipts_core_company_memberships_t~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.result_membership_id },
                        principalSchema: "hrm",
                        principalTable: "core_company_memberships",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_invitation_receipts_actor_issuer_actor_subject~",
                schema: "hrm",
                table: "core_company_invitation_receipts",
                columns: new[] { "actor_issuer", "actor_subject", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_company_invitation_receipts_tenant_id_legal_entity_id_~",
                schema: "hrm",
                table: "core_company_invitation_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "invitation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_invitation_receipts_tenant_id_legal_entity_id~1",
                schema: "hrm",
                table: "core_company_invitation_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "result_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_invitations_tenant_id_inviter_local_user_id_in~",
                schema: "hrm",
                table: "core_company_invitations",
                columns: new[] { "tenant_id", "inviter_local_user_id", "inviter_link_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_invitations_tenant_id_legal_entity_id_accepted~",
                schema: "hrm",
                table: "core_company_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "accepted_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_invitations_tenant_id_legal_entity_id_recipien~",
                schema: "hrm",
                table: "core_company_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "recipient_hash", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_core_company_invitations_token_hash",
                schema: "hrm",
                table: "core_company_invitations",
                column: "token_hash",
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION hrm.guard_core_company_invitation() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN
                  IF TG_OP='DELETE' THEN
                    RAISE EXCEPTION 'Company invitation history cannot be deleted' USING ERRCODE='23514';
                  END IF;
                  IF OLD.state <> 'pending' OR NEW.state NOT IN ('accepted','revoked') OR NEW.revision <> OLD.revision+1
                    OR ROW(NEW.id,NEW.tenant_id,NEW.legal_entity_id,NEW.issuer,NEW.inviter_subject,
                      NEW.inviter_link_id,NEW.inviter_local_user_id,NEW.recipient_hash,NEW.token_hash,
                      NEW.protected_delivery,NEW.role,NEW.expires_at,NEW.created_at,NEW.created_by,NEW.is_archived)
                    IS DISTINCT FROM ROW(OLD.id,OLD.tenant_id,OLD.legal_entity_id,OLD.issuer,OLD.inviter_subject,
                      OLD.inviter_link_id,OLD.inviter_local_user_id,OLD.recipient_hash,OLD.token_hash,
                      OLD.protected_delivery,OLD.role,OLD.expires_at,OLD.created_at,OLD.created_by,OLD.is_archived) THEN
                    RAISE EXCEPTION 'Company invitation binding and terminal history are immutable' USING ERRCODE='23514';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER guard_core_company_invitation BEFORE UPDATE OR DELETE ON hrm.core_company_invitations
                  FOR EACH ROW EXECUTE FUNCTION hrm.guard_core_company_invitation();
                CREATE FUNCTION hrm.guard_core_company_invitation_receipt() RETURNS trigger
                LANGUAGE plpgsql AS $$ BEGIN
                  RAISE EXCEPTION 'Company invitation receipts are immutable' USING ERRCODE='23514';
                END $$;
                CREATE TRIGGER guard_core_company_invitation_receipt BEFORE UPDATE OR DELETE ON hrm.core_company_invitation_receipts
                  FOR EACH ROW EXECUTE FUNCTION hrm.guard_core_company_invitation_receipt();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM hrm.core_company_invitations) OR EXISTS (SELECT 1 FROM hrm.core_company_invitation_receipts) THEN
                    RAISE EXCEPTION 'Cannot discard populated invitation history or delivery obligations' USING ERRCODE='23514';
                  END IF;
                END $$;
                DROP TRIGGER guard_core_company_invitation_receipt ON hrm.core_company_invitation_receipts;
                DROP FUNCTION hrm.guard_core_company_invitation_receipt();
                DROP TRIGGER guard_core_company_invitation ON hrm.core_company_invitations;
                DROP FUNCTION hrm.guard_core_company_invitation();
                """);
            migrationBuilder.DropTable(
                name: "core_company_invitation_receipts",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "core_company_invitations",
                schema: "hrm");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_external_identity_links_tenant_id_local_user_id_id",
                schema: "hrm",
                table: "external_identity_links");

            migrationBuilder.CreateIndex(
                name: "IX_external_identity_links_tenant_id_local_user_id",
                schema: "hrm",
                table: "external_identity_links",
                columns: new[] { "tenant_id", "local_user_id" });
        }
    }
}
