using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    [Theory]
    [InlineData("branch")][InlineData("department")]
    public async Task Company_wide_reviewer_selected_scope_cannot_review_or_return_another_scope_request(string kind)
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigurePrincipalRequester(fixture.Owner, linkedEmployee: false);
        var (scopeA, scopeB) = await fixture.ConfigureRequestReviewScopes(kind);
        var needed = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, PrincipalRequests, fixture.Owner,
            new { purpose = "Need in second " + kind, neededBy = needed, lines = new[] {
                new { description = "Supplies", unitCode = "each", requestedQuantity = "1", estimatedUnitPrice = "10" } } },
            selectedLocation: scopeB);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid(); var path = $"{PrincipalRequests}/{id}";
        var submittedResponse = await SendJson(fixture.Client, HttpMethod.Post, path + "/submit", fixture.Owner,
            new ProcurementRequestRoutes.VersionCommand(created.GetProperty("version").GetInt64()), selectedLocation: scopeB);
        Assert.Equal(HttpStatusCode.OK, submittedResponse.StatusCode);
        var submitted = await submittedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var version = submitted.GetProperty("version").GetInt64();
        var before = await fixture.OriginalValueFootprint();

        // The reviewer really has company-wide Core grants (no branch assignments),
        // but choosing A must constrain both the review read and the return command.
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.SendScopedRequestReview(HttpMethod.Get, path + "/review", scopeA)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.SendScopedRequestReview(HttpMethod.Post, path + "/return-for-changes", scopeA,
                new { version, reason = "Wrong selected scope" })).StatusCode);
        Assert.Equal(before, await fixture.OriginalValueFootprint());

        Assert.Equal(HttpStatusCode.OK,
            (await fixture.SendScopedRequestReview(HttpMethod.Get, path + "/review", scopeB)).StatusCode);
        var companyRead = await fixture.SendScopedRequestReview(HttpMethod.Get, path + "/review", null);
        Assert.Equal(HttpStatusCode.OK, companyRead.StatusCode);
        Assert.Equal("Submitted", (await companyRead.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var returned = await fixture.SendScopedRequestReview(HttpMethod.Post, path + "/return-for-changes", null,
            new { version, reason = "Company-wide reviewer requested a correction" });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal("ReturnedForChanges", (await returned.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(1, await fixture.RequestAuditCount(id, "ReturnedForChanges"));
        Assert.Equal(fixture.Owner, (await fixture.PrincipalRequestStored(id)).RequesterPrincipalId);
    }

    private sealed partial class Fixture
    {
        public async Task<(Guid First, Guid Second)> ConfigureRequestReviewScopes(string kind)
        {
            using var services = app.Services.CreateScope();
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var reviewer = await core.LocalUsers.SingleAsync(x => x.Id == Other);
            reviewer.RolesCsv = "procurement_manager"; reviewer.WorkerId = null;
            var role = await core.TenantRoleAssignments.SingleOrDefaultAsync(x => x.RoleKey == "procurement_manager");
            if (role is null)
            {
                role = new TenantRoleAssignment { RoleKey = "procurement_manager", RoleName = "Synthetic request reviewer", Active = true };
                core.TenantRoleAssignments.Add(role);
            }
            role.Active = true; role.PermissionsCsv = "procurement-access,procurement-request-return";
            foreach (var assignment in await core.UserBranchAssignments.Where(x => x.UserId == Other && !x.IsArchived).ToArrayAsync())
                assignment.IsArchived = true;
            Guid first; Guid second;
            if (kind == "branch")
            {
                first = await core.WorkLocations.Where(x => x.LegalEntityId == company).Select(x => x.Id).SingleAsync();
                var branch = new WorkLocation { Code = "PR-REVIEW-B", Name = "Second request branch", LegalEntityId = company };
                core.WorkLocations.Add(branch); second = branch.Id;
                core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = Owner, LocationId = branch.Id });
            }
            else
            {
                foreach (var assignment in await core.UserBranchAssignments.Where(x => x.UserId == Owner && !x.IsArchived).ToArrayAsync())
                    assignment.IsArchived = true;
                var a = new OrgUnit { Code = "PR-REVIEW-A", Name = "First request department", LegalEntityId = company,
                    Status = "active", EffectiveFrom = new DateOnly(2026, 1, 1) };
                var b = new OrgUnit { Code = "PR-REVIEW-B", Name = "Second request department", LegalEntityId = company,
                    Status = "active", EffectiveFrom = new DateOnly(2026, 1, 1) };
                core.OrgUnits.AddRange(a, b); first = a.Id; second = b.Id;
            }
            await core.SaveChangesAsync(); return (first, second);
        }
        public Task<HttpResponseMessage> SendScopedRequestReview(HttpMethod method, string path, Guid? selected, object? body = null)
        {
            var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            request.Headers.Add("X-Test-User", Other.ToString("D"));
            request.Headers.Add("X-Test-Role", "procurement_manager");
            request.Headers.Add("X-Shell-Entity", company.ToString("D"));
            if (selected.HasValue) request.Headers.Add("X-Shell-Location", selected.Value.ToString("D"));
            return Client.SendAsync(request);
        }
    }
}
