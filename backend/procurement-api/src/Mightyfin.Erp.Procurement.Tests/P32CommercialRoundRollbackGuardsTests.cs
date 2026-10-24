using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Migrations;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class P32CommercialRoundRollbackGuardsTests
{
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "fixture";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public string SubjectId { get; } = Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }

    [Fact]
    public void Every_modeled_P32_family_is_locked_and_vetoed_before_downgrade()
    {
        using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only_not_opened;Username=none;Password=none")
            .Options, new Scope());
        var owned = db.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => x is not null && (x.StartsWith("rfq_commercial_", StringComparison.Ordinal)
                || x.StartsWith("rfq_prepared_", StringComparison.Ordinal)
                || x.StartsWith("rfq_preparation_", StringComparison.Ordinal)
                || x.StartsWith("principal_rfq_commercial_", StringComparison.Ordinal)))
            .Cast<string>().ToHashSet(StringComparer.Ordinal);
        Assert.Equal(11, owned.Count);
        var sql = P32CommercialRoundRollbackGuards.BeforeDownSql;
        var locked = Regex.Matches(sql[..sql.IndexOf(" IN ACCESS EXCLUSIVE MODE", StringComparison.Ordinal)],
                @"procurement\.([a-z_]+)")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var vetoed = Regex.Matches(sql, @"EXISTS \(SELECT 1 FROM procurement\.([a-z_]+)\)")
            .Select(x => x.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        Assert.True(owned.IsSubsetOf(locked));
        Assert.True(owned.SetEquals(vetoed));
        Assert.Contains("ERRCODE = '55000'", sql, StringComparison.Ordinal);
    }
}
