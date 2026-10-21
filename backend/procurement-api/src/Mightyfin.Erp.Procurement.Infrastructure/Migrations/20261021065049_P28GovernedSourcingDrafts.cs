using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P28GovernedSourcingDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_ref",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "sourcing_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "method_id",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "method_revision",
                schema: "procurement",
                table: "sourcing_events",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_draft_fingerprint",
                schema: "procurement",
                table: "sourcing_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_evidence_id",
                schema: "procurement",
                table: "sourcing_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sourcing_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_schema_version = table.Column<int>(type: "integer", nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
                    command_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_command_receipts", x => x.id);
                    table.UniqueConstraint("ak_rfq_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_rfq_receipt_identity", "operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND response_schema_version=1 AND result_version>0 AND command_fingerprint ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND ((operation='Create' AND response_status_code=201) OR (operation='Cancel' AND response_status_code=200))");
                    table.ForeignKey(
                        name: "FK_sourcing_command_receipts_sourcing_events_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_draft_evidence",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    maker_subject_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    maker_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    original_draft_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_sourcing_draft_evidence", x => x.id);
                    table.UniqueConstraint("ak_rfq_evidence_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_rfq_original_envelope", "schema_version=1 AND payload_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND create_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_sourcing_draft_evidence_sourcing_events_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sourcing_demand_bindings",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_version = table.Column<long>(type: "bigint", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_version = table.Column<long>(type: "bigint", nullable: false),
                    core_approval_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_outcome_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_decision_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_rules_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    finance_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finance_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method_revision = table.Column<long>(type: "bigint", nullable: false),
                    method_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    minimum_quote_count = table.Column<int>(type: "integer", nullable: false),
                    maker_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_estimate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estimated_value = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    category_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    department_ref = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_sourcing_demand_bindings", x => x.id);
                    table.UniqueConstraint("ak_rfq_binding_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_rfq_binding_value", "target_type='SourcingEvent' AND quantity>0 AND unit_estimate>=0 AND estimated_value=quantity*unit_estimate AND request_version>=submitted_version AND submitted_version>0 AND method_revision>0 AND minimum_quote_count BETWEEN 0 AND 20 AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_budget_check_snapshots_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.finance_snapshot_id },
                        principalSchema: "procurement",
                        principalTable: "budget_check_snapshots",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_procurement_methods_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.method_id },
                        principalSchema: "procurement",
                        principalTable: "procurement_methods",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_allocations_tenan~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.allocation_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_allocations",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_approval_outcomes~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.approval_outcome_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_outcomes",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_approval_submissi~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_approval_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_purchase_request_lines_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_request_id, x.purchase_request_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_request_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_sourcing_draft_evidence_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_evidence_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_draft_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_sourcing_events_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_events",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sourcing_demand_bindings_sourcing_lines_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_line_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_rfq_line_exact_source",
                schema: "procurement",
                table: "sourcing_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "purchase_request_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_events_tenant_id_legal_entity_id_method_id",
                schema: "procurement",
                table: "sourcing_events",
                columns: new[] { "tenant_id", "legal_entity_id", "method_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')))");

            migrationBuilder.CreateIndex(
                name: "ux_rfq_allocation_exact_target",
                schema: "procurement",
                table: "purchase_request_allocations",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_request_id", "purchase_request_line_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_command_receipts_tenant_id_legal_entity_id_sourcin~",
                schema: "procurement",
                table: "sourcing_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "ux_rfq_receipt_command",
                schema: "procurement",
                table: "sourcing_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "operation", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_approval~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "approval_outcome_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_finance_~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "finance_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_method_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "method_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_purchase~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_request_id", "purchase_request_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_source_e~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "source_evidence_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sourcing_demand_bindings_tenant_id_legal_entity_id_submissi~",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "ux_rfq_binding_allocation",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "allocation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_binding_line",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_binding_source",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id", "purchase_request_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_evidence_create_key",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "create_command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_evidence_event",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_rfq_evidence_exact_event",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_sourcing_events_procurement_methods_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "sourcing_events",
                columns: new[] { "tenant_id", "legal_entity_id", "method_id" },
                principalSchema: "procurement",
                principalTable: "procurement_methods",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            // Frozen exact ownership overlays; mutable relation fields never become EF principal keys.
            migrationBuilder.Sql("""
                ALTER TABLE procurement.sourcing_events ADD CONSTRAINT fk_rfq_event_original
                  FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,id)
                  REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_original
                  FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,sourcing_event_id)
                  REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_line
                  FOREIGN KEY (tenant_id,legal_entity_id,sourcing_line_id,sourcing_event_id,purchase_request_line_id)
                  REFERENCES procurement.sourcing_lines (tenant_id,legal_entity_id,id,sourcing_event_id,purchase_request_line_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.sourcing_demand_bindings ADD CONSTRAINT fk_rfq_binding_exact_hold
                  FOREIGN KEY (tenant_id,legal_entity_id,allocation_id,purchase_request_id,purchase_request_line_id,target_type,sourcing_event_id)
                  REFERENCES procurement.purchase_request_allocations (tenant_id,legal_entity_id,id,purchase_request_id,purchase_request_line_id,target_type,target_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lock original demand first, matching command ownership order, before checking evidence.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.purchase_requests, procurement.purchase_request_lines,
                    procurement.purchase_request_allocations, procurement.sourcing_events,
                    procurement.sourcing_lines, procurement.sourcing_draft_evidence,
                    procurement.sourcing_demand_bindings, procurement.sourcing_command_receipts,
                    procurement.audit_events, procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.sourcing_events
                        WHERE execution_version <> 0 OR branch_ref IS NOT NULL
                           OR maker_worker_id IS NOT NULL OR method_id IS NOT NULL
                           OR method_revision IS NOT NULL OR source_evidence_id IS NOT NULL
                           OR original_draft_fingerprint IS NOT NULL)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_draft_evidence)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_demand_bindings)
                      OR EXISTS (SELECT 1 FROM procurement.sourcing_command_receipts)
                      OR EXISTS (SELECT 1 FROM procurement.audit_events
                        WHERE record_type IN ('SourcingDraftEvidence','SourcingDemandBinding','SourcingCommandReceipt')
                           OR (record_type='SourcingEvent' AND action IN ('DraftReserved','DraftCancelled'))
                           OR COALESCE(changed_fields_json::text,'') ~
                            '"(SourceEvidenceId|OriginalDraftFingerprint|sourceEvidenceId|originalDraftFingerprint)"')
                      OR EXISTS (SELECT 1 FROM procurement.outbox_events
                        WHERE event_type IN ('sourcing.draft_reserved.v1','sourcing.draft_cancelled.v1')
                           OR COALESCE(payload_json::text,'') ~
                            '"(SourceEvidenceId|OriginalDraftFingerprint|sourceEvidenceId|originalDraftFingerprint)"')
                    THEN
                        RAISE EXCEPTION 'GovernedSourcingRollbackWouldLoseEvidence';
                    END IF;
                END $guard$;
                ALTER TABLE procurement.sourcing_events DROP CONSTRAINT fk_rfq_event_original;
                ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_original;
                ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_exact_line;
                ALTER TABLE procurement.sourcing_demand_bindings DROP CONSTRAINT fk_rfq_binding_exact_hold;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_sourcing_events_procurement_methods_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropTable(
                name: "sourcing_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_demand_bindings",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "sourcing_draft_evidence",
                schema: "procurement");

            migrationBuilder.DropIndex(
                name: "ux_rfq_line_exact_source",
                schema: "procurement",
                table: "sourcing_lines");

            migrationBuilder.DropIndex(
                name: "IX_sourcing_events_tenant_id_legal_entity_id_method_id",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropIndex(
                name: "ux_rfq_allocation_exact_target",
                schema: "procurement",
                table: "purchase_request_allocations");

            migrationBuilder.DropColumn(
                name: "branch_ref",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "method_id",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "method_revision",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "original_draft_fingerprint",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropColumn(
                name: "source_evidence_id",
                schema: "procurement",
                table: "sourcing_events");
        }
    }
}
