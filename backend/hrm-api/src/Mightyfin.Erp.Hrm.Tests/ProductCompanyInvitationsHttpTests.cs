using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Fixture = Mightyfin.Erp.Hrm.Tests.ProductCompanyTeamHttpTests.Fixture;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductCompanyInvitationsHttpTests
{
    private static string Path(Fixture f) => "/invitations?company_id="+f.Core.Company.ToString("D")+"&link_id="+f.ActorLink.ToString("D");
    private static Dictionary<string, object?> Issue(Fixture f) => new() { ["company_id"]=f.Core.Company.ToString("D"),
        ["link_id"]=f.ActorLink.ToString("D"),["command_key"]=Guid.NewGuid().ToString("D"),
        ["email"]="invited@example.test",["role"]="member",["confirmed"]=true };
    private static async Task<JsonElement> Body(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task Confirmed_issue_list_retry_and_revoke_use_current_licensed_company_scope_without_exposing_secrets()
    {
        await using var f = await Fixture.Start(invitations:true); await f.SignIn();
        Assert.True((await Body(await f.Get("/session"))).GetProperty("invitation_management_available").GetBoolean());
        var command = Issue(f); var issued = await f.Post(command,path:"/invitations/issue");
        Assert.Equal(HttpStatusCode.Created,issued.StatusCode);
        var first = await Body(issued); Assert.Equal("Issued",first.GetProperty("status").GetString());
        Assert.DoesNotContain("token",first.GetRawText()); Assert.DoesNotContain("recipient",first.GetRawText());
        Assert.Equal("Existing",(await Body(await f.Post(command,path:"/invitations/issue"))).GetProperty("status").GetString());
        var list = await f.Get(Path(f)); Assert.Equal(HttpStatusCode.OK,list.StatusCode);
        Assert.Contains("no-store",list.Headers.CacheControl!.ToString());
        var page = await Body(list); var row = Assert.Single(page.GetProperty("invitations").EnumerateArray());
        Assert.Equal("invited@example.test",row.GetProperty("recipient").GetString());
        Assert.Equal("pending",row.GetProperty("state").GetString()); Assert.False(page.GetProperty("operational_access_granted").GetBoolean());
        var delivery=row.GetProperty("delivery");Assert.Equal("queued",delivery.GetProperty("state").GetString());
        Assert.Equal(0,delivery.GetProperty("attempts").GetInt32());Assert.Equal(JsonValueKind.Null,delivery.GetProperty("accepted_at").ValueKind);
        Assert.DoesNotContain("protected_delivery",page.GetRawText()); Assert.DoesNotContain("token",page.GetRawText());
        var revoke = new Dictionary<string,object?> { ["company_id"]=command["company_id"],["link_id"]=command["link_id"],
            ["command_key"]=Guid.NewGuid().ToString("D"),["invitation_id"]=row.GetProperty("invitation_id").GetString(),
            ["expected_revision"]=1,["confirmed"]=true };
        Assert.Equal("Revoked",(await Body(await f.Post(revoke,path:"/invitations/revoke"))).GetProperty("status").GetString());
        Assert.Equal("Existing",(await Body(await f.Post(revoke,path:"/invitations/revoke"))).GetProperty("status").GetString());
        Assert.Equal("revoked",(await Body(await f.Get(Path(f)))).GetProperty("invitations")[0].GetProperty("state").GetString());
        f.State.Status = CommercialLicenseStatus.Denied;
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post(command,path:"/invitations/issue")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Get(Path(f))).StatusCode);
        await using var db = f.Core.Context(); Assert.Single(await db.CompanyInvitations.ToArrayAsync());
        Assert.Equal(2,await db.CompanyInvitationReceipts.CountAsync()); Assert.Single(await db.CompanyMemberships.ToArrayAsync());
        Assert.Empty(await db.LocalSessions.ToArrayAsync()); Assert.Empty(await db.LocalCredentialLinks.ToArrayAsync());
        Assert.Empty(await db.TenantRoleAssignments.ToArrayAsync());
    }

    [Fact]
    public async Task Licensed_list_reports_the_bound_mail_outcome_without_leaking_transport_errors_or_granting_access()
    {
        await using var f=await Fixture.Start(invitations:true);await f.SignIn();
        Assert.Equal(HttpStatusCode.Created,(await f.Post(Issue(f),path:"/invitations/issue")).StatusCode);
        await using(var db=f.Core.Context()) {
            var intent=await db.OutboxMessages.SingleAsync(x=>x.EventType==Mightyfin.Erp.Hrm.Infrastructure.CompanyInvitationDeliveryStore.EventType);
            intent.Status="delivery-accepted";intent.PublishAttempts=1;intent.LastTransport="kuubu-smtp";intent.PublishedAt=f.Core.Clock.GetUtcNow();
            await db.SaveChangesAsync();
        }
        var page=await Body(await f.Get(Path(f)));var row=page.GetProperty("invitations")[0];
        Assert.Equal("pending",row.GetProperty("state").GetString());Assert.Equal("accepted",row.GetProperty("delivery").GetProperty("state").GetString());
        Assert.Equal(1,row.GetProperty("delivery").GetProperty("attempts").GetInt32());
        Assert.DoesNotContain("last_error",page.GetRawText());Assert.DoesNotContain("last_transport",page.GetRawText());
        await using(var db=f.Core.Context()) {
            var intent=await db.OutboxMessages.SingleAsync(x=>x.EventType==Mightyfin.Erp.Hrm.Infrastructure.CompanyInvitationDeliveryStore.EventType);
            intent.LastError="unreviewed private diagnostic";await db.SaveChangesAsync();
        }
        Assert.Equal("unavailable",(await Body(await f.Get(Path(f)))).GetProperty("invitations")[0].GetProperty("delivery").GetProperty("state").GetString());
        f.State.Status=CommercialLicenseStatus.Denied;
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Get(Path(f))).StatusCode);
        await using var check=f.Core.Context();Assert.Single(await check.CompanyMemberships.ToArrayAsync());
        Assert.Empty(await check.LocalSessions.ToArrayAsync());Assert.Empty(await check.LocalCredentialLinks.ToArrayAsync());
    }

    [Theory]
    [InlineData("missing-cookie")][InlineData("missing-token")][InlineData("forged-token")]
    [InlineData("foreign-origin")][InlineData("non-json")][InlineData("unconfirmed")]
    [InlineData("extra")][InlineData("duplicate")][InlineData("role")][InlineData("email")]
    [InlineData("uppercase-key")][InlineData("oversized")]
    public async Task Forged_or_unreviewed_requests_never_issue(string fault)
    {
        await using var f = await Fixture.Start(invitations:true); await f.SignIn(); var body = Issue(f);
        if (fault=="unconfirmed") body["confirmed"]=false;
        if (fault=="extra") body["actor_subject"]="42";
        if (fault=="role") body["role"]="owner";
        if (fault=="email") body["email"]="not-an-email";
        if (fault=="uppercase-key") body["command_key"]="ABCDEF12-2345-6789-ABCD-EF1234567890";
        var text=JsonSerializer.Serialize(body);
        if(fault=="duplicate") text=text.Replace("\"role\":\"member\"","\"role\":\"administrator\",\"role\":\"member\"");
        if(fault=="oversized") text=new string('x',8193);
        var response=await f.PostRaw(text,fault,"/invitations/issue");
        Assert.Contains(response.StatusCode,new[]{HttpStatusCode.BadRequest,HttpStatusCode.Forbidden,HttpStatusCode.Unauthorized,HttpStatusCode.RequestEntityTooLarge});
        await using var db=f.Core.Context(); Assert.Empty(await db.CompanyInvitations.ToArrayAsync());
        Assert.Empty(await db.CompanyInvitationReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task Availability_defaults_off_and_ordinary_identity_cannot_replace_team_purpose()
    {
        await using var off=await Fixture.Start(); await off.SignIn();
        Assert.False((await Body(await off.Get("/session"))).GetProperty("invitation_management_available").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await off.Get(Path(off))).StatusCode);
        await using var f=await Fixture.Start(invitations:true); await f.SignIn(ordinary:true);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Get(Path(f))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Post(Issue(f),path:"/invitations/issue")).StatusCode);
    }

    [Fact]
    public async Task Foreign_company_links_member_roles_and_mid_license_revocation_cannot_disclose_or_mutate_invitations()
    {
        await using var f=await Fixture.Start(invitations:true); await f.SignIn();
        var body=Issue(f); body["company_id"]=Guid.NewGuid().ToString("D");
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post(body,path:"/invitations/issue")).StatusCode);
        var member=await f.Core.Add("43",CoreCompanyRoles.Member); f.ActorLink=member.Link; await f.SignIn("43");
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Get(Path(f))).StatusCode);
        f.ActorLink=f.Core.Owner.Link; await f.SignIn();
        f.State.BeforeReturn=async()=>{await using var db=f.Core.Context();(await db.CompanyMemberships.SingleAsync(x=>x.Id==f.Core.Owner.Member)).IsActive=false;await db.SaveChangesAsync();};
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post(Issue(f),path:"/invitations/issue")).StatusCode);
        await using var inspect=f.Core.Context(); Assert.Empty(await inspect.CompanyInvitations.ToArrayAsync());
    }

    [Fact]
    public async Task Invitation_pages_are_bounded_and_never_include_delivery_envelopes_or_secrets()
    {
        await using var f=await Fixture.Start(invitations:true); await f.SignIn();
        for(var i=0;i<21;i++) { var command=Issue(f);command["email"]="person"+i+"@example.test";
            Assert.Equal(HttpStatusCode.Created,(await f.Post(command,path:"/invitations/issue")).StatusCode); }
        var first=await Body(await f.Get(Path(f))); Assert.Equal(20,first.GetProperty("invitations").GetArrayLength());
        var second=await Body(await f.Get(Path(f)+"&after="+first.GetProperty("next_cursor").GetString()));
        Assert.Single(second.GetProperty("invitations").EnumerateArray()); Assert.Equal(JsonValueKind.Null,second.GetProperty("next_cursor").ValueKind);
        Assert.Equal(21,first.GetProperty("invitations").EnumerateArray().Concat(second.GetProperty("invitations").EnumerateArray())
            .Select(x=>x.GetProperty("invitation_id").GetGuid()).Distinct().Count());
        Assert.DoesNotContain("protected_delivery",first.GetRawText()); Assert.DoesNotContain("review_url",first.GetRawText());
        Assert.DoesNotContain("token",first.GetRawText()); Assert.DoesNotContain("tenant",first.GetRawText());
    }

    [Fact]
    public async Task Administrator_cannot_issue_or_revoke_privileged_invitations()
    {
        await using var f=await Fixture.Start(invitations:true); await f.SignIn();var issue=Issue(f);issue["role"]="administrator";
        var invited=await Body(await f.Post(issue,path:"/invitations/issue"));
        var admin=await f.Core.Add("44",CoreCompanyRoles.Administrator);f.ActorLink=admin.Link;await f.SignIn("44");
        var denied=Issue(f);denied["email"]="different@example.test";denied["role"]="administrator";
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post(denied,path:"/invitations/issue")).StatusCode);
        var revoke=new Dictionary<string,object?> { ["company_id"]=f.Core.Company.ToString("D"),["link_id"]=admin.Link.ToString("D"),
            ["command_key"]=Guid.NewGuid().ToString("D"),["invitation_id"]=invited.GetProperty("invitation_id").GetString(),["expected_revision"]=1,["confirmed"]=true };
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Post(revoke,path:"/invitations/revoke")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await f.Get(Path(f))).StatusCode);
        await using var db=f.Core.Context();Assert.Single(await db.CompanyInvitations.ToArrayAsync());Assert.Single(await db.CompanyInvitationReceipts.ToArrayAsync());
    }

    [Theory]
    [InlineData("&after=invalid")][InlineData("&customer_id=foreign")][InlineData("&company_id=duplicate")]
    public async Task Selectors_are_canonical_and_cannot_inject_actor_or_billing_scope(string suffix)
    {
        await using var f=await Fixture.Start(invitations:true);await f.SignIn();
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Get(Path(f)+suffix)).StatusCode);
    }
}
