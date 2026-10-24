using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ContractReviewReadinessTests
{
    [Fact]
    public void Policy_privileges_do_not_require_review_or_document_writes()
    {
        var sql = Sql(ContractReviewStorePurpose.PolicyWrite);
        Assert.Contains("has_table_privilege(current_user,'procurement.policy_versions','UPDATE')", sql);
        Assert.Contains("has_table_privilege(current_user,'procurement.contract_review_policy_identity','INSERT')", sql);
        Assert.Contains(ProcurementPrincipalApprovalStoreReadiness.NoCoreWorkflowDmlSql[7..], sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'core_approval.cases','INSERT')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.contract_review_submissions','INSERT')", sql);
        Assert.DoesNotContain("core_documents", sql);
        Assert.DoesNotContain("outbox_events", sql);
    }

    [Fact]
    public void Review_privileges_require_actual_case_mutation_and_append_only_companions_without_policy_write()
    {
        var sql = Sql(ContractReviewStorePurpose.ReviewWrite);
        foreach (var name in new[] { "cases", "stages", "assignments" })
            Assert.Contains($"has_table_privilege(current_user,'core_approval.{name}','UPDATE')", sql);
        foreach (var table in ProcurementContractReviewSchemaCapture.TableNames.Where(x => !x.EndsWith("policy_identity")))
            Assert.Contains($"has_table_privilege(current_user,'{table}','INSERT')", sql);
        Assert.Contains(ProcurementInvoiceUnitReviewSchemaReadiness.CoreDecisionPrivilegesSql[7..], sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.policy_versions','UPDATE')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.contract_review_policy_identity','INSERT')", sql);
        Assert.DoesNotContain("core_documents", sql);
    }

    [Fact]
    public void Read_requires_no_insert_or_update_grant_and_every_purpose_rejects_column_evidence_updates()
    {
        var read = Sql(ContractReviewStorePurpose.Read);
        Assert.DoesNotContain(",'INSERT')", read);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.supplier_contracts','UPDATE')", read);
        foreach (var purpose in Enum.GetValues<ContractReviewStorePurpose>())
        foreach (var table in ProcurementContractReviewSchemaCapture.TableNames)
        {
            var sql = Sql(purpose);
            Assert.Contains($"NOT has_any_column_privilege(current_user,'{table}','UPDATE')", sql);
            Assert.Contains($"NOT has_table_privilege(current_user,'{table}','DELETE,TRUNCATE')", sql);
        }
        Assert.Contains("pg_has_role(current_user,c.relowner,'MEMBER')", read);
        Assert.Contains("rolbypassrls", read);
    }

    [Fact]
    public void Physical_capture_requires_every_literal_and_exact_model_partial_predicate()
    {
        Assert.True(ProcurementContractReviewSchemaCapture.Populated);
        Assert.Equal(5, ProcurementContractReviewSchemaCapture.Checks.Count);
        Assert.Equal(12, ProcurementContractReviewSchemaCapture.ForeignKeys.Count);
        var missing = ProcurementContractReviewSchemaCapture.ForeignKeys.ToDictionary(x => x.Key, x => x.Value);
        missing.Remove(missing.Keys.First());
        Assert.False(ProcurementContractReviewSchemaCapture.Complete(ProcurementContractReviewSchemaCapture.Checks,
            missing, ProcurementContractReviewSchemaCapture.ParentKeys, ProcurementContractReviewSchemaCapture.PartialPredicates));
        Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(Mightyfin.Erp.Procurement.Domain.ContractReviewPolicyIdentityRecord),
            "ux_p35_contract_policy_created", "stage = 'Created'", out var actual));
        Assert.Equal("((stage)::text = 'Created'::text)", actual);
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            typeof(Mightyfin.Erp.Procurement.Domain.ContractReviewPolicyIdentityRecord),
            "ux_p35_contract_policy_created", "stage = 'Published'", out _));
    }

    [Fact]
    public void Actual_policy_model_resolves_all_three_unique_indexes_without_loosening_filtered_admission()
    {
        using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-used.invalid;Database=contract-model-not-opened").Options, new Scope());
        var type = typeof(Mightyfin.Erp.Procurement.Domain.ContractReviewPolicyIdentityRecord);
        var entity = owner.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
        var indexes = entity.GetIndexes().Where(x => x.IsUnique).ToArray();
        var expected = new Dictionary<string, string?>
        {
            ["ux_p35_contract_policy_revision"] = null,
            ["ux_p35_contract_policy_created"] = "((stage)::text = 'Created'::text)",
            ["ux_p35_contract_policy_published"] = "((stage)::text = 'Published'::text)"
        };
        Assert.Equal(3, indexes.Length);
        Assert.True(expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(indexes.Select(x => x.GetDatabaseName()!)));
        foreach (var index in indexes)
        {
            Assert.True(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
                type, index.GetDatabaseName(), index.GetFilter(), out var actual));
            Assert.Equal(expected[index.GetDatabaseName()!], actual);
        }
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            type, "ux_p35_contract_policy_unknown", "stage = 'Created'", out _));
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            type, "ux_p35_contract_policy_revision", "stage = 'Created'", out _));
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            type, "ux_p35_contract_policy_created", null, out _));
        Assert.False(ProcurementPrincipalCommercialSchemaInspection.TryResolveIndexPredicate(
            type, "ux_p35_contract_policy_published", "stage = 'Created'", out _));
        Assert.Equal(System.Data.ConnectionState.Closed, owner.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task Legacy_readiness_implementation_does_not_admit_new_policy_writes()
    {
        IProcurementContractReviewSchemaReadiness legacy = new LegacyReadiness();
        Assert.True(await legacy.CanRecordAsync(null!, default));
        Assert.False(await legacy.CanRecordPolicyAsync(null!, default));
        Assert.False(await legacy.IsConfiguredAsync(default));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public async Task Host_composes_real_default_off_provider_and_never_substitutes_missing_owner(string? setting, bool enabled)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Procurement:ContractReview:Enabled"] = setting }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        ProcurementHostIntegration.AddServices(services, "Host=never-used.invalid;Database=contract-test", config);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<ProcurementContractReviewOptions>();
        Assert.Equal(enabled, options.Enabled);
        Assert.False(options.ExecutionDatabaseConfigured);
        var readiness = provider.GetRequiredService<IProcurementContractReviewSchemaReadiness>();
        Assert.IsType<ProcurementContractReviewStoreReadiness>(readiness);
        Assert.False(await readiness.IsReadReadyAsync(null!, default));
        Assert.False(await readiness.CanRecordPolicyAsync(null!, default));
        Assert.False(await readiness.CanRecordAsync(null!, default));
    }

    [Theory]
    [InlineData(false, true, true, true, true, true)]
    [InlineData(true, false, true, true, true, true)]
    [InlineData(true, true, false, false, false, false)]
    [InlineData(true, true, true, false, true, true)]
    [InlineData(true, true, true, true, false, true)]
    [InlineData(true, true, true, true, true, false)]
    public async Task Configured_admission_rejects_invalid_options_before_resolving_execution(
        bool enabled, bool configured, bool registerApprovalOptions, bool approvalsEnabled,
        bool principalEnabled, bool approvalConfigured)
    {
        var services = new ServiceCollection();
        if (registerApprovalOptions) services.AddSingleton(new ProcurementApprovalExecutionOptions
        { Enabled = approvalsEnabled, PrincipalExecutionEnabled = principalEnabled, ExecutionDatabaseConfigured = approvalConfigured });
        var resolutions = 0;
        services.AddSingleton<ProcurementApprovalTransaction>(_ =>
        {
            resolutions++;
            throw new InvalidOperationException("Execution must not be resolved for invalid configuration.");
        });
        using var provider = services.BuildServiceProvider();
        var readiness = new ProcurementContractReviewStoreReadiness(new() { Enabled = enabled, ExecutionDatabaseConfigured = configured },
            new ConfigurationBuilder().Build(), provider);
        Assert.False(await readiness.IsConfiguredAsync(default));
        Assert.Equal(0, resolutions);
    }

    [Fact]
    public async Task Writes_without_supplied_transaction_deny_before_opening_any_connection()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:ProcurementApprovalExecution"] = "Host=never-used.invalid;Database=contract-test;Username=execution" }).Build();
        using var services = new ServiceCollection().BuildServiceProvider();
        var readiness = new ProcurementContractReviewStoreReadiness(new() { Enabled = true, ExecutionDatabaseConfigured = true }, config, services);
        await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=never-used.invalid;Database=contract-test;Username=record").Options, new Scope());
        Assert.False(await readiness.CanRecordPolicyAsync(owner, default));
        Assert.False(await readiness.CanRecordAsync(owner, default));
        Assert.Equal(System.Data.ConnectionState.Closed, owner.Database.GetDbConnection().State);
        Assert.Null(owner.Database.CurrentTransaction);
    }

    private static string Sql(ContractReviewStorePurpose purpose) => ProcurementContractReviewStoreReadiness.PrivilegeSql(
        purpose, ProcurementContractReviewSchemaCapture.TableNames.Concat(new[] { "procurement.policy_versions", "procurement.supplier_contracts", "procurement.supplier_contract_versions", "procurement.contract_rates", "procurement.suppliers", "procurement.audit_events" }).ToArray());
    private sealed class LegacyReadiness : IProcurementContractReviewSchemaReadiness
    {
        public Task<bool> IsReadReadyAsync(ProcurementDbContext owner, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> CanRecordAsync(ProcurementDbContext owner, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class Scope : IProcurementScope
    {
        public string TenantId => "contract-readiness";
        public Guid LegalEntityId => Guid.Parse("11111111-1111-1111-1111-111111111111");
        public string SubjectId => "22222222-2222-2222-2222-222222222222";
        public Guid? WorkLocationId => null;
        public Guid? OrgUnitId => null;
        public bool IsConfined => false;
        public string? CorrelationId => null;
    }
}
