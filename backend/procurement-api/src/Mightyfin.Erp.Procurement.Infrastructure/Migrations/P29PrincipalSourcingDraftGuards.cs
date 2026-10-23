namespace Mightyfin.Erp.Procurement.Infrastructure.Migrations;

// Run as the first statement of P29 Down, before restoring the worker-only constraints.
// The migration transaction keeps these locks until the old schema is restored.
internal static class P29PrincipalSourcingDraftGuards
{
    internal const string BeforeDownSql = """
        LOCK TABLE procurement.sourcing_events, procurement.sourcing_draft_evidence,
          procurement.sourcing_demand_bindings, procurement.sourcing_command_receipts
          IN ACCESS EXCLUSIVE MODE;
        DO $$
        BEGIN
          IF EXISTS (SELECT 1 FROM procurement.sourcing_events WHERE execution_version = 2)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_draft_evidence WHERE schema_version = 2)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_demand_bindings WHERE identity_schema_version = 2)
            OR EXISTS (SELECT 1 FROM procurement.sourcing_command_receipts WHERE response_schema_version = 2) THEN
            RAISE EXCEPTION 'P29 principal sourcing evidence prevents downgrade' USING ERRCODE = '55000';
          END IF;
        END $$;
        """;
}
