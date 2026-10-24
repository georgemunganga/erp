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
    public void Genuine_P30_catalog_retains_the_captured_P28_payment_fingerprint()
    {
        // Actual private PG17 RFQ05 capture, source 19faf916, history through
        // 20261023234712. This regression records compatibility, not role proof.
        const string captured = "a5e05163346be79ccaee5aa11093e377a9d0a5f86046311759698ca72435e29b";
        Assert.Equal(captured, ProcurementManualPaymentGovernanceReadiness.ExpectedFingerprint);
        Assert.Contains("('procurement','operational_policy_identity_evidence')",
            ProcurementManualPaymentGovernanceReadiness.FingerprintSql);
        Assert.Contains("pg_get_constraintdef(c.oid)", ProcurementManualPaymentGovernanceReadiness.FingerprintSql);
        Assert.DoesNotContain("SOURCING_PUBLISH", ProcurementManualPaymentGovernanceReadiness.OperationalCheckDefinition);
    }

}
