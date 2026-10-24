using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementRfqCommercialStoreReadinessTests
{
    private sealed class SourceReady : IProcurementSourcingDraftSchemaReadiness
    {
        public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class DocumentReady(bool ready) : IDocumentSchemaReadiness
    {
        public Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(ready);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Document_command_preflight_denies_missing_feature_or_schema_readiness(
        bool feature, bool schema)
    {
        const string execution = "Host=localhost;Database=private_review;Username=finite_execution";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProcurementApprovalExecution"] = execution,
            ["ConnectionStrings:CoreDocuments"] = execution,
        }).Build();
        using var services = new ServiceCollection()
            .AddSingleton(new ProcurementSourcingDraftExecutionOptions
                { Enabled = true, ExecutionDatabaseConfigured = true })
            .AddSingleton<IProcurementSourcingDraftSchemaReadiness>(new SourceReady())
            .AddSingleton(new CoreDocumentFeature(feature))
            .AddSingleton<IDocumentSchemaReadiness>(new DocumentReady(schema))
            .AddSingleton<IConfiguration>(config).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var error = await Assert.ThrowsAsync<ProcurementSourcingExecutionRoutes.Problem>(() =>
            ProcurementSourcingExecutionRoutes.RequireCommercialDocumentCommandFoundation(
                http, null, CancellationToken.None));
        Assert.Equal("RfqCommercialDocumentsUnavailable", error.Message);
        Assert.Equal(503, error.Status);
    }

    [Fact]
    public async Task Document_command_preflight_accepts_only_matching_configured_document_store()
    {
        const string execution = "Host=localhost;Database=private_review;Username=finite_execution";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProcurementApprovalExecution"] = execution,
            ["ConnectionStrings:CoreDocuments"] = execution,
        }).Build();
        using var services = new ServiceCollection()
            .AddSingleton(new ProcurementSourcingDraftExecutionOptions
                { Enabled = true, ExecutionDatabaseConfigured = true })
            .AddSingleton<IProcurementSourcingDraftSchemaReadiness>(new SourceReady())
            .AddSingleton(new CoreDocumentFeature(true))
            .AddSingleton<IDocumentSchemaReadiness>(new DocumentReady(true))
            .AddSingleton<IConfiguration>(config).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        // This proves only bounded preflight; owning-transaction physical grants
        // are checked by the same method when a Procurement context is supplied.
        await ProcurementSourcingExecutionRoutes.RequireCommercialDocumentCommandFoundation(
            http, null, CancellationToken.None);
        config["ConnectionStrings:CoreDocuments"] =
            "Host=localhost;Database=other_store;Username=finite_execution";
        var error = await Assert.ThrowsAsync<ProcurementSourcingExecutionRoutes.Problem>(() =>
            ProcurementSourcingExecutionRoutes.RequireCommercialDocumentCommandFoundation(
                http, null, CancellationToken.None));
        Assert.Equal("RfqCommercialDocumentsUnavailable", error.Message);
    }

    [Fact]
    public void Physical_capture_is_complete_and_retains_non_model_parent_links()
    {
        Assert.True(ProcurementRfqCommercialSchemaCapture.Populated);
        Assert.Equal("20261024182504_P32PrincipalCommercialEvidence",
            ProcurementRfqCommercialSchemaCapture.MigrationId);
        Assert.Equal(15, ProcurementRfqCommercialSchemaCapture.Types.Length);
        Assert.Equal(15, ProcurementRfqCommercialSchemaCapture.TableNames.Count);
        Assert.Equal(15, ProcurementRfqCommercialSchemaCapture.Checks.Count);
        Assert.Equal(9, ProcurementRfqCommercialSchemaCapture.OwnershipNames.Count);
        Assert.Equal(3, ProcurementRfqCommercialSchemaCapture.ParentKeys.Count);
        Assert.Contains("procurement.rfq_commercial_drafts.ck_pr32_draft",
            ProcurementRfqCommercialSchemaCapture.Checks.Keys);
        Assert.Contains("procurement.principal_rfq_commercial_round_openings.ck_principal_rfq_commercial_opening",
            ProcurementRfqCommercialSchemaCapture.Checks.Keys);
        Assert.Contains("procurement.principal_rfq_commercial_offer_submissions.fk_p32_offer_bid_invitation",
            ProcurementRfqCommercialSchemaCapture.OwnershipNames);
        Assert.Contains("procurement.operational_policy_identity_evidence.ak_p32_policy_round_identity",
            ProcurementRfqCommercialSchemaCapture.ParentKeys.Keys);
    }

    [Fact]
    public void Unrecognized_or_missing_model_family_and_CHECK_fail_closed()
    {
        var tables = ProcurementRfqCommercialSchemaCapture.TableNames.ToHashSet(StringComparer.Ordinal);
        var checks = ProcurementRfqCommercialSchemaCapture.Checks.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.True(ProcurementRfqCommercialStoreReadiness.MatchesModelCatalog(tables, checks));
        tables.Remove("procurement.rfq_commercial_drafts");
        Assert.False(ProcurementRfqCommercialStoreReadiness.MatchesModelCatalog(tables, checks));
        tables.Add("procurement.rfq_commercial_drafts");
        checks.Remove("procurement.rfq_commercial_drafts.ck_pr32_draft");
        checks.Add("procurement.rfq_commercial_drafts.ck_unreviewed");
        Assert.False(ProcurementRfqCommercialStoreReadiness.MatchesModelCatalog(tables, checks));
    }

    [Fact]
    public void Read_contract_does_not_require_commercial_or_Core_writes()
    {
        var sql = ProcurementRfqCommercialStoreReadiness.PrivilegeSql(
            ["procurement.rfq_commercial_drafts", "procurement.rfq_commercial_receipts"], false);
        Assert.Contains("has_table_privilege(current_user,'procurement.rfq_commercial_drafts','SELECT')", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.rfq_commercial_receipts','UPDATE')", sql);
        Assert.Contains("NOT has_table_privilege(current_user,'procurement.rfq_commercial_receipts','DELETE,TRUNCATE')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.rfq_commercial_receipts','INSERT')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'core_approval.cases','INSERT')", sql);
    }

    [Fact]
    public void Write_contract_only_mutates_draft_head_and_shared_Core_case_state()
    {
        var sql = ProcurementRfqCommercialStoreReadiness.PrivilegeSql(
            ["procurement.rfq_commercial_drafts", "procurement.rfq_commercial_receipts"], true);
        Assert.Contains("has_table_privilege(current_user,'procurement.rfq_commercial_drafts','UPDATE')", sql);
        Assert.Contains("has_table_privilege(current_user,'procurement.rfq_commercial_receipts','INSERT')", sql);
        Assert.Contains("has_table_privilege(current_user,'core_approval.decisions','INSERT')", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'core_approval.decisions','UPDATE')", sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.audit_events','UPDATE')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.rfq_commercial_receipts','UPDATE')", sql);
        Assert.DoesNotContain("has_table_privilege(current_user,'procurement.purchase_requests','UPDATE')", sql);
    }

    [Fact]
    public async Task Disabled_and_missing_owner_are_never_ready()
    {
        var unavailable = new ProcurementRfqCommercialStoreReadiness(new ProcurementRfqCommercialOptions(),
            null!, null!, null!, null!, null!);
        Assert.False(await unavailable.IsReadyAsync(null!, true, CancellationToken.None));
        var enabled = new ProcurementRfqCommercialStoreReadiness(new ProcurementRfqCommercialOptions { Enabled = true },
            null!, null!, null!, null!, null!);
        Assert.False(await enabled.IsReadyAsync(null!, true, CancellationToken.None));
    }
}
