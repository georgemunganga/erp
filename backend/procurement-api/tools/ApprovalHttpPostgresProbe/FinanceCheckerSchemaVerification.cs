using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    /// <summary>Future P27 rehearsal: genuine owning-store reader, rollback-only bootstrap DDL,
    /// actual restricted-role checks. Does not manufacture Finance data, workers or approvals.</summary>
    public async Task VerifyFinanceCheckerReadiness()
    {
        async Task<bool> Runtime(bool capture)
        {
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(runtimeConnection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
                new FinanceSchemaScope(OrderCompany, OrderFinance));
            return await new ProcurementFinanceCheckerSchemaReadiness().IsReadyAsync(db, capture, default);
        }
        Require(await Runtime(false) && await Runtime(true),
            "Finance checker schema is not genuinely ready: verify P27 columns, exact PostgreSQL CHECK literal manifest, scoped FK and runtime privileges.");
        var runtimeRole = new NpgsqlConnectionStringBuilder(runtimeConnection).Username
            ?? throw new InvalidOperationException("Runtime role required for private Finance readiness proof.");
        var quotedRole = new NpgsqlCommandBuilder().QuoteIdentifier(runtimeRole);
        var cases = new List<(string Label, string Sql, bool Read, bool Capture)>
        {
            ("missing-column", "ALTER TABLE procurement.budget_check_snapshots DROP COLUMN checked_by_worker_id", false, false),
            ("missing-check", "ALTER TABLE procurement.budget_check_snapshots DROP CONSTRAINT ck_budget_checker_identity", false, false),
            ("lookalike-check", "ALTER TABLE procurement.budget_check_snapshots DROP CONSTRAINT ck_budget_checker_identity; ALTER TABLE procurement.budget_check_snapshots ADD CONSTRAINT ck_budget_checker_identity CHECK (true)", false, false),
            ("unvalidated-check", "ALTER TABLE procurement.budget_check_snapshots DROP CONSTRAINT ck_budget_checker_identity; ALTER TABLE procurement.budget_check_snapshots ADD CONSTRAINT ck_budget_checker_identity CHECK ((checker_identity_schema_version=0 AND checked_by_worker_id IS NULL AND checker_identity_sha256 IS NULL) OR (checker_identity_schema_version=1 AND checked_by_worker_id IS NOT NULL AND checked_by_worker_id<>'00000000-0000-0000-0000-000000000000'::uuid AND checker_identity_sha256 IS NOT NULL AND checker_identity_sha256 ~ '^[a-f0-9]{64}$')) NOT VALID", false, false),
            ("wrong-default", "ALTER TABLE procurement.purchase_request_approval_submissions ALTER COLUMN finance_identity_schema_version SET DEFAULT 99", false, false),
            ("missing-source-fk", "DO $drop$ DECLARE target text; BEGIN SELECT conname INTO STRICT target FROM pg_constraint WHERE contype='f' AND conrelid='procurement.purchase_request_approval_submissions'::regclass AND confrelid='procurement.budget_check_snapshots'::regclass; EXECUTE format('ALTER TABLE procurement.purchase_request_approval_submissions DROP CONSTRAINT %I', target); END $drop$", false, false),
        };
        if (OrderDistinctRuntime)
        {
            cases.Add(("missing-select", $"REVOKE SELECT ON procurement.budget_check_snapshots FROM {quotedRole}", false, false));
            cases.Add(("missing-capture-insert", $"REVOKE INSERT ON procurement.budget_check_snapshots FROM {quotedRole}", true, false));
            cases.Add(("missing-audit-insert", $"REVOKE INSERT ON procurement.audit_events FROM {quotedRole}", true, false));
        }
        foreach (var item in cases)
        {
            await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
            await using var tx = await owner.BeginTransactionAsync();
            try
            {
                await using (var ddl = new NpgsqlCommand(item.Sql, owner, tx)) await ddl.ExecuteNonQueryAsync();
                // Same physical transaction sees its own uncommitted DDL, but reads as the finite
                // runtime role. Another connection would block on DDL and prove no readiness semantics.
                await using (var role = new NpgsqlCommand($"SET LOCAL ROLE {quotedRole}", owner, tx)) await role.ExecuteNonQueryAsync();
                await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                    .UseNpgsql(owner, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
                    new FinanceSchemaScope(OrderCompany, OrderFinance));
                await db.Database.UseTransactionAsync(tx);
                var reader = new ProcurementFinanceCheckerSchemaReadiness();
                Require(await reader.IsReadyAsync(db, false, default) == item.Read,
                    "Unexpected Finance read readiness after rollback-only " + item.Label);
                Require(await reader.IsReadyAsync(db, true, default) == item.Capture,
                    "Unexpected Finance capture readiness after rollback-only " + item.Label);
            }
            finally { await tx.RollbackAsync(); }
            Require(await Runtime(false) && await Runtime(true), "Finance schema/privileges did not restore after " + item.Label);
        }
        Console.WriteLine("PASS Finance checker actual schema readiness: required columns/defaults/exact validated CHECKs and read/capture privileges; all bootstrap tampering rolled back. Restricted privilege negatives=" + OrderDistinctRuntime);
    }

    private sealed class FinanceSchemaScope(Guid company, Guid actor) : IProcurementScope
    {
        public string TenantId => "probe";
        public Guid LegalEntityId => company;
        public string SubjectId => actor.ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => "finance-schema-probe";
    }
}
