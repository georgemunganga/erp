using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P37PrincipalGoodsReturnDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "principal_goods_return_drafts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acceptance_fact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    acceptance_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accepted_fact_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    creator_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    creator_worker_alias_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_principal_goods_return_drafts", x => x.id);
                    table.UniqueConstraint("ak_pgret_draft_branch", x => new { x.tenant_id, x.legal_entity_id, x.id, x.branch_id });
                    table.UniqueConstraint("ak_pgret_draft_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pgret_draft", "schema_version=1 AND revision>0 AND state IN ('Draft','Cancelled') AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND current_revision_id<>'00000000-0000-0000-0000-000000000000'::uuid AND source_sha256 IS NOT NULL AND source_sha256 ~ '^[0-9a-f]{64}$' AND acceptance_sha256 IS NOT NULL AND acceptance_sha256 ~ '^[0-9a-f]{64}$' AND accepted_fact_quantity>0 AND creator_principal_id IS NOT NULL AND creator_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND creator_principal_id<>'00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_pgret_acceptance",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.acceptance_fact_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_facts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgret_issued",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.issued_version_id },
                        principalSchema: "procurement",
                        principalTable: "principal_order_issued_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgret_order_line",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.purchase_order_line_id },
                        principalSchema: "procurement",
                        principalTable: "purchase_order_lines",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgret_receipt",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.receipt_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgret_source",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.source_state_id },
                        principalSchema: "procurement",
                        principalTable: "principal_receiving_sources",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_goods_return_revisions",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence_reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    proposal_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_principal_goods_return_revisions", x => x.id);
                    table.UniqueConstraint("ak_pgret_revision_command", x => new { x.tenant_id, x.legal_entity_id, x.draft_id, x.revision, x.actor_principal_id, x.command_key });
                    table.UniqueConstraint("ak_pgret_revision_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pgret_revision", "schema_version=1 AND revision>0 AND state IN ('Draft','Cancelled') AND quantity>0 AND reason IS NOT NULL AND length(btrim(reason))>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND proposal_sha256 IS NOT NULL AND proposal_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id<>'00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_pgret_revision_draft",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id },
                        principalSchema: "procurement",
                        principalTable: "principal_goods_return_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "principal_goods_return_receipts",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_key = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    actor_principal_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    request_json = table.Column<string>(type: "text", nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    response_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    result_revision = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_principal_goods_return_receipts", x => x.id);
                    table.UniqueConstraint("ak_pgret_receipt_scope", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.CheckConstraint("ck_pgret_receipt", "schema_version=1 AND operation IN ('Create','Revise','Cancel') AND ((operation='Create' AND response_status=201 AND result_revision=1) OR (operation IN ('Revise','Cancel') AND response_status=200 AND result_revision>1)) AND result_revision>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND request_sha256 IS NOT NULL AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 IS NOT NULL AND response_sha256 ~ '^[0-9a-f]{64}$' AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id<>'00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "fk_pgret_receipt_draft",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id, x.branch_id },
                        principalSchema: "procurement",
                        principalTable: "principal_goods_return_drafts",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id", "branch_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pgret_receipt_revision",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.draft_id, x.result_revision, x.actor_principal_id, x.command_key },
                        principalSchema: "procurement",
                        principalTable: "principal_goods_return_revisions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "draft_id", "revision", "actor_principal_id", "command_key" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pgret_branch_created",
                schema: "procurement",
                table: "principal_goods_return_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "branch_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_acc~",
                schema: "procurement",
                table: "principal_goods_return_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "acceptance_fact_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_iss~",
                schema: "procurement",
                table: "principal_goods_return_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "issued_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_pur~",
                schema: "procurement",
                table: "principal_goods_return_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "purchase_order_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_rec~",
                schema: "procurement",
                table: "principal_goods_return_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_goods_return_drafts_tenant_id_legal_entity_id_sou~",
                schema: "procurement",
                table: "principal_goods_return_drafts",
                columns: new[] { "tenant_id", "legal_entity_id", "source_state_id" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_goods_return_receipts_tenant_id_legal_entity_id_~1",
                schema: "procurement",
                table: "principal_goods_return_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "result_revision", "actor_principal_id", "command_key" });

            migrationBuilder.CreateIndex(
                name: "IX_principal_goods_return_receipts_tenant_id_legal_entity_id_d~",
                schema: "procurement",
                table: "principal_goods_return_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ux_pgret_command",
                schema: "procurement",
                table: "principal_goods_return_receipts",
                columns: new[] { "tenant_id", "legal_entity_id", "command_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pgret_revision",
                schema: "procurement",
                table: "principal_goods_return_revisions",
                columns: new[] { "tenant_id", "legal_entity_id", "draft_id", "revision" },
                unique: true);
            migrationBuilder.Sql(P37PrincipalGoodsReturnOwnershipSql.AfterUpSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P37PrincipalGoodsReturnRollbackGuards.BeforeDownSql);
            migrationBuilder.Sql(P37PrincipalGoodsReturnOwnershipSql.BeforeDropSql);

            migrationBuilder.DropTable(
                name: "principal_goods_return_receipts",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_goods_return_revisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "principal_goods_return_drafts",
                schema: "procurement");
        }
    }
}
