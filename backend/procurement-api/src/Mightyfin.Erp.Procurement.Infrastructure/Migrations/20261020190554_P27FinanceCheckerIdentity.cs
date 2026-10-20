using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P27FinanceCheckerIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "finance_identity_schema_version",
                schema: "procurement",
                table: "purchase_request_approval_submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "checked_by_worker_id",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "checker_identity_schema_version",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "checker_identity_sha256",
                schema: "procurement",
                table: "budget_check_snapshots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retained identity/pins and their audit cannot be silently removed by downgrade.
            // Lock before inspecting usage so concurrent capture cannot race the veto.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.budget_check_snapshots,
                    procurement.purchase_request_approval_submissions,
                    procurement.audit_events, procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM procurement.budget_check_snapshots
                        WHERE checker_identity_schema_version <> 0
                           OR checked_by_worker_id IS NOT NULL OR checker_identity_sha256 IS NOT NULL)
                      OR EXISTS (SELECT 1 FROM procurement.purchase_request_approval_submissions
                        WHERE finance_identity_schema_version <> 0)
                      OR EXISTS (SELECT 1 FROM procurement.audit_events
                        WHERE COALESCE(changed_fields_json::text,'') ~
                            '"(CheckerIdentitySchemaVersion|CheckedByWorkerId|CheckerIdentitySha256|FinanceIdentitySchemaVersion|checkerIdentitySchemaVersion|checkedByWorkerId|checkerIdentitySha256|financeIdentitySchemaVersion)"')
                      OR EXISTS (SELECT 1 FROM procurement.outbox_events
                        WHERE COALESCE(payload_json::text,'') ~
                            '"(CheckerIdentitySchemaVersion|CheckedByWorkerId|CheckerIdentitySha256|FinanceIdentitySchemaVersion|checkerIdentitySchemaVersion|checkedByWorkerId|checkerIdentitySha256|financeIdentitySchemaVersion)"')
                    THEN
                        RAISE EXCEPTION 'FinanceCheckerIdentityRollbackWouldLoseEvidence';
                    END IF;
                END $guard$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_pr_finance_identity_version",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_budget_checker_identity",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "finance_identity_schema_version",
                schema: "procurement",
                table: "purchase_request_approval_submissions");

            migrationBuilder.DropColumn(
                name: "checked_by_worker_id",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "checker_identity_schema_version",
                schema: "procurement",
                table: "budget_check_snapshots");

            migrationBuilder.DropColumn(
                name: "checker_identity_sha256",
                schema: "procurement",
                table: "budget_check_snapshots");
        }
    }
}
