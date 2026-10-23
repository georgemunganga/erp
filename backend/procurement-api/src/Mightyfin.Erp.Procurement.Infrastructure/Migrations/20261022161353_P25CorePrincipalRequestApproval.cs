using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P25CorePrincipalRequestApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_worker_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_finance_identity_version",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_budget_checker_identity",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_assignment_authority_identity",
                schema: "procurement",
                table: "assignment_authority_evidence");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_worker_id",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "requester_core_worker_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "requester_identity_schema_version",
                schema: "procurement",
                table: "purchase_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "requester_identity_sha256",
                schema: "procurement",
                table: "purchase_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "principal_identity_sha256",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requester_identity_sha256",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "requester_principal_id",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "identity_schema_version",
                schema: "procurement",
                table: "assignment_authority_evidence",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                sql: "schema_version IN (1,2) AND (schema_version=2 OR (actor_worker_id IS NOT NULL AND recipient_worker_id IS NOT NULL)) AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND (schema_version=1 OR (action='PurchaseRequest.Approve' AND permission='procurement-approval-decide'))\nAND authority_version>0 AND grant_record_version_at_action>0\nAND (actor_worker_id IS NULL OR actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND (recipient_worker_id IS NULL OR recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_original_principal_identity",
                schema: "procurement",
                table: "purchase_requests",
                sql: "(requester_identity_schema_version=0 AND requester_core_worker_id IS NULL AND requester_identity_sha256 IS NULL) OR (requester_identity_schema_version=1 AND requester_principal_id IS NOT NULL AND requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (requester_core_worker_id IS NULL OR requester_core_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND created_by IS NOT NULL AND created_by=requester_principal_id::text AND source_channel IS NOT NULL AND source_channel='ERP' AND (on_behalf_of_subject_id IS NULL OR length(btrim(on_behalf_of_subject_id))=0) AND requester_identity_sha256 IS NOT NULL AND requester_identity_sha256 ~ '^[a-f0-9]{64}$')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_principal_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "(snapshot_schema_version IN (1,2) AND requester_principal_id IS NULL AND requester_identity_sha256 IS NULL AND principal_identity_sha256 IS NULL) OR (snapshot_schema_version=3 AND requester_principal_id IS NOT NULL AND requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND maker_subject_id IS NOT NULL AND maker_subject_id=requester_principal_id::text AND requester_identity_sha256 IS NOT NULL AND requester_identity_sha256 ~ '^[a-f0-9]{64}$' AND principal_identity_sha256 IS NOT NULL AND principal_identity_sha256 ~ '^[a-f0-9]{64}$')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "snapshot_schema_version IN (1,2,3) AND submitted_business_version > 0 AND policy_version_number > 0 AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_worker_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "(snapshot_schema_version = 1 AND maker_worker_id IS NULL AND requester_worker_id IS NULL AND worker_identity_sha256 IS NULL) OR (snapshot_schema_version = 2 AND maker_worker_id IS NOT NULL AND requester_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_identity_sha256 IS NOT NULL AND worker_identity_sha256 ~ '^[a-f0-9]{64}$') OR (snapshot_schema_version=3 AND worker_identity_sha256 IS NULL AND (maker_worker_id IS NULL OR maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_worker_id IS NULL OR requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_finance_identity_version",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "(finance_identity_schema_version = 0 AND snapshot_schema_version IN (1,2)) OR (finance_identity_schema_version = 1 AND snapshot_schema_version = 2) OR (finance_identity_schema_version=2 AND snapshot_schema_version=3)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_budget_checker_identity",
                schema: "procurement",
                table: "budget_check_snapshots",
                sql: "(checker_identity_schema_version = 0 AND checked_by_worker_id IS NULL AND checker_identity_sha256 IS NULL) OR (checker_identity_schema_version = 1 AND checked_by_worker_id IS NOT NULL AND checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND checker_identity_sha256 IS NOT NULL AND checker_identity_sha256 ~ '^[a-f0-9]{64}$') OR (checker_identity_schema_version=2 AND (checked_by_worker_id IS NULL OR checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND checked_by_subject_id IS NOT NULL AND checked_by_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND checked_by_subject_id <> '00000000-0000-0000-0000-000000000000' AND checker_identity_sha256 IS NOT NULL AND checker_identity_sha256 ~ '^[a-f0-9]{64}$')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_assignment_authority_identity",
                schema: "procurement",
                table: "assignment_authority_evidence",
                sql: "identity_schema_version IN (1,2) AND (identity_schema_version=1 OR (principal_id IS NOT NULL AND principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND principal_id <> '00000000-0000-0000-0000-000000000000')) AND core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND grant_revision > 0 AND authority_version > 0 AND length(btrim(principal_id)) > 0 AND assignee_kind IN ('LineManager','ProcurementManager') AND evidence_sha256 ~ '^[a-f0-9]{64}$' AND (assignee_worker_id IS NULL OR assignee_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Refuse destructive downgrade while principal evidence remains.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.purchase_requests,
                    procurement.budget_check_snapshots,
                    procurement.spending_authority_identity_evidence,
                    procurement.purchase_request_approval_submissions,
                    procurement.assignment_authority_evidence,
                    procurement.audit_events,
                    procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $p25_principal_approval_down$
                BEGIN
                    IF EXISTS(SELECT 1 FROM procurement.purchase_requests
                            WHERE requester_identity_schema_version <> 0
                                OR requester_core_worker_id IS NOT NULL OR requester_identity_sha256 IS NOT NULL)
                        OR EXISTS(SELECT 1 FROM procurement.budget_check_snapshots
                            WHERE checker_identity_schema_version NOT IN (0,1))
                        OR EXISTS(SELECT 1 FROM procurement.spending_authority_identity_evidence
                            WHERE schema_version <> 1 OR actor_worker_id IS NULL OR recipient_worker_id IS NULL)
                        OR EXISTS(SELECT 1 FROM procurement.purchase_request_approval_submissions
                            WHERE snapshot_schema_version NOT IN (1,2) OR finance_identity_schema_version NOT IN (0,1)
                                OR requester_principal_id IS NOT NULL OR requester_identity_sha256 IS NOT NULL
                                OR principal_identity_sha256 IS NOT NULL)
                        OR EXISTS(SELECT 1 FROM procurement.assignment_authority_evidence WHERE identity_schema_version <> 1)
                        OR EXISTS(SELECT 1 FROM procurement.audit_events
                            WHERE (record_type IN ('PurchaseRequest','BudgetCheckSnapshot','SpendingAuthorityIdentityRecord',
                                        'PurchaseRequestApprovalSubmission','PurchaseRequestAssignmentAuthorityEvidence')
                                    AND changed_fields_json->'principalEvidenceVersion' = '2'::jsonb
                                    AND changed_fields_json->>'principalEvidenceKind' IN ('PurchaseRequestOriginalIdentity',
                                        'FinanceCheckerIdentity','SpendingAuthorityIdentity','PurchaseRequestApprovalSubmission',
                                        'PurchaseRequestAssignmentAuthority'))
                                OR (record_type = 'PurchaseRequest' AND
                                    (changed_fields_json->'principalApprovalSchemaVersion' = '1'::jsonb
                                        OR changed_fields_json->'CheckerIdentitySchemaVersion' = '2'::jsonb)))
                        OR EXISTS(SELECT 1 FROM procurement.outbox_events
                            WHERE aggregate_type = 'purchase_request' AND
                                (payload_json->'principalApprovalSchemaVersion' = '1'::jsonb
                                    OR event_type IN ('purchase_request.approval_opened.v2',
                                        'purchase_request.approval_decision_recorded.v2','purchase_request.approved.v2',
                                        'purchase_request.rejected.v2','purchase_request.returned.v2',
                                        'procurement.finance_release.requested.v2','procurement.finance_release.confirmed.v2')))
                    THEN
                        RAISE EXCEPTION USING ERRCODE='55000',
                            MESSAGE='Retained Core-principal request approval evidence prevents destructive downgrade.';
                    END IF;
                END;
                $p25_principal_approval_down$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_original_principal_identity",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_principal_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_approval_worker_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_finance_identity_version",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_budget_checker_identity",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_assignment_authority_identity",
                schema: "procurement",
                table: "assignment_authority_evidence");

            migrationBuilder.DropColumn(
                name: "requester_core_worker_id",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "requester_identity_schema_version",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "requester_identity_sha256",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "principal_identity_sha256",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropColumn(
                name: "requester_identity_sha256",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropColumn(
                name: "requester_principal_id",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropColumn(
                name: "identity_schema_version",
                schema: "procurement",
                table: "assignment_authority_evidence");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_worker_id",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "actor_worker_id",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_authority_identity_evidence",
                schema: "procurement",
                table: "spending_authority_identity_evidence",
                sql: "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND grant_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND authority_version>0 AND grant_record_version_at_action>0\nAND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND recipient_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid\nAND length(btrim(authority_reference))>0 AND length(btrim(permission))>0 AND length(btrim(action))>0\nAND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND actor_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND recipient_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND recipient_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND configuration_owner_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'\nAND configuration_owner_principal_id <> '00000000-0000-0000-0000-000000000000'\nAND business_fingerprint IS NOT NULL AND business_fingerprint ~ '^[0-9a-fA-F]{64}$'\nAND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$'\nAND isfinite(action_at) AND created_at=action_at AND created_by=actor_principal_id\nAND version=1 AND updated_at IS NULL AND updated_by IS NULL\nAND ((stage='Created' AND grant_record_version_at_action=1\n    AND created_evidence_id IS NULL AND created_evidence_stage IS NULL\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Submitted' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NULL AND submitted_evidence_stage IS NULL)\nOR (stage='Activated' AND created_evidence_id IS NOT NULL AND created_evidence_id<>id\n    AND created_evidence_stage IS NOT NULL AND created_evidence_stage='Created'\n    AND submitted_evidence_id IS NOT NULL AND submitted_evidence_id<>id AND submitted_evidence_id<>created_evidence_id\n    AND submitted_evidence_stage IS NOT NULL AND submitted_evidence_stage='Submitted'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_submission_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "snapshot_schema_version IN (1,2) AND submitted_business_version > 0 AND policy_version_number > 0 AND core_approval_case_id <> '00000000-0000-0000-0000-000000000000'::uuid AND opening_command_key <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_approval_worker_identity",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "(snapshot_schema_version = 1 AND maker_worker_id IS NULL AND requester_worker_id IS NULL AND worker_identity_sha256 IS NULL) OR (snapshot_schema_version = 2 AND maker_worker_id IS NOT NULL AND requester_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND worker_identity_sha256 IS NOT NULL AND worker_identity_sha256 ~ '^[a-f0-9]{64}$')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_finance_identity_version",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                sql: "finance_identity_schema_version = 0 OR (finance_identity_schema_version = 1 AND snapshot_schema_version = 2)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_budget_checker_identity",
                schema: "procurement",
                table: "budget_check_snapshots",
                sql: "(checker_identity_schema_version = 0 AND checked_by_worker_id IS NULL AND checker_identity_sha256 IS NULL) OR (checker_identity_schema_version = 1 AND checked_by_worker_id IS NOT NULL AND checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND checker_identity_sha256 IS NOT NULL AND checker_identity_sha256 ~ '^[a-f0-9]{64}$')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pr_assignment_authority_identity",
                schema: "procurement",
                table: "assignment_authority_evidence",
                sql: "core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid AND grant_revision > 0 AND authority_version > 0 AND length(btrim(principal_id)) > 0 AND assignee_kind IN ('LineManager','ProcurementManager') AND evidence_sha256 ~ '^[a-f0-9]{64}$' AND (assignee_worker_id IS NULL OR assignee_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
        }
    }
}
