using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

internal sealed class PaymentHttpJourney(WebApplicationFactory<MonthlyLeaveAccrualWorker> host,
    PaymentFixture fixture)
{
    private const string Root = "/api/procurement/v1/manual-payments";
    private const string Policies = "/api/procurement/v1/configuration/policies";
    private readonly Dictionary<string, UserSession> sessions = new(StringComparer.Ordinal);
    private readonly List<string> checks = [];
    internal IReadOnlyList<string> Checks => checks;

    internal async Task LoginAndPublishPolicy()
    {
        foreach (var (role, person) in fixture.People)
        {
            var client = host.CreateClient(new WebApplicationFactoryClientOptions
                { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var login = await client.PostAsJsonAsync("/api/hrm/auth/login", new { person.Email, person.Password });
            RequireStatus(login, HttpStatusCode.OK, "real-local-login");
            var cookie = login.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.FirstOrDefault(x => x.StartsWith("hrm_session=", StringComparison.Ordinal))?.Split(';')[0] : null;
            Require(!string.IsNullOrWhiteSpace(cookie), "real-local-cookie");
            sessions.Add(role, new(client, cookie!));
            using var context = await Send(role, HttpMethod.Get, "/api/procurement/v1/context",
                branch: role == "outsider" ? fixture.OtherBranch : fixture.Branch);
            RequireStatus(context, HttpStatusCode.OK, "scoped-Core-context");
            using var contextJson = await JsonDocument.ParseAsync(await context.Content.ReadAsStreamAsync());
            Require(contextJson.RootElement.GetProperty("workerId").ValueKind == JsonValueKind.Null,
                "Core-account-without-employee");
        }
        checks.Add("normal Core cookie auth and optional employee identity");

        // Real policy lifecycle: maker and publisher have separate finite configuration
        // actions. The database is not seeded with a fabricated Published row.
        var effective = DateTimeOffset.UtcNow.AddSeconds(30);
        const string rules = """
            {"approvalType":"SingleApprover","identitySchemaVersion":2,
             "independentCreatorApprover":true,"independentRecorderConfirmer":true,
             "maximumByCurrency":{"ZMW":"1000.0000"},"policyKey":"MANUAL_PAYMENT_REVIEW",
             "schemaVersion":1,"transactionType":"ManualPaymentReview"}
            """;
        using var created = await Send("policy_author", HttpMethod.Post, Policies,
            new { policyKey = "MANUAL_PAYMENT_REVIEW", effectiveFrom = effective, effectiveTo = (DateTimeOffset?)null, rulesJson = rules });
        var policy = await Read(created, HttpStatusCode.Created, "policy-draft");
        var policyId = policy.GetProperty("id").GetGuid();
        var version = policy.GetProperty("version").GetInt64();
        using var submitted = await Send("policy_author", HttpMethod.Post, $"{Policies}/{policyId:D}/submit-review",
            new { version, reason = "Private isolated manual payment HTTP acceptance" });
        var pending = await Read(submitted, HttpStatusCode.OK, "policy-submission");
        version = pending.GetProperty("version").GetInt64();
        using var published = await Send("policy_publisher", HttpMethod.Post, $"{Policies}/{policyId:D}/publish",
            new { version, reason = "Independent private acceptance authority" });
        var active = await Read(published, HttpStatusCode.OK, "independent-policy-publication");
        Require(active.GetProperty("state").GetString() == "Published", "published-policy-state");
        var delay = effective.AddMilliseconds(100) - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.FromSeconds(30)) throw new InvalidOperationException("Unexpected policy activation delay.");
        if (delay > TimeSpan.Zero) await Task.Delay(delay);
        checks.Add("real independent payment policy lifecycle");
    }

    internal async Task Verify()
    {
        var maker = fixture.People["maker"].Id;
        var approver = fixture.People["approver"].Id;
        var recorder = fixture.People["recorder"].Id;
        var confirmer = fixture.People["confirmer"].Id;
        var draft = new { branchId = fixture.Branch, invoiceId = fixture.Invoice, amount = "100.1250",
            approverPrincipalId = approver.ToString("D"), recorderPrincipalId = recorder.ToString("D"),
            confirmerPrincipalId = confirmer.ToString("D"), purpose = "Synthetic manual payment review", title = "Private fixture" };
        var createKey = Guid.NewGuid();
        // Simulate a lost Create response: do not parse the POST body. Recover only by
        // actor and key, without knowing the newly allocated request ID.
        using var created = await Send("maker", HttpMethod.Post, Root, draft, createKey);
        Require(created.StatusCode == HttpStatusCode.Created, "payment-create");
        using var recovered = await Send("maker", HttpMethod.Get, $"{Root}/commands/{createKey:D}");
        var recovery = await Read(recovered, HttpStatusCode.OK, "lost-create-recovery");
        var id = recovery.GetProperty("id").GetGuid();
        Require(recovery.GetProperty("version").GetInt64() == 1
            && recovery.GetProperty("status").GetString() == "Draft", "recorded-create-result");
        checks.Add("actor-owned lost Create response recovery");

        using (var outsiderRecovery = await Send("outsider", HttpMethod.Get, $"{Root}/commands/{createKey:D}",
            branch: fixture.OtherBranch))
            Require(outsiderRecovery.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                "other-branch-recovery-denied");
        using (var outsiderRead = await Send("outsider", HttpMethod.Get, $"{Root}/{id:D}", branch: fixture.OtherBranch))
            Require(outsiderRead.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                "other-branch-record-denied");
        checks.Add("current Core branch scope before read and recovery");

        using (var changedKey = await Send("maker", HttpMethod.Post, Root,
            new { draft.branchId, draft.invoiceId, amount = "100.2500", draft.approverPrincipalId,
                draft.recorderPrincipalId, draft.confirmerPrincipalId, draft.purpose, draft.title }, createKey))
            Require(changedKey.StatusCode == HttpStatusCode.Conflict, "same-key-changed-payload-denied");
        using (var self = await Send("maker", HttpMethod.Post, Root,
            new { draft.branchId, draft.invoiceId, draft.amount, approverPrincipalId = maker.ToString("D"),
                draft.recorderPrincipalId, draft.confirmerPrincipalId, draft.purpose, draft.title }, Guid.NewGuid()))
            Require((int)self.StatusCode is >= 400 and < 500, "self-assignment-denied");
        using (var overprecise = await Send("maker", HttpMethod.Post, Root,
            new { draft.branchId, draft.invoiceId, amount = "1.00001", draft.approverPrincipalId,
                draft.recorderPrincipalId, draft.confirmerPrincipalId, draft.purpose, draft.title }, Guid.NewGuid()))
            Require(overprecise.StatusCode == HttpStatusCode.UnprocessableEntity, "five-decimal-input-denied");

        using var revised = await Send("maker", HttpMethod.Put, $"{Root}/{id:D}",
            new { expectedVersion = 1, amount = "100.2500", draft.approverPrincipalId,
                draft.recorderPrincipalId, draft.confirmerPrincipalId, draft.purpose, draft.title }, Guid.NewGuid());
        var revision = await Read(revised, HttpStatusCode.OK, "draft-revision");
        Require(revision.GetProperty("version").GetInt64() == 2
            && revision.GetProperty("amountExact").GetString() == "100.25", "exact-revision-amount");
        using (var stale = await Send("maker", HttpMethod.Put, $"{Root}/{id:D}",
            new { expectedVersion = 1, amount = "99.0000", draft.approverPrincipalId,
                draft.recorderPrincipalId, draft.confirmerPrincipalId, draft.purpose, draft.title }, Guid.NewGuid()))
            Require(stale.StatusCode == HttpStatusCode.Conflict, "stale-CAS-denied");
        checks.Add("exact amount, self-assignment, key reuse and CAS denials");

        var version = 2L;
        version = await Move("maker", id, "submit", version, "PendingApproval");
        using (var makerVote = await Send("maker", HttpMethod.Post, $"{Root}/{id:D}/approve",
            new { expectedVersion = version }, Guid.NewGuid()))
            Require(makerVote.StatusCode == HttpStatusCode.Forbidden, "maker-self-approval-denied");
        version = await Move("approver", id, "approve", version, "Approved");
        using (var makerRecord = await Send("maker", HttpMethod.Post, $"{Root}/{id:D}/record",
            new { expectedVersion = version, paidOn = DateOnly.FromDateTime(DateTime.UtcNow), amount = "100.2500",
                externalMethod = "External bank transfer", externalReference = "SYNTHETIC-REFERENCE" }, Guid.NewGuid()))
            Require(makerRecord.StatusCode == HttpStatusCode.Forbidden, "record-action-denied");
        using var recorded = await Send("recorder", HttpMethod.Post, $"{Root}/{id:D}/record",
            new { expectedVersion = version, paidOn = DateOnly.FromDateTime(DateTime.UtcNow), amount = "100.2500",
                externalMethod = "External bank transfer", externalReference = "SYNTHETIC-REFERENCE" }, Guid.NewGuid());
        var record = await Read(recorded, HttpStatusCode.OK, "external-payment-recorded");
        Require(record.GetProperty("status").GetString() == "PaymentRecorded", "recorded-is-not-paid");
        version = record.GetProperty("version").GetInt64();
        version = await Move("confirmer", id, "confirm", version, "ConfirmedManually");
        using (var detail = await Send("maker", HttpMethod.Get, $"{Root}/{id:D}"))
        {
            var view = await Read(detail, HttpStatusCode.OK, "final-manual-payment-detail");
            Require(view.GetProperty("request").GetProperty("status").GetString() == "ConfirmedManually"
                && view.GetProperty("request").GetProperty("amountExact").GetString() == "100.25"
                && view.GetProperty("coreCases").GetArrayLength() == 2, "final-Core-linked-manual-history");
        }
        checks.Add("independent approve, actual external Record and independent Confirm");

        var second = new { branchId = fixture.Branch, invoiceId = fixture.Invoice, amount = "25.0000",
            draft.approverPrincipalId, draft.recorderPrincipalId, draft.confirmerPrincipalId,
            purpose = "Return and cancel fixture", title = "Private return" };
        using var secondCreated = await Send("maker", HttpMethod.Post, Root, second, Guid.NewGuid());
        var secondResult = await Read(secondCreated, HttpStatusCode.Created, "second-draft");
        var secondId = secondResult.GetProperty("id").GetGuid();
        var secondVersion = await Move("maker", secondId, "submit", 1, "PendingApproval");
        secondVersion = await Move("approver", secondId, "return", secondVersion, "Draft", "Needs correction");
        _ = await Move("maker", secondId, "cancel", secondVersion, "Cancelled", "No longer needed");
        checks.Add("return and cancellation with retained history");
    }

    private async Task<long> Move(string role, Guid id, string action, long version, string status, string? reason = null)
    {
        using var result = await Send(role, HttpMethod.Post, $"{Root}/{id:D}/{action}",
            new { expectedVersion = version, reason }, Guid.NewGuid());
        var body = await Read(result, HttpStatusCode.OK, action);
        Require(body.GetProperty("status").GetString() == status, action + "-state");
        return body.GetProperty("version").GetInt64();
    }

    private async Task<HttpResponseMessage> Send(string role, HttpMethod method, string path,
        object? body = null, Guid? key = null, Guid? branch = null)
    {
        var session = sessions[role];
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", session.Cookie);
        request.Headers.Add("X-Shell-Entity", fixture.Company.ToString("D"));
        request.Headers.Add("X-Shell-Location", (branch ?? fixture.Branch).ToString("D"));
        if (key is { } commandKey) request.Headers.Add("Idempotency-Key", commandKey.ToString("D"));
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return await session.Client.SendAsync(request);
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response, HttpStatusCode expected, string name)
    {
        RequireStatus(response, expected, name);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private static void Require(bool condition, string check)
    { if (!condition) throw new PaymentProbeAssertion(check); }

    private static void RequireStatus(HttpResponseMessage response, HttpStatusCode expected, string check)
    { if (response.StatusCode != expected) throw new PaymentProbeAssertion(check, (int)response.StatusCode); }

    private sealed record UserSession(HttpClient Client, string Cookie);
}

internal sealed class PaymentProbeAssertion(string check, int? statusCode = null) : Exception
{
    internal string Check { get; } = check;
    internal int? StatusCode { get; } = statusCode;
}
