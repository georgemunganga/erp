using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementManualQuotationReadinessTests
{
    [Theory]
    [InlineData("Host=private;Database=erp;Username=execution", true)]
    [InlineData("Host=private;Database=erp;Username=record", false)]
    [InlineData("Host=private;Database=erp;Username=erp", false)]
    [InlineData("Host=other;Database=erp;Username=execution", false)]
    [InlineData("Host=private;Database=other;Username=execution", false)]
    [InlineData("Host=private;Port=5433;Database=erp;Username=execution", false)]
    public void Only_actual_configured_execution_login_on_same_database_is_eligible(string actual, bool expected)
    {
        var config = Config("Host=private;Database=erp;Username=record", "Host=private;Database=erp;Username=execution");
        Assert.Equal(expected, ProcurementManualQuotationSchemaReadiness.TrustedExecutionRole(config, actual, out var role));
        Assert.Equal(expected ? "execution" : "", role);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Host=private;Database=erp;Username=execution", "Host=private;Database=erp;Username=execution")]
    [InlineData("Host=other;Database=erp;Username=record", "Host=private;Database=erp;Username=execution")]
    public void Missing_separate_or_mismatched_connection_configuration_cannot_admit_store(string? record, string? execution)
    {
        Assert.False(ProcurementManualQuotationSchemaReadiness.TrustedExecutionRole(Config(record, execution),
            "Host=private;Database=erp;Username=execution", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Append_only_and_finance_ownership_denials_apply_in_both_modes(bool write)
    {
        var sql = ProcurementManualQuotationSchemaReadiness.PrivilegeSql(write, "execution");
        Assert.Contains("session_user=current_user AND current_user='execution'", sql);
        foreach (var table in ProcurementManualQuotationSchemaReadiness.AppendTables)
        {
            Assert.Contains($"NOT has_any_column_privilege(current_user,'procurement.{table}','UPDATE')", sql);
            Assert.Contains($"NOT has_table_privilege(current_user,'procurement.{table}','DELETE,TRUNCATE,TRIGGER')", sql);
            Assert.Equal(write, sql.Contains($"has_table_privilege(current_user,'procurement.{table}','INSERT')", StringComparison.Ordinal));
        }
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs','INSERT')", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.finance_payment_status_refs','UPDATE')", sql);
    }

    [Fact]
    public void Read_admission_requires_no_insert_update_or_active_business_configuration()
    {
        var read = ProcurementManualQuotationSchemaReadiness.PrivilegeSql(false, "execution");
        Assert.DoesNotContain("AND has_table_privilege(current_user,'procurement.supplier_bids','INSERT')", read);
        Assert.DoesNotContain("AND has_table_privilege(current_user,'procurement.supplier_bids','UPDATE')", read);
        Assert.DoesNotContain("current_setting('transaction_read_only')", read);
        var write = ProcurementManualQuotationSchemaReadiness.PrivilegeSql(true, "execution");
        Assert.Contains("current_setting('transaction_read_only')='off'", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.supplier_bids','INSERT')", write);
        Assert.Contains("has_table_privilege(current_user,'procurement.supplier_bids','UPDATE')", write);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("check")]
    [InlineData("index")]
    [InlineData("rls")]
    public void Native_named_shape_rejects_material_catalog_changes(string change)
    {
        var node = JsonNode.Parse(ProcurementManualQuotationNativeShape.Json)!;
        var table = node["procurement.sourcing_manual_contacts"]!;
        if (change == "default") table["columns"]![0]!["default"] = "unapproved";
        if (change == "check") table["constraints"]!["ck_manual_contact_v2"]!["validated"] = false;
        if (change == "index") table["indexes"]![0]!["ready"] = false;
        if (change == "rls") table["rls"] = true;
        Assert.True(ProcurementManualQuotationNativeShape.Matches(ProcurementManualQuotationNativeShape.Json));
        Assert.False(ProcurementManualQuotationNativeShape.Matches(node.ToJsonString()));
        Assert.False(ProcurementManualQuotationNativeShape.Matches(null));
    }

    private static IConfiguration Config(string? record, string? execution) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Procurement"] = record,
            ["ConnectionStrings:ProcurementApprovalExecution"] = execution }).Build();
}
