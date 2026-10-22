using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
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
    public async Task Configured_Npgsql_store_and_true_option_cannot_create_readiness_without_actual_generated_manifest()
    {
        var scope=new Scope();
        await using var owner=new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_a_runtime_store;Username=invalid").Options,scope);
        var enabled=new ProcurementApprovalExecutionOptions {Enabled=true,PrincipalExecutionEnabled=true,ExecutionDatabaseConfigured=true};
        Assert.True(enabled.PrincipalExecutionEnabled);
        await using var core=new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=design_only_not_a_runtime_store;Username=invalid").Options,new FixedTenantAccessor(scope.TenantId));
        var candidate=new ProcurementPrincipalApprovalStoreReadiness(scope,core);
        Assert.False(ProcurementPrincipalApprovalStoreReadiness.ManifestCaptured);
        Assert.False(await candidate.IsReadyAsync(owner,default));Assert.False(await candidate.CanRecordAsync(owner,default));
        Assert.False(await candidate.CanExecuteAsync(owner,default));
        Assert.Equal(System.Data.ConnectionState.Closed,owner.Database.GetDbConnection().State);
        var production=new UnavailableProcurementPrincipalApprovalSchemaReadiness();
        Assert.False(await production.CanExecuteAsync(owner,default));
        Assert.False(new ProcurementApprovalExecutionOptions().PrincipalExecutionEnabled);
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
