using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Mightyfin.Erp.Hrm.Api;
namespace Mightyfin.Erp.Hrm.Tests;

public sealed class InvoiceIntakeWriteScopeTests
{
    [Theory]
    [InlineData("raw-all-fallback")]
    [InlineData("resolved-all")]
    [InlineData("company-only")]
    [InlineData("org-unit")]
    public async Task Every_intake_mutation_rejects_unselected_scope_before_body_or_any_database(string mode)
    {
        var http=new DefaultHttpContext();var scope=new ProcurementRequestScope();var branch=Guid.NewGuid();
        var principal=Guid.NewGuid();
        scope.Set("intake-scope-test",Guid.NewGuid(),principal.ToString("D"),
            mode is "raw-all-fallback" or "org-unit" ? branch : null,
            mode=="org-unit" ? Guid.NewGuid() : null,false,"test",mode=="resolved-all");
        if(mode=="raw-all-fallback")http.Request.Headers["X-Shell-Branch-Scope"]="all";
        // Null contexts deliberately prove the guard runs before intake storage or Core access.
        var capture=await ProcurementInvoiceIntakeRoutes.Capture(http.Request,null!,scope,null!,default);
        var update=await ProcurementInvoiceIntakeRoutes.Update(Guid.NewGuid(),http.Request,null!,scope,null!,http,default);
        var cancel=await ProcurementInvoiceIntakeRoutes.Cancel(Guid.NewGuid(),new(1,"Correction"),null!,scope,http,default);
        foreach(var result in new[]{capture,update,cancel})
        {
            Assert.Equal(422,Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            var body=JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal("invoice-selected-branch-required",body.GetProperty("code").GetString());
        }
    }
    [Fact]
    public void Selected_branch_is_required_for_every_actor_including_organisation_wide()
    {
        var scope=new ProcurementRequestScope();var principal=Guid.NewGuid();
        scope.Set("intake-scope-test",Guid.NewGuid(),principal.ToString("D"),Guid.NewGuid(),null,false,"test");
        Assert.True(ProcurementInvoiceIntakeRoutes.IntakeWriteContextAllowed(scope,new DefaultHttpContext().Request));
    }
}
