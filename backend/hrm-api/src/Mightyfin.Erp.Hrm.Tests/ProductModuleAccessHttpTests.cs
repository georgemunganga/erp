using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Team=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProductOperationalFlowTests
{
    private static string ModuleQuery(Team.Fixture db)=>"?company_id="+db.Company.ToString("D")+"&link_id="+db.Owner.Link.ToString("D");
    private static string ModuleBody(Team.Fixture db,Guid key,bool revoke=false,int expected=0)=>JsonSerializer.Serialize(new{
        company_id=db.Company,link_id=db.Owner.Link,membership_id=db.Owner.Member,membership_revision=1,module="hrm",expected_revision=expected,command_key=key,
        operation=revoke?"revoke":"grant",roles=revoke?Array.Empty<string>():["hr_ops"],actions=revoke?Array.Empty<string>():["hrm-access"],
        scope=revoke?"none":"company",location_ids=Array.Empty<string>(),all_company_locations_confirmed=!revoke,confirmed=true});
    [Fact]
    public async Task Module_management_uses_current_operational_sign_in_strict_review_and_replay_without_default_ERP_admission()
    {
        await using var db=await Team.Fixture.Start();await using var f=await Fixture.Start(companies:true,moduleApi:CoreModuleAccessApiTests.Api(db));
        var url=ProductOperationalFlow.Prefix+"/module-access";var query=ModuleQuery(db);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get(url+"/members"+query)).StatusCode);
        var signed=await f.Callback(await f.Challenge());var cookie=SetCookie(signed,ProductOperationalFlow.Cookie)+"; "+SetCookie(signed,ProductOperationalBrowser.Cookie);
        var session=await (await f.Get(ProductOperationalFlow.Prefix+"/session",cookie)).Content.ReadFromJsonAsync<JsonElement>();var csrf=session.GetProperty("csrf_token").GetString()!;
        var members=await f.Get(url+"/members"+query,cookie);Assert.Equal(HttpStatusCode.OK,members.StatusCode);
        var list=await members.Content.ReadFromJsonAsync<JsonElement>();Assert.Single(list.GetProperty("members").EnumerateArray());Assert.False(list.GetProperty("operational_access_granted").GetBoolean());
        var reviewUrl=url+"/review"+query+"&membership_id="+db.Owner.Member.ToString("D")+"&module=hrm";
        var initial=await f.Get(reviewUrl,cookie);Assert.Equal(HttpStatusCode.OK,initial.StatusCode);Assert.True(initial.Headers.CacheControl!.NoStore);
        Assert.Equal(JsonValueKind.Null,(await initial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("grant").ValueKind);
        var key=Guid.NewGuid();var body=ModuleBody(db,key);
        var save=await f.Post("/module-access/commands",cookie,csrf,body:body);Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        Assert.Equal("Changed",(await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal("Existing",(await (await f.Post("/module-access/commands",cookie,csrf,body:body)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var changed=await (await f.Get(reviewUrl,cookie)).Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(1,changed.GetProperty("grant").GetProperty("revision").GetInt32());
        Assert.False(changed.GetProperty("operational_access_granted").GetBoolean());
        Assert.Equal(HttpStatusCode.OK,(await f.Post("/module-access/commands",cookie,csrf,body:ModuleBody(db,Guid.NewGuid(),true,1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Post("/module-access/commands",cookie,csrf,body:body)).StatusCode);
        var revoked=await (await f.Get(reviewUrl,cookie)).Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("revoked",revoked.GetProperty("grant").GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get("/erp-protected",cookie)).StatusCode);
        await using var check=db.Context();Assert.Empty(await check.LocalSessions.ToArrayAsync());Assert.Empty(await check.TenantRoleAssignments.ToArrayAsync());Assert.Equal(2,await check.ModuleAccessVersions.CountAsync());
    }
    [Theory]
    [InlineData("csrf")][InlineData("origin")][InlineData("content-type")][InlineData("unknown-field")][InlineData("duplicate")][InlineData("unknown-action")]
    [InlineData("query")][InlineData("too-large")][InlineData("missing-field")][InlineData("implicit-company")]
    public async Task Module_command_rejects_unreviewed_input_before_provider_or_database_side_effects(string fault)
    {
        await using var db=await Team.Fixture.Start();await using var f=await Fixture.Start(companies:true,moduleApi:CoreModuleAccessApiTests.Api(db));
        var signed=await f.Callback(await f.Challenge());var cookie=SetCookie(signed,ProductOperationalFlow.Cookie)+"; "+SetCookie(signed,ProductOperationalBrowser.Cookie);
        var session=await (await f.Get(ProductOperationalFlow.Prefix+"/session",cookie)).Content.ReadFromJsonAsync<JsonElement>();var csrf=session.GetProperty("csrf_token").GetString()!;
        var body=ModuleBody(db,Guid.NewGuid());body=fault switch{
            "unknown-field"=>body[..^1]+",\"tenant_id\":\"foreign\"}","duplicate"=>body[..^1]+",\"confirmed\":true}",
            "unknown-action"=>body.Replace("hrm-access","unknown-action"),"too-large"=>new string(' ',32769),"missing-field"=>"{}",
            "implicit-company"=>body.Replace("\"all_company_locations_confirmed\":true","\"all_company_locations_confirmed\":false"),_=>body};
        var reads=f.Channel.Reads;
        var response=await f.Post("/module-access/commands"+(fault=="query"?"?tenant_id=foreign":""),cookie,fault=="csrf"?"forged":csrf,
            origin:fault=="origin"?"https://foreign.invalid":Fixture.Origin,json:fault!="content-type",body:body);
        Assert.Equal(fault is "csrf" or "origin" or "content-type"?HttpStatusCode.Forbidden:fault=="too-large"?HttpStatusCode.RequestEntityTooLarge:HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Equal(reads,f.Channel.Reads);await using var check=db.Context();Assert.Empty(await check.ModuleAccessVersions.ToArrayAsync());Assert.Empty(await check.ModuleAccessReceipts.ToArrayAsync());
    }
    [Fact]
    public async Task Module_endpoints_default_off_and_selectors_fail_before_identity_observation()
    {
        await using(var f=await Fixture.Start(companies:true)) {
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Get(ProductOperationalFlow.Prefix+"/module-access/members")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Post("/module-access/commands","","x",body:"{\"confirmed\":true}")).StatusCode);Assert.Equal(0,f.Channel.Reads);
        }
        await using var db=await Team.Fixture.Start();await using var enabled=await Fixture.Start(companies:true,moduleApi:CoreModuleAccessApiTests.Api(db));
        foreach(var suffix in new[]{"/members?company_id=bad","/members"+ModuleQuery(db)+"&after=bad","/locations"+ModuleQuery(db)+"&tenant_id=foreign",
            "/review"+ModuleQuery(db)+"&membership_id="+db.Owner.Member+"&module=hrm&module=procurement"})
            Assert.Equal(HttpStatusCode.BadRequest,(await enabled.Get(ProductOperationalFlow.Prefix+"/module-access"+suffix)).StatusCode);
        Assert.Equal(0,enabled.Channel.Reads);
    }
    [Fact]
    public void Module_configuration_cannot_enable_without_operational_identity_company_discovery_and_licensing()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ERP:ModuleAccess:Enabled"]="true"}).Build();
        Assert.False(ProductModuleAccessFlow.Enabled(config));Assert.Throws<InvalidOperationException>(()=>ProductModuleAccessFlow.Register(new ServiceCollection(),config,"Host=synthetic;Database=synthetic"));
        ProductModuleAccessFlow.Register(new ServiceCollection(),new ConfigurationBuilder().Build(),null);
    }
}
