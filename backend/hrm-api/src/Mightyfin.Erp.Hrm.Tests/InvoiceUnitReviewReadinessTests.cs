using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Xunit;
namespace Mightyfin.Erp.Hrm.Tests;
public sealed class InvoiceUnitReviewReadinessTests
{
    [Fact]
    public void Execution_is_default_off_and_uncaptured_schema_cannot_claim_ready()
    {
        var options = new ProcurementInvoiceUnitReviewOptions();
        Assert.False(options.Enabled); Assert.False(options.ExecutionDatabaseConfigured);
        Assert.False(ProcurementInvoiceUnitReviewSchemaReadiness.ManifestCaptured);
    }
    [Fact]
    public void New_purpose_permissions_have_no_bootstrap_role_grants()
    {
        foreach (var action in new[] { "procurement-invoice-unit-review-request", "procurement-invoice-unit-review-read", "procurement-invoice-unit-review" })
            Assert.Empty(ProcurementActionCatalog.BootstrapGrants[action]);
    }
    [Fact]
    public void Immutable_Core_decisions_require_insert_without_update_or_destructive_privileges()
    {
        var sql = ProcurementInvoiceUnitReviewSchemaReadiness.CoreDecisionPrivilegesSql;
        Assert.Contains("'INSERT'", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE')", sql);
        Assert.Contains("NOT has_table_privilege(current_user,'core_approval.decisions','DELETE,TRUNCATE')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'core_approval.decisions','UPDATE')", sql);
    }
}
