using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ProcurementCrudHttpProbe;
internal static partial class Program
{
    const string SystemSupplierRole = "system_supplier_reviewer";
    const string SystemSupplierActions = "procurement-access,procurement-vendor-read,procurement-vendor-manage,procurement-vendor-approve,procurement-vendor-suspend";

    // Synthetic outer authentication only; every supplier effect below goes through
    // the real mapped handler, actual current Core grants and actual PostgreSQL.
    static async Task SystemSupplierLifecycle(HttpClient client)
    {
        await using (var core = Core())
        {
            Check(await core.LocalUsers.CountAsync(x => (x.Id == SystemActor || x.Id == SystemReviewer) && x.WorkerId == null && x.IsActive) == 2,
                "Supplier lifecycle requires two real workerless Core accounts.");
            var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == SystemSupplierRole);
            Check(role.Active && role.PermissionsCsv == SystemSupplierActions, "Supplier reviewer grants were widened.");
        }
        var context = await SupplierSend(client, SystemReviewer, HttpMethod.Get, "/context");
        Check(context.GetProperty("principalId").GetGuid() == SystemReviewer && context.GetProperty("workerId").ValueKind == JsonValueKind.Null,
            "Supplier reviewer context fabricated an employee.");
        var actions = context.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()).ToHashSet();
        Check(actions.SetEquals(SystemSupplierActions.Split(',')), "Finite supplier reviewer context exposed extra actions.");
        await SupplierSend(client, SystemActor, HttpMethod.Post, "/configuration/categories",
            new { code = "SYS-SUP", name = "System user supplier proof" }, HttpStatusCode.Created);
        object Draft(string name) => new { legalName = "System supplier lifecycle", displayName = name, countryCode = "ZM", categoryCode = "SYS-SUP" };
        var saved = await SupplierSend(client, SystemActor, HttpMethod.Post, "/vendors", Draft("System supplier lifecycle"), HttpStatusCode.Created);
        var id = Id(saved); var path = "/vendors/" + id;
        var contact = await SupplierSend(client, SystemActor, HttpMethod.Post, path + "/contacts",
            new { version = Version(saved), contact = new { name = "System supplier contact", email = "supplier-proof@example.test" } }, HttpStatusCode.Created);
        Check(Version(contact) == Version(saved) + 1, "Contact did not advance supplier revision exactly once."); saved = contact;
        var qualification = await SupplierSend(client, SystemActor, HttpMethod.Post, path + "/qualifications",
            new { version = Version(saved), qualification = new { qualificationType = "Business registration", issuer = "Synthetic evidence issuer",
                referenceNumber = "SYNTHETIC-NOT-REAL-CERTIFICATION", expiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2) } }, HttpStatusCode.Created);
        Check(Version(qualification) == Version(saved) + 1, "Qualification did not advance supplier revision."); saved = qualification;
        var qualificationId = Id(qualification);
        saved = await SupplierStep(client, SystemActor, path + "/submit", saved, v => new { version = v, businessReason = "Actual mapped workerless supplier review proof" });
        var before = await SupplierLifecycleFootprint();
        await SupplierSend(client, SystemActor, HttpMethod.Post, path + "/approve",
            new { version = Version(saved), reason = "Maker must never approve", duplicatesReviewed = true }, HttpStatusCode.Forbidden);
        Check(before == await SupplierLifecycleFootprint(), "Self approval leaked supplier effects.");
        saved = await SupplierStep(client, SystemReviewer, path + "/return", saved, v => new { version = v, reason = "Clarify trading display name" });
        var returned = saved;
        saved = await SupplierSend(client, SystemActor, HttpMethod.Put, path,
            new { version = Version(saved), draft = Draft("Revised system supplier lifecycle") });
        Check(Version(saved) == Version(returned) + 1, "Returned correction did not advance supplier revision.");
        saved = await SupplierStep(client, SystemActor, path + "/submit", saved, v => new { version = v, businessReason = "Clarified information resubmitted" });
        before = await SupplierLifecycleFootprint();
        await SupplierSend(client, SystemReviewer, HttpMethod.Post, path + "/approve",
            new { version = Version(saved), reason = "Qualification not reviewed", duplicatesReviewed = true }, HttpStatusCode.Conflict);
        Check(before == await SupplierLifecycleFootprint(), "Pending qualification was silently verified.");
        var reviewed = await SupplierSend(client, SystemReviewer, HttpMethod.Post, path + "/qualifications/" + qualificationId + "/review",
            new { version = Version(saved), decision = "Verified", reason = "Explicit synthetic qualification assessment; not external verification" });
        Check(reviewed.GetProperty("supplierVersion").GetInt64() == Version(saved) + 1, "Qualification review supplier CAS drifted.");
        saved = await SupplierSend(client, SystemReviewer, HttpMethod.Get, path);
        before = await SupplierLifecycleFootprint();
        await SupplierSend(client, SystemReviewer, HttpMethod.Post, path + "/approve",
            new { version = Version(saved) - 1, reason = "Stale approval", duplicatesReviewed = true }, HttpStatusCode.Conflict);
        await SupplierSend(client, SystemReviewer, HttpMethod.Post, path + "/approve",
            new { version = Version(saved), reason = "Foreign company", duplicatesReviewed = true }, HttpStatusCode.Forbidden, foreign: true);
        Check(before == await SupplierLifecycleFootprint(), "Stale/foreign approval changed supplier evidence.");
        await SetSystemSupplierActions("procurement-access,procurement-vendor-read");
        await SupplierSend(client, SystemReviewer, HttpMethod.Post, path + "/approve",
            new { version = Version(saved), reason = "Revoked approval grant", duplicatesReviewed = true }, HttpStatusCode.Forbidden);
        Check(before == await SupplierLifecycleFootprint(), "Revoked grant still produced supplier effects.");
        await SetSystemSupplierActions(SystemSupplierActions);
        var submitted = saved;
        saved = await SupplierStep(client, SystemReviewer, path + "/approve", saved, v => new { version = v, reason = "Independent actual workerless Core reviewer", duplicatesReviewed = true });
        before = await SupplierLifecycleFootprint();
        await SupplierSend(client, SystemReviewer, HttpMethod.Post, path + "/approve",
            new { version = Version(submitted), reason = "Repeat approval", duplicatesReviewed = true }, HttpStatusCode.Conflict);
        await SupplierSend(client, SystemActor, HttpMethod.Post, path + "/suspend",
            new { version = Version(saved), reason = "Maker must not suspend own supplier", existingOrdersReviewed = true }, HttpStatusCode.Forbidden);
        await SupplierSend(client, SystemActor, HttpMethod.Post, path + "/withdraw",
            new { version = Version(saved), reason = "Active supplier cannot be withdrawn" }, HttpStatusCode.Conflict);
        Check(before == await SupplierLifecycleFootprint(), "Replayed/independence/terminal denial leaked effects.");
        // A third genuine Core actor places the hold. The workerless reviewer can
        // then independently reactivate; the maker and hold actor remain excluded.
        saved = await SupplierStep(client, Other, path + "/suspend", saved, v => new { version = v, reason = "Explicit synthetic risk hold", existingOrdersReviewed = true });
        before = await SupplierLifecycleFootprint();
        await SupplierSend(client, Other, HttpMethod.Post, path + "/reactivate",
            new { version = Version(saved), reason = "Hold actor cannot release own hold", dueDiligenceReviewed = true }, HttpStatusCode.Forbidden);
        await SupplierSend(client, SystemActor, HttpMethod.Post, path + "/reactivate",
            new { version = Version(saved), reason = "Creator cannot release hold", dueDiligenceReviewed = true }, HttpStatusCode.Forbidden);
        Check(before == await SupplierLifecycleFootprint(), "Hold self release leaked effects.");
        saved = await SupplierStep(client, SystemReviewer, path + "/reactivate", saved, v => new { version = v, reason = "Independent review resolved synthetic hold", dueDiligenceReviewed = true });
        var withdrawn = await SupplierSend(client, SystemActor, HttpMethod.Post, "/vendors", Draft("Separate withdrawn system draft"), HttpStatusCode.Created);
        await SupplierStep(client, SystemActor, "/vendors/" + Id(withdrawn) + "/withdraw", withdrawn,
            v => new { version = v, reason = "Separate draft no longer needed" });
        await using var db = Db();
        var supplier = await db.Suppliers.AsNoTracking().SingleAsync(x => x.Id == id);
        Check(supplier.CreatedBy == SystemActor.ToString("D") && supplier.Status == "Active" && supplier.IsOrderable
            && supplier.StatusDecidedBy == SystemReviewer.ToString("D") && supplier.Version == Version(saved)
            && supplier.PaymentVerificationToken == null, "Supplier owning reload lost principal/state/version or invented Finance verification.");
        var decisions = await db.SupplierDecisions.AsNoTracking().Where(x => x.SupplierId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync();
        Check(decisions.Select(x => x.ToStatus).SequenceEqual(new[] { "Submitted", "More Information Required", "Submitted", "Active", "Suspended", "Active" }),
            "Supplier decision trail lost an actual mapped transition.");
        Check(decisions.Where(x => x.ToStatus is "More Information Required" or "Active").All(x => x.ActorSubjectId == SystemReviewer.ToString("D"))
            && decisions.Single(x => x.ToStatus == "Suspended").ActorSubjectId == Other.ToString("D"), "Supplier independence attribution changed.");
        var retained = await db.SupplierQualifications.AsNoTracking().SingleAsync(x => x.Id == qualificationId);
        Check(retained.ReviewStatus == "Verified" && retained.ReviewedBy == SystemReviewer.ToString("D")
            && await db.AuditEvents.AnyAsync(x => x.RecordId == qualificationId && x.Action == "Reviewed" && x.ActorSubjectId == SystemReviewer.ToString("D"))
            && await db.AuditEvents.AnyAsync(x => x.RecordId == id && x.ActorSubjectId == SystemActor.ToString("D"))
            && await db.AuditEvents.AnyAsync(x => x.RecordId == id && x.ActorSubjectId == SystemReviewer.ToString("D")), "Supplier qualification/audit evidence incomplete.");
    }

    static async Task SetSystemSupplierActions(string actions)
    {
        await using var core = Core(); var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == SystemSupplierRole);
        role.PermissionsCsv = actions; await core.SaveChangesAsync();
    }
    static async Task<JsonElement> SupplierStep(HttpClient client, Guid actor, string path, JsonElement previous, Func<long, object> input)
    {
        var result = await SupplierSend(client, actor, HttpMethod.Post, path, input(Version(previous)));
        Check(Version(result) == Version(previous) + 1, "Supplier transition did not advance CAS exactly once: " + path); return result;
    }
    static async Task<JsonElement> SupplierSend(HttpClient client, Guid actor, HttpMethod method, string path,
        object? body = null, HttpStatusCode expected = HttpStatusCode.OK, bool foreign = false)
    {
        stage = "system-supplier HTTP " + method + " " + path;
        using var request = new HttpRequestMessage(method, Root + path);
        request.Headers.Add("X-Crud-Actor", actor.ToString("D")); request.Headers.Add("X-Shell-Entity", (foreign ? ForeignCompany : Company).ToString("D"));
        request.Headers.Add("X-Shell-Location", Branch.ToString("D")); if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request); Interlocked.Increment(ref requests); var text = await response.Content.ReadAsStringAsync();
        Check(response.StatusCode == expected, $"{stage}: expected {(int)expected}, got {(int)response.StatusCode}; code={SafeCode(text)}");
        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }
    static Task<string> SupplierLifecycleFootprint() => Scalar("SELECT jsonb_build_object('suppliers',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.suppliers t),'contacts',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.supplier_contacts t),'qualifications',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.supplier_qualifications t),'decisions',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.supplier_decisions t),'audit',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.audit_events t),'outbox',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM procurement.outbox_events t))::text");
}
