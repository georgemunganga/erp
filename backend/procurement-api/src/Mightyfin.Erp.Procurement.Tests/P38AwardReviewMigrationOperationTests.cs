using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class P38AwardReviewMigrationOperationTests
{
    private sealed record Scope(string TenantId, Guid LegalEntityId) : IProcurementScope
    {
        public string SubjectId => "p38-migration-model-only";
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Raw_up_only_adds_five_owned_families_with_named_constraints()
    {
        using var db = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql("Host=never-used.invalid;Database=model_only").Options,
            new Scope("p38-migration-test", Guid.NewGuid()));
        var model = db.GetService<IDesignTimeModel>().Model;
        var mapped = new[] { typeof(PrincipalRfqRecommendationSubmission),
                typeof(PrincipalRfqAwardReviewSubmission), typeof(PrincipalRfqAwardReviewLine),
                typeof(PrincipalRfqAwardReviewOutcome), typeof(PrincipalRfqAwardReviewReceipt) }
            .Select(type => Assert.IsType<string>(model.FindEntityType(type)?.GetTableName()))
            .ToHashSet(StringComparer.Ordinal);
        var up = new P38PrincipalSupplierAwardReview().UpOperations;
        var tables = up.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(5, mapped.Count);
        Assert.True(mapped.SetEquals(tables.Select(x => x.Name)));
        Assert.Equal(9, tables.Sum(x => x.ForeignKeys.Count));
        Assert.Equal(9, tables.Sum(x => x.UniqueConstraints.Count));
        Assert.Equal(5, tables.Sum(x => x.CheckConstraints.Count));
        Assert.All(up, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.All(tables.SelectMany(x => x.ForeignKeys), x =>
            Assert.Equal(ReferentialAction.Restrict, x.OnDelete));
        Assert.All(tables.SelectMany(x => x.UniqueConstraints).Select(x => x.Name)
            .Concat(tables.SelectMany(x => x.ForeignKeys).Select(x => x.Name))
            .Concat(up.OfType<CreateIndexOperation>().Select(x => x.Name)), name =>
            Assert.InRange(name!.Length, 1, 63));
    }

    [Fact]
    public void First_down_statement_locks_and_vetoes_exact_five_mapped_families_and_effects()
    {
        using var db = new ProcurementDbContext(
            new DbContextOptionsBuilder<ProcurementDbContext>()
                .UseNpgsql("Host=never-used.invalid;Database=model_only").Options,
            new Scope("p38-migration-test", Guid.NewGuid()));
        var model = db.GetService<IDesignTimeModel>().Model;
        var owned = new[] { typeof(PrincipalRfqRecommendationSubmission),
                typeof(PrincipalRfqAwardReviewSubmission), typeof(PrincipalRfqAwardReviewLine),
                typeof(PrincipalRfqAwardReviewOutcome), typeof(PrincipalRfqAwardReviewReceipt) }
            .Select(type => Assert.IsType<string>(model.FindEntityType(type)?.GetTableName()))
            .ToHashSet(StringComparer.Ordinal);
        var down = new P38PrincipalSupplierAwardReview().DownOperations;
        Assert.Equal(6, down.Count);
        var veto = Assert.IsType<SqlOperation>(down[0]);
        Assert.Equal(P38SupplierAwardReviewRollbackGuards.BeforeDownSql, veto.Sql);
        Assert.False(veto.SuppressTransaction);
        var dropped = down.Skip(1).Select(x => Assert.IsType<DropTableOperation>(x).Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(owned.SetEquals(dropped));

        var sql = veto.Sql;
        var lockPart = sql[..sql.IndexOf(" IN ACCESS EXCLUSIVE MODE", StringComparison.Ordinal)];
        var locked = Regex.Matches(lockPart, @"procurement\.([a-z_]+)")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var vetoed = Regex.Matches(sql, @"EXISTS \(SELECT 1 FROM procurement\.([a-z_]+)")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        owned.UnionWith(new[] { "audit_events", "outbox_events" });
        Assert.True(owned.SetEquals(locked));
        Assert.True(owned.SetEquals(vetoed));
        Assert.Contains("ERRCODE = '55000'", sql, StringComparison.Ordinal);
        var exactRecordTypes = new[] { "PrincipalRfqRecommendationSubmission",
            "PrincipalRfqAwardReviewSubmission", "PrincipalRfqAwardReviewLine",
            "PrincipalRfqAwardReviewOutcome", "PrincipalRfqAwardReviewReceipt" };
        var auditRecordTypes = Regex.Matches(sql[
            sql.IndexOf("record_type IN", StringComparison.Ordinal)..
                sql.IndexOf("OR action =", StringComparison.Ordinal)], @"'([A-Za-z]+)'")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var outboxAggregateTypes = Regex.Matches(sql[
            sql.IndexOf("aggregate_type IN", StringComparison.Ordinal)..
                sql.IndexOf("OR event_type =", StringComparison.Ordinal)], @"'([A-Za-z]+)'")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.True(exactRecordTypes.ToHashSet(StringComparer.Ordinal).SetEquals(auditRecordTypes));
        Assert.True(exactRecordTypes.ToHashSet(StringComparer.Ordinal).SetEquals(outboxAggregateTypes));
        Assert.Contains("action = 'SubmitAwardReview'", sql, StringComparison.Ordinal);
        Assert.Contains("aggregate_type = 'PrincipalRfqAwardReviewSubmission'", sql, StringComparison.Ordinal);
        Assert.Contains("event_type = 'sourcing.award_review_submitted.v1'", sql, StringComparison.Ordinal);
    }
}
