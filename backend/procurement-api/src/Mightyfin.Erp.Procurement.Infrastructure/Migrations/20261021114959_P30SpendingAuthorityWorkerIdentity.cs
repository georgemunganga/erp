using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P30SpendingAuthorityWorkerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spending_authority_identity_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    configuration_owner_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_record_version_at_action = table.Column<long>(type: "bigint", nullable: false),
                    business_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_spending_authority_identity_evidence", x => x.id);
                    table.UniqueConstraint("ak_authority_identity_lineage", x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.id, x.stage });
                    table.UniqueConstraint("AK_spending_authority_identity_evidence_tenant_id_legal_entity~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_authority_identity_evidence", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND authority_version>0 AND grant_record_version_at_action>0\nAND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
                    table.ForeignKey(
                        name: "fk_authority_identity_created",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.created_evidence_id, x.created_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "grant_id", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_authority_identity_grant",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_authority_identity_submitted",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id, x.submitted_evidence_id, x.submitted_evidence_stage },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "grant_id", "id", "stage" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_identity_evidence_tenant_id_legal_entit~1",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "submitted_evidence_id", "submitted_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_identity_evidence_tenant_id_legal_entity~",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "created_evidence_id", "created_evidence_stage" });

            migrationBuilder.CreateIndex(
                name: "ux_authority_identity_action_revision",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "stage", "grant_record_version_at_action" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_authority_identity_activation",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id" },
                unique: true,
                filter: "stage = 'Activated'");

            migrationBuilder.CreateIndex(
                name: "ux_authority_identity_origin",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id" },
                unique: true,
                filter: "stage = 'Created'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retained identity is audit evidence. Empty development stores can roll back;
            // used stores require a reviewed recovery plan instead of silently losing it.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.spending_authority_grants,
                    procurement.spending_authority_identity_evidence,
                    procurement.audit_events IN ACCESS EXCLUSIVE MODE;
                DO $authority_identity_down$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.spending_authority_identity_evidence)
                        OR EXISTS (SELECT 1 FROM procurement.audit_events
                            WHERE record_type = 'SpendingAuthorityIdentityRecord') THEN
                        RAISE EXCEPTION USING ERRCODE = '55000',
                            MESSAGE = 'Retained spending authority identity prevents destructive downgrade.';
                    END IF;
                END
                $authority_identity_down$;
                """);
            migrationBuilder.DropTable(
                name: "spending_authority_identity_evidence",
                schema: "procurement");
        }
    }
}
