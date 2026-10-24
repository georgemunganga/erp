namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>
/// First statement of a future corrective migration's Down path, before restoring the
/// narrower P28/P30 authority identity CHECK. The generator must wire this explicitly.
/// A retained PO or sourcing publication authority cannot be translated into PR authority.
/// </summary>
internal static class P30AuthorityPurposeCorrectionGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.spending_authority_grants,
          procurement.spending_authority_identity_evidence IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (
            SELECT 1 FROM procurement.spending_authority_identity_evidence
            WHERE schema_version = 2
              AND (action <> 'PurchaseRequest.Approve'
                OR permission <> 'procurement-approval-decide')
          ) THEN
            RAISE EXCEPTION 'Retained non-PR principal authority identity prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
