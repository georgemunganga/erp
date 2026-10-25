using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P36HumanRecommendationDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_p36_rec_source_line",
                schema: "procurement",
                table: "sourcing_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_p36_rec_seal",
                schema: "procurement",
                table: "principal_rfq_commercial_round_seals",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "submission_set_sha256" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_p36_rec_offer",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions",
                columns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "supplier_id", "snapshot_sha256" });

            migrationBuilder.CreateTable(
                name: "principal_rfq_recommendation_drafts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_round_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opening_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    offer_set_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    prepared_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_plan_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_definition_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    maker_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    maker_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_revision = table.Column<int>(type: "integer", nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("pk_p32_rec_draft", x => x.id);
                    table.UniqueConstraint("ak_p32_rec_draft_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p36_rec_head", x => new { x.tenant_id, x.legal_entity_id, x.id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id });
                    table.CheckConstraint("ck_p36_rec_head", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND round_id <> '00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id <> '00000000-0000-0000-0000-000000000000'::uuid AND seal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND prepared_plan_id <> '00000000-0000-0000-0000-000000000000'::uuid AND evaluation_plan_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND current_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND maker_principal_id IS NOT NULL AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND maker_principal_id <> '00000000-0000-0000-0000-000000000000' AND (maker_worker_alias_id IS NULL OR maker_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND current_revision BETWEEN 1 AND 100 AND approved_round_sha256 ~ '^[a-f0-9]{64}$' AND opening_sha256 ~ '^[a-f0-9]{64}$' AND offer_set_sha256 ~ '^[a-f0-9]{64}$' AND evaluation_definition_sha256 ~ '^[a-f0-9]{64}$' AND current_snapshot_sha256 ~ '^[a-f0-9]{64}$'");
                    table.ForeignKey(
                        name: "fk_p36_rec_opened",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.unsealing_id, x.round_id, x.sourcing_event_id, x.branch_id, x.opening_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_offer_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p36_rec_plan",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.prepared_plan_id, x.sourcing_event_id, x.branch_id, x.evaluation_plan_revision_id, x.evaluation_definition_sha256 },
                        principalSchema: "procurement",
                        principalTable: "rfq_prepared_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id", "branch_id", "draft_revision_id", "evaluation_rules_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p36_rec_round",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.round_id, x.prepared_plan_id, x.sourcing_event_id, x.branch_id, x.approved_round_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_openings",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "prepared_plan_id", "sourcing_event_id", "branch_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p36_rec_seal",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.seal_id, x.round_id, x.sourcing_event_id, x.branch_id, x.offer_set_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_round_seals",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "submission_set_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_recommendation_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    previous_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    actor_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_p32_rec_rev", x => x.id);
                    table.UniqueConstraint("ak_p32_rec_rev_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.UniqueConstraint("ak_p36_rec_line_rev", x => new { x.tenant_id, x.legal_entity_id, x.id, x.draft_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id });
                    table.UniqueConstraint("ak_p36_rec_prev", x => new { x.tenant_id, x.legal_entity_id, x.id, x.draft_id, x.snapshot_sha256 });
                    table.UniqueConstraint("ak_p36_rec_revision", x => new { x.tenant_id, x.legal_entity_id, x.id, x.draft_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id, x.actor_principal_id, x.command_key, x.request_sha256 });
                    table.CheckConstraint("ck_p36_rec_revision", "schema_version=1 AND id <> '00000000-0000-0000-0000-000000000000'::uuid AND draft_id <> '00000000-0000-0000-0000-000000000000'::uuid AND round_id <> '00000000-0000-0000-0000-000000000000'::uuid AND unsealing_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_event_id <> '00000000-0000-0000-0000-000000000000'::uuid AND branch_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND revision BETWEEN 1 AND 100 AND request_sha256 ~ '^[a-f0-9]{64}$' AND snapshot_sha256 ~ '^[a-f0-9]{64}$' AND snapshot_sha256=encode(sha256(convert_to(snapshot_json,'UTF8')),'hex') AND ((revision=1 AND previous_revision_id IS NULL AND previous_snapshot_sha256 IS NULL) OR (revision>1 AND previous_revision_id IS NOT NULL AND previous_revision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND previous_revision_id<>id AND previous_snapshot_sha256 IS NOT NULL AND previous_snapshot_sha256 ~ '^[a-f0-9]{64}$'))");
                    table.ForeignKey(
                        name: "fk_p36_rec_prev",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.previous_revision_id, x.draft_id, x.previous_snapshot_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_recommendation_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p36_rec_revision_head",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_recommendation_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_recommendation_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sourcing_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_exact = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proposed_value_exact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("pk_p32_rec_line", x => x.id);
                    table.UniqueConstraint("ak_p32_rec_line_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p36_rec_line", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND draft_id <> '00000000-0000-0000-0000-000000000000'::uuid AND revision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND offer_id <> '00000000-0000-0000-0000-000000000000'::uuid AND supplier_id <> '00000000-0000-0000-0000-000000000000'::uuid AND sourcing_line_id <> '00000000-0000-0000-0000-000000000000'::uuid AND quantity_exact IS NOT NULL AND quantity_exact ~ '^(0|[1-9][0-9]{0,13})(\\.[0-9]{1,4})?$' AND quantity_exact !~ '^0(\\.0+)?$' AND proposed_value_exact IS NOT NULL AND proposed_value_exact ~ '^(0|[1-9][0-9]{0,29})(\\.[0-9]{1,8})?$' AND proposed_value_exact !~ '^0(\\.0+)?$' AND offer_sha256 ~ '^[a-f0-9]{64}$' AND currency_code ~ '^[A-Z]{3}$' AND rationale IS NOT NULL AND length(btrim(rationale))>0");
                    table.ForeignKey(
                        name: "fk_p36_rec_line_offer",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.offer_id, x.round_id, x.sourcing_event_id, x.branch_id, x.supplier_id, x.offer_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_commercial_offer_submissions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "opening_id", "sourcing_event_id", "branch_id", "supplier_id", "snapshot_sha256" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p36_rec_line_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.revision_id, x.draft_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_recommendation_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_p36_rec_source_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.sourcing_line_id, x.sourcing_event_id },
                        principalSchema: "procurement",
                        principalTable: "sourcing_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "sourcing_event_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_rfq_recommendation_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unsealing_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_p32_rec_ack", x => x.id);
                    table.UniqueConstraint("ak_p32_rec_ack_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_p36_rec_ack", "id <> '00000000-0000-0000-0000-000000000000'::uuid AND draft_id <> '00000000-0000-0000-0000-000000000000'::uuid AND revision_id <> '00000000-0000-0000-0000-000000000000'::uuid AND round_id <> '00000000-0000-0000-0000-000000000000'::uuid AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND request_sha256 ~ '^[a-f0-9]{64}$' AND response_sha256 ~ '^[a-f0-9]{64}$' AND response_sha256=encode(sha256(convert_to(response_json,'UTF8')),'hex')");
                    table.ForeignKey(
                        name: "fk_p36_rec_ack_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.revision_id, x.draft_id, x.round_id, x.unsealing_id, x.sourcing_event_id, x.branch_id, x.actor_principal_id, x.command_key, x.request_sha256 },
                        principalSchema: "procurement",
                        principalTable: "principal_rfq_recommendation_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "draft_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id", "actor_principal_id", "command_key", "request_sha256" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_drafts_tenant_id_legal_entity_~",
                schema: "procurement",
                table: "principal_rfq_recommendation_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "prepared_plan_id", "sourcing_event_id", "branch_id", "evaluation_plan_revision_id", "evaluation_definition_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_drafts_tenant_id_legal_entity~1",
                schema: "procurement",
                table: "principal_rfq_recommendation_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "round_id", "prepared_plan_id", "sourcing_event_id", "branch_id", "approved_round_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_drafts_tenant_id_legal_entity~2",
                schema: "procurement",
                table: "principal_rfq_recommendation_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "seal_id", "round_id", "sourcing_event_id", "branch_id", "offer_set_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_drafts_tenant_id_legal_entity~3",
                schema: "procurement",
                table: "principal_rfq_recommendation_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "unsealing_id", "round_id", "sourcing_event_id", "branch_id", "opening_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p36_rec_round_maker",
                schema: "procurement",
                table: "principal_rfq_recommendation_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "round_id", "maker_principal_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_lines_tenant_id_legal_entity_~1",
                schema: "procurement",
                table: "principal_rfq_recommendation_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "offer_id", "round_id", "sourcing_event_id", "branch_id", "supplier_id", "offer_sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_lines_tenant_id_legal_entity_~2",
                schema: "procurement",
                table: "principal_rfq_recommendation_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "draft_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_lines_tenant_id_legal_entity_i~",
                schema: "procurement",
                table: "principal_rfq_recommendation_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "sourcing_line_id", "sourcing_event_id" });

            migrationBuilder.CreateIndex(
                name: "ux_p36_rec_line_choice",
                schema: "procurement",
                table: "principal_rfq_recommendation_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "offer_id", "sourcing_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_receipts_tenant_id_legal_entit~",
                schema: "procurement",
                table: "principal_rfq_recommendation_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "revision_id", "draft_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id", "actor_principal_id", "command_key", "request_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p36_rec_actor_key",
                schema: "procurement",
                table: "principal_rfq_recommendation_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "actor_principal_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_revisions_tenant_id_legal_ent~1",
                schema: "procurement",
                table: "principal_rfq_recommendation_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "round_id", "unsealing_id", "sourcing_event_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_rfq_recommendation_revisions_tenant_id_legal_enti~",
                schema: "procurement",
                table: "principal_rfq_recommendation_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "previous_revision_id", "draft_id", "previous_snapshot_sha256" });

            migrationBuilder.CreateIndex(
                name: "ux_p36_rec_revision_no",
                schema: "procurement",
                table: "principal_rfq_recommendation_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P36RecommendationDraftGuards.BeforeDownSql);

            migrationBuilder.DropTable(
                name: "principal_rfq_recommendation_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_recommendation_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_recommendation_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_rfq_recommendation_drafts",
                schema: "procurement");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_p36_rec_source_line",
                schema: "procurement",
                table: "sourcing_lines");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_p36_rec_seal",
                schema: "procurement",
                table: "principal_rfq_commercial_round_seals");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_p36_rec_offer",
                schema: "procurement",
                table: "principal_rfq_commercial_offer_submissions");
        }
    }
}
