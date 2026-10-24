namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Root must place this as the first statement of the genuine P33 Down.
// The migration transaction holds these locks until the old schema is restored.
// An edited or merely origin-captured Draft cannot lose its retained history.
// The existing P30 receipt table also retains Edit acknowledgments after a
// selective removal of P33-owned rows; restoring its old CHECK would reject them.
internal static class P33PrincipalOrderDraftRevisionGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_order_commercial_origins,
          procurement.principal_order_draft_commercial_revisions,
          procurement.principal_order_effective_source_slices,
          procurement.principal_order_draft_receipts
          IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_order_commercial_origins)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_draft_commercial_revisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_effective_source_slices)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_draft_receipts
                       WHERE operation = 'Edit') THEN
            RAISE EXCEPTION 'P33 principal order draft revision evidence prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
