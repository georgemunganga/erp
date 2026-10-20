using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P25PositiveReceivingExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.AlterColumn<decimal>(
                name: "accepted_amount",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(38,8)",
                precision: 38,
                scale: 8,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<Guid>(
                name: "certification_effect_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "current_accepted_net_amount",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(38,8)",
                precision: 38,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "current_accepted_quantity",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "evidence_ref",
                schema: "procurement",
                table: "service_acceptances",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "service_acceptances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_binding_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "procurement",
                table: "service_acceptances",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_worker_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "policy_version_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "service_acceptances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "effect_key",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_ref",
                schema: "procurement",
                table: "receipt_inspections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "receipt_inspections",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "inspector_worker_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ledger_sequence",
                schema: "procurement",
                table: "receipt_inspections",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "physical_binding_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "physical_observation_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "policy_version_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "qualified_quantity",
                schema: "procurement",
                table: "receipt_inspections",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "receipt_inspections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "branch_ref",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "issued_version_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "procurement",
                table: "goods_receipts",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receiver_worker_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "recorded_at",
                schema: "procurement",
                table: "goods_receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "submission_id",
                schema: "procurement",
                table: "goods_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "initially_rejected_quantity",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "physical_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "physical_effect_key",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "physical_evidence_ref",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "physical_observation_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "physical_policy_version_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "physical_recorded_at",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "physical_sequence",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_service_acceptances_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_receipt_inspections_tenant_id_legal_entity_id_id_goods_rece~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "goods_receipt_line_id", "source_state_binding_id", "physical_binding_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_id_purchase_~",
                schema: "procurement",
                table: "purchase_order_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_order_issued_versions_tenant_id_legal_entity_id_id~",
                schema: "procurement",
                table: "purchase_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "submission_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_order_delivery_evidence_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "purchase_order_delivery_evidence",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "issued_version_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_goods_receipts_tenant_id_legal_entity_id_id_purchase_order_~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_binding_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_goods_receipt_lines_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id", "physical_binding_id" });

            migrationBuilder.CreateTable(
                name: "receiving_source_states",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_version = table.Column<int>(type: "integer", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_evidence_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    classification_json = table.Column<string>(type: "text", nullable: false),
                    classification_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    current_accepted_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    current_accepted_net_amount = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
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
                    table.PrimaryKey("PK_receiving_source_states", x => x.id);
                    table.UniqueConstraint("AK_receiving_source_states_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("AK_receiving_source_states_tenant_id_legal_entity_id_id_purcha~", x => new { x.tenant_id, x.legal_entity_id, x.id, x.purchase_order_id, x.issued_version_id, x.purchase_order_line_id });
                    table.CheckConstraint("ck_receiving_source_state", "execution_version = 1 AND sequence >= 0 AND current_accepted_quantity >= 0 AND current_accepted_net_amount >= 0 AND source_sha256 ~ '^[a-f0-9]{64}$'");
                    table.ForeignKey(
                        name: "FK_receiving_source_states_policy_versions_tenant_id_legal_ent~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.classification_policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_source_states_purchase_order_delivery_evidence_te~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.delivery_evidence_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_delivery_evidence",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "issued_version_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_source_states_purchase_order_issued_versions_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id, x.purchase_order_id, x.submission_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "submission_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_source_states_purchase_order_lines_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id, x.purchase_order_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receiving_acceptance_effects",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    acceptance_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    physical_observation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inspection_evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_acceptance_effect_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correction_evidence = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    net_amount_delta = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    actor_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    deliverable_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    period_from = table.Column<DateOnly>(type: "date", nullable: true),
                    period_to = table.Column<DateOnly>(type: "date", nullable: true),
                    ledger_sequence = table.Column<long>(type: "bigint", nullable: false),
                    effect_key = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    receiving_source_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    goods_receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_acceptance_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_receiving_acceptance_effects", x => x.id);
                    table.UniqueConstraint("AK_receiving_acceptance_effects_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_receiving_positive_effect", "kind = 1 AND quantity_delta > 0 AND net_amount_delta >= 0 AND ledger_sequence > 0 AND original_acceptance_effect_id IS NULL AND correction_evidence IS NULL AND ((goods_receipt_line_id IS NOT NULL AND service_acceptance_id IS NULL AND acceptance_record_id = goods_receipt_line_id AND physical_observation_id IS NOT NULL) OR (service_acceptance_id IS NOT NULL AND goods_receipt_line_id IS NULL AND acceptance_record_id = service_acceptance_id AND physical_observation_id IS NULL AND inspection_evidence_id IS NULL))");
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_goods_receipt_lines_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.goods_receipt_line_id, x.receiving_source_state_id, x.physical_observation_id },
                        principalSchema: "procurement",
                        principalTable: "goods_receipt_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id", "physical_binding_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_policy_versions_tenant_id_lega~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receiving_policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_receipt_inspections_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.inspection_evidence_id, x.goods_receipt_line_id, x.receiving_source_state_id, x.physical_observation_id },
                        principalSchema: "procurement",
                        principalTable: "receipt_inspections",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "goods_receipt_line_id", "source_state_binding_id", "physical_binding_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_receiving_source_states_tenant~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receiving_source_state_id },
                        principalSchema: "procurement",
                        principalTable: "receiving_source_states",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receiving_acceptance_effects_service_acceptances_tenant_id_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.service_acceptance_id, x.receiving_source_state_id },
                        principalSchema: "procurement",
                        principalTable: "service_acceptances",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "deliverable_ref" },
                unique: true,
                filter: "certified_at IS NOT NULL AND execution_version = 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_service_execution",
                schema: "procurement",
                table: "service_acceptances",
                sql: "execution_version = 0 OR (execution_version = 1 AND source_purchase_order_id IS NOT NULL AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (receiving_source_state_id IS NULL OR source_state_binding_id = receiving_source_state_id) AND (certified_at IS NULL OR (receiving_source_state_id IS NOT NULL AND certification_effect_id IS NOT NULL AND owner_worker_id IS NOT NULL AND policy_version_id IS NOT NULL AND accepted_quantity > 0 AND accepted_amount >= 0)))");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_observation_id" },
                unique: true,
                filter: "physical_observation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "ledger_sequence" },
                unique: true,
                filter: "ledger_sequence IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_inspection_execution",
                schema: "procurement",
                table: "receipt_inspections",
                sql: "execution_version = 0 OR (execution_version = 1 AND receiving_source_state_id IS NOT NULL AND physical_observation_id IS NOT NULL AND physical_binding_id = physical_observation_id AND source_state_binding_id = receiving_source_state_id AND inspector_worker_id IS NOT NULL AND policy_version_id IS NOT NULL AND ledger_sequence IS NOT NULL AND ledger_sequence > 0 AND effect_key IS NOT NULL AND inspected_at IS NOT NULL AND qualified_quantity >= 0 AND qualified_quantity <= inspected_quantity)");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_issued_version_id_~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id", "submission_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_header_execution",
                schema: "procurement",
                table: "goods_receipts",
                sql: "execution_version = 0 OR (execution_version = 1 AND issued_version_id IS NOT NULL AND submission_id IS NOT NULL AND issued_binding_id = issued_version_id AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (recorded_at IS NULL OR (receiver_worker_id IS NOT NULL AND receiver_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)))");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "source_purchase_order_id", "issued_binding_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_observation_id" },
                unique: true,
                filter: "physical_observation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_poli~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "physical_sequence" },
                unique: true,
                filter: "physical_sequence IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_line_execution",
                schema: "procurement",
                table: "goods_receipt_lines",
                sql: "execution_version = 0 OR (execution_version = 1 AND source_purchase_order_id IS NOT NULL AND issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (receiving_source_state_id IS NULL OR source_state_binding_id = receiving_source_state_id) AND (physical_observation_id IS NULL OR (receiving_source_state_id IS NOT NULL AND physical_binding_id = physical_observation_id AND physical_sequence IS NOT NULL AND physical_sequence > 0 AND physical_policy_version_id IS NOT NULL AND physical_effect_key IS NOT NULL AND physical_recorded_at IS NOT NULL AND received_quantity > 0 AND initially_rejected_quantity >= 0 AND initially_rejected_quantity <= received_quantity)))");

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_effe~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "effect_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_good~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_insp~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "inspection_evidence_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_rec~1",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "ledger_sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_rece~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_serv~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id", "receiving_source_state_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_classific~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "classification_policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_delivery_~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "delivery_evidence_id", "issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_issued_v~1",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id", "submission_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_issued_ve~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_source_states_tenant_id_legal_entity_id_purchase_~",
                schema: "procurement",
                table: "receiving_source_states",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id", "purchase_order_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_tenant_id_legal_entity_~1",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id", "source_purchase_order_id", "issued_binding_id" },
                principalSchema: "procurement",
                principalTable: "goods_receipts",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_binding_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "physical_policy_version_id" },
                principalSchema: "procurement",
                principalTable: "policy_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipt_lines_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" },
                principalSchema: "procurement",
                principalTable: "receiving_source_states",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_goods_receipts_purchase_order_issued_versions_tenant_id_leg~",
                schema: "procurement",
                table: "goods_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id", "submission_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "submission_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receipt_inspections_goods_receipt_lines_tenant_id_legal_en~1",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" },
                principalSchema: "procurement",
                principalTable: "goods_receipt_lines",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id", "physical_binding_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receipt_inspections_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                principalSchema: "procurement",
                principalTable: "policy_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_acceptances_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" },
                principalSchema: "procurement",
                principalTable: "policy_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_service_acceptances_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "receiving_source_state_id", "source_purchase_order_id", "issued_binding_id", "purchase_order_line_id" },
                principalSchema: "procurement",
                principalTable: "receiving_source_states",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id", "issued_version_id", "purchase_order_line_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve every configured or used receiving extension and exact legacy amounts.
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                LOCK TABLE procurement.receiving_source_states, procurement.receiving_acceptance_effects,
                    procurement.goods_receipts, procurement.goods_receipt_lines, procurement.receipt_inspections,
                    procurement.service_acceptances, procurement.policy_versions,
                    procurement.commercial_command_receipts, procurement.audit_events, procurement.outbox_events
                    IN ACCESS EXCLUSIVE MODE;
                DO $p25_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.receiving_source_states)
                        OR EXISTS (SELECT 1 FROM procurement.receiving_acceptance_effects)
                        OR EXISTS (SELECT 1 FROM procurement.goods_receipts WHERE execution_version <> 0
                            OR issued_version_id IS NOT NULL OR submission_id IS NOT NULL
                            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR branch_ref IS NOT NULL OR receiver_worker_id IS NOT NULL OR recorded_at IS NOT NULL OR notes IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.goods_receipt_lines WHERE execution_version <> 0
                            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
                            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR initially_rejected_quantity <> 0 OR physical_observation_id IS NOT NULL
                            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR physical_policy_version_id IS NOT NULL OR physical_evidence_ref IS NOT NULL
                            OR physical_sequence IS NOT NULL OR physical_effect_key IS NOT NULL OR physical_recorded_at IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.receipt_inspections WHERE execution_version <> 0
                            OR physical_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR physical_observation_id IS NOT NULL OR receiving_source_state_id IS NOT NULL
                            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR qualified_quantity <> 0 OR inspector_worker_id IS NOT NULL OR policy_version_id IS NOT NULL
                            OR evidence_ref IS NOT NULL OR ledger_sequence IS NOT NULL OR effect_key IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.service_acceptances WHERE execution_version <> 0
                            OR receiving_source_state_id IS NOT NULL OR source_purchase_order_id IS NOT NULL
                            OR source_state_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR issued_binding_id <> '00000000-0000-0000-0000-000000000000'::uuid
                            OR owner_worker_id IS NOT NULL OR policy_version_id IS NOT NULL OR evidence_ref IS NOT NULL
                            OR certification_effect_id IS NOT NULL OR current_accepted_quantity <> 0 OR current_accepted_net_amount <> 0 OR notes IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM procurement.policy_versions WHERE upper(btrim(policy_key)) = 'RECEIVING')
                        OR EXISTS (SELECT 1 FROM procurement.commercial_command_receipts WHERE operation IN
                            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
                        OR EXISTS (SELECT 1 FROM procurement.audit_events WHERE action IN
                            ('ReceivingDraft','ReceivingRecord','ReceivingInspect','ReceivingAccept','ReceivingCancel','ServiceDraft','ServiceCertify','ServiceCancel'))
                        OR EXISTS (SELECT 1 FROM procurement.audit_events
                            WHERE record_type IN ('ReceivingSourceState','ReceivingAcceptanceEffect'))
                        OR EXISTS (SELECT 1 FROM procurement.outbox_events WHERE aggregate_type = 'Receiving'
                            OR event_type IN ('procurement.goods.recorded.v1','procurement.goods.inspected.v1','procurement.fulfilment.accepted.v1')) THEN
                        RAISE EXCEPTION 'P25RollbackRequiresEvidencePreservingRecovery: receiving has been configured or used; use a reviewed forward fix or protected restore, preserving records and evidence.';
                    END IF;
                    -- P25 widens an EXISTING service amount. Even a v0 owner-written value must not
                    -- silently round or overflow on restoration of the prior numeric(18,4) column.
                    IF EXISTS (SELECT 1 FROM procurement.service_acceptances
                        WHERE accepted_amount <> trunc(accepted_amount, 4) OR abs(accepted_amount) >= 100000000000000) THEN
                        RAISE EXCEPTION 'P25RollbackAmountWouldLosePrecision: retained service amount does not fit numeric(18,4) exactly.';
                    END IF;
                END $p25_guard$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_goods_receipts_tenant_id_legal_entity_~1",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipt_lines_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_goods_receipts_purchase_order_issued_versions_tenant_id_leg~",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_receipt_inspections_goods_receipt_lines_tenant_id_legal_en~1",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropForeignKey(
                name: "FK_receipt_inspections_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropForeignKey(
                name: "FK_service_acceptances_policy_versions_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropForeignKey(
                name: "FK_service_acceptances_receiving_source_states_tenant_id_legal~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropTable(
                name: "receiving_acceptance_effects",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "receiving_source_states",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_service_acceptances_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "IX_service_acceptances_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropCheckConstraint(
                name: "ck_service_execution",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_receipt_inspections_tenant_id_legal_entity_id_id_goods_rece~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_inspection_execution",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_order_lines_tenant_id_legal_entity_id_id_purchase_~",
                schema: "procurement",
                table: "purchase_order_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_order_issued_versions_tenant_id_legal_entity_id_id~",
                schema: "procurement",
                table: "purchase_order_issued_versions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_order_delivery_evidence_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "purchase_order_delivery_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_goods_receipts_tenant_id_legal_entity_id_id_purchase_order_~",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipts_tenant_id_legal_entity_id_issued_version_id_~",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_header_execution",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_goods_receipt_lines_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_obse~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_physical_poli~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_so~1",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_receiving_sou~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_line_execution",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "certification_effect_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "current_accepted_net_amount",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "current_accepted_quantity",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "evidence_ref",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "issued_binding_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "owner_worker_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "policy_version_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropColumn(
                name: "effect_key",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "evidence_ref",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "inspector_worker_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "ledger_sequence",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "physical_binding_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "physical_observation_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "policy_version_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "qualified_quantity",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropColumn(
                name: "branch_ref",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "issued_version_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "receiver_worker_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "recorded_at",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "submission_id",
                schema: "procurement",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "execution_version",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "initially_rejected_quantity",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "issued_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_effect_key",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_evidence_ref",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_observation_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_policy_version_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_recorded_at",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "physical_sequence",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "receiving_source_state_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "source_purchase_order_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "source_state_binding_id",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.AlterColumn<decimal>(
                name: "accepted_amount",
                schema: "procurement",
                table: "service_acceptances",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,8)",
                oldPrecision: 38,
                oldScale: 8);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_id" });
        }
    }
}
