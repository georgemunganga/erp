using Microsoft.EntityFrameworkCore;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Compatibility fence for existing invoice correction during the
/// additive P31 rollout. This is not command readiness or activation.</summary>
internal static class ProcurementPrincipalInvoiceSchema
{
    private const string CountSql = """
        SELECT count(*)::integer AS "Value" FROM pg_tables WHERE schemaname='procurement'
        AND tablename IN ('principal_invoice_states','principal_invoice_mappings',
          'principal_invoice_mapping_lines','principal_invoice_unit_reviews',
          'principal_invoice_review_assignments','principal_invoice_review_attestations',
          'principal_invoice_review_outcomes','principal_invoice_comparisons',
          'principal_invoice_exposure_states','principal_invoice_claims','principal_invoice_receipts',
          'principal_invoice_discrepancies','principal_invoice_discrepancy_actions')
        """;

    internal static bool Installed(ProcurementDbContext db)
    {
        if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return true;
        return Complete(db.Database.SqlQueryRaw<int>(CountSql).Single());
    }

    internal static bool Complete(int count) => count switch
        {
            0 => false,
            13 => true,
            _ => throw new InvalidOperationException("PrincipalInvoiceSchemaIncomplete")
        };
}
