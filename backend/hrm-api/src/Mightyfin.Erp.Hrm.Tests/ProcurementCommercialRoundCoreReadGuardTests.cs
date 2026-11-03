using Mightyfin.Erp.Hrm.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCommercialRoundCoreReadGuardTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Both_B1_profiles_enforce_the_private_Core_read_boundary(bool record)
        => Assert.Contains(ProcurementCommercialRoundCoreReadGuard.Sql,
            ProcurementCommercialRoundReadiness.PrivilegeSql(record, "finite_procurement_role"));

    [Fact]
    public void Required_Core_projection_keeps_existing_minimum_and_does_not_require_optional_shared_reads()
    {
        var required = ProcurementCommercialRoundCoreReadGuard.RequiredReadColumns;
        foreach (var projection in ProcurementInvoiceReplacementSchemaReadiness.CoreReadColumns)
            Assert.All(projection.Value, column => Assert.Contains(column, required[projection.Key]));
        Assert.Contains("display_name", required["hrm.local_users"]);
        Assert.Contains("time_zone_id", required["hrm.legal_entities"]);
        Assert.DoesNotContain("code", required["hrm.work_locations"]);
        Assert.False(required.ContainsKey("hrm.currency_precision_versions"));
        Assert.All(required, projection => Assert.All(projection.Value,
            column => Assert.True(ProcurementCommercialRoundCoreReadGuard.Allows(projection.Key, column))));
    }

    [Fact]
    public void Existing_shared_Core_read_projections_are_permitted_without_a_whole_HRM_table_grant()
    {
        foreach (var projection in ProcurementManualPaymentReadSchemaReadiness.ReadColumns
            .Where(x => x.Key.StartsWith("hrm.", StringComparison.Ordinal)))
            Assert.All(projection.Value,
                column => Assert.True(ProcurementCommercialRoundCoreReadGuard.Allows(projection.Key, column)));
        Assert.True(ProcurementCommercialRoundCoreReadGuard.Allows("hrm.legal_entities", "currency"));
        Assert.True(ProcurementCommercialRoundCoreReadGuard.Allows("hrm.currency_precision_versions", "minor_units"));
        Assert.True(ProcurementCommercialRoundCoreReadGuard.Allows("hrm.__hrm_migrations", "ProductVersion"));
    }

    [Theory]
    [InlineData(typeof(OrgUnit), nameof(OrgUnit.EffectiveFrom), "hrm.org_units", "effective_from")]
    [InlineData(typeof(OrgUnit), nameof(OrgUnit.EffectiveTo), "hrm.org_units", "effective_to")]
    [InlineData(typeof(Worker), nameof(Worker.ManagerId), "hrm.workers", "manager_id")]
    public void Existing_principal_approval_reads_are_optional_without_admitting_private_or_unknown_columns(
        Type type, string property, string table, string column)
    {
        using var core = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_opened;Username=invalid;Timeout=1").Options,
            new FixedTenantAccessor("optional-core-projection"));
        var entity = core.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
        var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
        Assert.Equal(table, store.Schema + "." + store.Name);
        Assert.Equal(column, entity.FindProperty(property)!.GetColumnName(store));
        Assert.Contains(property, ProcurementPrincipalApprovalStoreReadiness.CoreIdentityReadProperties[type]);
        Assert.Contains("\"" + column + "\"", ProcurementPrincipalApprovalStoreReadiness.CoreIdentitySelectSql(entity));

        Assert.True(ProcurementCoreReadProjectionGuard.Allows(table, column));
        Assert.True(ProcurementCommercialRoundCoreReadGuard.Allows(table, column));
        Assert.Contains("('" + table[4..] + "','" + column + "')", ProcurementCoreReadProjectionGuard.Sql);
        Assert.False(ProcurementCoreReadProjectionGuard.RequiredReadColumns.TryGetValue(table, out var shared)
            && shared.Contains(column, StringComparer.Ordinal));
        Assert.False(ProcurementPlanReviewSchemaReadiness.CoreColumns.TryGetValue(table, out var plan)
            && plan.Contains(column, StringComparer.Ordinal));
        Assert.False(ProcurementCoreReadProjectionGuard.Allows(table, "future_private_column"));
        Assert.False(ProcurementCoreReadProjectionGuard.Allows("hrm.local_users", "password_hash"));
        Assert.False(ProcurementCoreReadProjectionGuard.Allows("hrm.payroll_items", "gross_pay"));
    }

    [Theory]
    [InlineData("hrm.local_users", "password_hash")]
    [InlineData("hrm.local_users", "email")]
    [InlineData("hrm.local_users", "recovery_token")]
    [InlineData("hrm.workers", "national_id")]
    [InlineData("hrm.payroll_runs", "id")]
    [InlineData("hrm.currency_precision_command_receipts", "response_json")]
    [InlineData("hrm.unlisted_employee_view", "id")]
    [InlineData("hrm.local_users", "future_private_column")]
    public void Sensitive_or_unlisted_Core_columns_are_not_permitted(string table, string column)
        => Assert.False(ProcurementCommercialRoundCoreReadGuard.Allows(table, column));

    [Fact]
    public void Guard_uses_effective_column_privileges_and_rejects_unlisted_views_and_future_columns()
    {
        var sql = ProcurementCommercialRoundCoreReadGuard.Sql;
        Assert.Contains("has_column_privilege(current_user,c.oid,a.attnum,'SELECT')", sql);
        Assert.Contains("c.relkind IN ('r','p','v','m','f')", sql);
        Assert.Contains("a.attnum>0 AND NOT a.attisdropped", sql);
        Assert.Contains("allowed.table_name=c.relname", sql);
        Assert.Contains("allowed.column_name=a.attname", sql);
        Assert.DoesNotContain("password_hash", sql);
        Assert.DoesNotContain("currency_precision_command_receipts", sql);
        Assert.DoesNotContain("payroll", sql);
        Assert.DoesNotContain("SET ROLE", sql, StringComparison.OrdinalIgnoreCase);
    }
}
