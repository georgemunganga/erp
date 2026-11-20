using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoreModuleAccessGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_work_locations_tenant_id_id",
                schema: "hrm",
                table: "work_locations",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "core_module_access_versions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_revision = table.Column<int>(type: "integer", nullable: false),
                    module_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    previous_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_revision = table.Column<int>(type: "integer", nullable: false),
                    policy_version = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    role_keys_csv = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    action_codes_csv = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: false),
                    scope_kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    acting_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acting_membership_revision = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_module_access_versions", x => x.id);
                    table.UniqueConstraint("AK_core_module_access_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_core_module_access_versions_tenant_id_legal_entity_id_membe~", x => new { x.tenant_id, x.legal_entity_id, x.membership_id, x.module_code, x.id, x.revision });
                    table.CheckConstraint("CK_module_access_module", "module_code IN ('hrm','procurement')");
                    table.CheckConstraint("CK_module_access_not_archived", "NOT is_archived");
                    table.CheckConstraint("CK_module_access_predecessor", "(revision = 1 AND previous_version_id IS NULL) OR (revision > 1 AND previous_version_id IS NOT NULL)");
                    table.CheckConstraint("CK_module_access_state", "(state = 'granted' AND scope_kind IN ('company','locations') AND length(action_codes_csv) > 0) OR (state = 'revoked' AND scope_kind = 'none' AND role_keys_csv = '' AND action_codes_csv = '')");
                    table.CheckConstraint("CK_module_access_version", "policy_version = 1 AND membership_revision >= 1 AND acting_membership_revision >= 1 AND revision >= 1 AND previous_revision >= 0 AND CAST(revision AS bigint) = CAST(previous_revision AS bigint) + 1");
                    table.ForeignKey(
                        name: "FK_core_module_access_versions_core_company_memberships_tenant~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.acting_membership_id },
                        principalSchema: "hrm",
                        principalTable: "core_company_memberships",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_module_access_versions_core_company_memberships_tenan~1",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.membership_id },
                        principalSchema: "hrm",
                        principalTable: "core_company_memberships",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_module_access_versions_core_module_access_versions_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.membership_id, x.module_code, x.previous_version_id, x.previous_revision },
                        principalSchema: "hrm",
                        principalTable: "core_module_access_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "membership_id", "module_code", "id", "revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "core_module_access_locations",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_version_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_core_module_access_locations", x => x.id);
                    table.CheckConstraint("CK_module_access_location_not_archived", "NOT is_archived");
                    table.ForeignKey(
                        name: "FK_core_module_access_locations_core_module_access_versions_te~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_version_id },
                        principalSchema: "hrm",
                        principalTable: "core_module_access_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_module_access_locations_work_locations_tenant_id_locat~",
                        columns: x => new { x.tenant_id, x.location_id },
                        principalSchema: "hrm",
                        principalTable: "work_locations",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "core_module_access_receipts",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    acting_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_subject = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_version_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_core_module_access_receipts", x => x.id);
                    table.CheckConstraint("CK_module_access_receipt", "result_revision >= 1 AND length(request_hash) = 64 AND NOT is_archived");
                    table.ForeignKey(
                        name: "FK_core_module_access_receipts_core_company_memberships_tenant~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.acting_membership_id },
                        principalSchema: "hrm",
                        principalTable: "core_company_memberships",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_core_module_access_receipts_core_module_access_versions_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.membership_id, x.module_code, x.result_version_id, x.result_revision },
                        principalSchema: "hrm",
                        principalTable: "core_module_access_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "membership_id", "module_code", "id", "revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_locations_tenant_id_grant_version_id_loc~",
                schema: "hrm",
                table: "core_module_access_locations",
                columns: new[] { "tenant_id", "grant_version_id", "location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_locations_tenant_id_legal_entity_id_gran~",
                schema: "hrm",
                table: "core_module_access_locations",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_locations_tenant_id_location_id",
                schema: "hrm",
                table: "core_module_access_locations",
                columns: new[] { "tenant_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_receipts_actor_issuer_actor_subject_comm~",
                schema: "hrm",
                table: "core_module_access_receipts",
                columns: new[] { "actor_issuer", "actor_subject", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_receipts_tenant_id_legal_entity_id_actin~",
                schema: "hrm",
                table: "core_module_access_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "acting_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_receipts_tenant_id_legal_entity_id_membe~",
                schema: "hrm",
                table: "core_module_access_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "membership_id", "module_code", "result_version_id", "result_revision" });

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_versions_tenant_id_legal_entity_id_actin~",
                schema: "hrm",
                table: "core_module_access_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "acting_membership_id" });

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_versions_tenant_id_legal_entity_id_memb~1",
                schema: "hrm",
                table: "core_module_access_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "membership_id", "module_code", "previous_version_id", "previous_revision" });

            migrationBuilder.CreateIndex(
                name: "IX_core_module_access_versions_tenant_id_legal_entity_id_membe~",
                schema: "hrm",
                table: "core_module_access_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "membership_id", "module_code", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep authorization decisions and command receipts recoverable.
            // Roll back application code while retaining this schema once used.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    LOCK TABLE hrm.core_module_access_versions,
                        hrm.core_module_access_locations,
                        hrm.core_module_access_receipts IN ACCESS EXCLUSIVE MODE;
                    IF EXISTS (SELECT 1 FROM hrm.core_module_access_versions)
                        OR EXISTS (SELECT 1 FROM hrm.core_module_access_locations)
                        OR EXISTS (SELECT 1 FROM hrm.core_module_access_receipts) THEN
                        RAISE EXCEPTION 'Cannot remove populated module access evidence; retain schema and roll back application code';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "core_module_access_locations",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "core_module_access_receipts",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "core_module_access_versions",
                schema: "hrm");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_work_locations_tenant_id_id",
                schema: "hrm",
                table: "work_locations");
        }
    }
}
