using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Source contract for the finite execution role. The actual PostgreSQL
/// role/grant admission remains the private owning-catalog rehearsal.</summary>
public sealed class ProcurementManualPaymentGovernanceReadinessTests
{
    [Theory]
    [InlineData("hrm.local_users")]
    [InlineData("hrm.tenant_role_assignments")]
    [InlineData("hrm.hr_user_branch_assignments")]
    [InlineData("hrm.legal_entities")]
    [InlineData("hrm.work_locations")]
    [InlineData("hrm.workers")]
    [InlineData("hrm.org_units")]
    public void Each_current_Core_payment_read_must_be_individually_granted(string table)
    {
        var sql = ProcurementManualPaymentGovernanceReadiness.PrivilegeSql();
        var required = "has_table_privilege(current_user,'" + table + "','SELECT')";
        Assert.Contains(table, ProcurementManualPaymentGovernanceReadiness.RequiredCoreReadTables);
        Assert.Contains(" AND " + required + " AND ", sql);
        Assert.Equal(1, sql.Split(required, StringSplitOptions.None).Length - 1);
        // A missing table-level SELECT makes its required AND term false. No
        // alternative OR, broad Core ownership, or Core identity write can fill it.
        Assert.Contains("has_schema_privilege(current_user,'hrm','USAGE')", sql);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql, sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'" + table + "','INSERT')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'" + table + "','UPDATE')", sql);
    }
}
