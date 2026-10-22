using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private const string SourcingIdentityMigration = "20261022052824_P21CanonicalGovernedProcurementFoundation";

    /// <summary>Actual finite-role owning-store readiness, including excess privileges. Private
    /// bootstrap faults are transaction-local and always rolled back; no record is mutated to
    /// establish readiness, and no production registration is replaced here.</summary>
    private async Task VerifySourcingPolicySchemaReadiness()
    {
        Require(distinctRuntime, "P29 readiness requires a distinct finite runtime principal.");
        var roleName = new NpgsqlConnectionStringBuilder(runtimeConnection).Username
            ?? throw new InvalidOperationException("P29 private runtime role required.");
        var role = new NpgsqlCommandBuilder().QuoteIdentifier(roleName);
        ProcurementRequestScope SourceScope()
        {
            var scope = new ProcurementRequestScope();
            scope.Set("probe", company, buyer.ToString("D"), null, null, false, "sourcing-policy-schema-probe");
            return scope;
        }
        async Task<bool> Runtime()
        {
            await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql(runtimeConnection, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, SourceScope());
            Require((await db.Database.GetAppliedMigrationsAsync()).Contains(SourcingIdentityMigration, StringComparer.Ordinal),
                "Current P29 readiness contract requires the exact reviewed applied migration.");
            return await new ProcurementSourcingPolicyIdentitySchemaReadiness().IsReadyAsync(db, default);
        }
        Require(await Runtime(), "Actual finite-role P29 readiness positive failed; no schema or provider substitution is allowed.");
        var cases = new (string Label, string Sql)[]
        {
            ("missing-identity-select", $"REVOKE SELECT ON procurement.sourcing_policy_identity_evidence FROM {role}"),
            ("missing-identity-insert", $"REVOKE INSERT ON procurement.sourcing_policy_identity_evidence FROM {role}"),
            ("excess-identity-column-update", $"GRANT UPDATE(evidence_sha256) ON procurement.sourcing_policy_identity_evidence TO {role}"),
            ("excess-identity-truncate", $"GRANT TRUNCATE ON procurement.sourcing_policy_identity_evidence TO {role}"),
            ("excess-audit-truncate", $"GRANT TRUNCATE ON procurement.audit_events TO {role}"),
            ("excess-outbox-truncate", $"GRANT TRUNCATE ON procurement.outbox_events TO {role}"),
            ("excess-policy-truncate", $"GRANT TRUNCATE ON procurement.policy_versions TO {role}"),
            ("missing-owner-fk", "ALTER TABLE procurement.sourcing_policy_identity_evidence DROP CONSTRAINT fk_source_policy_identity_owner"),
            ("missing-created-fk", "ALTER TABLE procurement.sourcing_policy_identity_evidence DROP CONSTRAINT fk_source_policy_identity_created"),
            ("missing-submitted-fk", "ALTER TABLE procurement.sourcing_policy_identity_evidence DROP CONSTRAINT fk_source_policy_identity_submitted"),
            ("lookalike-check", "ALTER TABLE procurement.sourcing_policy_identity_evidence DROP CONSTRAINT ck_source_policy_identity; ALTER TABLE procurement.sourcing_policy_identity_evidence ADD CONSTRAINT ck_source_policy_identity CHECK(true)"),
            ("lookalike-created-index", "DROP INDEX procurement.ux_source_policy_identity_created; CREATE UNIQUE INDEX ux_source_policy_identity_created ON procurement.sourcing_policy_identity_evidence(tenant_id,legal_entity_id,policy_version_id) WHERE false"),
            ("missing-P29-history", $"DELETE FROM procurement.__procurement_migrations WHERE \"MigrationId\"='{SourcingIdentityMigration}'"),
            ("missing-audit-insert", $"REVOKE INSERT ON procurement.audit_events FROM {role}"),
        };
        foreach (var item in cases)
        {
            var before = await PolicyFootprint();
            await using (var owner = new NpgsqlConnection(databaseConnection))
            {
                await owner.OpenAsync();
                await using var tx = await owner.BeginTransactionAsync();
                try
                {
                    await using (var fault = new NpgsqlCommand(item.Sql, owner, tx)) await fault.ExecuteNonQueryAsync();
                    await using (var finiteRole = new NpgsqlCommand($"SET LOCAL ROLE {role}", owner, tx)) await finiteRole.ExecuteNonQueryAsync();
                    // A new runtime connection would block on uncommitted DDL or miss its ACL.
                    // Adopt the exact physical owning transaction, then inspect as its finite role.
                    await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                        .UseNpgsql(owner, x => x.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, SourceScope());
                    await db.Database.UseTransactionAsync(tx);
                    Require(!await new ProcurementSourcingPolicyIdentitySchemaReadiness().IsReadyAsync(db, default),
                        "P29 readiness incorrectly accepted rollback-only " + item.Label);
                    Require(!db.ChangeTracker.HasChanges(), "P29 readiness created tracked effects: " + item.Label);
                }
                finally { await tx.RollbackAsync(); }
            }
            Require(before == await PolicyFootprint(), "P29 readiness fault/rollback changed persisted rows: " + item.Label);
            Require(await Runtime(), "P29 readiness failed to restore original schema/ACL/history after " + item.Label);
            Console.WriteLine("PASS P29 readiness denial+restoration: " + item.Label);
        }
        Console.WriteLine("PASS P29 actual finite-role owning-store readiness positive and 14 isolated rollback-only negatives; row fingerprints unchanged. This is not RFQ publication authority.");
    }
}
