using System.Net;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductOperationalRefreshTests
{
    private sealed class Fixture:HttpMessageHandler,IDisposable
    {
        internal readonly ProductOperationalCredentialTests.Clock Clock=new();
        private readonly HttpClient http;
        internal readonly ProductOperationalRefresh Client;
        internal string? Fault;
        internal int Status=200,Calls;
        internal bool Wait;
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Fixture(){http=new(this,disposeHandler:false);Client=new(http,Clock,ProductOperationalCredentialTests.Client,"private-client-secret");}
        internal Task<OperationalRefreshResult> Refresh(CancellationToken ct=default)=>Client.RefreshAsync("original-refresh+value/with=encoding",ct);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Calls++;Assert.Equal(ProductOperationalRefresh.Endpoint,request.RequestUri!.AbsoluteUri);Assert.Equal(HttpMethod.Post,request.Method);
            Assert.Null(request.Headers.Authorization);Assert.Equal("application/x-www-form-urlencoded",request.Content!.Headers.ContentType!.MediaType);
            var form=await request.Content.ReadAsStringAsync(cancellation);
            Assert.Contains("grant_type=refresh_token",form);Assert.Contains("client_id="+ProductOperationalCredentialTests.Client,form);
            Assert.Contains("client_secret=private-client-secret",form);Assert.Contains("refresh_token=original-refresh%2Bvalue%2Fwith%3Dencoding",form);
            Assert.DoesNotContain("scope=",form);Assert.DoesNotContain("subject=",form);
            Entered.TrySetResult();if(Wait)await Task.Delay(Timeout.InfiniteTimeSpan,cancellation);
            if(Fault=="network")throw new HttpRequestException();if(Fault=="timeout")throw new OperationCanceledException();
            var packet=new Dictionary<string,object?>{["access_token"]="new.private.access",["refresh_token"]="new.private.refresh",["token_type"]="Bearer",["expires_in"]=300};
            switch(Fault){
                case "missing":packet.Remove("refresh_token");break;
                case "extra":packet["id_token"]="unrequested";break;
                case "type":packet["token_type"]="bearer";break;
                case "scope":packet["scope"]="product:identity product:access";break;
                case "scope-valid":packet["scope"]="product:identity product:erp-session";break;
                case "zero":packet["expires_in"]=0;break;
                case "long":packet["expires_in"]=301;break;
                case "wrong-type":packet["expires_in"]="300";break;
                case "reused":packet["refresh_token"]="original-refresh+value/with=encoding";break;
                case "control":packet["refresh_token"]="invalid\nrefresh";break;
                case "access":packet["access_token"]="invalid access";break;
                case "oversized":packet["refresh_token"]=new string('x',70000);break;
            }
            var text=JsonSerializer.Serialize(packet);
            if(Fault=="duplicate")text=text.Replace("\"expires_in\":300","\"expires_in\":1,\"expires_in\":300");
            if(Fault=="json")text="{";
            if(Fault=="transit")Clock.Advance(TimeSpan.FromSeconds(300));
            if(Fault=="rollback")Clock.Now-=TimeSpan.FromSeconds(1);
            return new HttpResponseMessage((HttpStatusCode)(Fault=="redirect"?302:Status)){Content=new StringContent(text,Encoding.UTF8,Fault=="html"?"text/html":"application/json")};
        }
        protected override void Dispose(bool disposing){if(disposing)http.Dispose();base.Dispose(disposing);}
    }
    [Theory]
    [InlineData(null)][InlineData("scope-valid")]
    public async Task Exact_confidential_refresh_returns_rotated_credentials_but_no_identity_grant(string? fault)
    {
        using var f=new Fixture{Fault=fault};var result=await f.Refresh();Assert.Equal(OperationalRefreshStatus.Refreshed,result.Status);
        Assert.NotNull(result.Tokens);Assert.True(result.Tokens.ExpiresAt<=f.Clock.Now.AddMinutes(5));
        Assert.DoesNotContain("new.private",result.ToString());Assert.DoesNotContain("new.private",result.Tokens.ToString());
    }
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("type")][InlineData("scope")][InlineData("zero")][InlineData("long")]
    [InlineData("wrong-type")][InlineData("reused")][InlineData("control")][InlineData("access")][InlineData("oversized")]
    [InlineData("duplicate")][InlineData("json")][InlineData("transit")][InlineData("rollback")][InlineData("redirect")]
    [InlineData("html")][InlineData("network")][InlineData("timeout")]
    public async Task Ambiguous_foreign_or_malformed_rotation_never_returns_tokens(string fault)
    {using var f=new Fixture{Fault=fault};var result=await f.Refresh();Assert.Equal(OperationalRefreshStatus.Unavailable,result.Status);Assert.Null(result.Tokens);}
    [Theory]
    [InlineData(400)][InlineData(401)][InlineData(403)]
    public async Task Definitive_provider_rejection_is_not_transient_success(int status)
    {using var f=new Fixture{Status=status};var result=await f.Refresh();Assert.Equal(OperationalRefreshStatus.Denied,result.Status);Assert.Null(result.Tokens);}
    [Fact]
    public async Task Caller_cancellation_propagates_without_reusing_credentials()
    {
        using var f=new Fixture{Wait=true};using var stop=new CancellationTokenSource();var pending=f.Refresh(stop.Token);
        await f.Entered.Task;await stop.CancelAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
        Assert.Equal(1,f.Calls);
    }
}
