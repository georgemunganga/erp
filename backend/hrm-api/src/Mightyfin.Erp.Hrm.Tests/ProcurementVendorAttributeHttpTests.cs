using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Actual mapped vendor handlers with existing synthetic auth/InMemory fixture.
/// These tests prove API/SaveChanges behavior, not PostgreSQL or production identity.</summary>
public sealed partial class ProcurementRequestHttpTests
{
    private static Dictionary<string, object?> AttributeDraft(object? attributes = null, bool includeAttributes = true)
    {
        var draft = new Dictionary<string, object?> { ["legalName"] = "Attribute Vendor Ltd",
            ["displayName"] = "Attribute Vendor", ["countryCode"] = "ZM", ["categoryCode"] = "EQUIPMENT",
            ["primaryContact"] = new { name = "Synthetic Contact", email = "attributes@example.test" } };
        if (includeAttributes) draft["attributes"] = attributes;
        return draft;
    }
    private static async Task<JsonElement> AttributeVendor(Fixture fixture)
    {
        var response = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors", fixture.Owner,
            AttributeDraft(new[] { new { kind = "CustomField", key = "Site", value = "Original" },
                new { kind = "ReportingTag", key = "Project", value = "A" } }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await AttributeDetail(fixture, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }
    private static async Task<JsonElement> AttributeDetail(Fixture fixture, Guid id)
    {
        var response = await Send(fixture.Client, HttpMethod.Get, $"/api/procurement/v1/vendors/{id}", fixture.Owner);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static JsonElement Attribute(JsonElement detail, string key)
        => detail.GetProperty("attributes").EnumerateArray().Single(x => x.GetProperty("key").GetString() == key);

    [Fact]
    public async Task Vendor_attributes_update_and_reload_preserve_owned_ids_and_advance_parent_CAS()
    {
        await using var fixture = await Fixture.Start(); var before = await AttributeVendor(fixture);
        var id = before.GetProperty("id").GetGuid(); var site = Attribute(before, "Site");
        var response = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/vendors/{id}", fixture.Owner,
            new { version = before.GetProperty("version").GetInt64(), draft = AttributeDraft(new[] {
                new { kind = "CustomField", key = " Site ", value = " Updated " },
                new { kind = "ReportingTag", key = "Department", value = "Operations" } }) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<JsonElement>(); var reloaded = await AttributeDetail(fixture, id);
        Assert.Equal(before.GetProperty("version").GetInt64() + 1, saved.GetProperty("version").GetInt64());
        Assert.Equal(saved.GetProperty("version").GetInt64(), reloaded.GetProperty("version").GetInt64());
        Assert.Equal(site.GetProperty("id").GetGuid(), Attribute(reloaded, "Site").GetProperty("id").GetGuid());
        Assert.Equal(site.GetProperty("version").GetInt64() + 1, Attribute(reloaded, "Site").GetProperty("version").GetInt64());
        Assert.Equal("Updated", Attribute(reloaded, "Site").GetProperty("value").GetString());
        Assert.Equal("A", Attribute(reloaded, "Project").GetProperty("value").GetString());
        Assert.Equal("Operations", Attribute(reloaded, "Department").GetProperty("value").GetString());
        Assert.Equal(1, await fixture.VendorDetailAuditCount(id, "update"));
        Assert.Equal(1, await fixture.VendorDetailAuditCount(site.GetProperty("id").GetGuid(), "update"));
    }

    [Theory] [InlineData("omitted")] [InlineData("null")] [InlineData("empty")]
    public async Task Vendor_attribute_patch_omission_null_and_empty_do_not_replace_or_delete_retained_rows(string mode)
    {
        await using var fixture = await Fixture.Start(); var before = await AttributeVendor(fixture); var id = before.GetProperty("id").GetGuid();
        var draft = AttributeDraft(mode == "empty" ? Array.Empty<object>() : null, mode != "omitted");
        draft["displayName"] = "Updated header";
        var response = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/vendors/{id}", fixture.Owner,
            new { version = before.GetProperty("version").GetInt64(), draft });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await AttributeDetail(fixture, id);
        Assert.Equal(before.GetProperty("attributes").GetRawText(), after.GetProperty("attributes").GetRawText());
    }

    [Theory] [InlineData(null)] [InlineData("   ")]
    public async Task Vendor_attribute_explicit_clear_retains_row_identity_and_other_values(string? value)
    {
        await using var fixture = await Fixture.Start(); var before = await AttributeVendor(fixture); var id = before.GetProperty("id").GetGuid();
        var response = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/vendors/{id}", fixture.Owner,
            new { version = before.GetProperty("version").GetInt64(), draft = AttributeDraft(new[] { new { kind = "CustomField", key = "Site", value } }) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var after = await AttributeDetail(fixture, id);
        Assert.Equal("", Attribute(after, "Site").GetProperty("value").GetString());
        Assert.Equal(Attribute(before, "Site").GetProperty("id").GetGuid(), Attribute(after, "Site").GetProperty("id").GetGuid());
        Assert.Equal("A", Attribute(after, "Project").GetProperty("value").GetString());
    }

    [Theory] [InlineData("duplicate")] [InlineData("kind")] [InlineData("key-length")]
    public async Task Invalid_attribute_patch_rejects_whole_header_and_all_attribute_changes_before_save(string fault)
    {
        await using var fixture = await Fixture.Start(); var before = await AttributeVendor(fixture); var id = before.GetProperty("id").GetGuid();
        var rows = new List<object> { new { kind = "CustomField", key = "Site", value = "Must not persist" } };
        rows.Add(new { kind = fault == "kind" ? "BankDetails" : "CustomField",
            key = fault == "duplicate" ? " Site " : fault == "key-length" ? new string('x', 121) : "Bank", value = "Invalid" });
        var draft = AttributeDraft(rows); draft["displayName"] = "Must not persist header";
        var response = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/vendors/{id}", fixture.Owner,
            new { version = before.GetProperty("version").GetInt64(), draft });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(before.GetRawText(), (await AttributeDetail(fixture, id)).GetRawText());
        Assert.Equal(0, await fixture.VendorDetailAuditCount(id, "update"));
        Assert.Equal(0, await fixture.VendorDetailAuditCount(Attribute(before, "Site").GetProperty("id").GetGuid(), "update"));
        var create = await SendJson(fixture.Client, HttpMethod.Post, "/api/procurement/v1/vendors", fixture.Owner, draft);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, create.StatusCode); // Shared validation also protects create.
    }

    [Fact]
    public async Task Attribute_patch_is_versioned_and_scoped_and_never_binds_a_client_child_id()
    {
        await using var fixture = await Fixture.Start(); var before = await AttributeVendor(fixture); var other = await AttributeVendor(fixture);
        var id = before.GetProperty("id").GetGuid(); var version = before.GetProperty("version").GetInt64();
        var draft = AttributeDraft(new[] { new { id = Attribute(other, "Site").GetProperty("id").GetGuid(), kind = "CustomField", key = "Site", value = "Owned only" } });
        var root = $"/api/procurement/v1/vendors/{id}";
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Put, root, fixture.Owner, new { version = version - 1, draft })).StatusCode);
        await fixture.ConfigureCoreRole(fixture.Other, "employee");
        await fixture.RestrictCoreAccountToCompany(fixture.Owner);
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Put, root, fixture.Other, new { version, draft })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(fixture.Client, HttpMethod.Put, root, fixture.Owner, new { version, draft }, company: fixture.OtherCompany)).StatusCode);
        Assert.Equal(before.GetRawText(), (await AttributeDetail(fixture, id)).GetRawText());
        Assert.Equal(HttpStatusCode.OK, (await SendJson(fixture.Client, HttpMethod.Put, root, fixture.Owner, new { version, draft })).StatusCode);
        var after = await AttributeDetail(fixture, id);
        Assert.Equal(Attribute(before, "Site").GetProperty("id").GetGuid(), Attribute(after, "Site").GetProperty("id").GetGuid());
        Assert.Equal("Owned only", Attribute(after, "Site").GetProperty("value").GetString());
        Assert.Equal(other.GetRawText(), (await AttributeDetail(fixture, other.GetProperty("id").GetGuid())).GetRawText());
    }

    [Fact]
    public async Task Attribute_patch_cannot_change_a_submitted_vendor_or_its_retained_fields()
    {
        await using var fixture = await Fixture.Start(); var created = await AttributeVendor(fixture); var id = created.GetProperty("id").GetGuid();
        var submit = await SendJson(fixture.Client, HttpMethod.Post, $"/api/procurement/v1/vendors/{id}/submit", fixture.Owner,
            new { version = created.GetProperty("version").GetInt64(), businessReason = "Synthetic vendor required" });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode); var before = await AttributeDetail(fixture, id);
        var response = await SendJson(fixture.Client, HttpMethod.Put, $"/api/procurement/v1/vendors/{id}", fixture.Owner,
            new { version = before.GetProperty("version").GetInt64(), draft = AttributeDraft(new[] { new { kind = "CustomField", key = "Site", value = "Forbidden" } }) });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before.GetRawText(), (await AttributeDetail(fixture, id)).GetRawText());
    }
}
