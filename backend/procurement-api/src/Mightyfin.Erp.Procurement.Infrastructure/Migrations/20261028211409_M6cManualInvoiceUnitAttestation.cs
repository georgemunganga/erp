using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M6cManualInvoiceUnitAttestation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_inv_unit_outcome",
                schema: "procurement",
                table: "invoice_unit_review_outcomes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inv_unit_outcome",
                schema: "procurement",
                table: "invoice_unit_review_outcomes",
                sql: "(state IN ('Verified','Returned','Rejected') AND evidence_schema_version IN (3,4)) AND final_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (reviewer_worker_id IS NULL OR reviewer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND reviewer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(completed_at)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Mightyfin.Erp.Procurement.Infrastructure.Data.M6cManualUnitReviewRollbackGuards.BeforeDownSql);
            migrationBuilder.DropCheckConstraint(
                name: "ck_inv_unit_outcome",
                schema: "procurement",
                table: "invoice_unit_review_outcomes");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inv_unit_outcome",
                schema: "procurement",
                table: "invoice_unit_review_outcomes",
                sql: "(state IN ('Verified','Returned','Rejected') AND evidence_schema_version=3) AND final_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (reviewer_worker_id IS NULL OR reviewer_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND reviewer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND reviewer_principal_id <> '00000000-0000-0000-0000-000000000000' AND evidence_sha256 IS NOT NULL AND evidence_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(evidence_json) BETWEEN 1 AND 524288 AND isfinite(completed_at)");
        }
    }
}
