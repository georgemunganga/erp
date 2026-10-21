using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P22SpendingAuthorityConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "spending_authority_grants",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    authority_version = table.Column<int>(type: "integer", nullable: false),
                    principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    permission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    configuration_owner_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    submitted_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_business_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    review_return_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    activated_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    published_business_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    revoked_by_principal_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    superseded_by_grant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_basis = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    maximum_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    all_branches = table.Column<bool>(type: "boolean", nullable: false),
                    branch_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    all_departments = table.Column<bool>(type: "boolean", nullable: false),
                    department_ref = table.Column<Guid>(type: "uuid", nullable: true),
                    all_categories = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_spending_authority_grants", x => x.id);
                    table.UniqueConstraint("AK_spending_authority_grants_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_spending_authority_grants_spending_authority_grants_tenant_~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.superseded_by_grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "spending_authority_categories",
                schema: "procurement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_spending_authority_categories", x => x.id);
                    table.UniqueConstraint("AK_spending_authority_categories_tenant_id_legal_entity_id_id", x => new { x.tenant_id, x.legal_entity_id, x.id });
                    table.ForeignKey(
                        name: "FK_spending_authority_categories_spending_authority_grants_ten~",
                        columns: x => new { x.tenant_id, x.legal_entity_id, x.grant_id },
                        principalSchema: "procurement",
                        principalTable: "spending_authority_grants",
                        principalColumns: new[] { "tenant_id", "legal_entity_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_categories_tenant_id_legal_entity_id_gra~",
                schema: "procurement",
                table: "spending_authority_categories",
                columns: new[] { "tenant_id", "legal_entity_id", "grant_id", "category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_branch_~",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "branch_ref", "department_ref", "state" });

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_princi~1",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_id", "permission", "action", "authority_reference", "authority_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_princip~",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "principal_id", "permission", "action", "authority_reference" },
                unique: true,
                filter: "state = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_spending_authority_grants_tenant_id_legal_entity_id_superse~",
                schema: "procurement",
                table: "spending_authority_grants",
                columns: new[] { "tenant_id", "legal_entity_id", "superseded_by_grant_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spending_authority_categories",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "spending_authority_grants",
                schema: "procurement");
        }
    }
}
