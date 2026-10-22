using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Npgsql;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    static bool policyRaceMethodCreated, policyRaceOverlapped;
    const string PolicyRoute = "/configuration/policies";
    static async Task PolicyDraftRetirement(HttpClient client)
    {
        stage = "policy-draft-retirement";
        object Draft(string rules) => new { policyKey = "PURCHASE_REQUEST",
            effectiveFrom = "2030-01-01T00:00:00Z", effectiveTo = (string?)null, rulesJson = rules };
        var created = await Send(client, HttpMethod.Post, PolicyRoute, Draft("{}"), HttpStatusCode.Created);
        var id = Id(created); var path = PolicyRoute + "/" + id;
        var detail = await Get(client, path); var list = await Get(client, PolicyRoute);
        Check(detail.GetProperty("draftRetirementVersion").GetInt32() == 1 && detail.GetProperty("canRetireDraft").GetBoolean(),
            "Actual unused policy action metadata missing.");
        Check(list.GetProperty("items").EnumerateArray().Single(x => Id(x) == id).GetProperty("canRetireDraft").GetBoolean(),
            "List and detail disagree on unused policy eligibility.");
        var edited = await Send(client, HttpMethod.Put, path, new { version = Version(detail),
            draft = Draft("{\"draftNote\":\"Unused synthetic draft; not a reviewed buying policy\"}") });
        Check(Version(edited) == Version(detail) + 1, "Policy draft edit did not advance CAS.");
        var current = await Get(client, path);
        await using (var db = Db())
        {
            var row = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == id);
            Check(row.State == "Draft" && row.Version == Version(current) && row.CreatedBy == Actor.ToString(), "Policy edit owner reload mismatch.");
            using var json = JsonDocument.Parse(row.RulesJson);
            Check(json.RootElement.GetProperty("draftNote").GetString() == "Unused synthetic draft; not a reviewed buying policy", "Draft rules did not persist.");
        }
        foreach (var invalid in new[] {
            (Version: Version(created), Reason: "Stale", Status: HttpStatusCode.Conflict),
            (Version: Version(current), Reason: " ", Status: HttpStatusCode.UnprocessableEntity),
            (Version: Version(current), Reason: new string('x', 1001), Status: HttpStatusCode.UnprocessableEntity) })
        {
            var footprint = await PolicyStoreFootprint();
            await Send(client, HttpMethod.Post, path + "/retire", new { version = invalid.Version, reason = invalid.Reason }, invalid.Status);
            Check(footprint == await PolicyStoreFootprint(), "Denied policy retirement changed owning store.");
        }
        var foreignFootprint = await PolicyStoreFootprint();
        await Send(client, HttpMethod.Get, path, null, HttpStatusCode.Forbidden, foreign: true);
        await Send(client, HttpMethod.Post, path + "/retire", new { version = Version(current), reason = "Foreign scope" }, HttpStatusCode.Forbidden, foreign: true);
        Check(foreignFootprint == await PolicyStoreFootprint(), "Foreign policy access changed owning store.");
        PolicyVersion before;
        int audits; int outbox;
        await using (var proof = Db())
        {
            before = await proof.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == id);
            audits = await proof.AuditEvents.CountAsync(); outbox = await proof.OutboxEvents.CountAsync();
        }
        var retired = await Send(client, HttpMethod.Post, path + "/retire", new { version = Version(current), reason = "  Unused synthetic draft  " });
        Check(retired.GetProperty("state").GetString() == "Retired" && Version(retired) == before.Version + 1, "Policy retirement response mismatch.");
        var reloaded = await Get(client, path);
        Check(reloaded.GetProperty("state").GetString() == "Retired" && !reloaded.GetProperty("canRetireDraft").GetBoolean(), "Retirement not retained on GET.");
        await using (var proof = Db())
        {
            var after = await proof.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == id);
            SameRetirementPolicy(before, after);
            Check(after.State == "Retired" && after.Version == before.Version + 1 && after.UpdatedBy == Actor.ToString(), "Owner policy CAS/actor incorrect.");
            var evidence = await proof.AuditEvents.SingleAsync(x => x.RecordId == id && x.RecordType == nameof(PolicyVersion) && x.Action == "DraftRetired");
            Check(evidence.ActorSubjectId == Actor.ToString(), "Retirement actor not the actual request principal.");
            using var json = JsonDocument.Parse(evidence.ChangedFieldsJson!);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(before.RulesJson)));
            Check(json.RootElement.GetProperty("reason").GetString() == "Unused synthetic draft"
                && json.RootElement.GetProperty("rulesSha256").GetString() == hash, "Retirement reason/rules hash not retained.");
            Check(await proof.AuditEvents.CountAsync() == audits + 2, "Retirement needs exactly explicit + automatic update audit.");
            Check(await proof.OutboxEvents.CountAsync() == outbox, "Policy retirement published an unrelated event.");
        }
        var retiredFootprint = await PolicyStoreFootprint();
        await Send(client, HttpMethod.Post, path + "/retire", new { version = Version(retired), reason = "Repeat" }, HttpStatusCode.Conflict);
        await Send(client, HttpMethod.Put, path, new { version = Version(retired), draft = Draft("{}") }, HttpStatusCode.Conflict);
        await Send(client, HttpMethod.Post, "/configuration/methods", new { code = "RETIRED_POLICY", name = "Must not persist", policyVersionId = id }, HttpStatusCode.UnprocessableEntity);
        Check(retiredFootprint == await PolicyStoreFootprint(), "Retired policy accepted mutation or new method reference.");
        var unbound = await Send(client, HttpMethod.Post, "/configuration/methods", new { code = "UNBOUND_POLICY", name = "Unbound synthetic draft" }, HttpStatusCode.Created);
        var methodPath = "/configuration/methods/" + Id(unbound);
        var unboundFootprint = await PolicyStoreFootprint();
        await Send(client, HttpMethod.Put, methodPath, new { version = Version(unbound), draft = new {
            code = "UNBOUND_POLICY", name = "Must not link retired policy", policyVersionId = id } }, HttpStatusCode.UnprocessableEntity);
        Check(unboundFootprint == await PolicyStoreFootprint(), "Denied method reference correction changed owning store.");
        await Send(client, HttpMethod.Post, methodPath + "/retire", new { version = Version(unbound), reason = "Synthetic negative fixture complete" });
        var effective = await Get(client, PolicyRoute + "/effective?key=PURCHASE_REQUEST");
        Check(effective.GetProperty("status").GetString() == "MissingPolicy" && !effective.GetProperty("isAuthority").GetBoolean(), "Draft CRUD invented effective policy authority.");
        await PolicyMethodReferenceRace(client, Draft("{}"));
    }

    static async Task PolicyMethodReferenceRace(HttpClient client, object draft)
    {
        stage = "simultaneous-policy-retire-method-reference-HTTP";
        var created = await Send(client, HttpMethod.Post, PolicyRoute, draft, HttpStatusCode.Created);
        var id = Id(created); PolicyVersion before;
        await using (var proof = Db()) before = await proof.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == id);
        var outsideBefore = await PolicyStoreRows();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<(HttpStatusCode Status, long Start, long End, string Code)> Race(string path, object body, Guid actor)
        {
            await gate.Task;
            using var request = new HttpRequestMessage(HttpMethod.Post, Root + path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-Crud-Actor", actor.ToString()); request.Headers.Add("X-Shell-Entity", Company.ToString()); request.Headers.Add("X-Shell-Location", Branch.ToString());
            var start = Stopwatch.GetTimestamp(); using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync(); var end = Stopwatch.GetTimestamp(); Interlocked.Increment(ref requests);
            return (response.StatusCode, start, end, SafeCode(content));
        }
        stage = "simultaneous-policy-retire-method-reference-HTTP";
        var retire = Race(PolicyRoute + "/" + id + "/retire", new { version = Version(created), reason = "Concurrent unused draft retirement" }, Actor);
        var method = Race("/configuration/methods", new { code = "POLICY_RACE", name = "Concurrent real reference", policyVersionId = id }, Other);
        gate.SetResult(); var outcomes = await Task.WhenAll(retire, method);
        var retirement = outcomes[0]; var reference = outcomes[1];
        policyRaceOverlapped = Math.Max(retirement.Start, reference.Start) < Math.Min(retirement.End, reference.End);
        Check(policyRaceOverlapped, "HTTP attempts did not overlap; this run cannot prove the intended concurrent scenario.");
        Check(retirement.Status is HttpStatusCode.OK or HttpStatusCode.Conflict, $"Unexpected concurrent retirement status {(int)retirement.Status}/{retirement.Code}; reference {(int)reference.Status}/{reference.Code}.");
        Check(reference.Status is HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity, $"Unexpected concurrent reference status {(int)reference.Status}/{reference.Code}; retirement {(int)retirement.Status}/{retirement.Code}.");
        Check((retirement.Status == HttpStatusCode.OK) != (reference.Status == HttpStatusCode.Created), "Race did not have exactly one compatible successful outcome.");
        policyRaceMethodCreated = reference.Status == HttpStatusCode.Created;
        await using (var proof = Db())
        {
            var policy = await proof.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == id);
            var linked = await proof.ProcurementMethods.AsNoTracking().Where(x => x.PolicyVersionId == id).ToListAsync();
            SameRetirementPolicy(before, policy);
            Check(!(policy.State == "Retired" && linked.Count != 0), "Concurrent HTTP persisted retired policy with a new method reference.");
            Check(linked.Count == (policyRaceMethodCreated ? 1 : 0), "Race response/reference count mismatch.");
            Check(policy.State == (policyRaceMethodCreated ? "Draft" : "Retired") && policy.Version == before.Version + (policyRaceMethodCreated ? 0 : 1), "Race owner policy state/CAS mismatch.");
            Check(await proof.AuditEvents.CountAsync(x => x.RecordId == id && x.Action == "DraftRetired") == (policyRaceMethodCreated ? 0 : 1), "Race retirement audit mismatch.");
            Check(await proof.AuditEvents.CountAsync(x => x.RecordId == id && x.Action == "update") == (policyRaceMethodCreated ? 0 : 1), "Race automatic policy audit mismatch.");
            if (policyRaceMethodCreated)
            {
                Check(linked[0].Code == "POLICY_RACE" && linked[0].CreatedBy == Other.ToString() && linked[0].Version == 1 && linked[0].Status == "Draft", "Race method did not retain actual other principal/current source.");
                Check(await proof.AuditEvents.CountAsync(x => x.RecordId == linked[0].Id && x.Action == "create" && x.ActorSubjectId == Other.ToString()) == 1, "Race method audit mismatch.");
            }
        }
        var outsideAfter = await PolicyStoreRows();
        using (var originalAudit = JsonDocument.Parse(outsideBefore["audit_events"]))
        using (var finalAudit = JsonDocument.Parse(outsideAfter["audit_events"]))
            Check(finalAudit.RootElement.GetArrayLength() == originalAudit.RootElement.GetArrayLength() + (policyRaceMethodCreated ? 1 : 2),
                "Race wrote an unexpected audit outside the successful operation.");
        Check(outsideBefore.Keys.SequenceEqual(outsideAfter.Keys), "Policy race changed owning table inventory.");
        foreach (var (table, rows) in outsideBefore)
            if (table is not ("policy_versions" or "procurement_methods" or "audit_events"))
                Check(outsideAfter.TryGetValue(table, out var after) && rows == after, "Policy race changed unrelated table " + table);
        var final = await Get(client, PolicyRoute + "/" + id);
        if (policyRaceMethodCreated)
        {
            Check(!final.GetProperty("canRetireDraft").GetBoolean(), "Referenced Draft still offers retirement.");
            var footprint = await PolicyStoreFootprint();
            await Send(client, HttpMethod.Post, PolicyRoute + "/" + id + "/retire", new { version = Version(final), reason = "Already referenced" }, HttpStatusCode.Conflict);
            Check(footprint == await PolicyStoreFootprint(), "Referenced draft retirement denial changed state.");
        }
    }

    static void SameRetirementPolicy(PolicyVersion before, PolicyVersion after)
    {
        Check(before.Id == after.Id && before.TenantId == after.TenantId && before.LegalEntityId == after.LegalEntityId
            && before.PolicyKey == after.PolicyKey && before.PolicyVersionNumber == after.PolicyVersionNumber
            && before.RulesJson == after.RulesJson && before.EffectiveFrom == after.EffectiveFrom && before.EffectiveTo == after.EffectiveTo
            && before.CreatedBy == after.CreatedBy && before.CreatedAt == after.CreatedAt, "Retirement changed original policy identity/rules/dates.");
        Check(after.ReviewRequestedAt is null && after.ReviewRequestedBy is null && after.ReviewReason is null
            && after.PublishedAt is null && after.PublishedBy is null && after.PublicationReason is null, "Draft fixture fabricated policy review/publication.");
    }
    static async Task<SortedDictionary<string, string>> PolicyStoreRows()
    {
        // Independent private owner SELECTs only; no row locks, schema changes or authority fixtures.
        await using var raw = new NpgsqlConnection(connection); await raw.OpenAsync();
        var tables = new List<string>();
        await using (var command = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='procurement' ORDER BY tablename", raw))
        await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            var quoted = "\"" + table.Replace("\"", "\"\"") + "\"";
            await using var command = new NpgsqlCommand("SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text FROM procurement." + quoted + " t", raw);
            snapshot.Add(table, (string)(await command.ExecuteScalarAsync())!);
        }
        return snapshot;
    }
    static async Task<string> PolicyStoreFootprint() => JsonSerializer.Serialize(await PolicyStoreRows());
}
