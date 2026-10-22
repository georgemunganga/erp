using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Vendor_category_reference_prevents_unused_draft_retirement()
    {
        await using var f = await Fixture.Start(); const string root = "/api/procurement/v1/configuration/categories";
        var created = await SendJson(f.Client, HttpMethod.Post, root, f.Owner,
            new ProcurementConfigurationRoutes.CategoryInput("VENDOR-USED", "Vendor category", null, null), "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var category = await created.Content.ReadFromJsonAsync<JsonElement>();
        var vendor = await SendJson(f.Client, HttpMethod.Post, "/api/procurement/v1/vendors", f.Owner,
            new { legalName = "Category user", displayName = "Category user", countryCode = "ZM", categoryCode = "VENDOR-USED" }, "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, vendor.StatusCode);
        var denial = await SendJson(f.Client, HttpMethod.Post, root + "/" + category.GetProperty("id").GetGuid() + "/retire", f.Owner,
            new { version = category.GetProperty("version").GetInt64(), reason = "Category is already used" }, "procurement_manager");
        Assert.Equal(HttpStatusCode.Conflict, denial.StatusCode);
        Assert.Equal("category-in-use", (await denial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("categories")]
    [InlineData("methods")]
    public async Task Reference_draft_retirement_is_versioned_scoped_and_retains_readable_history(string kind)
    {
        await using var f = await Fixture.Start();
        var root = "/api/procurement/v1/configuration/" + kind;
        object draft = kind == "categories"
            ? new ProcurementConfigurationRoutes.CategoryInput("REMOVE", "Unused draft", null, null)
            : new ProcurementConfigurationRoutes.MethodInput("REMOVE", "Unused draft", null, null);
        var created = await SendJson(f.Client, HttpMethod.Post, root, f.Owner, draft, "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = saved.GetProperty("id").GetGuid(); var version = saved.GetProperty("version").GetInt64();
        var action = root + "/" + id + "/retire";
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(f.Client, HttpMethod.Post, action, f.Owner,
            new { version, reason = "No longer needed" }, "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(f.Client, HttpMethod.Post, action, f.Owner,
            new { version = version + 1, reason = "No longer needed" }, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendJson(f.Client, HttpMethod.Post, action, f.Owner,
            new { version, reason = " " }, "procurement_manager")).StatusCode);
        var removed = await SendJson(f.Client, HttpMethod.Post, action, f.Owner,
            new { version, reason = "No longer needed" }, "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var record = await removed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Retired", record.GetProperty("status").GetString());
        Assert.True(record.GetProperty("version").GetInt64() > version);
        var read = await Send(f.Client, HttpMethod.Get, root + "/" + id, f.Owner, role: "procurement_manager");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("Retired", (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(f.Client, HttpMethod.Get, root + "/" + id,
            f.Owner, f.OtherCompany, "procurement_manager")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(f.Client, HttpMethod.Post, action, f.Owner,
            new { version = record.GetProperty("version").GetInt64(), reason = "Remove again" }, "procurement_manager")).StatusCode);
    }

    [Fact]
    public async Task Parent_category_cannot_be_retired_under_an_existing_child()
    {
        await using var f = await Fixture.Start(); const string root = "/api/procurement/v1/configuration/categories";
        var response = await SendJson(f.Client, HttpMethod.Post, root, f.Owner,
            new ProcurementConfigurationRoutes.CategoryInput("PARENT", "Parent", null, null), "procurement_manager");
        var parent = await response.Content.ReadFromJsonAsync<JsonElement>();
        var child = await SendJson(f.Client, HttpMethod.Post, root, f.Owner,
            new ProcurementConfigurationRoutes.CategoryInput("CHILD", "Child", "PARENT", null), "procurement_manager");
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);
        var denial = await SendJson(f.Client, HttpMethod.Post, root + "/" + parent.GetProperty("id").GetGuid() + "/retire", f.Owner,
            new { version = parent.GetProperty("version").GetInt64(), reason = "Cannot orphan child" }, "procurement_manager");
        Assert.Equal(HttpStatusCode.Conflict, denial.StatusCode);
        Assert.Equal("category-in-use", (await denial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var read = await Send(f.Client, HttpMethod.Get, root + "/" + parent.GetProperty("id").GetGuid(), f.Owner, role: "procurement_manager");
        Assert.Equal(parent.GetProperty("version").GetInt64(), (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt64());
    }
}
