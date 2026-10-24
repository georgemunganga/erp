using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Owning-model/SQL admission contract, not an executed PostgreSQL grant
/// proof. Actual finite-role positive/negative admission remains a release gate.</summary>
public sealed class ProcurementManualPaymentGovernanceReadinessTests
{
    private sealed record Tenant : ITenantAccessor
    { public string GetTenantId() => "payment-column-readiness"; }
    private static HrmDbContext Core() => new(new DbContextOptionsBuilder<HrmDbContext>()
        .UseNpgsql("Host=never-used.invalid;Database=not-opened").Options, new Tenant());

    [Fact]
    public void Column_only_Core_projection_has_no_table_SELECT_or_credentials_requirement()
    {
        using var core = Core();
        var model = core.GetService<IDesignTimeModel>().Model;
        var sql = ProcurementManualPaymentGovernanceReadiness.PrivilegeSql(model);
        foreach (var (type, properties) in ProcurementManualPaymentGovernanceReadiness.CoreReadProperties)
        {
            var entity = model.FindEntityType(type)!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, "hrm");
            Assert.DoesNotContain("has_table_privilege(current_user,'hrm." + table.Name + "','SELECT')", sql);
            foreach (var property in properties)
            {
                var column = entity.FindProperty(property)!.GetColumnName(table);
                var required = "has_column_privilege(current_user,'hrm." + table.Name + "','" + column + "','SELECT')";
                Assert.Contains(" AND " + required + " AND ", sql);
                Assert.Equal(1, sql.Split(required, StringSplitOptions.None).Length - 1);
            }
        }
        var reads = string.Join(" AND ", ProcurementManualPaymentGovernanceReadiness.CoreReadClauses(model));
        Assert.DoesNotContain("password", reads, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", reads, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salary", reads, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" OR ", reads);
    }

    [Theory]
    [InlineData("hrm.local_users", "worker_id")]
    [InlineData("hrm.local_users", "roles_csv")]
    [InlineData("hrm.local_users", "display_name")]
    [InlineData("hrm.tenant_role_assignments", "permissions_csv")]
    [InlineData("hrm.hr_user_branch_assignments", "location_id")]
    [InlineData("hrm.legal_entities", "time_zone_id")]
    [InlineData("hrm.work_locations", "code")]
    [InlineData("hrm.work_locations", "name")]
    public void Missing_individual_column_is_an_independent_required_AND_term(string table, string column)
    {
        using var core = Core();
        var model = core.GetService<IDesignTimeModel>().Model;
        var required = "has_column_privilege(current_user,'" + table + "','" + column + "','SELECT')";
        var clauses = ProcurementManualPaymentGovernanceReadiness.CoreReadClauses(model);
        Assert.Contains(required, clauses);
        // No table-level alternative or OR may rescue a missing individual read.
        // Real has_column_privilege evaluation is reserved for PostgreSQL proof.
        Assert.All(clauses, clause => Assert.StartsWith("has_column_privilege(current_user,", clause));
        Assert.Contains(" AND " + required + " AND ", ProcurementManualPaymentGovernanceReadiness.PrivilegeSql(model));
    }

    [Fact]
    public void Payment_labels_and_clock_extend_the_existing_Core_identity_projection_only()
    {
        foreach (var (type, properties) in ProcurementPrincipalApprovalStoreReadiness.CoreIdentityReadProperties)
        {
            var actual = ProcurementManualPaymentGovernanceReadiness.CoreReadProperties[type];
            var additions = type == typeof(LocalUser) ? new[] { nameof(LocalUser.DisplayName) }
                : type == typeof(WorkLocation) ? new[] { nameof(WorkLocation.Code), nameof(WorkLocation.Name) }
                : type == typeof(LegalEntity) ? new[] { nameof(LegalEntity.TimeZoneId) } : [];
            Assert.Equal(properties.Concat(additions).Distinct(StringComparer.Ordinal), actual);
        }
        Assert.Equal(7, ProcurementManualPaymentGovernanceReadiness.CoreReadProperties.Count);
    }

    [Fact]
    public void Narrower_reads_do_not_replace_Core_write_owner_or_elevated_membership_denials()
    {
        using var core = Core();
        var sql = ProcurementManualPaymentGovernanceReadiness.PrivilegeSql(core.Model);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoCoreIdentityWritesSql, sql);
        Assert.Contains(ProcurementSpendingAuthorityIdentitySchemaReadiness.NoOwnershipSql, sql);
        Assert.Contains("rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls", sql);
        Assert.Contains("pg_has_role(current_user,r.oid,'SET')", sql);
        Assert.Contains("has_any_column_privilege(current_user,c.oid,'UPDATE')", sql);
        Assert.Contains("has_schema_privilege(current_user,'hrm','CREATE')", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE')", sql);
    }

    [Fact]
    public async Task Missing_execution_connection_and_default_options_remain_closed()
    {
        var readiness = new ProcurementManualPaymentGovernanceReadiness("", new ProcurementRequestScope(),
            NullLogger<ProcurementManualPaymentGovernanceReadiness>.Instance);
        Assert.False(await readiness.IsReadyAsync(CancellationToken.None));
        Assert.False(new ProcurementManualPaymentOptions().Enabled);
        Assert.False(new ProcurementManualPaymentOptions().ExecutionDatabaseConfigured);
    }
    [Fact]
    public void P28_fingerprint_cannot_be_adopted_under_a_P30_migration_history()
    {
        var p28 = ProcurementManualPaymentGovernanceReadiness.MigrationId;
        var p30 = ProcurementManualPaymentGovernanceReadiness.P30MigrationId;
        var original = ProcurementManualPaymentGovernanceReadiness.ExpectedFingerprint;
        Assert.True(ProcurementManualPaymentGovernanceReadiness.FingerprintMatches(original, [p28]));
        Assert.False(ProcurementManualPaymentGovernanceReadiness.FingerprintMatches(original, [p28, p30]));
        Assert.False(ProcurementManualPaymentGovernanceReadiness.FingerprintMatches(original, [p30]));
        Assert.False(ProcurementManualPaymentGovernanceReadiness.FingerprintMatches(original, []));
        Assert.False(ProcurementManualPaymentGovernanceReadiness.FingerprintMatches(new string('0', 64), [p28]));
    }

    [Fact]
    public void Missing_actual_P30_fingerprint_never_reports_ready_or_falls_back()
    {
        // Deliberate source-stage hold, to be replaced by actual capture-bound
        // positive/tamper assertions when the owner supplies the real PG bytes.
        Assert.Empty(ProcurementManualPaymentGovernanceReadiness.P30ExpectedFingerprint);
        Assert.False(ProcurementManualPaymentGovernanceReadiness.FingerprintMatches("",
            [ProcurementManualPaymentGovernanceReadiness.MigrationId,
             ProcurementManualPaymentGovernanceReadiness.P30MigrationId]));
    }

}
