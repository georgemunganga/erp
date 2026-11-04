using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    private const string RequestCoreRoutingRules = """
        {"schemaVersion":2,"transactionType":"PurchaseRequest","currencyCode":"ZMW","amountBasis":"EstimatedLineBase","rules":[
          {"code":"DEFAULT","priority":0,"when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,"categoryCodes":[],"purchaseTypes":[],"branchIds":[],"departmentIds":[],"requesterRoleKeys":[]},
           "result":{"budgetReservation":"Required","sourcingMethodCode":null,"minimumQuoteCount":null,"approvalStages":[{"code":"EXECUTIVE","assignee":"ProcurementManager","mode":"One","binding":{"kind":"CoreRole","key":"executive"}}]}}
        ]}
        """;

    [Fact]
    public async Task Core_users_can_author_and_independently_publish_request_routes_without_HR()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        await fixture.ConfigurePrincipalRequester(fixture.Reviewer, false);
        await fixture.UseCompanyWideCoreConfiguration(fixture.Owner);
        await fixture.UseCompanyWideCoreConfiguration(fixture.Reviewer);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-configuration-read,procurement-configuration-edit,procurement-configuration-approve", "policy_owner", actorSpecific: false);
        await fixture.ConfigureCoreActions(fixture.Reviewer, "procurement-access,procurement-configuration-read,procurement-configuration-approve," + SpendingAuthorityConfiguration.ApprovalPermission, "executive", actorSpecific: false);
        const string path = "/api/procurement/v1/configuration/policies";
        using var created = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
            new ProcurementPolicyDraftRoutes.PolicyDraft("PURCHASE_REQUEST", DateTimeOffset.UtcNow.AddDays(1), null, RequestCoreRoutingRules));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        using var sent = await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{id}/submit-review", fixture.Owner,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(draft.GetProperty("version").GetInt64(), "Review the explicit Core executive route"));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var pending = await sent.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingReview", pending.GetProperty("state").GetString());
        var publish = new ProcurementPolicyDraftRoutes.PublicationInput(pending.GetProperty("version").GetInt64(), "Independent review of future request route");
        using var self = await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{id}/publish", fixture.Owner, publish);
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        using var approved = await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{id}/publish", fixture.Reviewer, publish);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var published = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Published", published.GetProperty("state").GetString());
        Assert.Contains("CoreRole", published.GetProperty("rulesJson").GetString());
    }

    [Fact]
    public async Task Branch_limited_editor_cannot_review_company_wide_request_policy()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-configuration-read,procurement-configuration-edit", "executive", actorSpecific: false);
        const string path = "/api/procurement/v1/configuration/policies";
        using var created = await SendJson(fixture.Client, HttpMethod.Post, path, fixture.Owner,
            new ProcurementPolicyDraftRoutes.PolicyDraft("PURCHASE_REQUEST", DateTimeOffset.UtcNow.AddDays(1), null, RequestCoreRoutingRules));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        using var sent = await SendJson(fixture.Client, HttpMethod.Post, $"{path}/{draft.GetProperty("id").GetGuid()}/submit-review", fixture.Owner,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(draft.GetProperty("version").GetInt64(), "Company-wide route cannot be assigned by a branch-limited editor"));
        Assert.Equal(HttpStatusCode.Forbidden, sent.StatusCode);
        Assert.Equal("request-policy-scope", (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private sealed partial class Fixture
    {
        public async Task UseCompanyWideCoreConfiguration(Guid principal)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext>();
            core.UserBranchAssignments.RemoveRange(await core.UserBranchAssignments.Where(x => x.UserId == principal).ToArrayAsync());
            await core.SaveChangesAsync();
        }
    }

    [Theory]
    [InlineData("request-roles")]
    [InlineData("request-approvers")]
    [InlineData("request-departments")]
    public async Task Request_routing_lookups_require_current_configuration_permission_without_an_employee(string kind)
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access,procurement-configuration-read");
        var path = "/api/procurement/v1/configuration/policies/lookups/" + kind;
        using var response = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isAuthority").GetBoolean());
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access");
        using var denied = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Core_role_binding_reads_current_active_roles_without_requiring_an_employee()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, false);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access," + SpendingAuthorityConfiguration.ApprovalPermission,
            "supervisor", actorSpecific: false);
        var current = await fixture.UnitReviewCorePerson(fixture.Owner);
        Assert.NotNull(current); Assert.Null(current.WorkerId);
        var binding = new PurchaseRequestApproverBinding("CoreRole", "supervisor");
        Assert.True(binding.Matches(current.PrincipalId, current.Roles.ToArray()));
        Assert.Contains(SpendingAuthorityConfiguration.ApprovalPermission, current.Permissions);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access", "supervisor", actorSpecific: false);
        current = await fixture.UnitReviewCorePerson(fixture.Owner);
        Assert.NotNull(current);
        Assert.DoesNotContain(SpendingAuthorityConfiguration.ApprovalPermission, current.Permissions);
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access", "ordinary", actorSpecific: false);
        current = await fixture.UnitReviewCorePerson(fixture.Owner);
        Assert.NotNull(current);
        Assert.False(binding.Matches(current.PrincipalId, current.Roles.ToArray()));
    }
}
