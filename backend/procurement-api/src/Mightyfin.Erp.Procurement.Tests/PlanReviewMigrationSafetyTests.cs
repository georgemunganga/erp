using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PlanReviewMigrationSafetyTests
{
    [Fact]
    public void Up_preserves_existing_tables_and_adds_only_owned_review_storage_branch_and_policy_check()
    {
        var operations = new ProcurementPlanGovernedReview().UpOperations;
        var tables = operations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[] { "plan_review_command_receipts", "plan_review_submissions" }, tables.Select(x => x.Name));
        Assert.All(tables, x => Assert.Equal("procurement", x.Schema));
        Assert.DoesNotContain(operations, x => x is DropTableOperation or DropColumnOperation or DropIndexOperation);
        var branch = Assert.Single(operations.OfType<AddColumnOperation>());
        Assert.Equal(("procurement", "procurement_plans", "branch_ref", true),
            (branch.Schema, branch.Table, branch.Name, branch.IsNullable));
        Assert.Null(branch.DefaultValue);
        Assert.Null(branch.DefaultValueSql);
        var check = Assert.Single(operations.OfType<AddCheckConstraintOperation>());
        Assert.Equal(("procurement", "operational_policy_identity_evidence", "ck_operational_policy_identity"),
            (check.Schema, check.Table, check.Name));
        Assert.Contains("'PROCUREMENT_PLAN_REVIEW'", check.Sql);
        Assert.All(tables.SelectMany(x => x.ForeignKeys), x => Assert.Equal(Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict, x.OnDelete));
    }

    [Fact]
    public void Down_locks_and_vetoes_all_retained_evidence_and_draft_scope_before_any_destructive_operation()
    {
        var operations = new ProcurementPlanGovernedReview().DownOperations;
        var guard = Assert.IsType<SqlOperation>(operations[0]);
        Assert.False(guard.SuppressTransaction);
        var lockEnd = guard.Sql.IndexOf("IN ACCESS EXCLUSIVE MODE;", StringComparison.Ordinal);
        Assert.True(lockEnd > 0);
        var locks = guard.Sql[..lockEnd];
        foreach (var table in new[] { "plan_review_command_receipts", "plan_review_submissions",
                     "operational_policy_identity_evidence", "procurement_plans" })
            Assert.Contains("procurement." + table, locks);
        Assert.True(lockEnd < guard.Sql.IndexOf("IF EXISTS", StringComparison.Ordinal));
        Assert.Contains("EXISTS (SELECT 1 FROM procurement.plan_review_command_receipts)", guard.Sql);
        Assert.Contains("EXISTS (SELECT 1 FROM procurement.plan_review_submissions)", guard.Sql);
        Assert.Contains("WHERE policy_key = 'PROCUREMENT_PLAN_REVIEW'", guard.Sql);
        Assert.Contains("WHERE branch_ref IS NOT NULL", guard.Sql);
        Assert.Contains("ERRCODE = '55000'", guard.Sql);
        Assert.DoesNotContain("tenant_id", guard.Sql);
        Assert.DoesNotContain("state =", guard.Sql);
        Assert.DoesNotContain("DELETE", guard.Sql);
        Assert.DoesNotContain("UPDATE", guard.Sql);
        Assert.DoesNotContain(operations.Skip(1), x => x is SqlOperation);
        Assert.Equal(new[] { "plan_review_command_receipts", "plan_review_submissions" },
            operations.OfType<DropTableOperation>().Select(x => x.Name));
        var droppedColumn = Assert.Single(operations.OfType<DropColumnOperation>());
        Assert.Equal(("procurement_plans", "branch_ref"), (droppedColumn.Table, droppedColumn.Name));
    }
}
