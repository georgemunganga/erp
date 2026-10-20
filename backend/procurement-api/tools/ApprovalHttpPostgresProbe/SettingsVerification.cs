using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    public async Task VerifySettings()
    {
        Require(distinctRuntime, "Settings proof requires a separate restricted runtime role.");
        await RuntimePrivileges.AssertDeniedAsync(runtimeConnection, fixtureServices);
        var path = Root + "/configuration/spending-authorities";
        foreach (var kind in new[] { "people", "branches", "departments", "categories" })
        {
            var lookup = await Get(path + "/lookups/" + kind + "?limit=1", buyer);
            Require(lookup.GetProperty("limit").GetInt32() == 1, "Lookup limit not respected.");
            Require(lookup.GetProperty("items").GetArrayLength() <= 1, "Unbounded settings lookup.");
            if (kind == "people")
                foreach (var person in lookup.GetProperty("items").EnumerateArray())
                    Require(person.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(
                        new[] { "displayName", "principalId", "workerId" }), "People lookup leaked unrelated Core fields.");
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Get, path + "/lookups/" + kind, requester);
        }
        var created = await Send(HttpMethod.Post, path, buyer, new
        {
            authorityReference = "EXACT-SETTINGS-LIMIT", principalId = manager1.ToString("D"),
            draft = new { currencyCode = "ZMW", amountBasis = "EstimatedLineBase", maximumAmount = "99999999999999.9999",
                effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), effectiveUntil = DateTimeOffset.UtcNow.AddDays(1),
                allBranches = true, allDepartments = true, allCategories = true },
        }, selectedBranch: false);
        await Check(created, HttpStatusCode.Created);
        var saved = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = saved.GetProperty("id").GetGuid();
        var read = await Send(HttpMethod.Get, path + "/" + id, buyer, selectedBranch: false);
        await Check(read, HttpStatusCode.OK);
        var detail = await read.Content.ReadFromJsonAsync<JsonElement>();
        Require(detail.GetProperty("maximumAmount").GetString() == "99999999999999.9999", "Exact amount lost in HTTP output.");
        Require(detail.GetProperty("principalDisplayName").GetString() == "manager1", "Core name missing from settings detail.");
        Require(detail.GetProperty("allowedActions").EnumerateArray().Any(x => x.GetString() == "Submit"), "Current eligible action missing.");
        using var services = Scope(buyer);
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var persisted = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == id);
        Require(persisted.MaximumAmount == 99999999999999.9999m, "PostgreSQL rounded the approved precision contract.");
        Console.WriteLine("PASS settings: all four real PostgreSQL lookup queries, bounded/minimal output, denied employee, exact18,4 persisted amount and current named actions.");
    }
}
