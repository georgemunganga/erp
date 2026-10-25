namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Authored owner contract only. Install first in the future genuine EF-generated
// P36 Down; this class is not a migration and does not install any schema.
internal static class P36RecommendationDraftGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_rfq_recommendation_drafts,
          procurement.principal_rfq_recommendation_revisions,
          procurement.principal_rfq_recommendation_lines,
          procurement.principal_rfq_recommendation_receipts
          IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_rfq_recommendation_drafts)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_recommendation_revisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_recommendation_lines)
            OR EXISTS (SELECT 1 FROM procurement.principal_rfq_recommendation_receipts) THEN
            RAISE EXCEPTION 'P36 recommendation draft history prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
