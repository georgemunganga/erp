using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Flow=Mightyfin.Erp.Hrm.Tests.ProductErpSessionFlowTests;

namespace Mightyfin.Erp.Hrm.Tests;
public sealed class ProductRequestAuthenticationTests
{
    private const string ContextPath="/api/core/v1/workspace-context";
    private static Task<HttpResponseMessage> Read(Flow.Fixture f,string cookies)=>f.Http.Get(ContextPath,cookies);
    private static async Task<Guid> ExtraBranch(Flow.Fixture f)
    {
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);var branch=new WorkLocation{LegalEntityId=f.Core.Target.Company,Code="EXTRA-SITE",Name="Other physical site"};
        db.WorkLocations.Add(branch);await db.SaveChangesAsync();return branch.Id;
    }
    [Fact]
    public async Task Actual_protected_Core_context_uses_selected_principal_company_and_finite_branches_without_legacy_assignments()
    {
        await using var f=await Flow.Fixture.Start(product:true);var extra=await ExtraBranch(f);
        var foreign=await f.Core.Core.Create("product_context_other_company_request_0000001");
        var selected=await f.Select();Assert.Equal(HttpStatusCode.OK,selected.StatusCode);var response=await Read(f,f.With(selected));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("product",data.GetProperty("contextKind").GetString());
        Assert.Equal(f.Core.Target.Tenant,data.GetProperty("tenantId").GetString());Assert.Equal(f.Core.Target.User,data.GetProperty("principalId").GetGuid());
        Assert.False(data.GetProperty("organisationWide").GetBoolean());Assert.Equal(f.Core.Target.Company,Assert.Single(data.GetProperty("companies").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(f.Core.Target.Location,Assert.Single(data.GetProperty("branches").EnumerateArray()).GetProperty("id").GetGuid());Assert.Empty(data.GetProperty("coreActions").EnumerateArray());
        Assert.DoesNotContain(extra.ToString("D"),data.GetRawText());Assert.DoesNotContain(foreign.CompanyId!.Value.ToString("D"),data.GetRawText());
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Empty(await db.UserBranchAssignments.ToArrayAsync());Assert.Empty(await db.LocalSessions.ToArrayAsync());
    }
    [Fact]
    public async Task Explicit_company_wide_selection_includes_owned_branches_but_never_another_company()
    {
        await using var f=await Flow.Fixture.Start(product:true);var extra=await ExtraBranch(f);var command=f.Command();
        command["scope"]="company";command["location_ids"]=Array.Empty<string>();command["all_company_locations_confirmed"]=true;
        var selected=await f.Select(command);Assert.Equal(HttpStatusCode.OK,selected.StatusCode);var response=await Read(f,f.With(selected));Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var data=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.True(data.GetProperty("organisationWide").GetBoolean());Assert.Single(data.GetProperty("companies").EnumerateArray());
        Assert.Contains(data.GetProperty("branches").EnumerateArray(),x=>x.GetProperty("id").GetGuid()==extra);
    }
    [Fact]
    public async Task Replaced_cookie_cannot_authenticate_and_new_cookie_resolves_the_new_company_tenant()
    {
        await using var f=await Flow.Fixture.Start(product:true);var first=await f.Select();var created=await f.Core.Core.Create("product_auth_second_company_request_000000001");
        var target=await f.Core.Grant(created.CompanyId!.Value);var second=await f.Select(f.Command(target));Assert.Equal(HttpStatusCode.OK,second.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Read(f,f.With(first))).StatusCode);
        var response=await Read(f,f.With(second));Assert.Equal(HttpStatusCode.OK,response.StatusCode);var data=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(target.Tenant,data.GetProperty("tenantId").GetString());Assert.Equal(target.Company,Assert.Single(data.GetProperty("companies").EnumerateArray()).GetProperty("id").GetGuid());
    }
    [Fact]
    public async Task Request_authentication_never_caches_success_across_person_or_license_failures()
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var cookies=f.With(selected);
        Assert.Equal(HttpStatusCode.OK,(await Read(f,cookies)).StatusCode);
        f.Http.Channel.Mode="outage";Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Read(f,cookies)).StatusCode);
        f.Http.Channel.Mode="";f.LicenseStatus=CommercialLicenseStatus.Unavailable;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Read(f,cookies)).StatusCode);
        f.LicenseStatus=CommercialLicenseStatus.Denied;Assert.Equal(HttpStatusCode.Forbidden,(await Read(f,cookies)).StatusCode);
        f.LicenseStatus=CommercialLicenseStatus.Authorized;Assert.Equal(HttpStatusCode.OK,(await Read(f,cookies)).StatusCode);
        f.Http.Channel.Mode="denied";Assert.Equal(HttpStatusCode.Unauthorized,(await Read(f,cookies)).StatusCode);
        f.Http.Channel.Mode="";Assert.Equal(HttpStatusCode.Unauthorized,(await Read(f,cookies)).StatusCode);
    }
    [Fact]
    public async Task Grant_revocation_removes_actual_protected_context_access()
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var cookies=f.With(selected);Assert.Equal(HttpStatusCode.OK,(await Read(f,cookies)).StatusCode);
        var core=f.Core.Core;var clock=core.Clock;var t=f.Core.Target;
        var person=new OperationalSubjectObservation("42",ProductOperationalFlowTests.Fixture.ClientId,clock.GetUtcNow().AddSeconds(30),clock,clock.GetTimestamp(),TimeSpan.FromSeconds(30));
        var license=new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav",["hrm"],2,clock.GetUtcNow().AddMinutes(1),clock,clock.GetTimestamp(),TimeSpan.FromMinutes(1));
        var revoked=await new CoreModuleAccessCommands(x=>core.Context(x),clock).ExecuteAsync(person,new(ProductCommercialLicense.Issuer,"42",t.Link),
            new(t.Company,t.Member,1,"hrm",1,Guid.NewGuid(),true,[],[],"none",[],false,true),license);
        Assert.Equal(ModuleAccessStatus.Changed,revoked.Status);Assert.Equal(HttpStatusCode.Forbidden,(await Read(f,cookies)).StatusCode);
    }
    [Fact]
    public async Task Archived_selected_branch_denies_instead_of_becoming_unconfined()
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();
        await using(var db=f.Core.Core.Context(f.Core.Target.Tenant)){(await db.WorkLocations.SingleAsync(x=>x.Id==f.Core.Target.Location)).IsArchived=true;await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.Forbidden,(await Read(f,f.With(selected))).StatusCode);
    }
    [Theory]
    [InlineData("X-Tenant")][InlineData("X-Tenant-Id")][InlineData("X-Shell-Entity")][InlineData("X-Shell-Location")][InlineData("X-Branch-Id")][InlineData("X-All-Branches")][InlineData("Authorization")][InlineData("query")]
    public async Task Client_selectors_cannot_replace_selected_authority_or_trigger_provider_calls(string selector)
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var reads=f.Http.Channel.Reads;var licenses=f.LicenseReads;
        using var request=new HttpRequestMessage(HttpMethod.Get,ContextPath+(selector=="query"?"?tenant_id=foreign":""));request.Headers.Add("Cookie",f.With(selected));
        if(selector!="query")request.Headers.Add(selector,"foreign");var response=await f.Http.Client.SendAsync(request);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Equal(reads,f.Http.Channel.Reads);Assert.Equal(licenses,f.LicenseReads);
    }
    [Theory]
    [InlineData("/api/legacy-anonymous")][InlineData("/erp-protected")]
    public async Task Unreviewed_endpoints_are_denied_even_with_valid_product_cookie(string path)
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var reads=f.Http.Channel.Reads;
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Http.Get(path,f.With(selected))).StatusCode);Assert.Equal(reads,f.Http.Channel.Reads);
    }
    [Theory]
    [InlineData("missing-erp")][InlineData("missing-parent")][InlineData("duplicate-erp")][InlineData("legacy-cookie")]
    public async Task Missing_or_ambiguous_credentials_never_fall_back_to_legacy_authentication(string fault)
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var cookies=f.With(selected);
        var erp=selected.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith(ProductErpSessionFlow.Cookie+"=")).Split(';')[0];
        if(fault=="missing-erp")cookies=f.Cookies;if(fault=="missing-parent")cookies=erp;
        if(fault=="duplicate-erp")cookies+="; "+erp;if(fault=="legacy-cookie")cookies="hrm_session="+ProductInvitationIntentStore.Random();
        Assert.Equal(HttpStatusCode.Unauthorized,(await Read(f,cookies)).StatusCode);
    }
    [Fact]
    public async Task ERP_logout_removes_actual_authenticated_context_authority()
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var cookies=f.With(selected);
        Assert.Equal(HttpStatusCode.OK,(await Read(f,cookies)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await f.Http.Post(ProductErpSessionFlow.Path+"/logout",cookies,f.Csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Read(f,cookies)).StatusCode);
    }
    [Fact]
    public void Product_tenant_resolution_has_no_process_default_or_local_identity_fallback()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ERP:AuthMode"]="product",["HRM:DefaultTenantId"]="foreign-default"}).Build();
        var http=new DefaultHttpContext();var accessor=new PrincipalTenantAccessor(new HttpContextAccessor{HttpContext=http},configuration);
        Assert.Equal("untrusted-product-request",accessor.GetTenantId());http.User=new(new ClaimsIdentity([new("tenant","foreign-default")],"local"));Assert.Equal("untrusted-product-request",accessor.GetTenantId());
        http.User=new(new ClaimsIdentity([new("tenant","forged"),new(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString("D"))],ProductRequestAuthentication.Scheme));
        Assert.Null(ProductRequestContext.Current(http));
    }
}
