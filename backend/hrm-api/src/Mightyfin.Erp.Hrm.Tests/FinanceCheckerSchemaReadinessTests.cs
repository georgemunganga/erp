using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

// Explicit TestServer-only override; production registration always uses the real owner-store reader.
internal sealed class FinanceCheckerTestSchemaReadiness : IProcurementFinanceCheckerSchemaReadiness
{
    public bool Ready { get; set; } = true;
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, bool capture, CancellationToken ct) => Task.FromResult(Ready);
}

public sealed class FinanceCheckerSchemaReadinessTests
{
    [Fact]
    public void Proposed_exact_manifest_preserves_groups_casts_scope_and_literal_bytes()
    {
        Assert.True(ProcurementFinanceCheckerSchemaReadiness.ExactChecks(
            ProcurementFinanceCheckerSchemaReadiness.BudgetExpression,
            ProcurementFinanceCheckerSchemaReadiness.SubmissionExpression));
        Assert.True(ProcurementFinanceCheckerSchemaReadiness.ExactChecks(
            "\n  " + ProcurementFinanceCheckerSchemaReadiness.BudgetExpression + "\t",
            ProcurementFinanceCheckerSchemaReadiness.SubmissionExpression));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("nullable-worker")]
    [InlineData("nullable-hash")]
    [InlineData("partial-legacy")]
    [InlineData("or-bypass")]
    [InlineData("cast")]
    [InlineData("other-column")]
    [InlineData("literal-space")]
    [InlineData("uppercase-hash")]
    [InlineData("unknown-version")]
    public void A_lookalike_name_cannot_compensate_for_a_changed_predicate(string kind)
    {
        string? budget = ProcurementFinanceCheckerSchemaReadiness.BudgetExpression;
        var submission = ProcurementFinanceCheckerSchemaReadiness.SubmissionExpression;
        switch (kind)
        {
            case "missing": budget = null; break;
            case "nullable-worker": budget = budget.Replace(" AND (checked_by_worker_id IS NOT NULL)", ""); break;
            case "nullable-hash": budget = budget.Replace(" AND (checker_identity_sha256 IS NOT NULL)", ""); break;
            case "partial-legacy": budget = budget.Replace("(checked_by_worker_id IS NULL)", "(true)"); break;
            case "or-bypass": budget = budget.Replace(" AND ", " OR "); break;
            case "cast": budget = budget.Replace("::uuid", "::text"); break;
            case "other-column": budget = budget.Replace("checked_by_worker_id", "requester_worker_id"); break;
            case "literal-space": budget = budget.Replace("{64}$'", "{64}$ '"); break;
            case "uppercase-hash": budget = budget.Replace("[a-f0-9]", "[A-Fa-f0-9]"); break;
            case "unknown-version": submission = submission.Replace("= 1", "= 99"); break;
        }
        Assert.False(ProcurementFinanceCheckerSchemaReadiness.ExactChecks(budget, submission));
    }

    [Fact]
    public async Task Production_reader_never_treats_InMemory_as_an_installed_or_privileged_schema()
    {
        using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Scope());
        var reader = new ProcurementFinanceCheckerSchemaReadiness();
        Assert.False(await reader.IsReadyAsync(owner, false, default));
        Assert.False(await reader.IsReadyAsync(owner, true, default));
    }

    [Fact]
    public void Manifest_checks_actual_columns_validated_constraints_and_exact_existing_scoped_FK()
    {
        var sql = ProcurementFinanceCheckerSchemaReadiness.ManifestSql;
        Assert.Contains("count(a.attname)=4", sql); Assert.Contains("format_type", sql);
        Assert.Contains("has_column_privilege", sql); Assert.Contains("has_table_privilege", sql);
        Assert.Contains("has_table_privilege(current_user,to_regclass('procurement.budget_check_snapshots'),'INSERT')", sql);
        Assert.Contains("c.contype='c' AND c.convalidated AND c.conislocal", sql);
        Assert.Contains("f.convalidated AND NOT f.condeferrable", sql);
        Assert.Contains("'tenant_id','legal_entity_id','finance_snapshot_id','purchase_request_id','submitted_business_version'", sql);
        Assert.Contains("'tenant_id','legal_entity_id','id','purchase_request_id','request_version'", sql);
        Assert.DoesNotContain("CREATE ", sql); Assert.DoesNotContain("INSERT INTO", sql);
    }

    [Fact]
    public void Returned_request_CRUD_submission_query_reads_only_old_scoped_ID_without_Finance_columns()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=returned_crud_design_only;Username=invalid").Options, new Scope());
        var sql = ProcurementRequestRoutes.ReleasedPriorSubmissionIds(db, Guid.NewGuid(), Guid.NewGuid(), 4).ToQueryString();
        Assert.Contains("purchase_request_approval_submissions", sql);
        Assert.Contains("purchase_request_id", sql); Assert.Contains("core_approval_case_id", sql);
        Assert.Contains("submitted_business_version", sql); Assert.Contains("tenant_id", sql); Assert.Contains("legal_entity_id", sql);
        Assert.DoesNotContain("finance_identity_schema_version", sql);
        Assert.DoesNotContain("finance_snapshot_json", sql); Assert.DoesNotContain("maker_worker_id", sql);
        Assert.False(db.ChangeTracker.HasChanges()); // Npgsql query generation only; no connection is opened.
    }

    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "schema-test";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId => "not-a-runtime-account";
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }
}
