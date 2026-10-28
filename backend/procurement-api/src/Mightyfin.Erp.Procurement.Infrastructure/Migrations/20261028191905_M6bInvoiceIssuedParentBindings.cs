using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6bInvoiceIssuedParentBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invoice_match_exposure_states_purchase_order_issued_version~",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_match_mapping_versions_purchase_order_issued_versio~",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_invoice_match_results_purchase_order_issued_versions_tenant~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_issued_vers~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_is~",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_iss~",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_exposure_state",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.AddColumn<int>(
                name: "source_schema_version",
                schema: "procurement",
                table: "invoice_match_results",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_schema_version",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "legacy_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_results",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN source_schema_version IS NULL THEN issued_version_id ELSE NULL END",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "principal_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_results",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN source_schema_version = 3 THEN issued_version_id ELSE NULL END",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "legacy_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN source_schema_version IS NULL THEN issued_version_id ELSE NULL END",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "principal_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN source_schema_version = 3 THEN issued_version_id ELSE NULL END",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "legacy_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN source_schema_version IS NULL THEN issued_version_id ELSE NULL END",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "principal_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                type: "uuid",
                nullable: true,
                computedColumnSql: "CASE WHEN source_schema_version = 3 THEN issued_version_id ELSE NULL END",
                stored: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_invoice_principal_issued_order_binding",
                schema: "procurement",
                table: "principal_order_issued_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_legacy_issu~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "legacy_issued_version_ref_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_principal_i~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_ref_id", "purchase_order_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results",
                sql: "(source_schema_version IS NULL OR (source_schema_version=3 AND execution_version=2 AND mapping_version_id IS NOT NULL AND purchase_order_id IS NOT NULL AND issued_version_id IS NOT NULL)) AND (execution_version = 0 OR (execution_version IN (1,2) AND mapping_version_id IS NOT NULL AND purchase_order_id IS NOT NULL AND issued_version_id IS NOT NULL AND invoice_version > 0 AND command_key IS NOT NULL AND (execution_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (execution_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000')) AND invoice_original_sha256 IS NOT NULL AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND input_fingerprint IS NOT NULL AND input_fingerprint ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_json IS NOT NULL AND source_watermarks_json IS NOT NULL AND outcome IN ('Compared','Blocked')))");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_le~",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "legacy_issued_version_ref_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_pr~",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_ref_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_leg~",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "legacy_issued_version_ref_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_pri~",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_ref_id", "purchase_order_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_exposure_state",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                sql: "sequence >= 0 AND (source_schema_version IS NULL OR source_schema_version=3) AND coverage_state IN ('Unknown','Verified','Stale') AND currency_code ~ '^[A-Z]{3}$' AND (coverage_state <> 'Verified' OR (owner_reference IS NOT NULL AND owner_watermark IS NOT NULL AND owner_evidence_sha256 IS NOT NULL AND owner_evidence_sha256 ~ '^[0-9a-f]{64}$' AND owner_valid_until IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "fk_invoicematchexposurestate_legacy_issued_order",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "legacy_issued_version_ref_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoicematchexposurestate_principal_issued_order",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_ref_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "principal_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoicematchmappingversion_legacy_issued_order",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "legacy_issued_version_ref_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoicematchmappingversion_principal_issued_order",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_ref_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "principal_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoicematchresult_legacy_issued_order",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "legacy_issued_version_ref_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_invoicematchresult_principal_issued_order",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_issued_version_ref_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "principal_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(M6bIssuedParentRollbackGuards.BeforeDownSql);

            migrationBuilder.DropForeignKey(
                name: "fk_invoicematchexposurestate_legacy_issued_order",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropForeignKey(
                name: "fk_invoicematchexposurestate_principal_issued_order",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropForeignKey(
                name: "fk_invoicematchmappingversion_legacy_issued_order",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropForeignKey(
                name: "fk_invoicematchmappingversion_principal_issued_order",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropForeignKey(
                name: "fk_invoicematchresult_legacy_issued_order",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropForeignKey(
                name: "fk_invoicematchresult_principal_issued_order",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_invoice_principal_issued_order_binding",
                schema: "procurement",
                table: "principal_order_issued_versions");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_legacy_issu~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_principal_i~",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_le~",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_pr~",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_leg~",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_pri~",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_exposure_state",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropColumn(
                name: "legacy_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "principal_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "legacy_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropColumn(
                name: "principal_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropColumn(
                name: "legacy_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropColumn(
                name: "principal_issued_version_ref_id",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.DropColumn(
                name: "source_schema_version",
                schema: "procurement",
                table: "invoice_match_results");

            migrationBuilder.DropColumn(
                name: "source_schema_version",
                schema: "procurement",
                table: "invoice_match_exposure_states");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_results_tenant_id_legal_entity_id_issued_vers~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_match_execution",
                schema: "procurement",
                table: "invoice_match_results",
                sql: "execution_version = 0 OR (execution_version IN (1,2) AND mapping_version_id IS NOT NULL AND purchase_order_id IS NOT NULL AND issued_version_id IS NOT NULL AND invoice_version > 0 AND command_key IS NOT NULL AND (execution_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (execution_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000')) AND invoice_original_sha256 IS NOT NULL AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND input_fingerprint IS NOT NULL AND input_fingerprint ~ '^[0-9a-f]{64}$' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$' AND evidence_json IS NOT NULL AND source_watermarks_json IS NOT NULL AND outcome IN ('Compared','Blocked'))");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_mapping_versions_tenant_id_legal_entity_id_is~",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_match_exposure_states_tenant_id_legal_entity_id_iss~",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_exposure_state",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                sql: "sequence >= 0 AND coverage_state IN ('Unknown','Verified','Stale') AND currency_code ~ '^[A-Z]{3}$' AND (coverage_state <> 'Verified' OR (owner_reference IS NOT NULL AND owner_watermark IS NOT NULL AND owner_evidence_sha256 IS NOT NULL AND owner_evidence_sha256 ~ '^[0-9a-f]{64}$' AND owner_valid_until IS NOT NULL))");

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_match_exposure_states_purchase_order_issued_version~",
                schema: "procurement",
                table: "invoice_match_exposure_states",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_match_mapping_versions_purchase_order_issued_versio~",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_invoice_match_results_purchase_order_issued_versions_tenant~",
                schema: "procurement",
                table: "invoice_match_results",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id", "purchase_order_id" },
                principalSchema: "procurement",
                principalTable: "purchase_order_issued_versions",
                principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "purchase_order_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
