using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P29SourcingPolicyIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sourcing_policy_identity_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_record_version_at_action = table.Column<long>(type: "bigint", nullable: false),
                    rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    submitted_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_evidence_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_policy_identity_evidence", x => x.id);
                    table.UniqueConstraint("ak_source_policy_identity_parent", x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.id, x.stage });
                    table.UniqueConstraint("AK_sourcing_policy_identity_evidence_tenant_id_legal_entity_id~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_source_policy_identity", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_record_version_at_action>0 AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0 AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(action_at) AND ((stage='Created' AND policy_record_version_at_action=1 AND created_evidence_id IS NULL AND created_evidence_stage IS NULL AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Published' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NOT NULL AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
                    table.ForeignKey(
                        name: "fk_source_policy_identity_created",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.created_evidence_id, x.created_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "sourcing_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_policy_identity_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_policy_identity_submitted",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.submitted_evidence_id, x.submitted_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "sourcing_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_policy_identity_evidence_tenant_id_legal_entity_i~1",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "created_evidence_id", "created_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_policy_identity_evidence_tenant_id_legal_entity_i~2",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "submitted_evidence_id", "submitted_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_policy_identity_evidence_tenant_id_legal_entity_id~",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_source_policy_identity_created",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                unique: true,
                filter: "stage = 'Created'");

            migrationBuilder.CreateIndex(
                name: "ux_source_policy_identity_published",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                unique: true,
                filter: "stage = 'Published'");

            migrationBuilder.CreateIndex(
                name: "ux_source_policy_identity_revision",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "stage", "policy_record_version_at_action" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lock both the new evidence and its owning policy before testing use.
            // A migration rollback must not erase a retained maker/reviewer identity.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.policy_versions,
                    procurement.sourcing_policy_identity_evidence IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.sourcing_policy_identity_evidence)
                    THEN
                        RAISE EXCEPTION 'SourcingPolicyIdentityRollbackWouldLoseEvidence'
                            USING ERRCODE = '55000';
                    END IF;
                END $guard$;
                """);

            migrationBuilder.DropTable(
                name: "sourcing_policy_identity_evidence",
                schema: "procurement");
        }
    }
}
