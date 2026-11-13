using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreExternalIdentityLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_local_users_tenant_id_id",
                schema: "hrm",
                table: "local_users",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "external_identity_links",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_external_identity_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_identity_links_local_users_tenant_id_local_user_id",
                        columns: x => new { x.tenant_id, x.local_user_id },
                        principalSchema: "hrm",
                        principalTable: "local_users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_external_identity_links_issuer_subject",
                schema: "hrm",
                table: "external_identity_links",
                columns: new[] { "issuer", "subject" });

            migrationBuilder.CreateIndex(
                name: "IX_external_identity_links_tenant_id_issuer_subject",
                schema: "hrm",
                table: "external_identity_links",
                columns: new[] { "tenant_id", "issuer", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_identity_links_tenant_id_local_user_id",
                schema: "hrm",
                table: "external_identity_links",
                columns: new[] { "tenant_id", "local_user_id" });

            // Bindings are durable evidence even if a later caller bypasses EF.
            // Operational revocation/archive fields remain separately mutable.
            migrationBuilder.Sql("""
                CREATE FUNCTION hrm.guard_external_identity_link_binding()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Identity link evidence cannot be deleted' USING ERRCODE = '23514';
                    END IF;
                    IF NEW.id IS DISTINCT FROM OLD.id
                        OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id
                        OR NEW.issuer IS DISTINCT FROM OLD.issuer
                        OR NEW.subject IS DISTINCT FROM OLD.subject
                        OR NEW.local_user_id IS DISTINCT FROM OLD.local_user_id THEN
                        RAISE EXCEPTION 'Identity link bindings cannot be changed' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER trg_external_identity_link_binding
                    BEFORE UPDATE OR DELETE ON hrm.external_identity_links
                    FOR EACH ROW EXECUTE FUNCTION hrm.guard_external_identity_link_binding();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM hrm.external_identity_links) THEN
                        RAISE EXCEPTION 'Identity link evidence is retained; roll back code without dropping it' USING ERRCODE = '23514';
                    END IF;
                END; $$;
                """);
            migrationBuilder.DropTable(
                name: "external_identity_links",
                schema: "hrm");

            migrationBuilder.Sql("DROP FUNCTION hrm.guard_external_identity_link_binding();");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_local_users_tenant_id_id",
                schema: "hrm",
                table: "local_users");
        }
    }
}
