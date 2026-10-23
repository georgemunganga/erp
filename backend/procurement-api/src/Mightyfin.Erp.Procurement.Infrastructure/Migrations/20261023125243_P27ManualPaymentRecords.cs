using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P27ManualPaymentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manual_payment_requests",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    creator_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    creator_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approver_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    approver_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorder_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    recorder_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    confirmer_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    approved_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    approved_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    approved_by_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    external_method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recorded_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    recorded_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    recorded_by_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    confirmed_by_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evidence_document_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_manual_payment_requests", x => x.id);
                    table.UniqueConstraint("ak_manual_payment_request_branch", x => new { x.tenant_id, x.legal_entity_id, x.id, x.branch_id });
                    table.UniqueConstraint("AK_manual_payment_requests_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_manual_payment_approval", "(approved_amount IS NULL AND approved_by_principal_id IS NULL AND approved_at IS NULL AND approved_by_core_linked_worker_id IS NULL) OR (approved_amount IS NOT NULL AND approved_by_principal_id IS NOT NULL AND approved_amount = amount AND approved_by_principal_id = approver_principal_id AND approved_at IS NOT NULL AND approved_at >= created_at)");
                    table.CheckConstraint("ck_manual_payment_confirmation", "(confirmed_by_principal_id IS NULL AND confirmed_at IS NULL AND confirmed_by_core_linked_worker_id IS NULL) OR (confirmed_by_principal_id IS NOT NULL AND confirmed_by_principal_id = confirmer_principal_id AND confirmed_at IS NOT NULL AND recorded_at IS NOT NULL AND confirmed_at >= recorded_at)");
                    table.CheckConstraint("ck_manual_payment_identity", "creator_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND creator_principal_id <> '00000000-0000-0000-0000-000000000000' AND approver_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND approver_principal_id <> '00000000-0000-0000-0000-000000000000' AND recorder_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND recorder_principal_id <> '00000000-0000-0000-0000-000000000000' AND confirmer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND confirmer_principal_id <> '00000000-0000-0000-0000-000000000000' AND (creator_core_linked_worker_id IS NULL OR creator_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (approver_core_linked_worker_id IS NULL OR approver_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (recorder_core_linked_worker_id IS NULL OR recorder_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (confirmer_core_linked_worker_id IS NULL OR confirmer_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (approved_by_core_linked_worker_id IS NULL OR approved_by_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (recorded_by_core_linked_worker_id IS NULL OR recorded_by_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (confirmed_by_core_linked_worker_id IS NULL OR confirmed_by_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND creator_principal_id <> approver_principal_id AND recorder_principal_id <> confirmer_principal_id AND (creator_core_linked_worker_id IS NULL OR approver_core_linked_worker_id IS NULL OR creator_core_linked_worker_id <> approver_core_linked_worker_id) AND (recorder_core_linked_worker_id IS NULL OR confirmer_core_linked_worker_id IS NULL OR recorder_core_linked_worker_id <> confirmer_core_linked_worker_id)");
                    table.CheckConstraint("ck_manual_payment_recording", "(recorded_amount IS NULL AND recorded_by_principal_id IS NULL AND recorded_at IS NULL AND recorded_by_core_linked_worker_id IS NULL AND paid_on IS NULL AND external_method IS NULL AND external_reference IS NULL) OR (recorded_amount IS NOT NULL AND recorded_by_principal_id IS NOT NULL AND external_method IS NOT NULL AND external_reference IS NOT NULL AND recorded_amount = amount AND recorded_by_principal_id = recorder_principal_id AND recorded_at IS NOT NULL AND approved_at IS NOT NULL AND recorded_at >= approved_at AND paid_on IS NOT NULL AND length(btrim(external_method)) > 0 AND length(btrim(external_reference)) > 0)");
                    table.CheckConstraint("ck_manual_payment_state_evidence", "(status NOT IN ('Approved','PaymentRecorded','ConfirmedManually','Disputed') OR approved_at IS NOT NULL) AND (status NOT IN ('PaymentRecorded','ConfirmedManually','Disputed') OR recorded_at IS NOT NULL) AND ((status = 'ConfirmedManually') = (confirmed_at IS NOT NULL)) AND (status IN ('PaymentRecorded','ConfirmedManually','Disputed') OR recorded_at IS NULL) AND (status NOT IN ('Draft','PendingApproval','Rejected') OR approved_at IS NULL) AND (status NOT IN ('Rejected','Cancelled','Disputed') OR (closure_reason IS NOT NULL AND length(btrim(closure_reason)) > 0))");
                    table.CheckConstraint("ck_manual_payment_values", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND amount > 0 AND version > 0 AND currency_code ~ '^[A-Z]{3}$' AND status IN ('Draft','PendingApproval','Approved','PaymentRecorded','ConfirmedManually','Rejected','Cancelled','Disputed')");
                    table.ForeignKey(
                        name: "FK_manual_payment_requests_supplier_invoices_tenant_id_legal_e~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_invoice_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_invoices",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_payment_requests_suppliers_tenant_id_legal_entity_id~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manual_payment_decisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_payment_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    from_version = table.Column<long>(type: "bigint", nullable: false),
                    to_version = table.Column<long>(type: "bigint", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    previous_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    new_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    recorded_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    previous_approver_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    new_approver_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    previous_approver_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_approver_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_recorder_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    new_recorder_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    previous_recorder_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_recorder_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_confirmer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    new_confirmer_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    previous_confirmer_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_confirmer_core_linked_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    external_method = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    evidence_document_ref_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_manual_payment_decisions", x => x.id);
                    table.UniqueConstraint("AK_manual_payment_decisions_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_manual_payment_transition_version", x => new { x.tenant_id, x.legal_entity_id, x.manual_payment_request_id, x.to_version });
                    table.CheckConstraint("ck_manual_payment_action_states", "(action = 'Create' AND from_status = 'None' AND to_status = 'Draft' AND from_version = 0) OR (from_version > 0 AND ((action = 'Revise' AND from_status = 'Draft' AND to_status = 'Draft') OR (action = 'Submit' AND from_status = 'Draft' AND to_status = 'PendingApproval') OR (action = 'Approve' AND from_status = 'PendingApproval' AND to_status = 'Approved') OR (action = 'Return' AND from_status = 'PendingApproval' AND to_status = 'Draft') OR (action = 'Reject' AND from_status = 'PendingApproval' AND to_status = 'Rejected') OR (action = 'Record' AND from_status = 'Approved' AND to_status = 'PaymentRecorded') OR (action = 'Confirm' AND from_status = 'PaymentRecorded' AND to_status = 'ConfirmedManually') OR (action = 'Cancel' AND from_status IN ('Draft','PendingApproval','Approved') AND to_status = 'Cancelled') OR (action = 'Dispute' AND from_status = 'PaymentRecorded' AND to_status = 'Disputed')))");
                    table.CheckConstraint("ck_manual_payment_transition", "branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND version = 1 AND from_version >= 0 AND to_version = from_version + 1 AND action IN ('Create','Revise','Submit','Approve','Return','Reject','Record','Confirm','Cancel','Dispute') AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_core_linked_worker_id IS NULL OR actor_core_linked_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                    table.ForeignKey(
                        name: "FK_manual_payment_decisions_manual_payment_requests_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.manual_payment_request_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "manual_payment_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "manual_payment_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_payment_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    result_version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_manual_payment_receipts", x => x.id);
                    table.UniqueConstraint("AK_manual_payment_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_manual_payment_receipt", "branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND version = 1 AND result_version > 0 AND action IN ('Create','Revise','Submit','Approve','Return','Reject','Record','Confirm','Cancel','Dispute') AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND request_sha256 ~ '^[0-9a-f]{64}$' AND jsonb_typeof(response_json::jsonb) = 'object'");
                    table.ForeignKey(
                        name: "FK_manual_payment_receipts_manual_payment_decisions_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.manual_payment_request_id, x.result_version },
                        principalSchema: "procurement",
                        principalTable: "manual_payment_decisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "manual_payment_request_id", "to_version" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_manual_payment_receipts_manual_payment_requests_tenant_id_l~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.manual_payment_request_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "manual_payment_requests",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_decisions_tenant_id_legal_entity_id_created_~",
                schema: "procurement",
                table: "manual_payment_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_decisions_tenant_id_legal_entity_id_manual_p~",
                schema: "procurement",
                table: "manual_payment_decisions",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_request_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_receipts_tenant_id_legal_entity_id_manual_p~1",
                schema: "procurement",
                table: "manual_payment_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_request_id", "result_version" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_receipts_tenant_id_legal_entity_id_manual_pa~",
                schema: "procurement",
                table: "manual_payment_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "manual_payment_request_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_manual_payment_actor_command",
                schema: "procurement",
                table: "manual_payment_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_tenant_id_legal_entity_id_branch_id~",
                schema: "procurement",
                table: "manual_payment_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "manual_payment_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_tenant_id_legal_entity_id_creator_p~",
                schema: "procurement",
                table: "manual_payment_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "creator_principal_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_tenant_id_legal_entity_id_supplier_~",
                schema: "procurement",
                table: "manual_payment_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_tenant_id_legal_entity_id_supplier~1",
                schema: "procurement",
                table: "manual_payment_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_invoice_id" });
            migrationBuilder.Sql("""
                ALTER TABLE procurement.manual_payment_requests ADD CONSTRAINT ck_manual_payment_finite_amounts CHECK (
                    amount <> 'NaN'::numeric AND (approved_amount IS NULL OR approved_amount <> 'NaN'::numeric)
                    AND (recorded_amount IS NULL OR recorded_amount <> 'NaN'::numeric));
                ALTER TABLE procurement.manual_payment_receipts ADD CONSTRAINT ck_manual_payment_ack_reference CHECK (
                    response_json::jsonb ?& ARRAY['id','version','status']
                    AND jsonb_typeof(response_json::jsonb -> 'id') = 'string'
                    AND jsonb_typeof(response_json::jsonb -> 'version') = 'number'
                    AND jsonb_typeof(response_json::jsonb -> 'status') = 'string'
                    AND (response_json::jsonb ->> 'id')::uuid = manual_payment_request_id
                    AND (response_json::jsonb ->> 'version')::bigint = result_version);
                ALTER TABLE procurement.manual_payment_receipts ADD CONSTRAINT uq_manual_payment_receipt_version
                    UNIQUE (tenant_id, legal_entity_id, manual_payment_request_id, result_version);
                CREATE FUNCTION procurement.reject_manual_payment_evidence_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Manual payment history and receipts are append-only' USING ERRCODE = '23514'; END; $$;
                CREATE TRIGGER manual_payment_decision_append_only BEFORE UPDATE OR DELETE ON procurement.manual_payment_decisions
                    FOR EACH ROW EXECUTE FUNCTION procurement.reject_manual_payment_evidence_mutation();
                CREATE TRIGGER manual_payment_receipt_append_only BEFORE UPDATE OR DELETE ON procurement.manual_payment_receipts
                    FOR EACH ROW EXECUTE FUNCTION procurement.reject_manual_payment_evidence_mutation();
                CREATE FUNCTION procurement.guard_manual_payment_request() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Manual payment requests must be cancelled, never deleted' USING ERRCODE = '23514';
                    END IF;
                    IF ROW(NEW.id, NEW.tenant_id, NEW.legal_entity_id, NEW.branch_id, NEW.supplier_invoice_id, NEW.supplier_id,
                        NEW.currency_code, NEW.creator_principal_id, NEW.creator_core_linked_worker_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM ROW(OLD.id, OLD.tenant_id, OLD.legal_entity_id, OLD.branch_id, OLD.supplier_invoice_id, OLD.supplier_id,
                        OLD.currency_code, OLD.creator_principal_id, OLD.creator_core_linked_worker_id, OLD.created_at, OLD.created_by)
                       OR NEW.version <> OLD.version + 1 OR OLD.status IN ('ConfirmedManually','Rejected','Cancelled','Disputed') THEN
                        RAISE EXCEPTION 'Manual payment original evidence or terminal state is immutable' USING ERRCODE = '23514';
                    END IF;
                    IF OLD.status <> 'Draft' AND ROW(NEW.amount, NEW.purpose, NEW.approver_principal_id, NEW.approver_core_linked_worker_id,
                        NEW.recorder_principal_id, NEW.recorder_core_linked_worker_id, NEW.confirmer_principal_id, NEW.confirmer_core_linked_worker_id)
                       IS DISTINCT FROM ROW(OLD.amount, OLD.purpose, OLD.approver_principal_id, OLD.approver_core_linked_worker_id,
                        OLD.recorder_principal_id, OLD.recorder_core_linked_worker_id, OLD.confirmer_principal_id, OLD.confirmer_core_linked_worker_id) THEN
                        RAISE EXCEPTION 'Submitted manual payment terms are immutable' USING ERRCODE = '23514';
                    END IF;
                    IF OLD.approved_at IS NOT NULL AND ROW(NEW.approved_amount, NEW.approved_by_principal_id, NEW.approved_by_core_linked_worker_id, NEW.approved_at)
                       IS DISTINCT FROM ROW(OLD.approved_amount, OLD.approved_by_principal_id, OLD.approved_by_core_linked_worker_id, OLD.approved_at) THEN
                        RAISE EXCEPTION 'Manual payment approval evidence is immutable' USING ERRCODE = '23514';
                    END IF;
                    IF OLD.recorded_at IS NOT NULL AND ROW(NEW.recorded_amount, NEW.recorded_by_principal_id, NEW.recorded_by_core_linked_worker_id,
                        NEW.recorded_at, NEW.paid_on, NEW.external_method, NEW.external_reference, NEW.evidence_document_ref_id)
                       IS DISTINCT FROM ROW(OLD.recorded_amount, OLD.recorded_by_principal_id, OLD.recorded_by_core_linked_worker_id,
                        OLD.recorded_at, OLD.paid_on, OLD.external_method, OLD.external_reference, OLD.evidence_document_ref_id) THEN
                        RAISE EXCEPTION 'Recorded external payment facts are immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER manual_payment_request_immutable BEFORE UPDATE OR DELETE ON procurement.manual_payment_requests
                    FOR EACH ROW EXECUTE FUNCTION procurement.guard_manual_payment_request();
                CREATE FUNCTION procurement.require_manual_payment_command_bundle() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE previous_state text; previous_version bigint; command_actor text; v_previous_amount numeric;
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        previous_state := 'None'; previous_version := 0; command_actor := NEW.created_by;
                        IF NEW.created_by IS DISTINCT FROM NEW.creator_principal_id THEN
                            RAISE EXCEPTION 'Manual payment creation actor must be its creator' USING ERRCODE = '23514';
                        END IF;
                    ELSE
                        previous_state := OLD.status; previous_version := OLD.version; command_actor := NEW.updated_by; v_previous_amount := OLD.amount;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM procurement.manual_payment_decisions d
                        JOIN procurement.manual_payment_receipts r ON r.tenant_id = d.tenant_id AND r.legal_entity_id = d.legal_entity_id
                            AND r.manual_payment_request_id = d.manual_payment_request_id AND r.result_version = d.to_version
                            AND r.branch_id = d.branch_id AND r.actor_principal_id = d.actor_principal_id AND r.action = d.action
                        WHERE d.tenant_id = NEW.tenant_id AND d.legal_entity_id = NEW.legal_entity_id
                            AND d.manual_payment_request_id = NEW.id AND d.branch_id = NEW.branch_id
                            AND d.to_version = NEW.version AND d.to_status = NEW.status
                            AND d.from_status = previous_state AND d.from_version = previous_version
                            AND d.actor_principal_id = command_actor
                            AND ((d.action IN ('Create','Revise','Submit') AND d.actor_principal_id = NEW.creator_principal_id)
                                OR (d.action IN ('Approve','Return','Reject') AND d.actor_principal_id = NEW.approver_principal_id)
                                OR (d.action = 'Record' AND d.actor_principal_id = NEW.recorder_principal_id)
                                OR (d.action IN ('Confirm','Dispute') AND d.actor_principal_id = NEW.confirmer_principal_id)
                                OR (d.action = 'Cancel' AND d.actor_principal_id IN (NEW.creator_principal_id, NEW.approver_principal_id)))
                            AND (d.action NOT IN ('Create','Revise') OR (d.new_amount = NEW.amount
                                AND (d.action <> 'Revise' OR d.previous_amount = v_previous_amount)))
                            AND r.response_json::jsonb ->> 'status' = d.to_status) THEN
                        RAISE EXCEPTION 'A manual payment transition and exact command receipt must commit together' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END; $$;
                CREATE CONSTRAINT TRIGGER manual_payment_command_bundle AFTER INSERT OR UPDATE ON procurement.manual_payment_requests
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.require_manual_payment_command_bundle();
                CREATE FUNCTION procurement.require_manual_payment_evidence_bundle() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM procurement.manual_payment_requests p
                        JOIN procurement.manual_payment_decisions d ON d.tenant_id = p.tenant_id AND d.legal_entity_id = p.legal_entity_id
                            AND d.manual_payment_request_id = p.id AND d.branch_id = p.branch_id
                        JOIN procurement.manual_payment_receipts r ON r.tenant_id = d.tenant_id AND r.legal_entity_id = d.legal_entity_id
                            AND r.manual_payment_request_id = d.manual_payment_request_id AND r.branch_id = d.branch_id
                            AND r.result_version = d.to_version AND r.actor_principal_id = d.actor_principal_id AND r.action = d.action
                        WHERE p.tenant_id = NEW.tenant_id AND p.legal_entity_id = NEW.legal_entity_id
                            AND p.id = NEW.manual_payment_request_id AND p.version >= d.to_version
                            AND ((TG_TABLE_NAME = 'manual_payment_decisions' AND d.id = NEW.id)
                                OR (TG_TABLE_NAME = 'manual_payment_receipts' AND r.id = NEW.id))
                            AND r.response_json::jsonb ->> 'status' = d.to_status) THEN
                        RAISE EXCEPTION 'Unmatched manual payment history or receipt is forbidden' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END; $$;
                CREATE CONSTRAINT TRIGGER manual_payment_history_bundle AFTER INSERT ON procurement.manual_payment_decisions
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.require_manual_payment_evidence_bundle();
                CREATE CONSTRAINT TRIGGER manual_payment_receipt_bundle AFTER INSERT ON procurement.manual_payment_receipts
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION procurement.require_manual_payment_evidence_bundle();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.manual_payment_requests)
                        OR EXISTS (SELECT 1 FROM procurement.manual_payment_decisions)
                        OR EXISTS (SELECT 1 FROM procurement.manual_payment_receipts)
                        OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE record_type IN
                            ('ManualPaymentRequest','ManualPaymentDecision','ManualPaymentCommandReceipt')
                            OR changed_fields_json ->> 'principalEvidenceKind' = 'ManualPayment') THEN
                        RAISE EXCEPTION 'Manual payment evidence is retained; downgrade is unsafe' USING ERRCODE = '23514';
                    END IF;
                END; $$;
                DROP TRIGGER manual_payment_history_bundle ON procurement.manual_payment_decisions;
                DROP TRIGGER manual_payment_receipt_bundle ON procurement.manual_payment_receipts;
                DROP FUNCTION procurement.require_manual_payment_evidence_bundle();
                DROP TRIGGER manual_payment_command_bundle ON procurement.manual_payment_requests;
                DROP TRIGGER manual_payment_request_immutable ON procurement.manual_payment_requests;
                DROP TRIGGER manual_payment_receipt_append_only ON procurement.manual_payment_receipts;
                DROP TRIGGER manual_payment_decision_append_only ON procurement.manual_payment_decisions;
                DROP FUNCTION procurement.require_manual_payment_command_bundle();
                DROP FUNCTION procurement.guard_manual_payment_request();
                DROP FUNCTION procurement.reject_manual_payment_evidence_mutation();
                """);
            migrationBuilder.DropTable(
                name: "manual_payment_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "manual_payment_decisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "manual_payment_requests",
                schema: "procurement");
        }
    }
}
