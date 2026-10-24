using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P32PrincipalCommercialEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_p32_bid_event_supplier",
                schema: "procurement",
                table: "supplier_bids",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "supplier_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_p32_invite_event_supplier",
                schema: "procurement",
                table: "sourcing_invitations",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "supplier_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_p32_policy_round_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_id", "policy_key", "stage", "rules_sha256" });

            migrationBuilder.CreateTable(
                name: "rfq_commercial_drafts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_identity_schema_version = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    owner_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_envelope_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_rfq_commercial_drafts", x => x.id);
                    table.UniqueConstraint("ak_pr32_draft_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr32_draft", "schema_version=1 AND source_identity_schema_version=2 AND revision>0 AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND owner_principal_id<>'00000000-0000-0000-0000-000000000000' AND (owner_worker_alias_id IS NULL OR owner_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid) AND original_fingerprint ~ '^[0-9a-f]{64}$' AND source_envelope_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_pr32_draft_event",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pr32_draft_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_draft_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rfq_commercial_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_rfq_commercial_revisions", x => x.id);
                    table.UniqueConstraint("ak_pr32_revision_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr32_revision", "schema_version=1 AND revision>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid) AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(snapshot_json) BETWEEN 1 AND 262144");
                    table.ForeignKey(
                        name: "fk_pr32_revision_draft",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id },
                        principalSchema: "procurement",
                        principalTable: "rfq_commercial_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rfq_commercial_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    result_revision = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_rfq_commercial_receipts", x => x.id);
                    table.UniqueConstraint("ak_pr32_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr32_receipt", "schema_version=1 AND result_revision>0 AND operation IN ('Create','Revise') AND response_status IN (200,201) AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_pr32_receipt_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_revision_id },
                        principalSchema: "procurement",
                        principalTable: "rfq_commercial_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rfq_prepared_plans",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_revision = table.Column<long>(type: "bigint", nullable: false),
                    event_version = table.Column<long>(type: "bigint", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    draft_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_policy_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    published_identity_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requirements_pack_required = table.Column<bool>(type: "boolean", nullable: false),
                    document_count = table.Column<int>(type: "integer", nullable: false),
                    document_use_key = table.Column<Guid>(type: "uuid", nullable: false),
                    document_use_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_selection_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    evaluation_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prepared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_rfq_prepared_plans", x => x.id);
                    table.UniqueConstraint("ak_p32_assess_plan", x => new { x.tenant_id, x.legal_entity_id, x.id, x.sourcing_event_id, x.branch_id, x.draft_revision_id, x.evaluation_rules_sha256 });
                    table.UniqueConstraint("ak_p32_prepared_round_source", x => new { x.tenant_id, x.legal_entity_id, x.id, x.sourcing_event_id, x.branch_id, x.snapshot_sha256 });
                    table.UniqueConstraint("ak_pr32_prepared_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr32_prepared", "schema_version=1 AND draft_revision>0 AND event_version>0 AND published_stage='Published' AND document_count BETWEEN 0 AND 100 AND (NOT requirements_pack_required OR document_count>0) AND ((document_count=0 AND document_use_set_id IS NULL AND document_selection_digest IS NULL) OR (document_count>0 AND document_use_set_id IS NOT NULL AND document_use_set_id<>'00000000-0000-0000-0000-000000000000'::uuid AND document_selection_digest IS NOT NULL AND document_selection_digest ~ '^[0-9a-f]{64}$')) AND document_use_key=draft_revision_id AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(snapshot_json) BETWEEN 1 AND 1000000");
                    table.ForeignKey(
                        name: "fk_pr32_prepared_policy_identity",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.published_policy_identity_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pr32_prepared_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_revision_id },
                        principalSchema: "procurement",
                        principalTable: "rfq_commercial_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_round_openings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_policy_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    published_policy_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_policy_identity_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_version = table.Column<long>(type: "bigint", nullable: false),
                    prepared_plan_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_officer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    closing_officer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_round", x => x.id);
                    table.UniqueConstraint("ak_p32_assess_round", x => new { x.tenant_id, x.legal_entity_id, x.id, x.prepared_plan_id, x.sourcing_event_id, x.branch_id, x.snapshot_sha256 });
                    table.UniqueConstraint("ak_p32_round_open_policy", x => new { x.tenant_id, x.legal_entity_id, x.id, x.sourcing_event_id, x.branch_id, x.commercial_policy_version_id, x.published_policy_identity_id, x.policy_rules_sha256, x.snapshot_sha256 });
                    table.UniqueConstraint("ak_p32_round_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.sourcing_event_id, x.branch_id });
                    table.UniqueConstraint("ak_p32_round_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_rfq_commercial_opening", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND commercial_policy_key='SOURCING_COMMERCIAL_ROUND' AND published_policy_identity_stage='Published' AND core_case_id=id AND event_version>0 AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND prepared_plan_id <> '00000000-0000-0000-0000-000000000000'::uuid AND commercial_policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND published_policy_identity_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND closing_officer_principal_id IS NOT NULL AND closing_officer_principal_id <> '00000000-0000-0000-0000-000000000000' AND closing_officer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (closing_officer_worker_alias_id IS NULL OR closing_officer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND prepared_plan_sha256 IS NOT NULL AND prepared_plan_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND opens_at<closes_at AND isfinite(opens_at) AND isfinite(closes_at)");
                    table.ForeignKey(
                        name: "fk_p32_round_event",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_round_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.commercial_policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_round_policy_identity",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.published_policy_identity_id, x.commercial_policy_version_id, x.commercial_policy_key, x.published_policy_identity_stage, x.policy_rules_sha256 },
                        principalSchema: "procurement",
                        principalTable: "operational_policy_identity_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_id", "policy_key", "stage", "rules_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_round_prepared",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.prepared_plan_id, x.sourcing_event_id, x.branch_id, x.prepared_plan_sha256 },
                        principalSchema: "procurement",
                        principalTable: "rfq_prepared_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rfq_preparation_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    prepared_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_command_receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_rfq_preparation_receipts", x => x.id);
                    table.UniqueConstraint("ak_pr32_preparation_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pr32_preparation_receipt", "schema_version=1 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND request_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_pr32_preparation_draft_command",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_command_receipt_id },
                        principalSchema: "procurement",
                        principalTable: "rfq_commercial_receipts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pr32_preparation_plan",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.prepared_plan_id },
                        principalSchema: "procurement",
                        principalTable: "rfq_prepared_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_round_votes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    case_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    case_version = table.Column<long>(type: "bigint", nullable: false),
                    assignment_version = table.Column<long>(type: "bigint", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_p32_vote", x => x.id);
                    table.UniqueConstraint("ak_p32_vote_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id });
                    table.UniqueConstraint("ak_p32_vote_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p32_vote_terminal_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id, x.core_case_id, x.core_decision_id, x.core_assignment_id, x.actor_principal_id });
                    table.CheckConstraint("ck_principal_rfq_commercial_vote", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_case_id=opening_id AND core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND action IN ('Approve','Return','Reject') AND case_state IN ('Pending','Approved','Returned','Rejected') AND case_version>0 AND assignment_version>0 AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND isfinite(decided_at)");
                    table.ForeignKey(
                        name: "fk_p32_vote_round_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_round_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_vote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opening_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    core_outcome_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_decision", x => x.id);
                    table.UniqueConstraint("ak_p32_decision_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id });
                    table.UniqueConstraint("ak_p32_decision_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p32_decision_vote_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.terminal_vote_id, x.opening_id, x.sourcing_event_id, x.branch_id });
                    table.CheckConstraint("ck_principal_rfq_commercial_decision", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND terminal_vote_id <> '00000000-0000-0000-0000-000000000000'::uuid AND state IN ('Approved','Returned','Rejected') AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND opening_sha256 IS NOT NULL AND opening_sha256 ~ '^[0-9a-f]{64}$' AND core_outcome_sha256 IS NOT NULL AND core_outcome_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(decided_at)");
                    table.ForeignKey(
                        name: "fk_p32_decision_event",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_decision_round_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_terminal_vote_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.terminal_vote_id, x.opening_id, x.sourcing_event_id, x.branch_id, x.core_case_id, x.core_decision_id, x.core_assignment_id, x.actor_principal_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_votes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "core_case_id", "core_decision_id", "core_assignment_id", "actor_principal_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_offer_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_bid_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_revision = table.Column<int>(type: "integer", nullable: false),
                    bid_version = table.Column<long>(type: "bigint", nullable: false),
                    original_document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_document_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_document_use_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_document_selection_digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorder_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    recorder_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reported_received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_offer", x => x.id);
                    table.UniqueConstraint("ak_p32_assess_offer", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id, x.snapshot_sha256 });
                    table.UniqueConstraint("ak_p32_offer_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id });
                    table.UniqueConstraint("ak_p32_offer_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_rfq_commercial_offer", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_bid_id <> '00000000-0000-0000-0000-000000000000'::uuid AND invitation_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND approved_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND draft_revision>0 AND bid_version>0 AND original_document_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND original_document_sha256 IS NOT NULL AND original_document_sha256 ~ '^[0-9a-f]{64}$' AND original_document_use_set_id <> '00000000-0000-0000-0000-000000000000'::uuid AND original_document_selection_digest IS NOT NULL AND original_document_selection_digest ~ '^[0-9a-f]{64}$' AND recorder_principal_id IS NOT NULL AND recorder_principal_id <> '00000000-0000-0000-0000-000000000000' AND recorder_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (recorder_worker_alias_id IS NULL OR recorder_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND reported_received_at<=submitted_at AND isfinite(reported_received_at) AND isfinite(submitted_at)");
                    table.ForeignKey(
                        name: "fk_p32_offer_approved_decision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approved_decision_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_offer_bid_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_bid_id, x.sourcing_event_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_bids",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "supplier_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_offer_invite_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.invitation_id, x.sourcing_event_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_invitations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "supplier_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_offer_supplier",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_round_seals",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    officer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    officer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submission_count = table.Column<int>(type: "integer", nullable: false),
                    submission_set_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_seal", x => x.id);
                    table.UniqueConstraint("ak_p32_seal_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id });
                    table.UniqueConstraint("ak_p32_seal_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p32_seal_set", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id, x.submission_count, x.submission_set_sha256 });
                    table.CheckConstraint("ck_principal_rfq_commercial_seal", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND approved_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND officer_principal_id IS NOT NULL AND officer_principal_id <> '00000000-0000-0000-0000-000000000000' AND officer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (officer_worker_alias_id IS NULL OR officer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND submission_count BETWEEN 0 AND 500 AND submission_set_sha256 IS NOT NULL AND submission_set_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(closed_at)");
                    table.ForeignKey(
                        name: "fk_p32_seal_approved_decision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approved_decision_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opening_target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vote_target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    offer_target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seal_target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_p32_receipt", x => x.id);
                    table.UniqueConstraint("ak_p32_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_rfq_commercial_receipt", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND target_id IS NOT NULL AND target_id <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND request_sha256 IS NOT NULL AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 IS NOT NULL AND response_sha256 ~ '^[0-9a-f]{64}$' AND isfinite(completed_at) AND ((operation='Open' AND opening_target_id IS NOT NULL AND opening_target_id=target_id AND vote_target_id IS NULL AND decision_target_id IS NULL AND offer_target_id IS NULL AND seal_target_id IS NULL AND response_status=201) OR (operation='Decide' AND vote_target_id IS NOT NULL AND vote_target_id=target_id AND opening_target_id IS NULL AND offer_target_id IS NULL AND seal_target_id IS NULL AND response_status=200) OR (operation='SubmitOffer' AND offer_target_id IS NOT NULL AND offer_target_id=target_id AND opening_target_id IS NULL AND vote_target_id IS NULL AND decision_target_id IS NULL AND seal_target_id IS NULL AND response_status=201) OR (operation='Close' AND seal_target_id IS NOT NULL AND seal_target_id=target_id AND opening_target_id IS NULL AND vote_target_id IS NULL AND decision_target_id IS NULL AND offer_target_id IS NULL AND response_status=200))");
                    table.ForeignKey(
                        name: "fk_p32_receipt_decision_vote",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.decision_target_id, x.vote_target_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "terminal_vote_id", "opening_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_receipt_offer_target",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.offer_target_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_offer_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_receipt_open_target",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_target_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_receipt_round_owner",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_receipt_seal_target",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.seal_target_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_seals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_receipt_vote_target",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.vote_target_id, x.opening_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_votes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_offer_openings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_policy_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    approved_round_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    officer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    officer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    offer_count = table.Column<int>(type: "integer", nullable: false),
                    offer_set_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_unseal", x => x.id);
                    table.UniqueConstraint("ak_p32_assess_opened", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id, x.snapshot_sha256 });
                    table.UniqueConstraint("ak_p32_unseal_command", x => new { x.tenant_id, x.legal_entity_id, x.id, x.opening_id, x.sourcing_event_id, x.branch_id, x.officer_principal_id, x.command_key, x.request_sha256 });
                    table.UniqueConstraint("ak_p32_unseal_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p32_unseal", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND seal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND published_policy_identity_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND officer_principal_id IS NOT NULL AND officer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND officer_principal_id<>'00000000-0000-0000-0000-000000000000' AND (officer_worker_alias_id IS NULL OR officer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND offer_count BETWEEN 0 AND 500 AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND approved_round_sha256 IS NOT NULL AND approved_round_sha256 ~ '^[0-9a-f]{64}$' AND request_sha256 IS NOT NULL AND request_sha256 ~ '^[0-9a-f]{64}$' AND offer_set_sha256 IS NOT NULL AND offer_set_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_json IS NOT NULL AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex') AND reason IS NOT NULL AND reference IS NOT NULL AND isfinite(opened_at)");
                    table.ForeignKey(
                        name: "fk_p32_unseal_exact_seal",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.seal_id, x.opening_id, x.sourcing_event_id, x.branch_id, x.offer_count, x.offer_set_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_seals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "submission_count", "submission_set_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_unseal_round_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.opening_id, x.sourcing_event_id, x.branch_id, x.policy_version_id, x.published_policy_identity_id, x.policy_rules_sha256, x.approved_round_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id", "commercial_policy_version_id", "published_policy_identity_id", "policy_rules_sha256", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_commercial_offer_opening_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_p32_unseal_receipt", x => x.id);
                    table.UniqueConstraint("ak_p32_unseal_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p32_unseal_receipt", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND response_status=200 AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND request_sha256 IS NOT NULL AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 IS NOT NULL AND response_sha256 ~ '^[0-9a-f]{64}$' AND response_json IS NOT NULL AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')");
                    table.ForeignKey(
                        name: "fk_p32_unseal_receipt_command",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.unsealing_id, x.opening_id, x.sourcing_event_id, x.branch_id, x.actor_principal_id, x.command_key, x.request_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_offer_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "officer_principal_id", "command_key", "request_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_criterion_assessments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prepared_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_plan_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_definition_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    approved_round_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opening_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    offer_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    criterion_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evaluator_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    evaluator_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    previous_assessment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_revision = table.Column<int>(type: "integer", nullable: true),
                    score = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    passed = table.Column<bool>(type: "boolean", nullable: true),
                    rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    original_document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_document_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_assess", x => x.id);
                    table.UniqueConstraint("ak_p32_assess_ack", x => new { x.tenant_id, x.legal_entity_id, x.id, x.round_id, x.unsealing_id, x.offer_id, x.event_id, x.branch_id, x.evaluator_principal_id, x.command_key, x.request_sha256 });
                    table.UniqueConstraint("ak_p32_assess_prev", x => new { x.tenant_id, x.legal_entity_id, x.id, x.round_id, x.offer_id, x.criterion_code, x.evaluator_principal_id, x.revision });
                    table.UniqueConstraint("ak_p32_assess_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p32_assess", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND round_id <> '00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id <> '00000000-0000-0000-0000-000000000000'::uuid AND offer_id <> '00000000-0000-0000-0000-000000000000'::uuid AND event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND prepared_plan_id <> '00000000-0000-0000-0000-000000000000'::uuid AND evaluation_plan_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND original_document_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND evaluator_principal_id IS NOT NULL AND evaluator_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND evaluator_principal_id <> '00000000-0000-0000-0000-000000000000' AND (evaluator_worker_alias_id IS NULL OR evaluator_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND revision BETWEEN 1 AND 100 AND ((revision=1 AND previous_assessment_id IS NULL AND previous_revision IS NULL) OR (revision>1 AND previous_assessment_id IS NOT NULL AND previous_revision IS NOT NULL AND previous_revision=revision-1)) AND criterion_code IS NOT NULL AND length(criterion_code)>0 AND kind IS NOT NULL AND rationale IS NOT NULL AND length(btrim(rationale))>0 AND ((kind='Scored' AND score IS NOT NULL AND score>=0 AND passed IS NULL) OR (kind='PassFail' AND score IS NULL AND passed IS NOT NULL)) AND evaluation_definition_sha256 IS NOT NULL AND evaluation_definition_sha256 ~ '^[a-f0-9]{64}$' AND approved_round_sha256 IS NOT NULL AND approved_round_sha256 ~ '^[a-f0-9]{64}$' AND opening_sha256 IS NOT NULL AND opening_sha256 ~ '^[a-f0-9]{64}$' AND offer_sha256 IS NOT NULL AND offer_sha256 ~ '^[a-f0-9]{64}$' AND original_document_sha256 IS NOT NULL AND original_document_sha256 ~ '^[a-f0-9]{64}$' AND request_sha256 IS NOT NULL AND request_sha256 ~ '^[a-f0-9]{64}$' AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[a-f0-9]{64}$' AND snapshot_json IS NOT NULL AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex') AND recorded_at IS NOT NULL AND isfinite(recorded_at)");
                    table.ForeignKey(
                        name: "fk_p32_assess_offer",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.offer_id, x.round_id, x.event_id, x.branch_id, x.offer_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_offer_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_assess_opened",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.unsealing_id, x.round_id, x.event_id, x.branch_id, x.opening_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_offer_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_assess_plan",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.prepared_plan_id, x.event_id, x.branch_id, x.evaluation_plan_revision_id, x.evaluation_definition_sha256 },
                        principalSchema: "procurement",
                        principalTable: "rfq_prepared_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id", "draft_revision_id", "evaluation_rules_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_assess_prev",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.previous_assessment_id, x.round_id, x.offer_id, x.criterion_code, x.evaluator_principal_id, x.previous_revision },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_criterion_assessments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "round_id", "offer_id", "criterion_code", "evaluator_principal_id", "revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p32_assess_round",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.round_id, x.prepared_plan_id, x.event_id, x.branch_id, x.approved_round_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "prepared_plan_id", "sourcing_event_id", "branch_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_criterion_assessment_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_assess_ack", x => x.id);
                    table.UniqueConstraint("ak_p32_assess_ack_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p32_assess_ack", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND assessment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND round_id <> '00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id <> '00000000-0000-0000-0000-000000000000'::uuid AND offer_id <> '00000000-0000-0000-0000-000000000000'::uuid AND event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND request_sha256 IS NOT NULL AND request_sha256 ~ '^[a-f0-9]{64}$' AND response_sha256 IS NOT NULL AND response_sha256 ~ '^[a-f0-9]{64}$' AND response_json IS NOT NULL AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')");
                    table.ForeignKey(
                        name: "fk_p32_assess_ack",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.assessment_id, x.round_id, x.unsealing_id, x.offer_id, x.event_id, x.branch_id, x.actor_principal_id, x.command_key, x.request_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_criterion_assessments",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "round_id", "unsealing_id", "offer_id", "event_id", "branch_id", "evaluator_principal_id", "command_key", "request_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_p32_bid_exact_invitation",
                schema: "procurement",
                table: "supplier_bids",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "supplier_id", "invitation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_published_identity_target",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "policy_version_id", "stage", "rules_sha256", "evidence_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_original_source_tuple",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_ref", "original_draft_fingerprint", "payload_sha256", "schema_version" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW','SOURCING_COMMERCIAL_ROUND') AND (policy_key NOT IN ('MANUAL_PAYMENT_REVIEW','SOURCING_COMMERCIAL_ROUND') OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_command_receipts_tenant_id_legal_~1",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_target_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_command_receipts_tenant_id_legal_~2",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "offer_target_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_command_receipts_tenant_id_legal_~3",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "seal_target_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_command_receipts_tenant_id_legal_~4",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "vote_target_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_command_receipts_tenant_id_legal_~5",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "decision_target_id", "vote_target_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_command_receipts_tenant_id_legal_e~",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_receipt_key",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_receipt_target",
                schema: "procurement",
                table: "principal_rfq_commercial_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_p32_unseal_receipt_command",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_opening_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "unsealing_id", "opening_id", "sourcing_event_id", "branch_id", "actor_principal_id", "command_key", "request_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_unseal_receipt_key",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_opening_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_unseal_receipt_target",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_opening_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "unsealing_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_p32_unseal_exact_seal",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "seal_id", "opening_id", "sourcing_event_id", "branch_id", "offer_count", "offer_set_sha256" });

            migrationBuilder.CreateIndex(
                name: "ix_p32_unseal_round_policy",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "sourcing_event_id", "branch_id", "policy_version_id", "published_policy_identity_id", "policy_rules_sha256", "approved_round_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_unseal_key",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_unseal_round",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_offer_submissions_tenant_id_legal_~",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_offer_submissions_tenant_id_legal~1",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "invitation_id", "sourcing_event_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_offer_submissions_tenant_id_legal~2",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_bid_id", "sourcing_event_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_offer_submissions_tenant_id_legal~3",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "approved_decision_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_offer_bid",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "supplier_bid_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_offer_supplier",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "supplier_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_decisions_tenant_id_legal_e~1",
                schema: "procurement",
                table: "principal_rfq_commercial_round_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_decisions_tenant_id_legal_e~2",
                schema: "procurement",
                table: "principal_rfq_commercial_round_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "terminal_vote_id", "opening_id", "sourcing_event_id", "branch_id", "core_case_id", "core_decision_id", "core_assignment_id", "actor_principal_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_decisions_tenant_id_legal_en~",
                schema: "procurement",
                table: "principal_rfq_commercial_round_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_decision_core",
                schema: "procurement",
                table: "principal_rfq_commercial_round_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_decision_round",
                schema: "procurement",
                table: "principal_rfq_commercial_round_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_openings_tenant_id_legal_en~1",
                schema: "procurement",
                table: "principal_rfq_commercial_round_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_openings_tenant_id_legal_en~2",
                schema: "procurement",
                table: "principal_rfq_commercial_round_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "prepared_plan_id", "sourcing_event_id", "branch_id", "prepared_plan_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_openings_tenant_id_legal_en~3",
                schema: "procurement",
                table: "principal_rfq_commercial_round_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "published_policy_identity_id", "commercial_policy_version_id", "commercial_policy_key", "published_policy_identity_stage", "policy_rules_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_openings_tenant_id_legal_ent~",
                schema: "procurement",
                table: "principal_rfq_commercial_round_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "commercial_policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_round_core_case",
                schema: "procurement",
                table: "principal_rfq_commercial_round_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_round_plan",
                schema: "procurement",
                table: "principal_rfq_commercial_round_openings",
                columns: new[] { "tenant_id", "legal_entity_id", "prepared_plan_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_seals_tenant_id_legal_entity~",
                schema: "procurement",
                table: "principal_rfq_commercial_round_seals",
                columns: new[] { "tenant_id", "legal_entity_id", "approved_decision_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_seal_round",
                schema: "procurement",
                table: "principal_rfq_commercial_round_seals",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_commercial_round_votes_tenant_id_legal_entity~",
                schema: "procurement",
                table: "principal_rfq_commercial_round_votes",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_vote_assignment",
                schema: "procurement",
                table: "principal_rfq_commercial_round_votes",
                columns: new[] { "tenant_id", "legal_entity_id", "opening_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_vote_core_decision",
                schema: "procurement",
                table: "principal_rfq_commercial_round_votes",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_criterion_assessment_receipts_tenant_id_legal~",
                schema: "procurement",
                table: "principal_rfq_criterion_assessment_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "assessment_id", "round_id", "unsealing_id", "offer_id", "event_id", "branch_id", "actor_principal_id", "command_key", "request_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_assess_ack_key",
                schema: "procurement",
                table: "principal_rfq_criterion_assessment_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_assess_ack_target",
                schema: "procurement",
                table: "principal_rfq_criterion_assessment_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "assessment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_criterion_assessments_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "principal_rfq_criterion_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "offer_id", "round_id", "event_id", "branch_id", "offer_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_criterion_assessments_tenant_id_legal_entity~1",
                schema: "procurement",
                table: "principal_rfq_criterion_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "prepared_plan_id", "event_id", "branch_id", "evaluation_plan_revision_id", "evaluation_definition_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_criterion_assessments_tenant_id_legal_entity~2",
                schema: "procurement",
                table: "principal_rfq_criterion_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "round_id", "prepared_plan_id", "event_id", "branch_id", "approved_round_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_criterion_assessments_tenant_id_legal_entity~3",
                schema: "procurement",
                table: "principal_rfq_criterion_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "unsealing_id", "round_id", "event_id", "branch_id", "opening_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_criterion_assessments_tenant_id_legal_entity~4",
                schema: "procurement",
                table: "principal_rfq_criterion_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "previous_assessment_id", "round_id", "offer_id", "criterion_code", "evaluator_principal_id", "previous_revision" });

            migrationBuilder.CreateIndex(
                name: "ux_p32_assess_key",
                schema: "procurement",
                table: "principal_rfq_criterion_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p32_assess_revision",
                schema: "procurement",
                table: "principal_rfq_criterion_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "round_id", "offer_id", "criterion_code", "evaluator_principal_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_commercial_drafts_tenant_id_legal_entity_id_source_evid~",
                schema: "procurement",
                table: "rfq_commercial_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "source_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pr32_draft_owner_tuple",
                schema: "procurement",
                table: "rfq_commercial_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "source_evidence_id", "branch_id", "owner_principal_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_one_event_draft",
                schema: "procurement",
                table: "rfq_commercial_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_commercial_receipts_tenant_id_legal_entity_id_draft_rev~",
                schema: "procurement",
                table: "rfq_commercial_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_revision_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pr32_original_command",
                schema: "procurement",
                table: "rfq_commercial_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_preparation_command_target",
                schema: "procurement",
                table: "rfq_commercial_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "draft_revision_id", "sourcing_event_id", "branch_id", "actor_principal_id", "command_key", "request_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_draft_revision",
                schema: "procurement",
                table: "rfq_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_head_revision_target",
                schema: "procurement",
                table: "rfq_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "revision", "snapshot_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_prepared_revision_target",
                schema: "procurement",
                table: "rfq_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "sourcing_event_id", "source_evidence_id", "branch_id", "actor_principal_id", "snapshot_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_revision_owner_tuple",
                schema: "procurement",
                table: "rfq_commercial_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "sourcing_event_id", "source_evidence_id", "branch_id", "command_key", "actor_principal_id", "snapshot_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_preparation_receipts_tenant_id_legal_entity_id_draft_co~",
                schema: "procurement",
                table: "rfq_preparation_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_command_receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rfq_preparation_receipts_tenant_id_legal_entity_id_prepared~",
                schema: "procurement",
                table: "rfq_preparation_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "prepared_plan_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pr32_preparation_command",
                schema: "procurement",
                table: "rfq_preparation_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rfq_prepared_plans_tenant_id_legal_entity_id_published_poli~",
                schema: "procurement",
                table: "rfq_prepared_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "published_policy_identity_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pr32_prepared_owner_tuple",
                schema: "procurement",
                table: "rfq_prepared_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "draft_revision_id", "sourcing_event_id", "branch_id", "actor_principal_id", "snapshot_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pr32_prepared_revision",
                schema: "procurement",
                table: "rfq_prepared_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_revision_id" },
                unique: true);
            // Authored exact ownership companions, installed only after all
            // genuine EF tables and nonpartial unique target indexes exist.
            migrationBuilder.Sql(RfqCommercialDraftOwnershipConstraintSql.Install);
            migrationBuilder.Sql(RfqPreparationOwnershipConstraintSql.Install);
            migrationBuilder.Sql(PrincipalRfqCommercialOfferInvitationOwnershipSql.Install);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retained business/audit history must veto before any DDL executes.
            migrationBuilder.Sql(P32CommercialRoundRollbackGuards.BeforeDownSql);
            migrationBuilder.Sql(PrincipalRfqCommercialOfferInvitationOwnershipSql.Remove);
            migrationBuilder.Sql(RfqPreparationOwnershipConstraintSql.Remove);
            migrationBuilder.Sql(RfqCommercialDraftOwnershipConstraintSql.Remove);
            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_offer_opening_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_criterion_assessment_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "rfq_preparation_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_criterion_assessments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "rfq_commercial_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_offer_submissions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_offer_openings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_round_seals",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_round_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_round_votes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_commercial_round_openings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "rfq_prepared_plans",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "rfq_commercial_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "rfq_commercial_drafts",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_p32_bid_event_supplier",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropIndex(
                name: "ux_p32_bid_exact_invitation",
                schema: "procurement",
                table: "supplier_bids");

            migrationBuilder.DropIndex(
                name: "ux_pr32_published_identity_target",
                schema: "procurement",
                table: "sourcing_policy_identity_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_p32_invite_event_supplier",
                schema: "procurement",
                table: "sourcing_invitations");

            migrationBuilder.DropIndex(
                name: "ux_pr32_original_source_tuple",
                schema: "procurement",
                table: "sourcing_draft_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_p32_policy_round_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW') AND (policy_key <> 'MANUAL_PAYMENT_REVIEW' OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");
        }
    }
}
