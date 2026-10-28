namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>First Down guard for B2's dual issued-parent references. B1 source
/// evidence remains protected. Independently pinned source-3 result/exposure
/// rows also veto removal of their discriminator and generated parent links.</summary>
internal static class M6bIssuedParentRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.invoice_match_exposure_states IN ACCESS EXCLUSIVE MODE;
        """ + M6bPrincipalInvoiceRollbackGuards.BeforeDownSql + """
        DO $m6b_issued_parent_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.invoice_match_exposure_states
                     WHERE source_schema_version=3)
            OR EXISTS (SELECT 1 FROM procurement.invoice_match_results
                       WHERE source_schema_version=3)
          THEN
            RAISE EXCEPTION USING ERRCODE='55000',
              MESSAGE='Retained principal issued-parent exposure or result prevents downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m6b_issued_parent_down$;
        """;
}
