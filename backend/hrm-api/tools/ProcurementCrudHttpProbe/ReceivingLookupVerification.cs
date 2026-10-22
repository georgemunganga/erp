using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    // Actual mapped purpose lookups only. Reference-category activation uses the
    // existing independent synthetic account; it grants no receiving authority.
    static async Task ReceivingLookups(HttpClient client)
    {
        const string path = "/configuration/policies/lookups/receiving/";
        var category = await Send(client, HttpMethod.Post, "/configuration/categories",
            new { code = "LOOKUP_ACTIVE", name = "Synthetic lookup active category" }, HttpStatusCode.Created);
        await LookupRequest(client, HttpMethod.Post, "/configuration/categories/" + Id(category) + "/activate",
            HttpStatusCode.OK, new { version = Version(category), reason = "Independent synthetic reference review" }, Other);
        var before = await LookupFootprint();
        var people = await LookupRequest(client, HttpMethod.Get, path + "people", HttpStatusCode.OK);
        Check(people.GetProperty("total").GetInt32() == 2, "Canonical people count must include only the two owning fixture workers.");
        var peopleRows = people.GetProperty("items").EnumerateArray().ToArray();
        Check(peopleRows.Select(x => x.GetProperty("id").GetGuid()).ToHashSet().SetEquals(new[] { Worker, OtherWorker }),
            "People lookup did not return canonical worker IDs.");
        Check(peopleRows.All(x => x.GetProperty("principalId").GetGuid() == (x.GetProperty("id").GetGuid() == Worker ? Actor : Other)
            && !x.TryGetProperty("email", out _) && !x.TryGetProperty("roles", out _)), "Purpose people leaked account facts or changed canonical principal binding.");
        var page = await LookupRequest(client, HttpMethod.Get, path + "people?offset=1&limit=1", HttpStatusCode.OK);
        Check(page.GetProperty("total").GetInt32() == 2 && page.GetProperty("items").GetArrayLength() == 1
            && page.GetProperty("offset").GetInt32() == 1 && page.GetProperty("limit").GetInt32() == 1,
            "People paging changed scoped count or page boundaries.");
        var selected = await LookupRequest(client, HttpMethod.Get, path + "people?ids=" + Worker.ToString("D"), HttpStatusCode.OK);
        Check(selected.GetProperty("total").GetInt32() == 1 && selected.GetProperty("items")[0].GetProperty("id").GetGuid() == Worker,
            "Saved worker selection did not resolve exactly.");
        var absent = await LookupRequest(client, HttpMethod.Get, path + "people?ids=" + ForeignCompany.ToString("D"), HttpStatusCode.OK);
        Check(absent.GetProperty("total").GetInt32() == 0 && absent.GetProperty("items").GetArrayLength() == 0,
            "Unknown saved worker ID fell back to a directory.");
        var sites = await LookupRequest(client, HttpMethod.Get, path + "sites?search=synthetic", HttpStatusCode.OK);
        Check(sites.GetProperty("total").GetInt32() == 1 && sites.GetProperty("items")[0].GetProperty("id").GetGuid() == Branch,
            "Site lookup escaped actual selected Core branch.");
        var categories = await LookupRequest(client, HttpMethod.Get, path + "categories", HttpStatusCode.OK);
        await using (var owner = Db())
        {
            var active = await owner.ProcurementCategories.AsNoTracking().Where(x => x.Status == "Active").Select(x => x.Id).ToArrayAsync();
            Check(categories.GetProperty("total").GetInt32() == active.Length
                && categories.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToHashSet().SetEquals(active),
                "Category picker included Draft/Retired or foreign categories.");
            Check(active.Contains(Id(category)), "Genuine independently activated category missing.");
        }
        foreach (var invalid in new[] { "unknown", "people?ids=", "people?ids=not-a-guid", "people?ids=" + Worker + "," + Worker,
            "people?ids=" + Worker + "&search=maker", "sites?offset=-1", "categories?limit=51" })
            await LookupRequest(client, HttpMethod.Get, path + invalid, HttpStatusCode.UnprocessableEntity);
        await Send(client, HttpMethod.Get, path + "people", null, HttpStatusCode.Forbidden, foreign: true);
        Check(before == await LookupFootprint(), "Read-only lookup/denial changed Core, Procurement, audit, outbox or migration rows.");
    }

    static async Task<JsonElement> LookupRequest(HttpClient client, HttpMethod method, string path, HttpStatusCode expected,
        object? body = null, Guid? actor = null)
    {
        stage = "receiving lookup " + method + " " + path;
        using var request = new HttpRequestMessage(method, Root + path);
        request.Headers.Add("X-Crud-Actor", (actor ?? Actor).ToString());
        request.Headers.Add("X-Shell-Entity", Company.ToString()); request.Headers.Add("X-Shell-Location", Branch.ToString());
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request); Interlocked.Increment(ref requests);
        var text = await response.Content.ReadAsStringAsync();
        Check(response.StatusCode == expected, $"{stage}: expected {(int)expected}, got {(int)response.StatusCode}; code={SafeCode(text)}");
        if (path.StartsWith("/configuration/policies/lookups/", StringComparison.Ordinal))
        {
            Check(response.Headers.CacheControl?.NoStore == true, "Purpose lookup must use no-store, including validation errors.");
            if (expected == HttpStatusCode.OK)
            {
                var value = JsonDocument.Parse(text).RootElement.Clone();
                Check(value.GetProperty("companyId").GetGuid() == Company && !value.GetProperty("isAuthority").GetBoolean()
                    && !value.GetProperty("canAcceptDelivery").GetBoolean(), "Lookup implied receiving authority or wrong company.");
                return value;
            }
        }
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // Independent new owner connection, all real table rows in both owned schemas.
    // Ordering by row digest preserves duplicates; nothing is excluded as an audit side effect.
    static async Task<string> LookupFootprint()
    {
        await using var owner = new NpgsqlConnection(connection); await owner.OpenAsync();
        await using var transaction = await owner.BeginTransactionAsync();
        await using (var settings = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL TIME ZONE 'UTC'; SET LOCAL DateStyle='ISO,YMD'", owner, transaction))
            await settings.ExecuteNonQueryAsync();
        var tables = new List<(string Schema, string Table)>();
        await using (var query = new NpgsqlCommand("SELECT schemaname,tablename FROM pg_tables WHERE schemaname IN ('hrm','procurement') ORDER BY schemaname,tablename", owner, transaction))
        await using (var reader = await query.ExecuteReaderAsync())
            while (await reader.ReadAsync()) tables.Add((reader.GetString(0), reader.GetString(1)));
        var parts = new List<string>();
        foreach (var (schema, table) in tables)
        {
            var quoted = '"' + schema.Replace("\"", "\"\"") + "\".\"" + table.Replace("\"", "\"\"") + '"';
            await using var query = new NpgsqlCommand($"SELECT count(*)::text||':'||encode(sha256(convert_to(coalesce(string_agg(h,'' ORDER BY h),''),'UTF8')),'hex') FROM (SELECT encode(sha256(convert_to(to_jsonb(r)::text,'UTF8')),'hex') h FROM {quoted} r)s", owner, transaction);
            parts.Add(schema + "." + table + ":" + (string)(await query.ExecuteScalarAsync())!);
        }
        await transaction.RollbackAsync();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }
}
