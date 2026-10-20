using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P25ReceivingStableEvidenceBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_receipt_inspections_goods_receipt_lines_tenant_id_legal_en~1",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropForeignKey(
                name: "FK_receiving_acceptance_effects_goods_receipt_lines_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropForeignKey(
                name: "FK_receiving_acceptance_effects_service_acceptances_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_service_acceptances_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_good~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_serv~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_goods_receipt_lines_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.CreateIndex(
                name: "ux_receiving_service_state_binding",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_good~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_serv~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" });

            migrationBuilder.CreateIndex(
                name: "ux_receiving_goods_physical_binding",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id", "physical_binding_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_receiving_acceptance_effects_goods_receipt_lines_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id" },
                principalSchema: "procurement",
                principalTable: "goods_receipt_lines",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receiving_acceptance_effects_service_acceptances_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id" },
                principalSchema: "procurement",
                principalTable: "service_acceptances",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                onDelete: ReferentialAction.Restrict);

            // Database-owned exact tuples remain separate from EF immutable identity keys.
            // Freeze this SQL in migration history; future helper changes must not rewrite it.
            migrationBuilder.Sql("""
                ALTER TABLE procurement.receipt_inspections
                  ADD CONSTRAINT fk_receiving_inspection_exact_physical
                  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
                  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.receiving_acceptance_effects
                  ADD CONSTRAINT fk_receiving_effect_exact_physical
                  FOREIGN KEY (tenant_id, legal_entity_id, goods_receipt_line_id, receiving_source_state_id, physical_observation_id)
                  REFERENCES procurement.goods_receipt_lines (tenant_id, legal_entity_id, id, source_state_binding_id, physical_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE procurement.receiving_acceptance_effects
                  ADD CONSTRAINT fk_receiving_effect_exact_service
                  FOREIGN KEY (tenant_id, legal_entity_id, service_acceptance_id, receiving_source_state_id)
                  REFERENCES procurement.service_acceptances (tenant_id, legal_entity_id, id, source_state_binding_id)
                  ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never restore the prior mutable-key mapping after receiving configuration/use.
            // This correction does not narrow amounts; the original P25 Down retains that guard.
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
                END $p25_guard$;
                ALTER TABLE procurement.receipt_inspections DROP CONSTRAINT fk_receiving_inspection_exact_physical;
                ALTER TABLE procurement.receiving_acceptance_effects DROP CONSTRAINT fk_receiving_effect_exact_physical;
                ALTER TABLE procurement.receiving_acceptance_effects DROP CONSTRAINT fk_receiving_effect_exact_service;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_receiving_acceptance_effects_goods_receipt_lines_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropForeignKey(
                name: "FK_receiving_acceptance_effects_service_acceptances_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropIndex(
                name: "ux_receiving_service_state_binding",
                schema: "procurement",
                table: "service_acceptances");

            migrationBuilder.DropIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_good~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_serv~",
                schema: "procurement",
                table: "receiving_acceptance_effects");

            migrationBuilder.DropIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections");

            migrationBuilder.DropIndex(
                name: "ux_receiving_goods_physical_binding",
                schema: "procurement",
                table: "goods_receipt_lines");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_service_acceptances_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "service_acceptances",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_goods_receipt_lines_tenant_id_legal_entity_id_id_source_sta~",
                schema: "procurement",
                table: "goods_receipt_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id", "physical_binding_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_good~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receiving_acceptance_effects_tenant_id_legal_entity_id_serv~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id", "receiving_source_state_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_inspections_tenant_id_legal_entity_id_goods_receipt~",
                schema: "procurement",
                table: "receipt_inspections",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" });

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
                name: "FK_receiving_acceptance_effects_goods_receipt_lines_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "goods_receipt_line_id", "receiving_source_state_id", "physical_observation_id" },
                principalSchema: "procurement",
                principalTable: "goods_receipt_lines",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id", "physical_binding_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_receiving_acceptance_effects_service_acceptances_tenant_id_~",
                schema: "procurement",
                table: "receiving_acceptance_effects",
                columns: new[] { "tenant_id", "legal_entity_id", "service_acceptance_id", "receiving_source_state_id" },
                principalSchema: "procurement",
                principalTable: "service_acceptances",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "source_state_binding_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
