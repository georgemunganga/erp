using System.Net;
using Mightyfin.Erp.Hrm.Api;
namespace Mightyfin.Erp.Hrm.Tests;
public sealed partial class ProductOperationalFlowTests
{
    [Fact] public async Task Verified_cookie_session_can_cross_guest_budget_without_borrowing_authority()
    {
        await using var f=await Fixture.Start();var response=await f.Callback(await f.Challenge());
        var cookie=SetCookie(response,ProductOperationalFlow.Cookie)+"; "+SetCookie(response,ProductOperationalBrowser.Cookie);
        for(var i=0;i<90;i++) using (var read=await f.Get(ProductOperationalFlow.Prefix+"/session",cookie)) Assert.Equal(HttpStatusCode.OK,read.StatusCode);
        using var absent=await f.Get(ProductOperationalFlow.Prefix+"/session");Assert.Equal(HttpStatusCode.Unauthorized,absent.StatusCode);
    }
    [Fact] public async Task Invented_cookie_and_headers_do_not_allocate_verified_session_budgets()
    {
        await using var f=await Fixture.Start();HttpResponseMessage? last=null;
        for(var i=0;i<65;i++)
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,ProductOperationalFlow.Prefix+"/session");
            request.Headers.Add("Cookie",ProductOperationalFlow.Cookie+"=invented-"+i);
            request.Headers.Add("X-Forwarded-For","192.0.2."+(i+1));request.Headers.Add("X-Kuubu-Session-Context",new string('a',64));
            last?.Dispose();last=await f.Client.SendAsync(request);
        }
        using(last){Assert.Equal(HttpStatusCode.TooManyRequests,last!.StatusCode);Assert.Equal(TimeSpan.FromSeconds(60),last.Headers.RetryAfter?.Delta);}
    }
}
