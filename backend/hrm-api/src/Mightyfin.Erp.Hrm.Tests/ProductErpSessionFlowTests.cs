using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Sessions=Mightyfin.Erp.Hrm.Tests.CoreProductSessionRegistryTests;
using Operational=Mightyfin.Erp.Hrm.Tests.ProductOperationalFlowTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductErpSessionFlowTests
{
    private sealed class Fixture:IAsyncDisposable
    {
        internal required Sessions.Fixture Core;internal required Operational.Fixture Http;
        internal string Cookies="",Csrf="";internal CommercialLicenseStatus LicenseStatus=CommercialLicenseStatus.Authorized;internal int LicenseReads;
        internal Dictionary<string,object> Command(Sessions.Target? target=null,Guid? key=null)
        {
            var t=target??Core.Target;
            return new(){["company_id"]=t.Company.ToString("D"),["link_id"]=t.Link.ToString("D"),["module"]="hrm",["grant_version_id"]=t.Version.ToString("D"),
                ["grant_revision"]=1,["scope"]="locations",["location_ids"]=new[]{t.Location.ToString("D")},["all_company_locations_confirmed"]=false,["confirmed"]=true,["command_key"]=(key??Guid.NewGuid()).ToString("D")};
        }
        internal Task<HttpResponseMessage> Select(Dictionary<string,object>? command=null,string? cookie=null,string? csrf=null,string origin=Operational.Fixture.Origin)
            =>Http.Post(ProductErpSessionFlow.Path+"/select",cookie??Cookies,csrf??Csrf,origin,body:JsonSerializer.Serialize(command??Command()));
        internal Task<HttpResponseMessage> Read(string cookie)=>Http.Get(ProductOperationalFlow.Prefix+ProductErpSessionFlow.Path+"/session",cookie);
        internal string With(HttpResponseMessage result)=>Cookies+"; "+Cookie(result,ProductErpSessionFlow.Cookie);
        internal static async Task<Fixture> Start()
        {
            var core=await Sessions.Fixture.Start();Fixture? f=null;
            Task<CommercialLicenseResult> License(string enrollment,CancellationToken ct){
                ct.ThrowIfCancellationRequested();Assert.Equal("01arz3ndektsv4rrffq69g5fav",enrollment);f!.LicenseReads++;
                return Task.FromResult(new CommercialLicenseResult(f.LicenseStatus,f.LicenseStatus==CommercialLicenseStatus.Authorized
                    ?new(enrollment,["hrm"],2,TimeProvider.System.GetUtcNow().AddMinutes(1),TimeProvider.System,TimeProvider.System.GetTimestamp(),TimeSpan.FromMinutes(1)):null));
            }
            var admission=new CoreProductSessionAdmission(t=>core.Core.Context(t),core.Protection,License,TimeProvider.System);
            var editor=new CoreModuleAccessApi(t=>core.Core.Context(t),License,TimeProvider.System);
            var http=await Operational.Fixture.Start(companies:true,moduleApi:editor,sessions:admission);f=new(){Core=core,Http=http};
            var callback=await http.Callback(await http.Challenge());Assert.Equal(HttpStatusCode.Redirect,callback.StatusCode);
            f.Cookies=Cookie(callback,ProductOperationalFlow.Cookie)+"; "+Cookie(callback,ProductOperationalBrowser.Cookie);
            var session=await http.Get(ProductOperationalFlow.Prefix+"/session",f.Cookies);Assert.Equal(HttpStatusCode.OK,session.StatusCode);
            f.Csrf=(await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrf_token").GetString()!;return f;
        }
        public async ValueTask DisposeAsync(){await Http.DisposeAsync();await Core.DisposeAsync();}
    }
    private static string Cookie(HttpResponseMessage response,string name)=>response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith(name+"=",StringComparison.Ordinal)).Split(';')[0];
    private static void NoSessionCookie(HttpResponseMessage response)=>Assert.False(response.Headers.TryGetValues("Set-Cookie",out var headers) && headers.Any(x=>x.StartsWith(ProductErpSessionFlow.Cookie+"=",StringComparison.Ordinal)));
    [Fact]
    public async Task Explicit_selection_issues_only_bound_secure_cookie_and_current_state_without_default_ERP_authentication()
    {
        await using var f=await Fixture.Start();var selected=await f.Select();Assert.Equal(HttpStatusCode.OK,selected.StatusCode);
        var header=selected.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith(ProductErpSessionFlow.Cookie+"="));
        foreach(var flag in new[]{"secure","httponly","samesite=lax","path=/"})Assert.Contains(flag,header.ToLowerInvariant());Assert.DoesNotContain("domain=",header.ToLowerInvariant());
        var token=Cookie(selected,ProductErpSessionFlow.Cookie).Split('=')[1];Assert.True(ProductInvitationIntentStore.Opaque(token));
        var text=await selected.Content.ReadAsStringAsync();Assert.DoesNotContain(token,text);Assert.Contains("no-store",selected.Headers.CacheControl!.ToString());
        var state=await f.Read(f.With(selected));Assert.Equal(HttpStatusCode.OK,state.StatusCode);var data=await state.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(data.GetProperty("session_established").GetBoolean());Assert.False(data.GetProperty("operational_access_granted").GetBoolean());
        Assert.Equal(f.Core.Target.Company,data.GetProperty("company_id").GetGuid());Assert.Equal(f.Core.Target.Location,data.GetProperty("location_ids")[0].GetGuid());
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Http.Get("/erp-protected",f.With(selected))).StatusCode);
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);var row=Assert.Single(await db.ProductSessions.ToArrayAsync());Assert.DoesNotContain(token,row.ProtectedToken);
        Assert.Empty(await db.LocalSessions.ToArrayAsync());Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
    }
    [Fact]
    public async Task Retry_after_unobserved_committed_response_recovers_same_cookie_without_duplicate_rows()
    {
        await using var f=await Fixture.Start();var command=f.Command();var lost=await f.Select(command);Assert.Equal(HttpStatusCode.OK,lost.StatusCode);
        var retry=await f.Select(command);Assert.Equal(HttpStatusCode.OK,retry.StatusCode);Assert.Equal(Cookie(lost,ProductErpSessionFlow.Cookie),Cookie(retry,ProductErpSessionFlow.Cookie));
        command["scope"]="company";command["location_ids"]=Array.Empty<string>();command["all_company_locations_confirmed"]=true;
        var conflict=await f.Select(command);Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);NoSessionCookie(conflict);
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Single(await db.ProductSessions.ToArrayAsync());Assert.Single(await db.AuditEntries.Where(x=>x.EntityType=="CoreProductSession").ToArrayAsync());
    }
    [Fact]
    public async Task ERP_logout_is_provider_independent_durable_and_idempotent_without_restoring_old_retry()
    {
        await using var f=await Fixture.Start();var command=f.Command();var first=await f.Select(command);var cookies=f.With(first);var reads=f.Http.Channel.Reads;f.Http.Channel.Mode="outage";
        for(var i=0;i<2;i++)Assert.Equal(HttpStatusCode.NoContent,(await f.Http.Post(ProductErpSessionFlow.Path+"/logout",cookies,f.Csrf)).StatusCode);
        Assert.Equal(reads,f.Http.Channel.Reads);f.Http.Channel.Mode="";
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Read(cookies)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await f.Select(command)).StatusCode);
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Single(await db.ProductSessionRevocations.ToArrayAsync());
    }
    [Fact]
    public async Task Parent_logout_revokes_selected_session_before_restored_cookies_can_be_used()
    {
        await using var f=await Fixture.Start();var first=await f.Select();var cookies=f.With(first);f.Http.Channel.Mode="outage";var reads=f.Http.Channel.Reads;
        var logout=await f.Http.Post("/logout",cookies,f.Csrf);Assert.Equal(HttpStatusCode.NoContent,logout.StatusCode);Assert.Equal(reads,f.Http.Channel.Reads);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"),x=>x.StartsWith(ProductErpSessionFlow.Cookie+"=;",StringComparison.Ordinal));
        f.Http.Channel.Mode="";Assert.Equal(HttpStatusCode.Unauthorized,(await f.Read(cookies)).StatusCode);
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Single(await db.ProductSessionRevocations.ToArrayAsync());
    }
    [Fact]
    public async Task Person_and_license_outage_or_denial_never_return_cached_session_authority()
    {
        await using var f=await Fixture.Start();var first=await f.Select();var cookies=f.With(first);
        f.Http.Channel.Mode="outage";Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Read(cookies)).StatusCode);
        f.Http.Channel.Mode="";Assert.Equal(HttpStatusCode.OK,(await f.Read(cookies)).StatusCode);
        f.LicenseStatus=CommercialLicenseStatus.Unavailable;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Read(cookies)).StatusCode);
        f.LicenseStatus=CommercialLicenseStatus.Denied;Assert.Equal(HttpStatusCode.Forbidden,(await f.Read(cookies)).StatusCode);
        f.LicenseStatus=CommercialLicenseStatus.Authorized;Assert.Equal(HttpStatusCode.OK,(await f.Read(cookies)).StatusCode);
        f.Http.Channel.Mode="denied";Assert.Equal(HttpStatusCode.Unauthorized,(await f.Read(cookies)).StatusCode);
        f.Http.Channel.Mode="";Assert.Equal(HttpStatusCode.Unauthorized,(await f.Read(cookies)).StatusCode);
    }
    [Theory]
    [InlineData("tenant")][InlineData("credential")][InlineData("empty")][InlineData("duplicates")][InlineData("module")][InlineData("confirmation")][InlineData("scope")]
    public async Task Unreviewed_selection_is_rejected_before_person_license_or_session_side_effects(string fault)
    {
        await using var f=await Fixture.Start();var command=f.Command();
        if(fault=="tenant")command["tenant_id"]="foreign";
        if(fault=="credential")command["credential_handle"]=ProductInvitationIntentStore.Random();
        if(fault=="empty")command["location_ids"]=Array.Empty<string>();
        if(fault=="duplicates")command["location_ids"]=new[]{f.Core.Target.Location.ToString("D"),f.Core.Target.Location.ToString("D")};
        if(fault=="module")command["module"]="billing";
        if(fault=="confirmation")command["confirmed"]=false;
        if(fault=="scope")command["all_company_locations_confirmed"]=true;
        var reads=f.Http.Channel.Reads;var selected=await f.Select(command);Assert.Equal(HttpStatusCode.BadRequest,selected.StatusCode);NoSessionCookie(selected);Assert.Equal(reads,f.Http.Channel.Reads);Assert.Equal(0,f.LicenseReads);
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Empty(await db.ProductSessions.ToArrayAsync());
    }
    [Theory]
    [InlineData("csrf")][InlineData("origin")][InlineData("identity")][InlineData("browser")]
    public async Task Forged_request_cannot_issue_a_session(string fault)
    {
        await using var f=await Fixture.Start();var cookies=f.Cookies;
        if(fault=="identity")cookies=cookies.Split(';')[1];if(fault=="browser")cookies=cookies.Split(';')[0];
        var response=await f.Select(cookie:cookies,csrf:fault=="csrf"?"forged":null,origin:fault=="origin"?"https://foreign.invalid":Operational.Fixture.Origin);
        Assert.Contains(response.StatusCode,new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden});NoSessionCookie(response);Assert.Equal(0,f.LicenseReads);
    }
    [Fact]
    public async Task Duplicate_ERP_cookie_is_rejected_before_provider_and_logout_requires_CSRF()
    {
        await using var f=await Fixture.Start();var selected=await f.Select();var cookies=f.With(selected);var reads=f.Http.Channel.Reads;
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Read(cookies+"; "+Cookie(selected,ProductErpSessionFlow.Cookie))).StatusCode);Assert.Equal(reads,f.Http.Channel.Reads);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Http.Post(ProductErpSessionFlow.Path+"/logout",cookies,"forged")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Read(cookies)).StatusCode);
    }
    [Fact]
    public async Task Explicit_company_switch_replaces_old_selection_and_old_retry_cannot_restore_it()
    {
        await using var f=await Fixture.Start();var firstCommand=f.Command();var first=await f.Select(firstCommand);var created=await f.Core.Core.Create("http_session_switch_second_company_request_00001");
        var secondTarget=await f.Core.Grant(created.CompanyId!.Value);var second=await f.Select(f.Command(secondTarget));Assert.Equal(HttpStatusCode.OK,second.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Read(f.With(first))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await f.Select(firstCommand)).StatusCode);
        var state=await f.Read(f.With(second));Assert.Equal(HttpStatusCode.OK,state.StatusCode);Assert.Equal(secondTarget.Company,(await state.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("company_id").GetGuid());
    }
    [Fact]
    public async Task Self_review_reports_current_grant_without_issuing_or_confirming_a_session()
    {
        await using var f=await Fixture.Start();var t=f.Core.Target;
        var review=await f.Http.Get(ProductOperationalFlow.Prefix+ProductErpSessionFlow.Path+$"/review?company_id={t.Company:D}&link_id={t.Link:D}&module=hrm",f.Cookies);
        Assert.Equal(HttpStatusCode.OK,review.StatusCode);NoSessionCookie(review);var data=await review.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(t.Version,data.GetProperty("grant_version_id").GetGuid());Assert.Equal(1,data.GetProperty("grant_revision").GetInt32());
        Assert.False(data.GetProperty("session_established").GetBoolean());Assert.False(data.GetProperty("operational_access_granted").GetBoolean());
        await using var db=f.Core.Core.Context(t.Tenant);Assert.Empty(await db.ProductSessions.ToArrayAsync());
        f.LicenseStatus=CommercialLicenseStatus.Denied;
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Http.Get(ProductOperationalFlow.Prefix+ProductErpSessionFlow.Path+$"/review?company_id={t.Company:D}&link_id={t.Link:D}&module=hrm",f.Cookies)).StatusCode);
    }
    [Fact]
    public async Task Ordinary_member_can_review_and_select_own_explicit_grant_without_owner_editor_authority()
    {
        await using var f=await Fixture.Start();var team=f.Core.Core;var owner=await team.Add("43",Mightyfin.Erp.Hrm.Domain.Entities.CoreCompanyRoles.Owner);
        var changed=await team.Run(team.Owner,team.Command(team.Owner,CompanyTeamOperation.ChangeRole,Mightyfin.Erp.Hrm.Domain.Entities.CoreCompanyRoles.Member));
        Assert.Equal(CompanyTeamStatus.Changed,changed.Status);
        var person=new OperationalSubjectObservation("43",Operational.Fixture.ClientId,team.Clock.GetUtcNow().AddSeconds(30),team.Clock,team.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
        var license=new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav",["hrm"],2,team.Clock.GetUtcNow().AddMinutes(1),team.Clock,team.Clock.GetTimestamp(),TimeSpan.FromMinutes(1));
        var t=f.Core.Target;
        var granted=await new CoreModuleAccessCommands(x=>team.Context(x),team.Clock).ExecuteAsync(person,new(ProductCommercialLicense.Issuer,"43",owner.Link),
            new(t.Company,t.Member,2,"hrm",1,Guid.NewGuid(),false,["hr_ops"],["hrm-access"],"company",[],true,true),license);
        Assert.Equal(ModuleAccessStatus.Changed,granted.Status);
        var path=ProductOperationalFlow.Prefix+ProductErpSessionFlow.Path+$"/review?company_id={t.Company:D}&link_id={t.Link:D}&module=hrm";
        var reviewed=await f.Http.Get(path,f.Cookies);Assert.Equal(HttpStatusCode.OK,reviewed.StatusCode);
        var data=await reviewed.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(2,data.GetProperty("grant_revision").GetInt32());
        var command=f.Command();command["grant_version_id"]=data.GetProperty("grant_version_id").GetString()!;command["grant_revision"]=2;
        var selected=await f.Select(command);Assert.Equal(HttpStatusCode.OK,selected.StatusCode);Assert.Equal(HttpStatusCode.OK,(await f.Read(f.With(selected))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Http.Get(ProductOperationalFlow.Prefix+$"/module-access/review?company_id={t.Company:D}&link_id={t.Link:D}&membership_id={t.Member:D}&module=hrm",f.Cookies)).StatusCode);
    }
    [Theory]
    [InlineData("company")][InlineData("link")]
    public async Task Foreign_review_selector_cannot_discover_license_or_grant(string selector)
    {
        await using var f=await Fixture.Start();var t=f.Core.Target;var company=selector=="company"?Guid.NewGuid():t.Company;var link=selector=="link"?Guid.NewGuid():t.Link;
        var response=await f.Http.Get(ProductOperationalFlow.Prefix+ProductErpSessionFlow.Path+$"/review?company_id={company:D}&link_id={link:D}&module=hrm",f.Cookies);
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);NoSessionCookie(response);Assert.Equal(0,f.LicenseReads);
    }
    [Fact]
    public async Task Lifecycle_endpoints_default_off_without_provider_or_cookie_side_effects()
    {
        await using var f=await Operational.Fixture.Start();var reads=f.Channel.Reads;
        var read=await f.Get(ProductOperationalFlow.Prefix+ProductErpSessionFlow.Path+"/session");Assert.Equal(HttpStatusCode.ServiceUnavailable,read.StatusCode);NoSessionCookie(read);
        var select=await f.Post(ProductErpSessionFlow.Path+"/select","","x");Assert.Equal(HttpStatusCode.ServiceUnavailable,select.StatusCode);NoSessionCookie(select);Assert.Equal(reads,f.Channel.Reads);
    }
}
