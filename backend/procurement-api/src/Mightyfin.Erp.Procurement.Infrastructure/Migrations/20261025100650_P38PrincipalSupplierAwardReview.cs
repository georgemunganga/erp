using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P38PrincipalSupplierAwardReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "principal_rfq_recommendation_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    award_review_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    revision_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_json = table.Column<string>(type: "text", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p38_rec_submit", x => x.id);
                    table.UniqueConstraint("ak_p38_rec_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.draft_id, x.award_review_submission_id, x.revision_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id, x.revision_sha256 });
                    table.UniqueConstraint("ak_p38_rec_sub_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p38_rec_submit", "schema_version=1 AND id<>'00000000-0000-0000-0000-000000000000'::uuid AND draft_id<>'00000000-0000-0000-0000-000000000000'::uuid AND award_review_submission_id<>'00000000-0000-0000-0000-000000000000'::uuid AND revision_id<>'00000000-0000-0000-0000-000000000000'::uuid AND revision>0 AND round_id<>'00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id<>'00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id<>'00000000-0000-0000-0000-000000000000'::uuid AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND revision_sha256 ~ '^[a-f0-9]{64}$' AND request_sha256 ~ '^[a-f0-9]{64}$' AND snapshot_sha256 ~ '^[a-f0-9]{64}$' AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex')");
                    table.ForeignKey(
                        name: "fk_p38_rec_revision_hash",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.revision_id, x.draft_id, x.revision_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_recommendation_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p38_rec_revision_scope",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.revision_id, x.draft_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_recommendation_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_award_review_submissions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    recommendation_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_revision = table.Column<int>(type: "integer", nullable: false),
                    recommendation_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    policy_version_number = table.Column<int>(type: "integer", nullable: false),
                    policy_rules_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    proposed_total_exact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assignment_pins_json = table.Column<string>(type: "text", nullable: false),
                    assignment_pins_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p38_review_submit", x => x.id);
                    table.UniqueConstraint("ak_p38_review_core_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.core_case_id });
                    table.UniqueConstraint("ak_p38_review_line_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.recommendation_submission_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id });
                    table.UniqueConstraint("ak_p38_review_receipt_owner", x => new { x.tenant_id, x.legal_entity_id, x.id, x.recommendation_submission_id, x.round_id, x.sourcing_event_id, x.branch_id, x.maker_principal_id, x.command_key, x.request_sha256 });
                    table.UniqueConstraint("ak_p38_review_sub_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p38_review_submit", "schema_version=1 AND id<>'00000000-0000-0000-0000-000000000000'::uuid AND recommendation_submission_id<>'00000000-0000-0000-0000-000000000000'::uuid AND draft_id<>'00000000-0000-0000-0000-000000000000'::uuid AND recommendation_revision_id<>'00000000-0000-0000-0000-000000000000'::uuid AND recommendation_revision>0 AND round_id<>'00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id<>'00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id<>'00000000-0000-0000-0000-000000000000'::uuid AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid AND policy_version_id<>'00000000-0000-0000-0000-000000000000'::uuid AND policy_version_number>0 AND policy_key='SOURCING_SUPPLIER_AWARD' AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND recommendation_sha256 ~ '^[a-f0-9]{64}$' AND policy_rules_sha256 ~ '^[a-f0-9]{64}$' AND request_sha256 ~ '^[a-f0-9]{64}$' AND assignment_pins_sha256 ~ '^[a-f0-9]{64}$' AND assignment_pins_sha256=encode(sha256(convert_to(assignment_pins_json,'UTF8')),'hex') AND currency_code ~ '^[A-Z]{3}$' AND proposed_total_exact ~ '^(0|[1-9][0-9]{0,29})(\\.[0-9]{1,8})?$'");
                    table.ForeignKey(
                        name: "fk_p38_review_policy",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id, x.policy_key, x.policy_version_number },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "policy_key", "policy_version_number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p38_review_rec",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.recommendation_submission_id, x.draft_id, x.id, x.recommendation_revision_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id, x.recommendation_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_recommendation_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "award_review_submission_id", "revision_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id", "revision_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_award_review_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_exact = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    retained_quote_total_exact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("pk_p38_review_line", x => x.id);
                    table.UniqueConstraint("ak_p38_line_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p38_review_line", "id<>'00000000-0000-0000-0000-000000000000'::uuid AND submission_id<>'00000000-0000-0000-0000-000000000000'::uuid AND recommendation_submission_id<>'00000000-0000-0000-0000-000000000000'::uuid AND round_id<>'00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id<>'00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id<>'00000000-0000-0000-0000-000000000000'::uuid AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND offer_id<>'00000000-0000-0000-0000-000000000000'::uuid AND supplier_id<>'00000000-0000-0000-0000-000000000000'::uuid AND sourcing_line_id<>'00000000-0000-0000-0000-000000000000'::uuid AND offer_sha256 ~ '^[a-f0-9]{64}$' AND quantity_exact ~ '^(0|[1-9][0-9]{0,13})(\\.[0-9]{1,4})?$' AND quantity_exact !~ '^0(\\.0+)?$' AND currency_code ~ '^[A-Z]{3}$' AND retained_quote_total_exact ~ '^(0|[1-9][0-9]{0,29})(\\.[0-9]{1,8})?$'");
                    table.ForeignKey(
                        name: "fk_p38_line_offer",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.offer_id, x.round_id, x.sourcing_event_id, x.branch_id, x.supplier_id, x.offer_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_offer_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "supplier_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p38_line_review",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.recommendation_submission_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_award_review_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "recommendation_submission_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p38_line_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_line_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_award_review_outcomes",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    terminal_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    core_state = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_p38_review_outcome", x => x.id);
                    table.UniqueConstraint("ak_p38_outcome_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p38_review_outcome", "id<>'00000000-0000-0000-0000-000000000000'::uuid AND submission_id<>'00000000-0000-0000-0000-000000000000'::uuid AND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid AND terminal_decision_id<>'00000000-0000-0000-0000-000000000000'::uuid AND ((core_state='Approved' AND source_outcome='CoreApprovedPendingAward') OR (core_state='Returned' AND source_outcome='Returned') OR (core_state='Rejected' AND source_outcome='Rejected'))");
                    table.ForeignKey(
                        name: "fk_p38_outcome_review",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.core_case_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_award_review_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "core_case_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_award_review_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p38_review_receipt", x => x.id);
                    table.UniqueConstraint("ak_p38_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p38_review_receipt", "id<>'00000000-0000-0000-0000-000000000000'::uuid AND submission_id<>'00000000-0000-0000-0000-000000000000'::uuid AND recommendation_submission_id<>'00000000-0000-0000-0000-000000000000'::uuid AND round_id<>'00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id<>'00000000-0000-0000-0000-000000000000'::uuid AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND request_sha256 ~ '^[a-f0-9]{64}$' AND response_sha256 ~ '^[a-f0-9]{64}$' AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')");
                    table.ForeignKey(
                        name: "fk_p38_receipt_review",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.submission_id, x.recommendation_submission_id, x.round_id, x.sourcing_event_id, x.branch_id, x.actor_principal_id, x.command_key, x.request_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_award_review_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "recommendation_submission_id", "round_id", "sourcing_event_id", "branch_id", "maker_principal_id", "command_key", "request_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_award_review_lines_tenant_id_legal_entity_id_~",
                schema: "procurement",
                table: "principal_rfq_award_review_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_line_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_award_review_lines_tenant_id_legal_entity_id~1",
                schema: "procurement",
                table: "principal_rfq_award_review_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "offer_id", "round_id", "sourcing_event_id", "branch_id", "supplier_id", "offer_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_award_review_lines_tenant_id_legal_entity_id~2",
                schema: "procurement",
                table: "principal_rfq_award_review_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "recommendation_submission_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p38_line_choice",
                schema: "procurement",
                table: "principal_rfq_award_review_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "offer_id", "sourcing_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_award_review_outcomes_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "principal_rfq_award_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "core_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p38_outcome_core_decision",
                schema: "procurement",
                table: "principal_rfq_award_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "terminal_decision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p38_outcome_submission",
                schema: "procurement",
                table: "principal_rfq_award_review_outcomes",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_award_review_receipts_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "principal_rfq_award_review_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "submission_id", "recommendation_submission_id", "round_id", "sourcing_event_id", "branch_id", "actor_principal_id", "command_key", "request_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p38_receipt_actor_key",
                schema: "procurement",
                table: "principal_rfq_award_review_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_award_review_submissions_tenant_id_legal_ent~1",
                schema: "procurement",
                table: "principal_rfq_award_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "recommendation_submission_id", "draft_id", "id", "recommendation_revision_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id", "recommendation_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_award_review_submissions_tenant_id_legal_enti~",
                schema: "procurement",
                table: "principal_rfq_award_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id", "policy_key", "policy_version_number" });

            migrationBuilder.CreateIndex(
                name: "ux_p38_review_actor_key",
                schema: "procurement",
                table: "principal_rfq_award_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "maker_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p38_review_core",
                schema: "procurement",
                table: "principal_rfq_award_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "core_case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p38_review_rec",
                schema: "procurement",
                table: "principal_rfq_award_review_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "recommendation_submission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_submissions_tenant_id_legal_e~1",
                schema: "procurement",
                table: "principal_rfq_recommendation_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "draft_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_submissions_tenant_id_legal_en~",
                schema: "procurement",
                table: "principal_rfq_recommendation_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "draft_id", "revision_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p38_rec_actor_key",
                schema: "procurement",
                table: "principal_rfq_recommendation_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "maker_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_p38_rec_revision",
                schema: "procurement",
                table: "principal_rfq_recommendation_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "revision_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P38SupplierAwardReviewRollbackGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "principal_rfq_award_review_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_award_review_outcomes",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_award_review_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_award_review_submissions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_recommendation_submissions",
                schema: "procurement");
        }
    }
}
