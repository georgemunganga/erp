namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>Uninstalled first-Down guard for the genuine M5d migration.</summary>
internal static class M5dPrincipalGoodsReturnRollbackGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_goods_return_drafts,
          procurement.principal_goods_return_revisions,
          procurement.principal_goods_return_receipts,
          procurement.audit_events,
          procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_goods_return_drafts)
            OR EXISTS (SELECT 1 FROM procurement.principal_goods_return_revisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_goods_return_receipts)
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type IN ('PrincipalGoodsReturnDraft','PrincipalGoodsReturnRevision',
                         'PrincipalGoodsReturnCommandReceipt'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE aggregate_type = 'PrincipalGoodsReturnDraft'
                          OR event_type IN ('procurement.goods_return.draft_create.v1',
                            'procurement.goods_return.draft_revise.v1',
                            'procurement.goods_return.draft_cancel.v1')) THEN
            RAISE EXCEPTION 'M5d goods return draft evidence prevents downgrade'
              USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
