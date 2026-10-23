using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P29CorePrincipalSourcingDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rfq_original_envelope",
                schema: "procurement",
                table: "sourcing_draft_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rfq_binding_value",
                schema: "procurement",
                table: "sourcing_demand_bindings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rfq_receipt_identity",
                schema: "procurement",
                table: "sourcing_command_receipts");

            migrationBuilder.AlterColumn<Guid>(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "maker_principal_id",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "requester_worker_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "identity_schema_version",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "maker_principal_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "principal_identity_sha256",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requester_principal_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "sourcing_command_receipts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1,2) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')) OR (execution_version=2 AND source_evidence_id IS NOT NULL AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rfq_original_envelope",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                sql: "schema_version IN (1,2) AND payload_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND create_command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND ((schema_version=1 AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND maker_principal_id IS NULL) OR (schema_version=2 AND maker_principal_id IS NOT NULL AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rfq_binding_value",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                sql: "target_type='SourcingEvent' AND quantity>0 AND unit_estimate>=0 AND estimated_value=quantity*unit_estimate AND request_version>=submitted_version AND submitted_version>0 AND method_revision>0 AND minimum_quote_count BETWEEN 0 AND 20 AND ((identity_schema_version=1 AND maker_worker_id IS NOT NULL AND requester_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND maker_principal_id IS NULL AND requester_principal_id IS NULL AND principal_identity_sha256 IS NULL) OR (identity_schema_version=2 AND maker_principal_id IS NOT NULL AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND requester_principal_id IS NOT NULL AND requester_principal_id <> '00000000-0000-0000-0000-000000000000' AND requester_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND principal_identity_sha256 IS NOT NULL AND principal_identity_sha256 ~ '^[a-f0-9]{64}$' AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_worker_id IS NULL OR requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rfq_receipt_identity",
                schema: "procurement",
                table: "sourcing_command_receipts",
                sql: "operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND ((response_schema_version=1 AND actor_worker_id IS NOT NULL AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) OR (response_schema_version=2 AND actor_principal_id IS NOT NULL AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid))) AND result_version>0 AND command_fingerprint ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND ((operation='Create' AND response_status_code=201) OR (operation='Cancel' AND response_status_code=200))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P29PrincipalSourcingDraftGuards.BeforeDownSql);

            migrationBuilder.DropCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rfq_original_envelope",
                schema: "procurement",
                table: "sourcing_draft_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rfq_binding_value",
                schema: "procurement",
                table: "sourcing_demand_bindings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rfq_receipt_identity",
                schema: "procurement",
                table: "sourcing_command_receipts");

            migrationBuilder.DropColumn(
                name: "maker_principal_id",
                schema: "procurement",
                table: "sourcing_draft_evidence");

            migrationBuilder.DropColumn(
                name: "identity_schema_version",
                schema: "procurement",
                table: "sourcing_demand_bindings");

            migrationBuilder.DropColumn(
                name: "maker_principal_id",
                schema: "procurement",
                table: "sourcing_demand_bindings");

            migrationBuilder.DropColumn(
                name: "principal_identity_sha256",
                schema: "procurement",
                table: "sourcing_demand_bindings");

            migrationBuilder.DropColumn(
                name: "requester_principal_id",
                schema: "procurement",
                table: "sourcing_demand_bindings");

            migrationBuilder.AlterColumn<Guid>(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "requester_worker_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "maker_worker_id",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "sourcing_command_receipts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sourcing_owned_draft",
                schema: "procurement",
                table: "sourcing_events",
                sql: "execution_version IN (0,1) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rfq_original_envelope",
                schema: "procurement",
                table: "sourcing_draft_evidence",
                sql: "schema_version=1 AND payload_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND create_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rfq_binding_value",
                schema: "procurement",
                table: "sourcing_demand_bindings",
                sql: "target_type='SourcingEvent' AND quantity>0 AND unit_estimate>=0 AND estimated_value=quantity*unit_estimate AND request_version>=submitted_version AND submitted_version>0 AND method_revision>0 AND minimum_quote_count BETWEEN 0 AND 20 AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rfq_receipt_identity",
                schema: "procurement",
                table: "sourcing_command_receipts",
                sql: "operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND response_schema_version=1 AND result_version>0 AND command_fingerprint ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND ((operation='Create' AND response_status_code=201) OR (operation='Cancel' AND response_status_code=200))");
        }
    }
}
