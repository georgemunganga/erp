using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M5aPrincipalOrderGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts");

            migrationBuilder.AlterColumn<string>(
                name: "operation",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateTable(
                name: "principal_order_funding_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    resulting_order_version = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manual_commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approved_manual_policy_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_reservations_json = table.Column<string>(type: "text", nullable: false),
                    attested_gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    buyer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    recorded_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_by_organisation_wide = table.Column<bool>(type: "boolean", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_funding_evidence", x => x.id);
                    table.UniqueConstraint("AK_principal_order_funding_evidence_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_funding", "schema_version=2 AND commercial_revision=1 AND attested_gross_amount>=0 AND length(btrim(manual_commitment_reference))>0 AND length(btrim(external_evidence_reference))>0 AND length(btrim(approved_manual_policy_reference))>0 AND currency_code ~ '^[A-Z]{3}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND recorded_by_principal_id IS NOT NULL AND recorded_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND recorded_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (recorded_by_worker_alias_id IS NULL OR recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_principal_order_funding_evidence_command", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_principal_order_funding_evidence_result_version", "resulting_order_version>0");
                    table.ForeignKey(
                        name: "FK_principal_order_funding_evidence_purchase_orders_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_release_requests",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_core_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    core_cancellation_decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    requested_order_version = table.Column<long>(type: "bigint", nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    requested_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_release_requests", x => x.id);
                    table.UniqueConstraint("AK_principal_order_release_requests_tenant_id_legal_entity_id_~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_release_request", "schema_version=2 AND ((original_status='Draft' AND commercial_revision=0) OR (original_status IN ('PendingApproval','Returned','Rejected','Approved') AND commercial_revision=1)) AND ((original_status='PendingApproval' AND cancelled_core_case_id IS NOT NULL AND cancelled_core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid AND core_cancellation_decision_id IS NOT NULL AND core_cancellation_decision_id<>'00000000-0000-0000-0000-000000000000'::uuid) OR (original_status<>'PendingApproval' AND cancelled_core_case_id IS NULL AND core_cancellation_decision_id IS NULL)) AND requested_order_version>0 AND length(btrim(reason))>0 AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND requested_by_principal_id IS NOT NULL AND requested_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND requested_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (requested_by_worker_alias_id IS NULL OR requested_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_principal_order_release_requests_command", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_principal_order_release_requests_principal_order_funding_ev~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_release_requests_purchase_orders_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    submitted_row_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_decision_json = table.Column<string>(type: "text", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    buyer_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    line_base_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_submissions", x => x.id);
                    table.UniqueConstraint("AK_principal_order_submissions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_submission", "schema_version=2 AND commercial_revision=1 AND submitted_row_version>1 AND policy_version_number>0 AND line_base_amount>0 AND gross_amount>=line_base_amount AND currency_code ~ '^[A-Z]{3}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND buyer_principal_id IS NOT NULL AND buyer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_principal_order_submissions_command", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_principal_order_submissions_policy_versions_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_submissions_principal_order_funding_evidenc~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_submissions_purchase_orders_tenant_id_legal~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_release_confirmations",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    resulting_order_version = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    external_release_evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approved_manual_policy_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    restored_source_reservations_json = table.Column<string>(type: "text", nullable: false),
                    released_gross_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    confirmed_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    confirmed_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_release_confirmations", x => x.id);
                    table.UniqueConstraint("AK_principal_order_release_confirmations_tenant_id_legal_entit~", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_release_confirmation", "schema_version=2 AND released_gross_amount>=0 AND currency_code ~ '^[A-Z]{3}$' AND length(btrim(manual_commitment_reference))>0 AND length(btrim(external_release_evidence_reference))>0 AND length(btrim(approved_manual_policy_reference))>0 AND confirmed_by_principal_id IS NOT NULL AND confirmed_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND confirmed_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (confirmed_by_worker_alias_id IS NULL OR confirmed_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_principal_order_release_confirmations_command", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_principal_order_release_confirmations_result_version", "resulting_order_version>0");
                    table.ForeignKey(
                        name: "FK_principal_order_release_confirmations_principal_order_fundi~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_release_confirmations_principal_order_relea~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.release_request_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_release_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_release_confirmations_purchase_orders_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_assignments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_revision = table.Column<long>(type: "bigint", nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authority_evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_principal_order_assignments", x => x.id);
                    table.UniqueConstraint("AK_principal_order_assignments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_assignment", "schema_version=2 AND grant_revision>0 AND authority_version>0 AND authority_evidence_sha256 ~ '^[0-9a-f]{64}$' AND principal_id IS NOT NULL AND principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND principal_id <> '00000000-0000-0000-0000-000000000000' AND (worker_alias_id IS NULL OR worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_principal_order_assignments_principal_order_submissions_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_assignments_spending_authority_grants_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_principal_order_decisions", x => x.id);
                    table.UniqueConstraint("AK_principal_order_decisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_decision", "schema_version=2 AND action IN ('Approve','Return','Reject') AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (action='Approve' OR (reason IS NOT NULL AND length(btrim(reason))>0)) AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_principal_order_decisions_command", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_principal_order_decisions_principal_order_submissions_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_issued_versions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    resulting_order_version = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commercial_revision = table.Column<int>(type: "integer", nullable: false),
                    commercial_snapshot_json = table.Column<string>(type: "text", nullable: false),
                    commercial_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    original_direct_source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    manual_commitment_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    authorized_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    authorized_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_issued_versions", x => x.id);
                    table.UniqueConstraint("AK_principal_order_issued_versions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_issued", "schema_version=2 AND commercial_revision=1 AND commercial_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND length(btrim(manual_commitment_reference))>0 AND authorized_by_principal_id IS NOT NULL AND authorized_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND authorized_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (authorized_by_worker_alias_id IS NULL OR authorized_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_principal_order_issued_versions_command", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_principal_order_issued_versions_result_version", "resulting_order_version>0");
                    table.ForeignKey(
                        name: "FK_principal_order_issued_versions_principal_order_funding_evi~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.funding_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_funding_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_issued_versions_principal_order_submissions~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_issued_versions_purchase_orders_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_order_acknowledgments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_manual_dispatch_policy_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    resulting_order_version = table.Column<long>(type: "bigint", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    channel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    supplier_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    recorded_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    recorded_by_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_principal_order_acknowledgments", x => x.id);
                    table.UniqueConstraint("AK_principal_order_acknowledgments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_principal_order_acknowledgment", "schema_version=2 AND outcome IN ('Accepted','Rejected') AND length(btrim(approved_manual_dispatch_policy_reference))>0 AND observed_at<=recorded_at AND length(btrim(channel))>0 AND length(btrim(supplier_reference))>0 AND length(btrim(evidence_reference))>0 AND recorded_by_principal_id IS NOT NULL AND recorded_by_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND recorded_by_principal_id <> '00000000-0000-0000-0000-000000000000' AND (recorded_by_worker_alias_id IS NULL OR recorded_by_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.CheckConstraint("ck_principal_order_acknowledgments_command", "command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("ck_principal_order_acknowledgments_result_version", "resulting_order_version>0");
                    table.ForeignKey(
                        name: "FK_principal_order_acknowledgments_principal_order_issued_vers~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_principal_order_acknowledgments_purchase_orders_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_orders",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                sql: "schema_version IN (1,2) AND (schema_version=2 OR (actor_worker_id IS NOT NULL AND recipient_worker_id IS NOT NULL)) AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (schema_version=1 OR ((action='PurchaseRequest.Approve' AND permission='procurement-approval-decide')\n    OR (action='SourcingEvent.Publish' AND permission='procurement-sourcing-publish')\n    OR (action='PurchaseOrder.Approve' AND permission='procurement-po-approve')))\nAND authority_version>0 AND grant_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (recipient_worker_id IS NULL OR recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders",
                sql: "(principal_draft_schema_version=0 AND original_direct_source_sha256 IS NULL) OR (principal_draft_schema_version=2 AND branch_ref IS NOT NULL AND buyer_subject_id IS NOT NULL AND buyer_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_subject_id <> '00000000-0000-0000-0000-000000000000' AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND finance_commitment_ref IS NULL AND ((status IN ('Draft','Cancelled','ReleasePending') AND approval_instance_ref IS NULL AND submitted_at IS NULL AND current_revision=0) OR (status IN ('PendingApproval','Approved','Returned','Rejected','ReleasePending','Cancelled','IssuePending','Issued','Disputed') AND approval_instance_ref IS NOT NULL AND submitted_at IS NOT NULL AND current_revision=1)) AND ((status='Issued' AND issued_at IS NOT NULL) OR (status<>'Issued' AND issued_at IS NULL)) AND ((status='Cancelled' AND closed_at IS NOT NULL) OR (status<>'Cancelled' AND closed_at IS NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                sql: "schema_version=2 AND operation IN ('Create','Cancel','Funding','Submit','Decide','Issue','Ack','ReleaseRequest','ReleaseConfirm') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND ((operation IN ('Create','Submit','Issue') AND response_status=201) OR (operation IN ('Cancel','Funding','Decide','Ack','ReleaseRequest','ReleaseConfirm') AND response_status=200))");

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_acknowledgments_tenant_id_legal_entity_id_c~",
                schema: "procurement",
                table: "principal_order_acknowledgments",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_acknowledgments_tenant_id_legal_entity_id_i~",
                schema: "procurement",
                table: "principal_order_acknowledgments",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_acknowledgments_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_order_acknowledgments",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_assignments_tenant_id_legal_entity_id_core_~",
                schema: "procurement",
                table: "principal_order_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_assignments_tenant_id_legal_entity_id_grant~",
                schema: "procurement",
                table: "principal_order_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_assignments_tenant_id_legal_entity_id_submi~",
                schema: "procurement",
                table: "principal_order_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_decisions_tenant_id_legal_entity_id_command~",
                schema: "procurement",
                table: "principal_order_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_decisions_tenant_id_legal_entity_id_core_de~",
                schema: "procurement",
                table: "principal_order_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_decisions_tenant_id_legal_entity_id_submiss~",
                schema: "procurement",
                table: "principal_order_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_funding_evidence_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "principal_order_funding_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_funding_evidence_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "principal_order_funding_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_issued_versions_tenant_id_legal_entity_id_c~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_issued_versions_tenant_id_legal_entity_id_f~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_issued_versions_tenant_id_legal_entity_id_p~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_issued_versions_tenant_id_legal_entity_id_s~",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_confirmations_tenant_id_legal_enti~1",
                schema: "procurement",
                table: "principal_order_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_confirmations_tenant_id_legal_enti~2",
                schema: "procurement",
                table: "principal_order_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_confirmations_tenant_id_legal_enti~3",
                schema: "procurement",
                table: "principal_order_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "release_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_confirmations_tenant_id_legal_entit~",
                schema: "procurement",
                table: "principal_order_release_confirmations",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_requests_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "principal_order_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_requests_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "principal_order_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_release_requests_tenant_id_legal_entity_id~2",
                schema: "procurement",
                table: "principal_order_release_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_comma~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_core_~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_approval_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_fundi~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "funding_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_polic~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_order_submissions_tenant_id_legal_entity_id_purch~",
                schema: "procurement",
                table: "principal_order_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_id", "commercial_revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(M5aPrincipalOrderGovernanceGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "principal_order_acknowledgments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_assignments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_release_confirmations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_issued_versions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_release_requests",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_submissions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_order_funding_evidence",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts");

            migrationBuilder.AlterColumn<string>(
                name: "operation",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                sql: "schema_version IN (1,2) AND (schema_version=2 OR (actor_worker_id IS NOT NULL AND recipient_worker_id IS NOT NULL)) AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (schema_version=1 OR ((action='PurchaseRequest.Approve' AND permission='procurement-approval-decide')\n    OR (action='SourcingEvent.Publish' AND permission='procurement-sourcing-publish')))\nAND authority_version>0 AND grant_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (recipient_worker_id IS NULL OR recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_draft_version",
                schema: "procurement",
                table: "purchase_orders",
                sql: "(principal_draft_schema_version=0 AND original_direct_source_sha256 IS NULL) OR (principal_draft_schema_version=2 AND branch_ref IS NOT NULL AND buyer_subject_id IS NOT NULL AND buyer_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_subject_id <> '00000000-0000-0000-0000-000000000000' AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled') AND finance_commitment_ref IS NULL AND approval_instance_ref IS NULL AND submitted_at IS NULL AND issued_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_po_principal_receipt",
                schema: "procurement",
                table: "principal_order_draft_receipts",
                sql: "schema_version=2 AND operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND ((operation='Create' AND response_status=201) OR (operation='Cancel' AND response_status=200))");
        }
    }
}
