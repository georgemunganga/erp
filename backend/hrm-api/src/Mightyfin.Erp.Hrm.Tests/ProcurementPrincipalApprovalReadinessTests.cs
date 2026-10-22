using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalApprovalReadinessTests
{
    private sealed class Scope:IApprovalScope,IProcurementScope
    {
        public string TenantId=>"readiness-only";
        public Guid LegalEntityId{get;}=Guid.NewGuid();
        public string SubjectId{get;}=Guid.NewGuid().ToString("D");
        public Guid? WorkLocationId=>null;
        public Guid? OrgUnitId=>null;
        public bool IsConfined=>false;
        public string? CorrelationId=>"no-captured-manifest";
    }

    [Fact]
    public async Task Captured_manifest_and_true_option_do_not_authorize_an_unavailable_store_or_production_provider()
    {
        var scope=new Scope();
        await using var owner=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_a_runtime_store;Username=invalid;Timeout=1").Options,scope);
        var enabled=new ProcurementApprovalExecutionOptions {Enabled=true,PrincipalExecutionEnabled=true,ExecutionDatabaseConfigured=true};
        Assert.True(enabled.PrincipalExecutionEnabled);
        await using var core=new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_a_runtime_store;Username=invalid;Timeout=1").Options,new FixedTenantAccessor(scope.TenantId));
        var candidate=new ProcurementPrincipalApprovalStoreReadiness(scope,core);
        Assert.True(ProcurementPrincipalApprovalStoreReadiness.ManifestCaptured);
        Assert.False(await candidate.IsReadyAsync(owner,default));Assert.False(await candidate.CanRecordAsync(owner,default));
        Assert.False(await candidate.CanExecuteAsync(owner,default));
        Assert.Equal(System.Data.ConnectionState.Closed,owner.Database.GetDbConnection().State);
        var production=new UnavailableProcurementPrincipalApprovalSchemaReadiness();
        Assert.False(await production.CanExecuteAsync(owner,default));
        Assert.False(new ProcurementApprovalExecutionOptions().PrincipalExecutionEnabled);
    }

    [Fact]
    public void Captured_manifest_matches_every_selected_model_check_and_filtered_unique_index()
    {
        using var owner=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_a_runtime_store;Username=invalid").Options,new Scope());
        var model=owner.GetService<IDesignTimeModel>().Model;
        Type[] types=[typeof(PurchaseRequest),typeof(PurchaseRequestLine),typeof(BudgetCheckSnapshot),
            typeof(ProcurementCategory),typeof(PolicyVersion),typeof(PolicyEvaluation),typeof(PurchaseRequestApprovalSubmission),
            typeof(PurchaseRequestAssignmentAuthorityEvidence),typeof(ProcurementSpendingAuthorityGrant),
            typeof(ProcurementSpendingAuthorityCategory),typeof(SpendingAuthorityIdentityRecord),
            typeof(PurchaseRequestApprovalOutcome),typeof(PurchaseRequestFinanceReleaseRequest),
            typeof(PurchaseRequestFinanceReleaseConfirmation),typeof(ProcurementApprovalCommandReceipt),typeof(AuditEvent),typeof(OutboxEvent)];
        var entities=types.Select(type=>model.FindEntityType(type)!).ToArray();
        var checks=entities.SelectMany(entity=>entity.GetCheckConstraints().Select(check=>
            entity.GetSchema()+"."+entity.GetTableName()+"."+check.Name)).Order(StringComparer.Ordinal).ToArray();
        var indexes=entities.SelectMany(entity=>entity.GetIndexes().Where(index=>index.IsUnique && index.GetFilter() is not null)
            .Select(index=>entity.GetSchema()+"."+entity.GetTableName()+"."+index.GetDatabaseName())).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(checks,ProcurementPrincipalApprovalStoreReadiness.CheckDefinitions.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(indexes,ProcurementPrincipalApprovalStoreReadiness.PartialPredicates.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(17,checks.Length);Assert.Equal(3,indexes.Length);
        Assert.Equal("20261022161353_P25CorePrincipalRequestApproval",ProcurementPrincipalApprovalStoreReadiness.MigrationId);
        Assert.All(ProcurementPrincipalApprovalStoreReadiness.CheckDefinitions.Values,value=>Assert.StartsWith("CHECK (",value));
    }

    [Fact]
    public void Core_readiness_uses_only_actual_identity_grant_and_optional_manager_projection_not_credentials_or_payroll()
    {
        using var core=new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_a_runtime_store;Username=invalid;Timeout=1").Options,new FixedTenantAccessor("read-only-model"));
        var model=core.GetService<IDesignTimeModel>().Model;
        foreach(var type in ProcurementPrincipalApprovalStoreReadiness.CoreIdentityReadProperties.Keys)
        {
            var sql=ProcurementPrincipalApprovalStoreReadiness.CoreIdentitySelectSql(model.FindEntityType(type)!);
            Assert.StartsWith("SELECT ",sql);Assert.EndsWith(" LIMIT 0",sql);
            Assert.DoesNotContain("*",sql);Assert.DoesNotContain("password",sql,StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("salary",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("email",sql,StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("\"worker_id\"",ProcurementPrincipalApprovalStoreReadiness.CoreIdentitySelectSql(model.FindEntityType(typeof(LocalUser))!));
        Assert.Contains("\"manager_id\"",ProcurementPrincipalApprovalStoreReadiness.CoreIdentitySelectSql(model.FindEntityType(typeof(Worker))!));
        var company=ProcurementPrincipalApprovalStoreReadiness.CoreIdentitySelectSql(model.FindEntityType(typeof(LegalEntity))!);
        Assert.Contains("\"currency\"",company);
        var department=ProcurementPrincipalApprovalStoreReadiness.CoreIdentitySelectSql(model.FindEntityType(typeof(OrgUnit))!);
        Assert.Contains("\"effective_from\"",department);Assert.Contains("\"effective_to\"",department);
        using var owner=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_a_runtime_store;Username=invalid;Timeout=1").Options,new Scope());
        var category=ProcurementPrincipalApprovalStoreReadiness.CategorySelectSql(owner.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProcurementCategory))!);
        Assert.Equal("SELECT \"tenant_id\",\"legal_entity_id\",\"code\",\"status\" FROM \"procurement\".\"procurement_categories\" LIMIT 0",category);
        Assert.DoesNotContain("expense_account",category,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name",category,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Append_and_Core_record_boundaries_reject_column_update_truncate_ownership_and_unrelated_workflow_mutations()
    {
        var sql=ProcurementPrincipalApprovalStoreReadiness.AppendSql("budget_check_snapshots",true);
        Assert.Contains("has_table_privilege(current_user,'procurement.budget_check_snapshots','INSERT')",sql);
        Assert.Contains("NOT has_any_column_privilege(current_user,'procurement.budget_check_snapshots','UPDATE')",sql);
        Assert.Contains("'DELETE,TRUNCATE'",sql);
        Assert.Contains("'MEMBER'",ProcurementPrincipalApprovalStoreReadiness.NoOwnersSql(["purchase_requests"]));
        Assert.Contains("'SET'",ProcurementPrincipalApprovalStoreReadiness.NoOwnersSql(["purchase_requests"]));
        Assert.Contains("has_any_column_privilege(current_user,c.oid,'INSERT')",ProcurementPrincipalApprovalStoreReadiness.NoCoreWorkflowDmlSql);
        Assert.Contains("has_any_column_privilege(current_user,c.oid,'UPDATE')",ProcurementPrincipalApprovalStoreReadiness.NoCoreWorkflowDmlSql);
        Assert.Contains("c.relname NOT IN ('cases','stages','assignments','decisions')",ProcurementPrincipalApprovalStoreReadiness.NoOtherCoreWorkflowDmlSql);
    }
}
