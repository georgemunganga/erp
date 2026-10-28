using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalInvoiceMatchingCapabilityTests
{
    private static ProcurementRequestScope Scope(Guid principal, Guid? branch=null,
        bool all=false, Guid? unit=null, bool confined=false)
    {
        var scope=new ProcurementRequestScope();
        scope.Set("invoice-capability",Guid.NewGuid(),principal.ToString("D"),branch,unit,
            confined,"test",explicitAllBranches:all);
        scope.AssignPrincipal(principal);
        return scope;
    }

    [Fact]
    public void Company_read_and_selected_branch_write_have_distinct_scope_guards()
    {
        var principal=Guid.NewGuid();
        var selected=Scope(principal,Guid.NewGuid());
        Assert.True(ProcurementPrincipalInvoiceMatchingCapability.ReadContext(selected));
        Assert.True(ProcurementPrincipalInvoiceMatchingCapability.WriteContext(selected,false));
        Assert.False(ProcurementPrincipalInvoiceMatchingCapability.WriteContext(selected,true));
        var all=Scope(principal,all:true);
        Assert.True(ProcurementPrincipalInvoiceMatchingCapability.ReadContext(all));
        Assert.False(ProcurementPrincipalInvoiceMatchingCapability.WriteContext(all,false));
        var noBranch=Scope(principal);
        Assert.True(ProcurementPrincipalInvoiceMatchingCapability.ReadContext(noBranch));
        Assert.False(ProcurementPrincipalInvoiceMatchingCapability.WriteContext(noBranch,false));
        Assert.False(ProcurementPrincipalInvoiceMatchingCapability.ReadContext(Scope(principal,unit:Guid.NewGuid())));
        Assert.False(ProcurementPrincipalInvoiceMatchingCapability.ReadContext(Scope(principal,confined:true)));
    }

    [Fact]
    public async Task Missing_execution_configuration_fails_closed_without_a_database_call()
    {
        var scope=Scope(Guid.NewGuid(),Guid.NewGuid());
        using var services=new ServiceCollection().AddSingleton(new ProcurementInvoiceMatchingExecutionOptions
            { Enabled=true,ExecutionDatabaseConfigured=false,PrincipalSourcesEnabled=true }).BuildServiceProvider();
        var result=await ProcurementPrincipalInvoiceMatchingCapability.ReadyAsync(
            services,scope,new DefaultHttpContext(),pending:false,default);
        Assert.False(result.Read);
        Assert.False(result.NewWork);
    }
}
