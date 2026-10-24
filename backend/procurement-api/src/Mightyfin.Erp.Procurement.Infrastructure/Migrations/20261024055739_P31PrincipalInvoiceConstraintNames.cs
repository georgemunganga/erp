using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P31PrincipalInvoiceConstraintNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL keys are already referenced by the P31 ownership FKs.
            // Rename in place: scaffolded drop/add would break those dependencies.
            migrationBuilder.Sql("""
                ALTER TABLE procurement.principal_receiving_sources
                    RENAME CONSTRAINT "AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~" TO ak_pi_receiving_source;
                ALTER TABLE procurement.principal_receiving_facts
                    RENAME CONSTRAINT "AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~" TO ak_pi_receiving_fact;
                ALTER TABLE procurement.principal_order_issued_versions
                    RENAME CONSTRAINT "AK_principal_order_issued_versions_tenant_id_legal_entity_id_i~" TO ak_pi_issued_order;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(P31PrincipalInvoiceOwnershipGuards.BeforeDownSql);
            migrationBuilder.Sql("""
                ALTER TABLE procurement.principal_receiving_sources
                    RENAME CONSTRAINT ak_pi_receiving_source TO "AK_principal_receiving_sources_tenant_id_legal_entity_id_id_pu~";
                ALTER TABLE procurement.principal_receiving_facts
                    RENAME CONSTRAINT ak_pi_receiving_fact TO "AK_principal_receiving_facts_tenant_id_legal_entity_id_id_sour~";
                ALTER TABLE procurement.principal_order_issued_versions
                    RENAME CONSTRAINT ak_pi_issued_order TO "AK_principal_order_issued_versions_tenant_id_legal_entity_id_i~";
                """);
        }
    }
}
