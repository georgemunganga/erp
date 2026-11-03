using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>The full payment writer checks the same finite Core projections as
/// retained reads. Actual PostgreSQL role admission remains a separate gate.</summary>
public sealed class ProcurementManualPaymentGovernanceReadinessTests
{
    [Fact]
    public void Finite_writer_requires_each_current_Core_column_without_table_wide_Core_select()
    {
        var sql = ProcurementManualPaymentGovernanceReadiness.PrivilegeSql();
        var projections = ProcurementManualPaymentReadSchemaReadiness.ReadColumns
            .Where(entry => entry.Key.StartsWith("hrm.", StringComparison.Ordinal)
                || entry.Key == "core_approval.\"__EFMigrationsHistory\""
                || entry.Key == "procurement.suppliers").ToArray();

        Assert.Equal(10, projections.Length); // Seven Core tables, two histories, and the source supplier.
        foreach (var projection in projections)
        {
            Assert.NotEmpty(projection.Value);
            Assert.DoesNotContain("has_table_privilege(current_user,'" + projection.Key + "','SELECT')", sql);
            foreach (var column in projection.Value)
            {
                var required = "has_column_privilege(current_user,'" + projection.Key
                    + "','" + column + "','SELECT')";
                Assert.Contains(required, sql);
                // The Core-owner migration proof repeats this one current
                // company-clock projection in its own schema assertion.
                var expectedCount = projection.Key == "hrm.legal_entities" && column == "time_zone_id" ? 2 : 1;
                Assert.Equal(expectedCount, sql.Split(required, StringSplitOptions.None).Length - 1);
            }
        }
        Assert.Contains("has_schema_privilege(current_user,'hrm','USAGE')", sql);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql, sql);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql, sql);
        Assert.Contains("has_table_privilege(current_user,'procurement.manual_payment_requests','INSERT')", sql);
        Assert.Contains("has_table_privilege(current_user,'core_approval.cases','INSERT')", sql);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.finance_payment_status_refs','INSERT')", sql);
        Assert.Contains("to_regclass('procurement.invoice_replacement_links') IS NULL", sql);
        Assert.Contains("has_table_privilege(current_user,to_regclass('procurement.invoice_replacement_links'),'SELECT')", sql);
        Assert.Contains("has_table_privilege(current_user,to_regclass('procurement.invoice_replacement_command_receipts'),'SELECT')", sql);
    }
}
