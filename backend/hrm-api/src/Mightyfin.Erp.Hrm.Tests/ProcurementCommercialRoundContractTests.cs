using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementCommercialRoundContractTests
{
    [Theory]
    [InlineData("read")][InlineData("manage")][InlineData("submit")][InlineData("review")][InlineData("cancel")]
    public void Round_plan_actions_are_explicit_Core_grants_not_legacy_admin_inheritance(string suffix)
    {
        var action="procurement-round-plan-"+suffix;
        Assert.True(ProcurementActionCatalog.RequiresExplicitConfiguredGrant(action));
        Assert.Empty(ProcurementActionCatalog.BootstrapGrants[action]);
        Assert.Contains(ProcurementActionCatalog.AdminReadCatalog(),x=>x.Key==action&&x.Group=="Sourcing and RFQs");
        Assert.False(ProcurementPermissions.Allows(action,[new Claim(ClaimTypes.Role,"erp_admin")]));
        Assert.True(ProcurementPermissions.Allows(action,[new Claim(ProcurementPermissions.ConfiguredClaim,"true"),new Claim(ProcurementPermissions.ActionClaim,"procurement-access"),new Claim(ProcurementPermissions.ActionClaim,action)]));
    }
    [Theory]
    [InlineData("/api/procurement/v1/sourcing/round-plans","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/history","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/versions/1","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/commands/22222222-2222-2222-2222-222222222222","GET",true)]
    [InlineData("/api/procurement/v1/sourcing/round-plans","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/revise","POST",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/versions/0","GET",false)]
    [InlineData("/api/procurement/v1/sourcing/11111111-1111-1111-1111-111111111111/round-plan/versions/1/extra","GET",false)]
    [InlineData("/api/procurement/v1/sourcing/00000000-0000-0000-0000-000000000000/round-plan","GET",false)]
    public void Company_All_middleware_only_admits_exact_read_paths(string path,string method,bool expected)
        => Assert.Equal(expected,ProcurementSourcingExecutionRoutes.RoundPlanAllReadPath(new PathString(path),method));
    [Fact]
    public void Flags_default_off_and_uncaptured_native_contract_cannot_admit_new_enrollment()
    {
        var options=new ProcurementCommercialRoundOptions();Assert.False(options.ReadEnabled);Assert.False(options.RecordEnabled);
        Assert.False(new ProcurementCommercialRoundReadiness(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).HasCapturedContract);
    }
}
