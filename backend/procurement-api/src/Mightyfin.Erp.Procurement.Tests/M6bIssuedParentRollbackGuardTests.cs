using System.Reflection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class M6bIssuedParentRollbackGuardTests
{
    [Fact]
    public void Guard_locks_exposure_then_retains_B1_veto_and_orphan_source_family_checks()
    {
        var type = typeof(ProcurementDbContext).Assembly.GetType(
            "Mightyfin.Erp.Procurement.Infrastructure.Migrations.M6bIssuedParentRollbackGuards", throwOnError: true)!;
        var sql = Assert.IsType<string>(type.GetField("BeforeDownSql",
            BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue());
        Assert.StartsWith("LOCK TABLE procurement.invoice_match_exposure_states IN ACCESS EXCLUSIVE MODE;",
            sql.TrimStart());
        Assert.Contains("procurement.invoice_match_mapping_versions", sql);
        Assert.Contains("procurement.invoice_match_allocations", sql);
        Assert.Contains("WHERE source_schema_version=3", sql);
        Assert.Contains("FROM procurement.invoice_match_exposure_states\n                     WHERE source_schema_version=3", sql);
        Assert.Contains("FROM procurement.invoice_match_results\n                       WHERE source_schema_version=3", sql);
        Assert.Contains("ERRCODE='55000'", sql);
        Assert.DoesNotContain("EXISTS (SELECT 1 FROM procurement.invoice_owner_baselines)", sql);
    }
}
