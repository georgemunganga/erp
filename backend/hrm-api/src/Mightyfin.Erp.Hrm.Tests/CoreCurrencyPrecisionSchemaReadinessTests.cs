using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreCurrencyPrecisionSchemaReadinessTests
{
    [Fact]
    public void Producer_uses_finite_retirement_columns_and_no_procurement_dependency()
    {
        var sql = CoreCurrencyPrecisionSchemaReadiness.PrivilegeSql(true, "finite_core_test");
        Assert.DoesNotContain("procurement.", sql);
        Assert.Contains("NOT has_table_privilege(current_user,'hrm.currency_precision_versions','UPDATE')", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'hrm.currency_precision_command_receipts','UPDATE')", sql);
        foreach (var column in new[] { "retired_at_utc", "retirement_reason", "updated_at", "updated_by" })
            Assert.Contains("has_column_privilege(current_user,'hrm.currency_precision_versions','" + column + "','UPDATE')", sql);
        Assert.Contains("a.attname<>ALL(ARRAY['retired_at_utc','retirement_reason','updated_at','updated_by'])", sql);
        Assert.Contains("has_table_privilege(current_user,'hrm.currency_precision_versions','INSERT')", sql);
        Assert.Contains("has_table_privilege(current_user,'hrm.currency_precision_command_receipts','INSERT')", sql);
    }

    [Fact]
    public void Read_mode_can_use_writer_role_without_requiring_writes_or_relaxing_finite_fences()
    {
        var sql = CoreCurrencyPrecisionSchemaReadiness.PrivilegeSql(false, "finite_core_test");
        Assert.DoesNotContain("'INSERT'", sql);
        Assert.Contains("has_table_privilege(current_user,'hrm.currency_precision_versions','SELECT')", sql);
        Assert.Contains("has_table_privilege(current_user,'hrm.currency_precision_command_receipts','SELECT')", sql);
        Assert.Contains("rolsuper", sql); Assert.Contains("rolbypassrls", sql);
        Assert.Contains("session_user=current_user", sql);
        Assert.Contains("'DELETE,TRUNCATE,TRIGGER'", sql);
        Assert.Contains("'REFERENCES'", sql);
    }

    [Fact]
    public void Configured_role_is_sql_quoted_and_cannot_change_the_predicate()
    {
        var sql = CoreCurrencyPrecisionSchemaReadiness.PrivilegeSql(false, "role' OR true --");
        Assert.Contains("current_user='role'' OR true --'", sql);
    }
}
