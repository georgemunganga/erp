using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M4PrincipalRfqPublicationReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "sourcing_publication_openings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_version_at_opening = table.Column<long>(type: "bigint", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    published_policy_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_draft_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_sourcing_publication_openings", x => x.id);
                    table.UniqueConstraint("AK_sourcing_publication_openings_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_publication_opening_v2", "schema_version=2 AND event_version_at_opening>0 AND policy_version_number>0 AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND published_policy_evidence_id <> '00000000-0000-0000-0000-000000000000'::uuid AND maker_principal_id IS NOT NULL AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (maker_core_linked_worker_id IS NULL OR maker_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(opened_at)");
                    table.ForeignKey(
                        name: "FK_sourcing_publication_openings_policy_versions_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_openings_sourcing_events_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_openings_sourcing_policy_identity_evid~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.published_policy_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_publication_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    case_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_version_at_decision = table.Column<long>(type: "bigint", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_sourcing_publication_decisions", x => x.id);
                    table.UniqueConstraint("AK_sourcing_publication_decisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_publication_decision_v2", "schema_version=2 AND action IN ('Approve','Return','Reject') AND case_state IN ('Pending','Approved','Returned','Rejected') AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_approval_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND event_version_at_decision>0 AND isfinite(decided_at)");
                    table.ForeignKey(
                        name: "FK_sourcing_publication_decisions_sourcing_events_tenant_id_le~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_decisions_sourcing_publication_opening~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_publication_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_sourcing_publication_receipts", x => x.id);
                    table.UniqueConstraint("AK_sourcing_publication_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_sourcing_publication_receipt_v2", "schema_version=2 AND operation IN ('Open','Decide') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND isfinite(completed_at) AND ((operation='Open' AND decision_id IS NULL AND response_status_code=201) OR (operation='Decide' AND decision_id IS NOT NULL AND response_status_code=200))");
                    table.ForeignKey(
                        name: "FK_sourcing_publication_receipts_sourcing_events_tenant_id_leg~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_receipts_sourcing_publication_decision~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.decision_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_publication_receipts_sourcing_publication_openings~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_publication_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                sql: "schema_version IN (1,2) AND (schema_version=2 OR (actor_worker_id IS NOT NULL AND recipient_worker_id IS NOT NULL)) AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (schema_version=1 OR ((action='PurchaseRequest.Approve' AND permission='procurement-approval-decide')\n    OR (action='SourcingEvent.Publish' AND permission='procurement-sourcing-publish')))\nAND authority_version>0 AND grant_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (recipient_worker_id IS NULL OR recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_record_version_at_action>0 AND actor_principal_id IS NOT NULL AND ((schema_version=1 AND actor_worker_id IS NOT NULL AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0) OR (schema_version=2 AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')) AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(action_at) AND ((stage='Created' AND policy_record_version_at_action=1 AND created_evidence_id IS NULL AND created_evidence_stage IS NULL AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Published' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NOT NULL AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1,2) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')) OR (execution_version=2 AND source_evidence_id IS NOT NULL AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','PublicationReview','Published','Cancelled')))");

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_co~",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_op~",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_decisions_tenant_id_legal_entity_id_so~",
                schema: "procurement",
                table: "sourcing_publication_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_cor~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_pol~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_pub~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "published_policy_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_openings_tenant_id_legal_entity_id_sou~",
                schema: "procurement",
                table: "sourcing_publication_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "event_version_at_opening" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_com~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_dec~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "decision_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_ope~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_publication_receipts_tenant_id_legal_entity_id_sou~",
                schema: "procurement",
                table: "sourcing_publication_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(M4PrincipalRfqPublicationGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "sourcing_publication_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_publication_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_publication_openings",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
            // EF supplies a temporary default for the nullable-to-required
            // transition. The retained schema had no default on this column.
            migrationBuilder.Sql("ALTER TABLE procurement.sourcing_policy_identity_evidence ALTER COLUMN actor_worker_id DROP DEFAULT;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                sql: "schema_version IN (1,2) AND (schema_version=2 OR (actor_worker_id IS NOT NULL AND recipient_worker_id IS NOT NULL)) AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (schema_version=1 OR (action='PurchaseRequest.Approve' AND permission='procurement-approval-decide'))\nAND authority_version>0 AND grant_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (recipient_worker_id IS NULL OR recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_source_policy_identity",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                sql: "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_record_version_at_action>0 AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(actor_principal_id))>0 AND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(action_at) AND ((stage='Created' AND policy_record_version_at_action=1 AND created_evidence_id IS NULL AND created_evidence_stage IS NULL AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL) OR (stage='Published' AND created_evidence_id IS NOT NULL AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created' AND submitted_evidence_id IS NOT NULL AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1,2) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')) OR (execution_version=2 AND source_evidence_id IS NOT NULL AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')))");
        }
    }
}
