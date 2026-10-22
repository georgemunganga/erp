using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task Recurring_plan_CRUD_uses_Core_owner_without_employee_and_current_permission_not_claims()
    {
        await using var fixture = await Fixture.Start();
        await fixture.ConfigureRecurringActors(withoutEmployee: true);
        var supplier = await fixture.AddInvoiceSupplier();
        object draft(string title = "Monthly system-user plan") => new {
            supplierId = supplier, title, frequency = "Monthly", firstCycleOn = "2026-11-01",
            nextCycleOn = "2026-11-01", expectedAmount = "10.0001",
            ownerPrincipalId = fixture.Other, ownerWorkerId = Guid.NewGuid()
        };
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath,
            fixture.Owner, draft(), "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid(); var path = $"{RecurringPath}/{id}";
        Assert.Equal(fixture.Owner, created.GetProperty("ownerPrincipalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("ownerWorkerId").ValueKind);
        Assert.Equal("ZMW", created.GetProperty("currencyCode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, role: "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Other, role: "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner,
            company: fixture.OtherCompany, role: "procurement_buyer")).StatusCode);
        var otherList = await Send(fixture.Client, HttpMethod.Get, RecurringPath, fixture.Other, role: "procurement_buyer");
        Assert.Equal(0, (await otherList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32());

        // An unchanged save consumes precisely one version and preserves optional HR metadata.
        var unchangedResponse = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, draft = draft() }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, unchangedResponse.StatusCode);
        var unchanged = await unchangedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, unchanged.GetProperty("version").GetInt64());
        var before = await fixture.RecurringAreaFootprint();
        Assert.Equal(HttpStatusCode.Conflict, (await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, draft = draft("Stale change") }, "procurement_buyer")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendJson(fixture.Client, HttpMethod.Post, path + "/close", fixture.Other,
            new { version = 2, reason = "Other user cannot close it" }, "procurement_buyer")).StatusCode);
        Assert.Equal(before, await fixture.RecurringAreaFootprint());
        await fixture.SetRecurringEditPermission(false);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendJson(fixture.Client, HttpMethod.Post, path + "/close", fixture.Owner,
            new { version = 2, reason = "Stale claims cannot restore permission" }, "procurement_buyer",
            actions: "procurement-access,procurement-recurring-edit")).StatusCode);
        Assert.Equal(before, await fixture.RecurringAreaFootprint());
        await fixture.SetRecurringEditPermission(true);
        var updatedResponse = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 2, draft = draft("Corrected system-user plan") }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, updated.GetProperty("version").GetInt64());
        var closed = await SendJson(fixture.Client, HttpMethod.Post, path + "/close", fixture.Owner,
            new { version = 3, reason = "Planning period complete" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var readResponse = await Send(fixture.Client, HttpMethod.Get, path, fixture.Owner, role: "procurement_buyer");
        var read = await readResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", read.GetProperty("status").GetString());
        Assert.Equal("Corrected system-user plan", read.GetProperty("title").GetString());
        Assert.Equal(fixture.Owner, read.GetProperty("ownerPrincipalId").GetGuid());
        Assert.Equal(JsonValueKind.Null, read.GetProperty("ownerWorkerId").ValueKind);
        Assert.Equal(0, (await fixture.RecurringFootprint(id)).Invoices);
        Assert.Equal(0, (await fixture.RecurringFootprint(id)).Handoffs);
    }

    [Fact]
    public async Task Recurring_owner_keeps_access_after_employee_profile_deactivation_without_rewriting_original_link()
    {
        await using var fixture = await Fixture.Start(); await fixture.ConfigureRecurringActors();
        var supplier = await fixture.AddInvoiceSupplier();
        var draft = new { supplierId = supplier, title = "Recurring historical employee link", frequency = "Monthly",
            firstCycleOn = "2026-11-01", nextCycleOn = "2026-11-01", expectedAmount = "1.0001", currencyCode = "ZMW" };
        var createdResponse = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath,
            fixture.Owner, draft, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid(); var path = $"{RecurringPath}/{id}";
        var originalWorker = created.GetProperty("ownerWorkerId").GetGuid();
        await fixture.DeactivatePrincipalEmployee(fixture.Owner);
        var updatedResponse = await SendJson(fixture.Client, HttpMethod.Put, path, fixture.Owner,
            new { version = 1, draft }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetProperty("version").GetInt64());
        Assert.Equal(originalWorker, updated.GetProperty("ownerWorkerId").GetGuid());
        Assert.Equal(fixture.Owner, updated.GetProperty("ownerPrincipalId").GetGuid());
        var closedResponse = await SendJson(fixture.Client, HttpMethod.Post, path + "/close", fixture.Owner,
            new { version = 2, reason = "Owner remains an authorized system user" }, "procurement_buyer");
        Assert.Equal(HttpStatusCode.OK, closedResponse.StatusCode);
        Assert.Equal(originalWorker, (await fixture.RecurringStored(id)).OwnerWorkerId);
        var newResponse = await SendJson(fixture.Client, HttpMethod.Post, RecurringPath, fixture.Owner, draft, "procurement_buyer");
        Assert.Equal(HttpStatusCode.Created, newResponse.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await newResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ownerWorkerId").ValueKind);
    }
}
