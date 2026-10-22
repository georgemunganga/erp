using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P22CorePrincipalOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "requester_worker_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "requester_principal_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_requests_tenant_id_legal_entity_id_requester_princ~",
                schema: "procurement",
                table: "purchase_requests",
                columns: new[] { "tenant_id", "legal_entity_id", "requester_principal_id", "status" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_purchase_request_identity",
                schema: "procurement",
                table: "purchase_requests",
                sql: "(requester_principal_id IS NULL OR requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_worker_id IS NULL OR requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_principal_id IS NOT NULL OR requester_worker_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never erase user ownership, fabricate a missing worker, or discard
            // identity evidence retained after an exceptional owner-side purge.
            // The normal EF migration transaction holds these locks through DDL.
            migrationBuilder.Sql("""
                LOCK TABLE procurement.purchase_requests, procurement.audit_events IN ACCESS EXCLUSIVE MODE;
                DO $identity_down$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM procurement.purchase_requests
                        WHERE requester_principal_id IS NOT NULL OR requester_worker_id IS NULL
                    ) OR EXISTS (
                        SELECT 1 FROM procurement.audit_events
                        WHERE record_type = 'PurchaseRequest' AND (
                            (jsonb_typeof(changed_fields_json) = 'array' AND
                                (changed_fields_json ? 'RequesterPrincipalId' OR changed_fields_json ? 'requesterPrincipalId'))
                            OR (jsonb_typeof(changed_fields_json) = 'object' AND (
                                COALESCE(changed_fields_json #> '{before,RequesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                                OR COALESCE(changed_fields_json #> '{after,RequesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                                OR COALESCE(changed_fields_json #> '{before,requesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                                OR COALESCE(changed_fields_json #> '{after,requesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                                OR COALESCE(changed_fields_json #> '{snapshot,RequesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                                OR COALESCE(changed_fields_json #> '{snapshot,requesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                                OR COALESCE(changed_fields_json #> '{submittedSnapshot,RequesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                                OR COALESCE(changed_fields_json #> '{submittedSnapshot,requesterPrincipalId}', 'null'::jsonb) <> 'null'::jsonb
                            ))
                        )
                    ) THEN
                        RAISE EXCEPTION 'Retained Core request identity prevents destructive downgrade.' USING ERRCODE = '55000';
                    END IF;
                END;
                $identity_down$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_purchase_requests_tenant_id_legal_entity_id_requester_princ~",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_purchase_request_identity",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.DropColumn(
                name: "requester_principal_id",
                schema: "procurement",
                table: "purchase_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "requester_worker_id",
                schema: "procurement",
                table: "purchase_requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
