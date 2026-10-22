using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P23CorePrincipalInvoiceUnitReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "identity_schema_version",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "invoice_unit_review_sources",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_version_at_open = table.Column<long>(type: "bigint", nullable: false),
                    mapping_revision = table.Column<int>(type: "integer", nullable: false),
                    original_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mapping_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    matching_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    maker_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_json = table.Column<string>(type: "text", nullable: false),
                    issued_json = table.Column<string>(type: "text", nullable: false),
                    source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_invoice_unit_review_sources", x => x.id);
                    table.UniqueConstraint("ak_inv_unit_source_case", x => new { x.tenant_id, x.legal_entity_id, x.id, x.core_case_id });
                    table.UniqueConstraint("AK_invoice_unit_review_sources_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_inv_unit_source", "invoice_version_at_open > 0 AND mapping_revision > 0 AND matching_policy_id <> review_policy_id AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND source_sha256 IS NOT NULL AND source_sha256 ~ '^[0-9a-f]{64}$' AND original_sha256 IS NOT NULL AND original_sha256 ~ '^[0-9a-f]{64}$' AND mapping_sha256 IS NOT NULL AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(source_json) BETWEEN 1 AND 262144 AND octet_length(issued_json) BETWEEN 1 AND 262144 AND isfinite(opened_at)");
                    table.ForeignKey(
                        name: "fk_inv_unit_source_mapping",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.mapping_version_id, x.supplier_invoice_id, x.purchase_order_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_match_mapping_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "supplier_invoice_id", "purchase_order_id", "issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inv_unit_source_matching_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.matching_policy_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inv_unit_source_review_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_policy_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_unit_review_assignments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_invoice_unit_review_assignments", x => x.id);
                    table.UniqueConstraint("AK_invoice_unit_review_assignments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_inv_unit_assignment", "core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (worker_id IS NULL OR worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND principal_id <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_inv_unit_assignment_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_unit_review_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_unit_review_outcomes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reviewer_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    evidence_schema_version = table.Column<int>(type: "integer", nullable: false),
                    evidence_json = table.Column<string>(type: "text", nullable: false),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_invoice_unit_review_outcomes", x => x.id);
                    table.UniqueConstraint("AK_invoice_unit_review_outcomes_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_inv_unit_outcome", "(state IN ('Verified','Returned','Rejected') AND evidence_schema_version=3) AND final_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (reviewer_worker_id IS NULL OR reviewer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND reviewer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(completed_at)");
                    table.ForeignKey(
                        name: "fk_inv_unit_outcome_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_unit_review_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_unit_review_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_json = table.Column<string>(type: "text", nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_invoice_unit_review_receipts", x => x.id);
                    table.UniqueConstraint("AK_invoice_unit_review_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_inv_unit_receipt", "operation IN ('Open','Decision') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND fingerprint IS NOT NULL AND fingerprint ~ '^[0-9a-f]{64}$' AND octet_length(response_json) BETWEEN 1 AND 262144 AND octet_length(request_json) BETWEEN 1 AND 65536 AND isfinite(completed_at)");
                    table.ForeignKey(
                        name: "fk_inv_unit_receipt_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.review_source_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_unit_review_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW')\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results",
                sql: "execution_version = 0 OR (execution_version IN (1,2) AND mapping_version_id IS NOT NULL AND purchase_order_id IS NOT NULL AND issued_version_id IS NOT NULL AND invoice_version > 0 AND command_key IS NOT NULL AND (execution_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (execution_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000')) AND invoice_original_sha256 IS NOT NULL AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND input_fingerprint IS NOT NULL AND input_fingerprint ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_json IS NOT NULL AND source_watermarks_json IS NOT NULL AND outcome IN ('Compared','Blocked'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                sql: "mapping_revision > 0 AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND identity_schema_version IN (1,2) AND (identity_schema_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (identity_schema_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'))");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_unit_review_assignments_tenant_id_legal_entity_id_r~",
                schema: "procurement",
                table: "invoice_unit_review_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_inv_unit_assignment",
                schema: "procurement",
                table: "invoice_unit_review_assignments",
                columns: new[] { "tenant_id", "legal_entity_id", "core_assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_unit_review_outcomes_tenant_id_legal_entity_id_revi~",
                schema: "procurement",
                table: "invoice_unit_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_inv_unit_outcome_source",
                schema: "procurement",
                table: "invoice_unit_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_unit_review_receipts_tenant_id_legal_entity_id_revi~",
                schema: "procurement",
                table: "invoice_unit_review_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "review_source_id" });

            migrationBuilder.CreateIndex(
                name: "ux_inv_unit_command_key",
                schema: "procurement",
                table: "invoice_unit_review_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_unit_review_sources_tenant_id_legal_entity_id_mappi~",
                schema: "procurement",
                table: "invoice_unit_review_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "mapping_version_id", "supplier_invoice_id", "purchase_order_id", "issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_unit_review_sources_tenant_id_legal_entity_id_match~",
                schema: "procurement",
                table: "invoice_unit_review_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "matching_policy_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_unit_review_sources_tenant_id_legal_entity_id_revie~",
                schema: "procurement",
                table: "invoice_unit_review_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "review_policy_id" });

            migrationBuilder.CreateIndex(
                name: "ux_inv_unit_source_case",
                schema: "procurement",
                table: "invoice_unit_review_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_inv_unit_source_mapping",
                schema: "procurement",
                table: "invoice_unit_review_sources",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id", "mapping_version_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- SOURCE REVIEW CANDIDATE ONLY. Not an emitted/applied migration.
                -- Must be the FIRST Down operation, in the ordinary EF transaction (no suppressTransaction).
                -- P23 candidate: embed reviewed literal SQL only into the genuinely generated new migration.
                -- Released P01-P22 migration bytes are immutable; this file is not an emitted migration.
                LOCK TABLE procurement.invoice_unit_review_sources,
                    procurement.invoice_unit_review_assignments,
                    procurement.invoice_unit_review_outcomes,
                    procurement.invoice_unit_review_receipts,
                    procurement.invoice_match_mapping_versions,
                    procurement.invoice_match_results,
                    procurement.policy_versions,
                    procurement.operational_policy_identity_evidence,
                    procurement.audit_events,
                    procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $p23_principal_original_unit_review$
                DECLARE retained_core_case boolean := false;
                BEGIN
                    -- Core owns this schema. Standalone Procurement stores may not have it. Never create
                    -- a Core schema/table or delete/alter a case merely to make a downgrade pass.
                    IF to_regclass('core_approval.cases') IS NOT NULL THEN
                        EXECUTE 'LOCK TABLE core_approval.cases IN ACCESS EXCLUSIVE MODE';
                        EXECUTE 'SELECT EXISTS(SELECT 1 FROM core_approval.cases
                            WHERE "SourceModule"=''procurement'' AND "SourceType"=''invoice_unit_review'')'
                            INTO retained_core_case;
                    END IF;
                    IF retained_core_case
                        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_sources)
                        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_assignments)
                        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_outcomes)
                        OR EXISTS(SELECT 1 FROM procurement.invoice_unit_review_receipts)
                        OR EXISTS(SELECT 1 FROM procurement.policy_versions WHERE policy_key='INVOICE_UNIT_REVIEW')
                        OR EXISTS(SELECT 1 FROM procurement.operational_policy_identity_evidence
                            WHERE policy_key='INVOICE_UNIT_REVIEW' OR schema_version<>1 OR actor_worker_id IS NULL)
                        OR EXISTS(SELECT 1 FROM procurement.invoice_match_mapping_versions
                            WHERE identity_schema_version<>1 OR actor_worker_id IS NULL)
                        OR EXISTS(SELECT 1 FROM procurement.invoice_match_results WHERE execution_version NOT IN (0,1))
                        OR EXISTS(SELECT 1 FROM procurement.audit_events
                            WHERE record_type IN ('InvoiceUnitReviewSource','InvoiceUnitReviewAssignmentEvidence',
                                'InvoiceUnitReviewOutcome','InvoiceUnitReviewCommandReceipt')
                                OR (record_type='PolicyVersion' AND changed_fields_json->>'PolicyKey'='INVOICE_UNIT_REVIEW')
                                OR (record_type IN ('OperationalPolicyIdentityRecord','InvoiceMatchMappingVersion','InvoiceMatchResult')
                                    AND changed_fields_json->'principalEvidenceVersion'='2'::jsonb
                                    AND changed_fields_json->>'principalEvidenceKind' IN
                                        ('OperationalPolicyIdentity','InvoiceMappingIdentity','InvoiceMatchExecution')))
                        OR EXISTS(SELECT 1 FROM procurement.outbox_events
                            WHERE aggregate_type IN ('InvoiceUnitReviewSource','InvoiceUnitReviewAssignmentEvidence',
                                'InvoiceUnitReviewOutcome','InvoiceUnitReviewCommandReceipt')
                                OR event_type IN ('invoice.original_units_reviewed.v1','supplier_invoice.compared.v2')
                                OR (aggregate_type IN ('OperationalPolicyIdentityRecord','InvoiceMatchMappingVersion','InvoiceMatchResult','SupplierInvoice')
                                    AND payload_json->'principalEvidenceVersion'='2'::jsonb))
                    THEN
                        RAISE EXCEPTION USING ERRCODE='55000',
                            MESSAGE='Retained principal invoice or policy evidence prevents destructive downgrade.';
                    END IF;
                END;
                $p23_principal_original_unit_review$;
                -- Only after this guard succeeds may generated Down remove the new tables/discriminator,
                -- restore the exact old operational/mapping/execution CHECKs and require legacy workers.
                -- No default/fabricated employee UUID, JSON/hash rewrite, old-row delete or granted exception.
                -- Genuine isolated PG Up/unusedDown/usedDown/audit-only/outbox-only negative proofs are required.
                """);

            migrationBuilder.DropTable(
                name: "invoice_unit_review_assignments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_unit_review_outcomes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_unit_review_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_unit_review_sources",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropColumn(
                name: "identity_schema_version",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version=1 AND policy_key IN ('RECEIVING','INVOICE_MATCHING')\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results",
                sql: "execution_version = 0 OR (execution_version = 1 AND mapping_version_id IS NOT NULL AND purchase_order_id IS NOT NULL AND issued_version_id IS NOT NULL AND invoice_version > 0 AND command_key IS NOT NULL AND actor_worker_id IS NOT NULL AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND invoice_original_sha256 IS NOT NULL AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND input_fingerprint IS NOT NULL AND input_fingerprint ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_json IS NOT NULL AND source_watermarks_json IS NOT NULL AND outcome IN ('Compared','Blocked'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                sql: "mapping_revision > 0 AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid");
        }
    }
}
