using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7PlanningPortalAndControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procurement_categories",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    parent_code = table.Column<string>(type: "text", nullable: true),
                    expense_account_ref = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_procurement_categories", x => x.id);
                    table.UniqueConstraint("AK_procurement_categories_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "procurement_comments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audience = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    author_subject_id = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_procurement_comments", x => x.id);
                    table.UniqueConstraint("AK_procurement_comments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "procurement_methods",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    policy_version_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_procurement_methods", x => x.id);
                    table.UniqueConstraint("AK_procurement_methods_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_procurement_methods_policy_versions_tenant_id_legal_entity_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.policy_version_id },
                        principalSchema: "procurement",
                        principalTable: "policy_versions",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_plans",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    financial_period_ref = table.Column<string>(type: "text", nullable: false),
                    department_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    owner_subject_id = table.Column<string>(type: "text", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    funding_source_ref = table.Column<string>(type: "text", nullable: true),
                    approval_instance_ref = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_procurement_plans", x => x.id);
                    table.UniqueConstraint("AK_procurement_plans_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "procurement_setup_assessments",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability_key = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    evidence_ref = table.Column<string>(type: "text", nullable: true),
                    evaluated_by = table.Column<string>(type: "text", nullable: true),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_procurement_setup_assessments", x => x.id);
                    table.UniqueConstraint("AK_procurement_setup_assessments_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "supplier_performance_reviews",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    total_score = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    reviewer_subject_id = table.Column<string>(type: "text", nullable: false),
                    dispute_reason = table.Column<string>(type: "text", nullable: true),
                    corrective_plan_ref = table.Column<string>(type: "text", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_supplier_performance_reviews", x => x.id);
                    table.UniqueConstraint("AK_supplier_performance_reviews_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_performance_reviews_suppliers_tenant_id_legal_enti~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_portal_memberships",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_subject_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    invitation_ref = table.Column<string>(type: "text", nullable: true),
                    invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_supplier_portal_memberships", x => x.id);
                    table.UniqueConstraint("AK_supplier_portal_memberships_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_portal_memberships_suppliers_tenant_id_legal_entit~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_id },
                        principalSchema: "procurement",
                        principalTable: "suppliers",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procurement_plan_lines",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    procurement_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    category_code = table.Column<string>(type: "text", nullable: true),
                    unit_code = table.Column<string>(type: "text", nullable: false),
                    estimated_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estimated_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    expected_on = table.Column<DateOnly>(type: "date", nullable: true),
                    funding_source_ref = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_procurement_plan_lines", x => x.id);
                    table.UniqueConstraint("AK_procurement_plan_lines_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_procurement_plan_lines_catalog_items_tenant_id_legal_entity~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.catalog_item_id },
                        principalSchema: "procurement",
                        principalTable: "catalog_items",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_procurement_plan_lines_procurement_plans_tenant_id_legal_en~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.procurement_plan_id },
                        principalSchema: "procurement",
                        principalTable: "procurement_plans",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_performance_criteria",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_performance_review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criterion_code = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    source_record_type = table.Column<string>(type: "text", nullable: true),
                    source_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_supplier_performance_criteria", x => x.id);
                    table.UniqueConstraint("AK_supplier_performance_criteria_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_performance_criteria_supplier_performance_reviews_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.supplier_performance_review_id },
                        principalSchema: "procurement",
                        principalTable: "supplier_performance_reviews",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_categories_tenant_id_legal_entity_id_code",
                schema: "procurement",
                table: "procurement_categories",
                columns: new[] { "tenant_id", "legal_entity_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_categories_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "procurement_categories",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_comments_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "procurement_comments",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_comments_tenant_id_legal_entity_id_owner_type_o~",
                schema: "procurement",
                table: "procurement_comments",
                columns: new[] { "tenant_id", "legal_entity_id", "owner_type", "owner_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_methods_tenant_id_legal_entity_id_code",
                schema: "procurement",
                table: "procurement_methods",
                columns: new[] { "tenant_id", "legal_entity_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_methods_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "procurement_methods",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_methods_tenant_id_legal_entity_id_policy_versio~",
                schema: "procurement",
                table: "procurement_methods",
                columns: new[] { "tenant_id", "legal_entity_id", "policy_version_id" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_plan_lines_tenant_id_legal_entity_id_catalog_it~",
                schema: "procurement",
                table: "procurement_plan_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "catalog_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_plan_lines_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "procurement_plan_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_plan_lines_tenant_id_legal_entity_id_procuremen~",
                schema: "procurement",
                table: "procurement_plan_lines",
                columns: new[] { "tenant_id", "legal_entity_id", "procurement_plan_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_plans_tenant_id_legal_entity_id_created_at",
                schema: "procurement",
                table: "procurement_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_plans_tenant_id_legal_entity_id_number",
                schema: "procurement",
                table: "procurement_plans",
                columns: new[] { "tenant_id", "legal_entity_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_procurement_setup_assessments_tenant_id_legal_entity_id_cap~",
                schema: "procurement",
                table: "procurement_setup_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "capability_key", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_procurement_setup_assessments_tenant_id_legal_entity_id_cre~",
                schema: "procurement",
                table: "procurement_setup_assessments",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_performance_criteria_tenant_id_legal_entity_id_cre~",
                schema: "procurement",
                table: "supplier_performance_criteria",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_performance_criteria_tenant_id_legal_entity_id_sup~",
                schema: "procurement",
                table: "supplier_performance_criteria",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_performance_review_id", "criterion_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_performance_reviews_tenant_id_legal_entity_id_crea~",
                schema: "procurement",
                table: "supplier_performance_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_performance_reviews_tenant_id_legal_entity_id_supp~",
                schema: "procurement",
                table: "supplier_performance_reviews",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "period_from", "period_to" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_portal_memberships_tenant_id_legal_entity_id_creat~",
                schema: "procurement",
                table: "supplier_portal_memberships",
                columns: new[] { "tenant_id", "legal_entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_portal_memberships_tenant_id_legal_entity_id_suppl~",
                schema: "procurement",
                table: "supplier_portal_memberships",
                columns: new[] { "tenant_id", "legal_entity_id", "supplier_id", "identity_subject_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procurement_categories",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "procurement_comments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "procurement_methods",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "procurement_plan_lines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "procurement_setup_assessments",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_performance_criteria",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_portal_memberships",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "procurement_plans",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "supplier_performance_reviews",
                schema: "procurement");
        }
    }
}
