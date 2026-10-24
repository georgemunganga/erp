using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class PrincipalOrderRevisionReadinessContractTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Capture_admission_requires_exact_declared_sets_and_lowercase_full_digest()
    {
        // Synthetic text tests admission only; never a PostgreSQL capture/proof.
        var checks = ProcurementPrincipalOrderRevisionSchemaCapture.CheckNames
            .ToDictionary(x => x, _ => "test-only CHECK");
        var keys = ProcurementPrincipalOrderRevisionSchemaCapture.ParentKeyNames
            .ToDictionary(x => x, _ => "test-only UNIQUE");
        var digest = new string('a', 64);
        Assert.True(ProcurementPrincipalOrderRevisionSchemaCapture.Complete(checks, keys, digest));
        Assert.False(ProcurementPrincipalOrderRevisionSchemaCapture.Complete(checks, keys, null));
        Assert.False(ProcurementPrincipalOrderRevisionSchemaCapture.Complete(checks, keys, new string('A', 64)));
        checks["procurement.unrelated.ck_other"] = "test-only CHECK";
        Assert.False(ProcurementPrincipalOrderRevisionSchemaCapture.Complete(checks, keys, digest));
        checks.Remove("procurement.unrelated.ck_other");
        keys.Remove(keys.Keys.First());
        Assert.False(ProcurementPrincipalOrderRevisionSchemaCapture.Complete(checks, keys, digest));
    }

    [Fact]
    public void Original_lineage_keeps_the_original_receipt_capture_and_no_revision_parent_override()
    {
        var capture = ProcurementPrincipalOrderRevisionSchemaCapture.Select(false)!;
        Assert.Equal(ProcurementPrincipalCommercialSchemaCapture.MigrationId, capture.MigrationId);
        Assert.Same(ProcurementPrincipalCommercialSchemaCapture.Checks, capture.Checks);
        Assert.Null(capture.ParentKeys);
        Assert.DoesNotContain("'Edit'", capture.Checks[
            "procurement.principal_order_draft_receipts.ck_po_principal_receipt"]);
    }

    [Fact]
    public void Retained_history_reads_do_not_require_append_rights_but_deny_mutation_and_owner_membership()
    {
        var read = ProcurementPrincipalOrderRevisionReadiness.HistoryPrivilegeClauses(false);
        var write = ProcurementPrincipalOrderRevisionReadiness.HistoryPrivilegeClauses(true);
        Assert.DoesNotContain(read, clause => clause.Contains("'INSERT'", StringComparison.Ordinal));
        Assert.All(read, clause => Assert.Contains(clause, write));
        Assert.Equal(3, write.Count(clause => clause.Contains("'INSERT'", StringComparison.Ordinal)));
        foreach (var table in ProcurementPrincipalOrderRevisionSchemaCapture.Tables)
        {
            Assert.Contains(read, clause => clause.Contains(table) && clause.EndsWith(",'SELECT')"));
            Assert.Contains(read, clause => clause.Contains(table) && clause.StartsWith("NOT has_any_column_privilege")
                && clause.EndsWith(",'UPDATE')"));
            Assert.Contains(read, clause => clause.Contains(table) && clause.EndsWith(",'DELETE,TRUNCATE')"));
            Assert.Contains(read, clause => clause.Contains(table) && clause.Contains("pg_has_role(current_user,c.relowner,'MEMBER')"));
        }
    }

    [Fact]
    public async Task Missing_owner_transaction_and_missing_execution_configuration_never_open_a_fallback()
    {
        var options = new PrincipalOrderRevisionOptions { Enabled = true };
        using var services = new ServiceCollection().BuildServiceProvider();
        var readiness = new ProcurementPrincipalOrderRevisionReadiness(options, services);
        using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-used.invalid;Database=revision-transaction-not-opened").Options,
            new Scope("p33-source-test", Guid.NewGuid(), Guid.NewGuid().ToString("D")));
        Assert.Null(owner.Database.CurrentTransaction);
        Assert.False(await readiness.IsReadyAsync(owner, CancellationToken.None));
        Assert.False(await readiness.IsReadyAsync(CancellationToken.None));
        Assert.Null(owner.Database.CurrentTransaction);
        Assert.Equal(System.Data.ConnectionState.Closed, owner.Database.GetDbConnection().State);
    }
}
