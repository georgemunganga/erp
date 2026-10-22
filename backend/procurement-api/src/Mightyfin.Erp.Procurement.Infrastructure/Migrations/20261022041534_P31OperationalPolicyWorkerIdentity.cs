using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P31OperationalPolicyWorkerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_operational_policy_identity_owner",
                schema: "procurement",
                table: "policy_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "policy_key", "policy_version_number" });

            migrationBuilder.CreateTable(
                name: "operational_policy_identity_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_operational_policy_identity_evidence", x => x.id);
                    table.UniqueConstraint("AK_operational_policy_identity_evidence_tenant_id_legal_entity~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_operational_policy_identity_lineage", x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.id, x.stage });
                    table.CheckConstraint("ck_operational_policy_identity", "schema_version=1 AND policy_key IN ('RECEIVING','INVOICE_MATCHING')\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
                    table.ForeignKey(
                        name: "fk_operational_policy_identity_created",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.created_evidence_id, x.created_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "operational_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operational_policy_identity_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_key", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operational_policy_identity_submitted",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.submitted_evidence_id, x.submitted_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "operational_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operational_policy_identity_evidence_tenant_id_legal_entit~1",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "created_evidence_id", "created_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_policy_identity_evidence_tenant_id_legal_entit~2",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "submitted_evidence_id", "submitted_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_operational_policy_identity_evidence_tenant_id_legal_entity~",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_operational_policy_identity_created",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key" },
                unique: true,
                filter: "stage = 'Created'");

            migrationBuilder.CreateIndex(
                name: "ux_operational_policy_identity_published",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key" },
                unique: true,
                filter: "stage = 'Published'");

            migrationBuilder.CreateIndex(
                name: "ux_operational_policy_identity_revision",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "stage", "policy_record_version_at_action" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Test and drop under the same lock scope; retained audit also vetoes
            // removal when an owner has selectively removed the action rows.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.policy_versions,
                    procurement.operational_policy_identity_evidence,
                    procurement.audit_events IN ACCESS EXCLUSIVE MODE;
                DO $operational_policy_identity_down$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.operational_policy_identity_evidence)
                        OR EXISTS (SELECT 1 FROM procurement.audit_events
                            WHERE record_type = 'OperationalPolicyIdentityRecord')
                    THEN
                        RAISE EXCEPTION USING ERRCODE = '55000',
                            MESSAGE = 'Retained operational policy identity prevents destructive downgrade.';
                    END IF;
                END
                $operational_policy_identity_down$;
                """);

            migrationBuilder.DropTable(
                name: "operational_policy_identity_evidence",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_operational_policy_identity_owner",
                schema: "procurement",
                table: "policy_versions");
        }
    }
}
