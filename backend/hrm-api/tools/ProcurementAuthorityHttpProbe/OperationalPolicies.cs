using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace ProcurementAuthorityHttpProbe;

internal static partial class Program
{
    // Reuse the existing genuine Core fixture, finite runtime role, mapped host,
    // snapshot comparison and worker changes. No second identity system/readiness stub.
    private static async Task VerifyOperationalPolicies(HttpClient client, WebApplication app)
    {
        stage = "operational-policy-runtime-readiness";
        await using (var services = app.Services.CreateAsyncScope())
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var readiness = services.ServiceProvider.GetRequiredService<IProcurementOperationalPolicyIdentitySchemaReadiness>();
            Check(await readiness.IsReadyAsync(db, default) && await readiness.CanRecordAsync(db, default),
                "Actual finite runtime cannot read/record P31 policy evidence.");
        }
        await SetMakerActive(true);
        await using (var db = Owner(Maker))
        {
            db.AddScoped(new ProcurementCategory { Code = "POLICY-HTTP", Name = "Synthetic policy category", Status = "Active" });
            await db.SaveChangesAsync();
        }
        foreach (var key in new[] { "RECEIVING", "INVOICE_MATCHING" })
        {
            var rules = OperationalRules(key);
            Check((key == "RECEIVING" ? ReceivingPolicyRules.Validate(rules) : InvoiceMatchingPolicyRules.Validate(rules)).Count == 0,
                "Synthetic policy rules do not satisfy the genuine parser.");
            for (var round = 0; round < 2; round++)
            {
                await SetMakerActive(true);
                var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(round + 2), TimeSpan.Zero);
                var draft = new { policyKey = key, effectiveFrom = start, effectiveTo = (DateTimeOffset?)null, rulesJson = rules };
                var created = await PolicyHttp(client, HttpMethod.Post, "", Maker, draft, HttpStatusCode.Created);
                var id = created.GetProperty("id").GetGuid();
                if (round == 0)
                {
                    await BindWorkers(ReviewerWorker, MakerWorker);
                    var before = await Footprint();
                    await PolicyHttp(client, HttpMethod.Put, $"/{id}", Maker,
                        new { version = created.GetProperty("version").GetInt64(), draft }, HttpStatusCode.Forbidden);
                    await PolicyHttp(client, HttpMethod.Post, $"/{id}/submit-review", Maker,
                        new { version = created.GetProperty("version").GetInt64(), reason = "Rebound author must not submit" }, HttpStatusCode.Forbidden);
                    Check(await Footprint() == before, "Denied rebound original author changed persisted records.");
                    await BindWorkers(MakerWorker, ReviewerWorker);
                }
                var updated = await PolicyHttp(client, HttpMethod.Put, $"/{id}", Maker,
                    new { version = created.GetProperty("version").GetInt64(), draft }, HttpStatusCode.OK);
                var pending = await PolicyHttp(client, HttpMethod.Post, $"/{id}/submit-review", Maker,
                    new { version = updated.GetProperty("version").GetInt64(), reason = "Review exact synthetic policy" }, HttpStatusCode.OK);
                if (round == 0)
                {
                    await BindWorkers(ReviewerWorker, MakerWorker);
                    var before = await Footprint();
                    foreach (var action in new[] { "publish", "return-for-changes" })
                        await PolicyHttp(client, HttpMethod.Post, $"/{id}/{action}", Reviewer,
                            new { version = pending.GetProperty("version").GetInt64(), reason = "Original person cannot review through another account" }, HttpStatusCode.Forbidden);
                    Check(await Footprint() == before, "Denied original-person review changed persisted records.");
                    await BindWorkers(MakerWorker, ReviewerWorker);
                }
                else await SetMakerActive(false);
                var published = await PolicyHttp(client, HttpMethod.Post, $"/{id}/publish", Reviewer,
                    new { version = pending.GetProperty("version").GetInt64(), reason = "Independent review of exact retained submission" }, HttpStatusCode.OK);
                Check(published.GetProperty("state").GetString() == "Published", "Actual policy publication did not succeed.");
                var effective = await PolicyHttp(client, HttpMethod.Get,
                    "/effective?key=" + key + "&at=" + Uri.EscapeDataString(start.AddHours(1).ToString("O")), Reviewer, null, HttpStatusCode.OK);
                Check(effective.GetProperty("status").GetString() == "Valid"
                    && effective.GetProperty("policy").GetProperty("id").GetGuid() == id, "Effective lookup did not verify the published policy.");
            }
        }
        await using (var db = Owner(Reviewer))
        {
            var policies = await db.PolicyVersions.AsNoTracking().Where(x => x.PolicyKey == "RECEIVING" || x.PolicyKey == "INVOICE_MATCHING").ToArrayAsync();
            Check(policies.Length == 4 && await db.OperationalPolicyIdentityRecords.CountAsync() == 12,
                "Actual lifecycle did not retain four policies and twelve exact identity stages.");
            foreach (var policy in policies)
            {
                var rows = await db.OperationalPolicyIdentityRecords.AsNoTracking().Where(x => x.PolicyVersionId == policy.Id).ToArrayAsync();
                var created = OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Created"));
                var submitted = OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted"));
                var published = OperationalPolicyIdentityStorage.Read(rows.Single(x => x.Stage == "Published"));
                OperationalPolicyWorkerIdentity.VerifyPublication(policy, created, submitted, published);
                Check(created.ActorWorkerId == MakerWorker && submitted.ActorWorkerId == MakerWorker && published.ActorWorkerId == ReviewerWorker,
                    "Stored policy actors differ from genuine Core people.");
            }
            Check(policies.Count(x => x.EffectiveTo is not null) == 2, "Verified replacement did not close only the preceding periods.");
        }
    }

    private static string OperationalRules(string key)
    {
        if (key == "RECEIVING") return JsonSerializer.Serialize(new
        {
            schemaVersion = 1, transactionType = "Receiving", currencyCode = "ZMW",
            rules = new[] { new { code = "HTTP-GOODS", categoryCode = "POLICY-HTTP", purchaseType = "Goods", kind = "Goods",
                siteIds = new[] { Branch }, inspectionRequired = false, checklistReference = (string?)null,
                inspectorWorkerIds = Array.Empty<Guid>(), serviceOwnerWorkerIds = Array.Empty<Guid>(),
                allowRequesterCertification = false, allowBuyerCertification = false, allowReceiverCertification = false,
                requireIndependentInspector = true, evidenceMode = "ControlledManualReference", evidenceReferencePrefix = "SYNTHETIC-HTTP" } }
        }, Json);
        var zero = new { absolute = "0", percent = "0", combination = "AllLimits" };
        return JsonSerializer.Serialize(new { schemaVersion = 1, transactionType = "InvoiceMatching", currencyCode = "ZMW", moneyScale = 2, rounding = "ToEven",
            rules = new[] { new { categoryCode = "POLICY-HTTP", method = "TwoWay", acceptanceKind = (string?)null, quantity = zero, unitPrice = zero, tax = zero, total = zero } } }, Json);
    }

    private static async Task<JsonElement> PolicyHttp(HttpClient client, HttpMethod method, string path, Guid actor, object? body, HttpStatusCode expected)
    {
        stage = "policy HTTP " + method + " " + path;
        using var request = new HttpRequestMessage(method, "/api/procurement/v1/configuration/policies" + path);
        request.Headers.Add("X-Authority-Probe-Actor", actor.ToString("D"));
        request.Headers.Add("X-Shell-Entity", Company.ToString("D")); request.Headers.Add("X-Shell-Location", Branch.ToString("D"));
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Check(response.StatusCode == expected, $"Actual policy {method} expected {(int)expected}, received {(int)response.StatusCode}; code "
            + (result.TryGetProperty("code", out var code) ? code.GetString() : "none"));
        return result;
    }
}
