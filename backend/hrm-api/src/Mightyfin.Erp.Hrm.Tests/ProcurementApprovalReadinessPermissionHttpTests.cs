using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    [Theory]
    [InlineData("procurement-approval-open", HttpStatusCode.OK)]
    [InlineData("procurement-request-return", HttpStatusCode.Forbidden)]
    [InlineData("procurement-request-read", HttpStatusCode.Forbidden)]
    public async Task Approval_readiness_requires_start_permission_without_unrelated_return_authority(
        string action, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.Start();
        var created = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/requests",
            fixture.Owner, Draft("Readiness permission regression"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await fixture.ConfigureCoreActions(fixture.Owner, "procurement-access," + action);
        var before = await fixture.OriginalValueFootprint();
        var response = await Send(fixture.Client, HttpMethod.Get,
            $"/api/procurement/v1/requests/{id}/approval-readiness", fixture.Owner);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("canOpenCase").GetBoolean());
            Assert.False(body.GetProperty("isAuthority").GetBoolean());
        }
        Assert.Equal(before, await fixture.OriginalValueFootprint());
    }
}
