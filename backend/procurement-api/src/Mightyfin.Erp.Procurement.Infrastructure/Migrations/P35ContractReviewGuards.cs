namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Install first in the genuine generated P35 Down. Review evidence must remain
// attached to its Core decisions; deleting a table is not a review reversal.
internal static class P35ContractReviewGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.contract_review_policy_identity,
          procurement.contract_review_submissions,
          procurement.contract_review_votes,
          procurement.contract_review_corrections,
          procurement.contract_review_command_receipts
          IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.contract_review_policy_identity)
            OR EXISTS (SELECT 1 FROM procurement.contract_review_submissions)
            OR EXISTS (SELECT 1 FROM procurement.contract_review_votes)
            OR EXISTS (SELECT 1 FROM procurement.contract_review_corrections)
            OR EXISTS (SELECT 1 FROM procurement.contract_review_command_receipts) THEN
            RAISE EXCEPTION 'P35 contract review evidence prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
