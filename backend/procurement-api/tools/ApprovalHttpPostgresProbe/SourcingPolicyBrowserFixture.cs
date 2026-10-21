using System.Net;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private string? sourcingPolicyBrowserBefore;
    private Guid sourcingPolicyBrowserMakerWorker, sourcingPolicyBrowserReviewerWorker;
    private DateTimeOffset sourcingPolicyBrowserStart, sourcingPolicyBrowserEnd;
    private const string BrowserSourcingReviewReason = "Browser independent review requested";
    private const string BrowserSourcingPublicationReason = "Browser independent sourcing settings approved";

    private async Task<string> PrepareSourcingPolicyBrowserManifest(int port)
    {
        Require(distinctRuntime, "Sourcing policy browser needs separate finite runtime/bootstrap roles.");
        await RuntimePrivileges.AssertDeniedAsync(runtimeConnection, fixtureServices, sourcingPolicies: true);
        await ConfigureFiniteSourcingPolicyActors();
        sourcingPolicyBrowserStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero);
        sourcingPolicyBrowserEnd = sourcingPolicyBrowserStart.AddDays(1);
        using (var services = Scope(buyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            sourcingPolicyBrowserMakerWorker = (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).WorkerId!.Value;
            sourcingPolicyBrowserReviewerWorker = (await core.LocalUsers.SingleAsync(x => x.Id == manager1)).WorkerId!.Value;
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.PolicyVersions.AnyAsync(x => x.PolicyKey == "SOURCING")
                && !await db.SourcingPolicyIdentityEvidenceRecords.AnyAsync(), "No sourcing policy/history may be preseeded for the browser.");
        }
        // Readiness belongs to the normal API container and its finite runtime connection;
        // the separate bootstrap provider above intentionally owns fixture data only.
        using (var runtime = app.Services.CreateScope())
        {
            var readiness = runtime.ServiceProvider.GetRequiredService<IProcurementSourcingPolicyIdentitySchemaReadiness>();
            Require(readiness is ProcurementSourcingPolicyIdentitySchemaReadiness
                && await readiness.IsReadyAsync(runtime.ServiceProvider.GetRequiredService<ProcurementDbContext>(), default),
                "Browser must use the genuinely ready normal-host P29 provider, never a fixture substitute.");
        }
        var makerLookup = await Get(SourcingPoliciesPath + "/lookups/sourcing-reviewers?principalIds=" + manager2.ToString("D"), buyer);
        Require(makerLookup.GetProperty("configurationVersion").GetInt32() == 1
            && makerLookup.GetProperty("total").GetInt32() == 1, "Normal-host sourcing lookup must compute real readiness and the scoped named person.");
        var selected = makerLookup.GetProperty("items").EnumerateArray().Single();
        Require(selected.GetProperty("principalId").GetGuid() == manager2, "Named canonical reviewer not returned.");
        await VerifyBrowserSourcingUnavailableMarker();
        var makerContext = await Get(Root + "/context", buyer);
        var reviewerContext = await Get(Root + "/context", manager1);
        sourcingPolicyBrowserBefore = await SourcingPolicyBrowserInvariant();
        return await WriteBrowserManifest(new {
            baseUrl = $"http://127.0.0.1:{port}", tenantId = "probe", companyId = company, branchId = branch,
            currencyCode = "ZMW", authenticationHeader = "X-Probe-User",
            actors = new {
                maker = new { principalId = buyer, workerId = sourcingPolicyBrowserMakerWorker, context = makerContext },
                reviewer = new { principalId = manager1, workerId = sourcingPolicyBrowserReviewerWorker, context = reviewerContext } },
            sourcingPolicy = new { policyKey = "SOURCING", currencyCode = "ZMW", makerPrincipalId = buyer,
                reviewerPrincipalId = manager1, namedApproverPrincipalId = manager2,
                namedApproverDisplayName = selected.GetProperty("displayName").GetString(),
                effectiveFrom = sourcingPolicyBrowserStart.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                effectiveTo = sourcingPolicyBrowserEnd.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                ruleCode = "BROWSER_POLICY", priority = 0, minimumAmountInclusive = "0", maximumAmountExclusive = (string?)null,
                minimumQuoteCount = 0, minimumInvitedSuppliers = 0, requirementsPackRequired = false,
                evaluationCriteriaRequired = false, closingDateRequired = false,
                stageCode = "BROWSER_REVIEW", assignee = "ProcurementManager", mode = "All",
                reviewReason = BrowserSourcingReviewReason, publicationReason = BrowserSourcingPublicationReason,
                expectedConfigurationVersion = makerLookup.GetProperty("configurationVersion").GetInt32(), noPolicyEffectsSeeded = true },
            warning = "Synthetic private fixture only. Normal owning-store readiness does not enable RFQ publication or external Finance authority."
        });
    }

    private async Task VerifyBrowserSourcingUnavailableMarker()
    {
        var role = new NpgsqlCommandBuilder().QuoteIdentifier(new NpgsqlConnectionStringBuilder(runtimeConnection).Username!);
        var before = await PolicyFootprint();
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        try
        {
            await using (var grant = new NpgsqlCommand($"GRANT UPDATE(evidence_sha256) ON procurement.sourcing_policy_identity_evidence TO {role}", owner)) await grant.ExecuteNonQueryAsync();
            var unavailable = await Get(SourcingPoliciesPath + "/lookups/sourcing-reviewers?principalIds=" + manager2.ToString("D"), buyer);
            Require(unavailable.GetProperty("configurationVersion").GetInt32() == 0
                && unavailable.GetProperty("total").GetInt32() == 1, "Unavailable P29 schema must preserve scoped names but remove configuration capability.");
            await Check(await Send(HttpMethod.Post, SourcingPoliciesPath, buyer, SourcingPolicyDraft(sourcingPolicyBrowserStart)), HttpStatusCode.ServiceUnavailable);
            Require(before == await PolicyFootprint(), "Schema-off browser preflight created policy/evidence/audit effects.");
        }
        finally
        {
            await using var revoke = new NpgsqlCommand($"REVOKE UPDATE(evidence_sha256) ON procurement.sourcing_policy_identity_evidence FROM {role}", owner);
            await revoke.ExecuteNonQueryAsync();
        }
        Require((await Get(SourcingPoliciesPath + "/lookups/sourcing-reviewers", buyer)).GetProperty("configurationVersion").GetInt32() == 1
            && before == await PolicyFootprint(), "Browser preflight did not restore readiness and original rows.");
    }

    private async Task<string> SourcingPolicyBrowserInvariant(Guid? policyId = null, Guid[]? evidenceIds = null)
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        var names = new List<string>();
        await using (var query = new NpgsqlCommand("SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement' AND c.relkind='r' ORDER BY c.relname", owner))
        await using (var rows = await query.ExecuteReaderAsync()) while (await rows.ReadAsync()) names.Add(rows.GetString(0));
        static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
        var parts = names.Select(name => {
            var filter = name switch {
                "policy_versions" => " WHERE r.id<>@policy",
                "sourcing_policy_identity_evidence" => " WHERE NOT(r.id=ANY(@evidence))",
                "audit_events" => " WHERE NOT(coalesce((r.record_type='PolicyVersion' AND r.record_id=@policy) OR (r.record_type='SourcingPolicyIdentityEvidence' AND r.record_id=ANY(@evidence)),false))",
                _ => "" };
            return "SELECT '" + name.Replace("'", "''") + "' AS name,coalesce(jsonb_agg(j ORDER BY j::text),'[]'::jsonb) AS rows FROM (SELECT to_jsonb(r) j FROM procurement." + Quote(name) + " r" + filter + ") original_rows";
        }).ToList();
        foreach (var table in new[] { "cases", "stages", "assignments", "decisions" })
            parts.Add($"SELECT 'core_{table}',coalesce(jsonb_agg(to_jsonb(r) ORDER BY r.\"Id\"),'[]'::jsonb) FROM core_approval.{table} r");
        await using var command = new NpgsqlCommand("SELECT jsonb_object_agg(name,rows)::text FROM (" + string.Join(" UNION ALL ", parts) + ") retained", owner);
        command.Parameters.AddWithValue("policy", policyId ?? Guid.Empty);
        command.Parameters.AddWithValue("evidence", evidenceIds ?? Array.Empty<Guid>());
        return ProcurementEvidenceEncoding.Digest((string)(await command.ExecuteScalarAsync())!);
    }

    public async Task VerifySourcingPolicyBrowserCompletion()
    {
        Require(sourcingPolicyBrowserBefore is not null, "No untouched sourcing policy browser fixture prepared.");
        // Fresh independent bootstrap-owned contexts after Kestrel shutdown, not browser success flags.
        using var services = Scope(buyer);
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
        var policy = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.PolicyKey == "SOURCING");
        var rows = await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking().OrderBy(x => x.PolicyRecordVersionAtAction).ToArrayAsync();
        Require(rows.Length == 3 && policy.State == "Published" && policy.Version == 3 && policy.PolicyVersionNumber == 1
            && policy.CreatedBy == buyer.ToString("D") && policy.ReviewRequestedBy == buyer.ToString("D") && policy.PublishedBy == manager1.ToString("D")
            && policy.EffectiveFrom == sourcingPolicyBrowserStart && policy.EffectiveTo == sourcingPolicyBrowserEnd
            && policy.ReviewReason == BrowserSourcingReviewReason && policy.PublicationReason == BrowserSourcingPublicationReason,
            "Browser must persist exactly one unchanged three-step published policy.");
        var created = rows.Single(x => x.Stage == "Created"); var submitted = rows.Single(x => x.Stage == "Submitted"); var published = rows.Single(x => x.Stage == "Published");
        SourcingPolicyIdentity.VerifyPublication(policy, created, submitted, published);
        Require(created.PolicyRecordVersionAtAction == 1 && submitted.PolicyRecordVersionAtAction == 2 && published.PolicyRecordVersionAtAction == 3
            && created.ActorWorkerId == sourcingPolicyBrowserMakerWorker && submitted.ActorWorkerId == sourcingPolicyBrowserMakerWorker
            && published.ActorWorkerId == sourcingPolicyBrowserReviewerWorker && rows.All(x => x.ActionAt.Ticks % 10 == 0
                && x.EvidenceSha256 == SourcingPolicyIdentity.Digest(x) && x.RulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(policy.RulesJson))
            && (await core.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == buyer)).WorkerId == sourcingPolicyBrowserMakerWorker
            && (await core.LocalUsers.AsNoTracking().SingleAsync(x => x.Id == manager1)).WorkerId == sourcingPolicyBrowserReviewerWorker,
            "Browser changed retained/current worker bindings, canonical rule digests or timestamp precision.");
        Require(SourcingPublicationPolicyRules.Validate(policy.RulesJson).Count == 0 && SourcingPublicationPolicyRules.AnalyzeCoverage(policy.RulesJson).Count == 0,
            "Browser did not persist a complete supported sourcing rules document.");
        using var document = JsonDocument.Parse(policy.RulesJson);
        var rule = document.RootElement.GetProperty("rules").EnumerateArray().Single(); var when = rule.GetProperty("when");
        var publication = rule.GetProperty("result").GetProperty("publication"); var stage = rule.GetProperty("result").GetProperty("approvalStages").EnumerateArray().Single();
        Require(document.RootElement.GetProperty("currencyCode").GetString() == "ZMW" && rule.GetProperty("code").GetString() == "BROWSER_POLICY" && rule.GetProperty("priority").GetInt32() == 0
            && when.GetProperty("minimumAmountInclusive").GetDecimal() == 0 && when.GetProperty("maximumAmountExclusive").ValueKind == JsonValueKind.Null
            && new[] { "categoryCodes", "purchaseTypes", "branchIds" }.All(key => when.GetProperty(key).GetArrayLength() == 0)
            && publication.GetProperty("minimumQuoteCount").GetInt32() == 0 && publication.GetProperty("minimumInvitedSuppliers").GetInt32() == 0
            && !publication.GetProperty("requirementsPackRequired").GetBoolean() && !publication.GetProperty("evaluationCriteriaRequired").GetBoolean() && !publication.GetProperty("closingDateRequired").GetBoolean()
            && stage.GetProperty("code").GetString() == "BROWSER_REVIEW" && stage.GetProperty("assignee").GetString() == "ProcurementManager" && stage.GetProperty("mode").GetString() == "All"
            && stage.GetProperty("principalIds").EnumerateArray().Single().GetString() == manager2.ToString("D"), "Browser explicit rule conditions/zero/false/named selection changed.");
        var ids = rows.Select(x => x.Id).ToArray();
        var audits = await db.AuditEvents.AsNoTracking().Where(x => x.RecordId == policy.Id || ids.Contains(x.RecordId)).ToArrayAsync();
        Require(audits.Length == 9 && audits.Count(x => x.RecordId == policy.Id && x.Action == "DraftCreated" && x.ActorSubjectId == buyer.ToString("D")) == 1
            && audits.Count(x => x.RecordId == policy.Id && x.Action == "ReviewRequested" && x.ActorSubjectId == buyer.ToString("D")) == 1
            && audits.Count(x => x.RecordId == policy.Id && x.Action == "Published" && x.ActorSubjectId == manager1.ToString("D")) == 1
            && audits.Count(x => x.RecordType == "PolicyVersion" && x.RecordId == policy.Id && x.Action == "create") == 1
            && audits.Count(x => x.RecordType == "PolicyVersion" && x.RecordId == policy.Id && x.Action == "update") == 2
            && rows.All(row => audits.Count(x => x.RecordType == "SourcingPolicyIdentityEvidence" && x.RecordId == row.Id && x.Action == "create" && x.ActorSubjectId == row.ActorPrincipalId) == 1),
            "Browser needs exactly three named and six automatic audits, without duplicate requests/effects.");
        foreach (var (action, state) in new[] { ("DraftCreated", "Draft"), ("ReviewRequested", "PendingReview"), ("Published", "Published") })
        {
            using var detail = JsonDocument.Parse(audits.Single(x => x.RecordId == policy.Id && x.Action == action).ChangedFieldsJson
                ?? throw new InvalidOperationException("Named policy audit payload missing"));
            Require(detail.RootElement.GetProperty("PolicyKey").GetString() == "SOURCING"
                && detail.RootElement.GetProperty("State").GetString() == state
                && detail.RootElement.GetProperty("rulesSha256").GetString() == ProcurementEvidenceEncoding.Digest(policy.RulesJson).ToUpperInvariant(),
                "Named policy audit did not retain its exact lifecycle state and original raw rules hash.");
        }
        Require(await SourcingPolicyBrowserInvariant(policy.Id, ids) == sourcingPolicyBrowserBefore,
            "Browser changed original Procurement/Finance/RFQ/outbox/Core approval rows outside its owned policy and audits.");
        Console.WriteLine("PASS sourcing policy browser owner: one actual Created→Submitted→Published policy, three exact retained worker/hash/microsecond facts, nine audits; original remaining Procurement/Core approval rows unchanged. No RFQ publication or Finance action.");
    }
}
