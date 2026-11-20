using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreWorkspaceAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_legal_entities_tenant_id_id",
                schema: "hrm",
                table: "legal_entities",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "core_workspace_bindings",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    client_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    enrollment_id = table.Column<string>(type: "character varying(26)", maxLength: 26, nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    purchased_limit = table.Column<int>(type: "integer", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_local_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    authority_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    license_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_workspace_bindings", x => x.id);
                    table.CheckConstraint("CK_workspace_capacity", "slot >= 1 AND slot <= purchased_limit AND purchased_limit <= 10000");
                    table.ForeignKey(
                        name: "FK_core_workspace_bindings_legal_entities_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id },
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_workspace_bindings_local_users_tenant_id_owner_local_u~",
                        columns: x => new { x.tenant_id, x.owner_local_user_id },
                        principalSchema: "hrm",
                        principalTable: "local_users",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_workspace_bindings_issuer_enrollment_id_slot",
                schema: "hrm",
                table: "core_workspace_bindings",
                columns: new[] { "issuer", "enrollment_id", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_workspace_bindings_issuer_subject_idempotency_hash",
                schema: "hrm",
                table: "core_workspace_bindings",
                columns: new[] { "issuer", "subject", "idempotency_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_workspace_bindings_tenant_id",
                schema: "hrm",
                table: "core_workspace_bindings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_workspace_bindings_tenant_id_legal_entity_id",
                schema: "hrm",
                table: "core_workspace_bindings",
                columns: new[] { "tenant_id", "legal_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_workspace_bindings_tenant_id_owner_local_user_id",
                schema: "hrm",
                table: "core_workspace_bindings",
                columns: new[] { "tenant_id", "owner_local_user_id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION hrm.guard_core_workspace_allocation()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Workspace allocation evidence is immutable' USING ERRCODE = '23514';
                END; $$;
                CREATE TRIGGER trg_core_workspace_allocation
                    BEFORE UPDATE OR DELETE ON hrm.core_workspace_bindings
                    FOR EACH ROW EXECUTE FUNCTION hrm.guard_core_workspace_allocation();

                CREATE FUNCTION hrm.guard_bound_workspace_company()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM hrm.core_workspace_bindings b
                        WHERE b.tenant_id = NEW.tenant_id AND b.legal_entity_id <> NEW.id) THEN
                        RAISE EXCEPTION 'Use purchased workspace allocation to create another company' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER trg_bound_workspace_company
                    BEFORE INSERT OR UPDATE ON hrm.legal_entities
                    FOR EACH ROW EXECUTE FUNCTION hrm.guard_bound_workspace_company();

                CREATE FUNCTION hrm.guard_initial_workspace_company()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM hrm.legal_entities e
                        WHERE e.tenant_id = NEW.tenant_id AND e.id = NEW.legal_entity_id)
                        AND EXISTS (SELECT 1 FROM hrm.legal_entities e
                        WHERE e.tenant_id = NEW.tenant_id AND e.id <> NEW.legal_entity_id) THEN
                        RAISE EXCEPTION 'An allocated workspace must contain exactly one company' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER trg_initial_workspace_company
                    BEFORE INSERT ON hrm.core_workspace_bindings
                    FOR EACH ROW EXECUTE FUNCTION hrm.guard_initial_workspace_company();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM hrm.core_workspace_bindings) THEN
                        RAISE EXCEPTION 'Cannot discard purchased workspace allocation evidence' USING ERRCODE = '23514';
                    END IF;
                END $$;
                DROP TRIGGER trg_core_workspace_allocation ON hrm.core_workspace_bindings;
                DROP TRIGGER trg_bound_workspace_company ON hrm.legal_entities;
                DROP TRIGGER trg_initial_workspace_company ON hrm.core_workspace_bindings;
                DROP FUNCTION hrm.guard_core_workspace_allocation();
                DROP FUNCTION hrm.guard_bound_workspace_company();
                DROP FUNCTION hrm.guard_initial_workspace_company();
                """);
            migrationBuilder.DropTable(
                name: "core_workspace_bindings",
                schema: "hrm");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_legal_entities_tenant_id_id",
                schema: "hrm",
                table: "legal_entities");
        }
    }
}
