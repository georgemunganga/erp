namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Root must place this as the first statement of the genuine P33 Down.
// The migration transaction holds these locks until the old schema is restored.
// An edited or merely origin-captured Draft cannot lose its retained history.
internal static class P33PrincipalOrderDraftRevisionGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_order_commercial_origins,
          procurement.principal_order_draft_commercial_revisions,
          procurement.principal_order_effective_source_slices
          IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_order_commercial_origins)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_draft_commercial_revisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_order_effective_source_slices) THEN
            RAISE EXCEPTION 'P33 principal order draft revision evidence prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
