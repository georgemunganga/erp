using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreProductSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_product_sessions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version = table.Column<int>(type: "integer", nullable: false),
                    issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    client_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    grant_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_revision = table.Column<int>(type: "integer", nullable: false),
                    scope_kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    scope_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    protected_token = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false),
                    credential_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    browser_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued_at = table.Column<long>(type: "bigint", nullable: false),
                    expires_at = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_product_sessions", x => x.id);
                    table.UniqueConstraint("AK_core_product_sessions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("CK_product_session_deadline", "expires_at > issued_at AND expires_at - issued_at <= 288000000000 AND issued_at >= 0 AND expires_at <= 3155378975999999999");
                    table.CheckConstraint("CK_product_session_policy", "policy_version = 1 AND grant_revision >= 1 AND issuer = 'https://commercial-api.greenwebb.tech' AND NOT is_archived");
                    table.CheckConstraint("CK_product_session_scope", "module_code IN ('hrm','procurement') AND scope_kind IN ('company','locations')");
                    table.CheckConstraint("CK_product_session_secrets", "length(scope_hash) = 64 AND length(token_hash) = 64 AND length(credential_hash) = 64 AND length(browser_hash) = 64 AND length(request_hash) = 64 AND length(protected_token) BETWEEN 44 AND 32768");
                    table.ForeignKey(
                        name: "FK_core_product_sessions_core_module_access_versions_tenant_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.membership_id, x.module_code, x.grant_version_id, x.grant_revision },
                        principalSchema: "hrm",
                        principalTable: "core_module_access_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "membership_id", "module_code", "id", "revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_product_sessions_external_identity_links_tenant_id_pri~",
                        columns: x => new { x.tenant_id, x.principal_id, x.link_id },
                        principalSchema: "hrm",
                        principalTable: "external_identity_links",
                        principalColumns: new[] { "tenant_id", "local_user_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "core_product_session_locations",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_product_session_locations", x => x.id);
                    table.CheckConstraint("CK_product_session_location", "NOT is_archived");
                    table.ForeignKey(
                        name: "FK_core_product_session_locations_core_product_sessions_tenant~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.session_id },
                        principalSchema: "hrm",
                        principalTable: "core_product_sessions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_product_session_locations_work_locations_tenant_id_loc~",
                        columns: x => new { x.tenant_id, x.location_id },
                        principalSchema: "hrm",
                        principalTable: "work_locations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "core_product_session_revocations",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_product_session_revocations", x => x.id);
                    table.CheckConstraint("CK_product_session_revocation", "reason IN ('logout','replaced') AND NOT is_archived");
                    table.ForeignKey(
                        name: "FK_core_product_session_revocations_core_product_sessions_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.session_id },
                        principalSchema: "hrm",
                        principalTable: "core_product_sessions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_product_session_locations_session_id_location_id",
                schema: "hrm",
                table: "core_product_session_locations",
                columns: new[] { "session_id", "location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_product_session_locations_tenant_id_legal_entity_id_se~",
                schema: "hrm",
                table: "core_product_session_locations",
                columns: new[] { "tenant_id", "legal_entity_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_product_session_locations_tenant_id_location_id",
                schema: "hrm",
                table: "core_product_session_locations",
                columns: new[] { "tenant_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_product_session_revocations_session_id",
                schema: "hrm",
                table: "core_product_session_revocations",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_product_session_revocations_tenant_id_legal_entity_id_~",
                schema: "hrm",
                table: "core_product_session_revocations",
                columns: new[] { "tenant_id", "legal_entity_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_product_sessions_issuer_subject_client_id_command_key",
                schema: "hrm",
                table: "core_product_sessions",
                columns: new[] { "issuer", "subject", "client_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_product_sessions_issuer_subject_client_id_issued_at",
                schema: "hrm",
                table: "core_product_sessions",
                columns: new[] { "issuer", "subject", "client_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "IX_product_sessions_grant_binding",
                schema: "hrm",
                table: "core_product_sessions",
                columns: new[] { "tenant_id", "legal_entity_id", "membership_id", "module_code", "grant_version_id", "grant_revision" });

            migrationBuilder.CreateIndex(
                name: "IX_core_product_sessions_tenant_id_principal_id_link_id",
                schema: "hrm",
                table: "core_product_sessions",
                columns: new[] { "tenant_id", "principal_id", "link_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_product_sessions_token_hash",
                schema: "hrm",
                table: "core_product_sessions",
                column: "token_hash",
                unique: true);

            if (ActiveProvider.Contains("Npgsql", System.StringComparison.Ordinal))
                migrationBuilder.Sql("""
                    CREATE FUNCTION hrm.reject_product_session_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                    BEGIN
                        RAISE EXCEPTION 'Product session evidence is immutable' USING ERRCODE = '23514';
                    END; $$;
                    CREATE TRIGGER trg_product_session_immutable BEFORE UPDATE OR DELETE ON hrm.core_product_sessions
                        FOR EACH ROW EXECUTE FUNCTION hrm.reject_product_session_mutation();
                    CREATE TRIGGER trg_product_session_location_immutable BEFORE UPDATE OR DELETE ON hrm.core_product_session_locations
                        FOR EACH ROW EXECUTE FUNCTION hrm.reject_product_session_mutation();
                    CREATE TRIGGER trg_product_session_revocation_immutable BEFORE UPDATE OR DELETE ON hrm.core_product_session_revocations
                        FOR EACH ROW EXECUTE FUNCTION hrm.reject_product_session_mutation();

                    CREATE FUNCTION hrm.guard_product_session_location_issue() RETURNS trigger LANGUAGE plpgsql AS $$
                    DECLARE inserted_xid bigint;
                    BEGIN
                        SELECT xmin::text::bigint INTO inserted_xid FROM hrm.core_product_sessions WHERE id = NEW.session_id;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'The issuing session must be visible in this transaction' USING ERRCODE = '23503';
                        END IF;
                        IF inserted_xid <> (txid_current() % 4294967296) THEN
                            RAISE EXCEPTION 'Session scope is sealed after its issue transaction' USING ERRCODE = '23514';
                        END IF;
                        RETURN NEW;
                    END; $$;
                    CREATE TRIGGER trg_product_session_location_issue BEFORE INSERT ON hrm.core_product_session_locations
                        FOR EACH ROW EXECUTE FUNCTION hrm.guard_product_session_location_issue();

                    CREATE FUNCTION hrm.check_product_session_scope() RETURNS trigger LANGUAGE plpgsql AS $$
                    DECLARE selected_session uuid; item hrm.core_product_sessions%ROWTYPE; branches bigint;
                    BEGIN
                        IF TG_TABLE_NAME = 'core_product_sessions' THEN selected_session := NEW.id;
                        ELSE selected_session := NEW.session_id; END IF;
                        SELECT * INTO item FROM hrm.core_product_sessions WHERE id = selected_session;
                        IF NOT EXISTS (SELECT 1 FROM hrm.core_company_memberships member
                            WHERE member.id = item.membership_id AND member.tenant_id = item.tenant_id
                                AND member.legal_entity_id = item.legal_entity_id AND member.local_user_id = item.principal_id)
                            OR NOT EXISTS (SELECT 1 FROM hrm.external_identity_links link
                            WHERE link.id = item.link_id AND link.tenant_id = item.tenant_id AND link.local_user_id = item.principal_id
                                AND link.issuer = item.issuer AND link.subject = item.subject) THEN
                            RAISE EXCEPTION 'Session identity and membership binding must match' USING ERRCODE = '23514';
                        END IF;
                        SELECT count(*) INTO branches FROM hrm.core_product_session_locations WHERE session_id = selected_session;
                        IF (item.scope_kind = 'company' AND branches <> 0)
                            OR (item.scope_kind = 'locations' AND (branches < 1 OR branches > 128)) THEN
                            RAISE EXCEPTION 'An explicit valid session branch scope is required' USING ERRCODE = '23514';
                        END IF;
                        IF EXISTS (SELECT 1 FROM hrm.core_product_session_locations l JOIN hrm.work_locations w ON w.id = l.location_id
                            WHERE l.session_id = selected_session AND (w.tenant_id <> item.tenant_id OR w.legal_entity_id <> item.legal_entity_id OR w.is_archived)) THEN
                            RAISE EXCEPTION 'Session branches must belong to the selected company' USING ERRCODE = '23514';
                        END IF;
                        IF item.token_hash !~ '^[0-9a-f]{64}$' OR item.scope_hash !~ '^[0-9a-f]{64}$'
                            OR item.credential_hash !~ '^[0-9a-f]{64}$' OR item.browser_hash !~ '^[0-9a-f]{64}$'
                            OR item.request_hash !~ '^[0-9a-f]{64}$' OR item.subject !~ '^[1-9][0-9]{0,18}$' THEN
                            RAISE EXCEPTION 'Invalid session binding format' USING ERRCODE = '23514';
                        END IF;
                        RETURN NEW;
                    END; $$;
                    CREATE CONSTRAINT TRIGGER trg_product_session_scope AFTER INSERT ON hrm.core_product_sessions
                        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hrm.check_product_session_scope();
                    CREATE CONSTRAINT TRIGGER trg_product_session_locations AFTER INSERT ON hrm.core_product_session_locations
                        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION hrm.check_product_session_scope();
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider.Contains("Npgsql", System.StringComparison.Ordinal))
                migrationBuilder.Sql("""
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM hrm.core_product_sessions) THEN
                            RAISE EXCEPTION 'Populated session evidence requires an explicit retention decision' USING ERRCODE = '55000';
                        END IF;
                    END; $$;
                    DROP TRIGGER trg_product_session_scope ON hrm.core_product_sessions;
                    DROP TRIGGER trg_product_session_locations ON hrm.core_product_session_locations;
                    DROP TRIGGER trg_product_session_location_issue ON hrm.core_product_session_locations;
                    DROP TRIGGER trg_product_session_immutable ON hrm.core_product_sessions;
                    DROP TRIGGER trg_product_session_location_immutable ON hrm.core_product_session_locations;
                    DROP TRIGGER trg_product_session_revocation_immutable ON hrm.core_product_session_revocations;
                    DROP FUNCTION hrm.check_product_session_scope();
                    DROP FUNCTION hrm.guard_product_session_location_issue();
                    DROP FUNCTION hrm.reject_product_session_mutation();
                    """);
            migrationBuilder.DropTable(
                name: "core_product_session_locations",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "core_product_session_revocations",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "core_product_sessions",
                schema: "hrm");

        }
    }
}
