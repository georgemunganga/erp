using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalOrderAuthorityAdmissionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Configuration_uses_the_dedicated_execution_read_proof_and_preserves_failure(bool ready)
    {
        var proof=new ExecutionReadProof(ready);
        using var services=new ServiceCollection()
            .AddSingleton<IProcurementPrincipalOrderGovernanceReadiness>(proof).BuildServiceProvider();
        var http=new DefaultHttpContext{RequestServices=services};
        var check=ProcurementSpendingAuthorityRoutes.RequirePrincipalOrderAuthoritySchema(
            http,"PurchaseOrder.Approve",default);
        if(ready)await check;
        else Assert.Equal("OrderAuthoritySchemaUnavailable",
            (await Assert.ThrowsAsync<InvalidOperationException>(()=>check)).Message);
        Assert.Equal(1,proof.ExecutionReads);
    }

    [Fact]
    public async Task Other_authority_actions_do_not_acquire_PO_governance_dependencies()
    {
        using var services=new ServiceCollection().BuildServiceProvider();
        await ProcurementSpendingAuthorityRoutes.RequirePrincipalOrderAuthoritySchema(
            new DefaultHttpContext{RequestServices=services},"PurchaseRequest.Approve",default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>
            ProcurementSpendingAuthorityRoutes.RequirePrincipalOrderAuthoritySchema(
                new DefaultHttpContext{RequestServices=services},"PurchaseOrder.Approve",default));
    }

    private sealed class ExecutionReadProof(bool ready):IProcurementPrincipalOrderGovernanceReadiness
    {
        internal int ExecutionReads{get;private set;}
        public Task<bool> IsReadyAsync(CancellationToken ct)
        {ExecutionReads++;return Task.FromResult(ready);}
        public Task<bool> IsReadyAsync(ProcurementDbContext record,bool write,CancellationToken ct)
            =>throw new InvalidOperationException("Configuration must not impersonate Execution on its Record connection.");
    }
}
