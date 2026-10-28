using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6PrincipalInvoiceOwnerBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.AddColumn<int>(
                name: "source_schema_version",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "invoice_owner_baselines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    captured_through_migration = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    catalog_manifest_json = table.Column<string>(type: "text", nullable: false),
                    catalog_manifest_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    inventory_set_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    initial_active_claims_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    initial_exposure_set_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    inventory_row_count = table.Column<int>(type: "integer", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    maker_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_invoice_owner_baselines", x => x.id);
                    table.UniqueConstraint("AK_invoice_owner_baselines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_owner_baseline", "schema_version=2 AND inventory_row_count>=0 AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND catalog_manifest_sha256 ~ '^[0-9a-f]{64}$' AND inventory_set_sha256 ~ '^[0-9a-f]{64}$' AND initial_active_claims_sha256 ~ '^[0-9a-f]{64}$' AND initial_exposure_set_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(catalog_manifest_json) BETWEEN 1 AND 262144 AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND isfinite(captured_at_utc)");
                });

            migrationBuilder.CreateTable(
                name: "invoice_owner_baseline_rows",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_row_version = table.Column<long>(type: "bigint", nullable: false),
                    original_row_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    immutable_fields_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    initial_classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    supplier_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_invoice_owner_baseline_rows", x => x.id);
                    table.UniqueConstraint("AK_invoice_owner_baseline_rows_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_owner_baseline_row", "source_row_id <> '00000000-0000-0000-0000-000000000000'::uuid AND source_row_version>0 AND table_name ~ '^[a-z][a-z0-9_]{0,99}$' AND initial_classification IN ('ClaimBearing','HistoricalOnly','MutableDraft','BlockedAmbiguity') AND original_row_sha256 ~ '^[0-9a-f]{64}$' AND immutable_fields_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_invoice_owner_baseline_rows_invoice_owner_baselines_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.baseline_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_owner_baselines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_owner_command_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    core_case_version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_invoice_owner_command_receipts", x => x.id);
                    table.UniqueConstraint("AK_invoice_owner_command_receipts_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_invoice_owner_command_receipt", "schema_version=2 AND version=1 AND updated_at IS NULL AND updated_by IS NULL AND isfinite(created_at) AND created_by=actor_principal_id AND baseline_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND operation IN ('Open','Decide') AND ((operation='Open' AND core_decision_id IS NULL AND response_status=201) OR (operation='Decide' AND core_decision_id IS NOT NULL AND core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND response_status=200)) AND core_case_version>0 AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(response_json) BETWEEN 2 AND 65536");
                    table.ForeignKey(
                        name: "FK_invoice_owner_command_receipts_invoice_owner_baselines_tena~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.baseline_id },
                        principalSchema: "procurement",
                        principalTable: "invoice_owner_baselines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW') AND (policy_key NOT IN ('MANUAL_PAYMENT_REVIEW','INVOICE_OWNER_BASELINE_REVIEW') OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                sql: "mapping_revision > 0 AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND identity_schema_version IN (1,2) AND (source_schema_version IS NULL OR (source_schema_version=3 AND identity_schema_version=2)) AND (identity_schema_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (identity_schema_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'))");

            migrationBuilder.CreateIndex(
                name: "ux_invoice_owner_original_row",
                schema: "procurement",
                table: "invoice_owner_baseline_rows",
                columns: new[] { "tenant_id", "legal_entity_id", "baseline_id", "table_name", "source_row_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_owner_baselines_tenant_id_legal_entity_id_captured_~",
                schema: "procurement",
                table: "invoice_owner_baselines",
                columns: new[] { "tenant_id", "legal_entity_id", "captured_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_owner_baseline_case",
                schema: "procurement",
                table: "invoice_owner_baselines",
                columns: new[] { "tenant_id", "legal_entity_id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_owner_command_receipts_tenant_id_legal_entity_id_ba~",
                schema: "procurement",
                table: "invoice_owner_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "baseline_id" });

            migrationBuilder.CreateIndex(
                name: "ux_invoice_owner_command_key",
                schema: "procurement",
                table: "invoice_owner_command_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(M6PrincipalInvoiceRollbackGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "invoice_owner_baseline_rows",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_owner_command_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "invoice_owner_baselines",
                schema: "procurement");

            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.DropColumn(
                name: "source_schema_version",
                schema: "procurement",
                table: "invoice_match_mapping_versions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_policy_identity",
                schema: "procurement",
                table: "operational_policy_identity_evidence",
                sql: "schema_version IN (1,2) AND policy_key IN ('RECEIVING','INVOICE_MATCHING','INVOICE_UNIT_REVIEW','MANUAL_PAYMENT_REVIEW') AND (policy_key <> 'MANUAL_PAYMENT_REVIEW' OR schema_version=2)\nAND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND policy_version_number>0 AND policy_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (schema_version=2 OR actor_worker_id IS NOT NULL)\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND rules_sha256 IS NOT NULL AND rules_sha256 ~ '^[0-9a-f]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND policy_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND policy_record_version_at_action>1\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Published' AND policy_record_version_at_action>2\n    AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invoice_mapping_evidence",
                schema: "procurement",
                table: "invoice_match_mapping_versions",
                sql: "mapping_revision > 0 AND mapping_sha256 ~ '^[0-9a-f]{64}$' AND invoice_original_sha256 ~ '^[0-9a-f]{64}$' AND identity_schema_version IN (1,2) AND (identity_schema_version=2 OR actor_worker_id IS NOT NULL) AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND actor_principal_id IS NOT NULL AND (identity_schema_version=1 OR (actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000'))");
        }
    }
}
