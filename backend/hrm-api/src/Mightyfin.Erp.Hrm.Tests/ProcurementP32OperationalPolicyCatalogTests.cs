using System.Security.Cryptography;
using System.Text;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Pure migration-bound catalog selection. Actual finite-role PostgreSQL
/// capture and current actor/business authorization are separate release gates.</summary>
public sealed class ProcurementP32OperationalPolicyCatalogTests
{
    private static readonly string P21 = ProcurementOperationalPolicyIdentitySchemaReadiness.MigrationId;
    private static readonly string P23 = ProcurementInvoiceUnitReviewSchemaReadiness.MigrationId;
    private static readonly string P28 = ProcurementManualPaymentGovernanceReadiness.MigrationId;
    private static readonly string P32 = ProcurementP32OperationalPolicyCatalog.MigrationId;

    [Fact]
    public void Historical_catalogs_remain_exact_before_P32()
    {
        Assert.Equal(ProcurementOperationalPolicyIdentitySchemaReadiness.CheckDefinition,
            ProcurementP32OperationalPolicyCatalog.SelectCheck([P21]));
        Assert.Equal(ProcurementInvoiceUnitReviewSchemaReadiness.OperationalCheckDefinition,
            ProcurementP32OperationalPolicyCatalog.SelectCheck([P21, P23]));
        Assert.Equal(ProcurementManualPaymentGovernanceReadiness.OperationalCheckDefinition,
            ProcurementP32OperationalPolicyCatalog.SelectCheck([P21, P23, P28]));
        Assert.Equal(ProcurementManualPaymentGovernanceReadiness.ExpectedFingerprint,
            ProcurementP32OperationalPolicyCatalog.SelectManualPaymentFingerprint([P21, P23, P28]));
        Assert.NotEqual(ProcurementP32OperationalPolicyCatalog.CheckDefinition,
            ProcurementP32OperationalPolicyCatalog.SelectCheck([P21, P23, P28]));
    }

    [Fact]
    public void P32_catalog_is_selected_only_with_complete_applied_lineage()
    {
        var history = new[] { P21, P23, P28, P32 };
        Assert.Equal(ProcurementP32OperationalPolicyCatalog.CheckDefinition,
            ProcurementP32OperationalPolicyCatalog.SelectCheck(history));
        Assert.Equal(ProcurementP32OperationalPolicyCatalog.ManualPaymentFingerprint,
            ProcurementP32OperationalPolicyCatalog.SelectManualPaymentFingerprint(history));
        Assert.Equal("5c5567961eda3b19ddc9f27a322ccb49946b4d058b9968504869a55edeb75afe",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                ProcurementP32OperationalPolicyCatalog.CheckDefinition))).ToLowerInvariant());
        Assert.Equal("c77d6d40a5cbd3b981e43338d56a8d337546a546c195656c969d3c96d148d129",
            ProcurementP32OperationalPolicyCatalog.ManualPaymentFingerprint);
        Assert.Contains("SOURCING", ProcurementP32OperationalPolicyCatalog.CheckDefinition);
    }

    [Fact]
    public void Missing_or_out_of_order_history_cannot_rescue_a_different_physical_check()
    {
        Assert.Null(ProcurementP32OperationalPolicyCatalog.SelectCheck([]));
        Assert.Null(ProcurementP32OperationalPolicyCatalog.SelectCheck([P32]));
        Assert.Null(ProcurementP32OperationalPolicyCatalog.SelectCheck([P21, P23, P32]));
        Assert.Null(ProcurementP32OperationalPolicyCatalog.SelectCheck([P21, P28, P32]));
        Assert.Null(ProcurementP32OperationalPolicyCatalog.SelectManualPaymentFingerprint([P21, P23, P32]));
        Assert.Null(ProcurementP32OperationalPolicyCatalog.SelectManualPaymentFingerprint([P28, P32]));
        Assert.NotEqual(ProcurementP32OperationalPolicyCatalog.CheckDefinition,
            ProcurementP32OperationalPolicyCatalog.SelectCheck([P21, P23, P28]));
        Assert.NotEqual(ProcurementManualPaymentGovernanceReadiness.ExpectedFingerprint,
            ProcurementP32OperationalPolicyCatalog.SelectManualPaymentFingerprint([P21, P23, P28, P32]));
    }
}
