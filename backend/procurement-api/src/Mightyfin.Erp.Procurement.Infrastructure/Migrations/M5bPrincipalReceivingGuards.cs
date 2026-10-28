namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

/// <summary>Run first in the transactional Down, before dropping constraints,
/// supporting indexes or tables. Retained receiving evidence requires fix-forward;
/// this guard never deletes evidence or resets PO/PR acceptance counters.</summary>
internal static class M5bPrincipalReceivingGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.principal_receiving_sources,
          procurement.principal_receiving_facts,
          procurement.principal_receiving_drafts,
          procurement.principal_receiving_revisions,
          procurement.principal_receiving_receipts,
          procurement.principal_receiving_line_bindings,
          procurement.audit_events,
          procurement.outbox_events IN ACCESS EXCLUSIVE MODE;
        DO $m5b_principal_receiving_down$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.principal_receiving_sources)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_facts)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_drafts)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_revisions)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_receipts)
            OR EXISTS (SELECT 1 FROM procurement.principal_receiving_line_bindings)
            OR EXISTS (SELECT 1 FROM procurement.audit_events
                       WHERE record_type IN ('PrincipalReceiving',
                         'PrincipalReceivingSourceState','PrincipalReceivingFact',
                         'PrincipalReceivingDraftHead','PrincipalReceivingDraftRevision',
                         'PrincipalReceivingCommandReceipt','PrincipalReceivingDraftLineBinding')
                         OR action IN ('PrincipalReceivingCreate','PrincipalReceivingRevise',
                           'PrincipalReceivingCancel','PrincipalReceivingRecord',
                           'PrincipalReceivingInspect','PrincipalReceivingAccept',
                           'PrincipalReceivingCertify'))
            OR EXISTS (SELECT 1 FROM procurement.outbox_events
                       WHERE aggregate_type='PrincipalReceiving'
                         OR idempotency_key LIKE 'receiving.principal.%')
          THEN
            RAISE EXCEPTION USING ERRCODE='55000',
              MESSAGE='Retained principal receiving evidence prevents downgrade; use compatible code or fix forward.';
          END IF;
        END
        $m5b_principal_receiving_down$;
        """;
}
