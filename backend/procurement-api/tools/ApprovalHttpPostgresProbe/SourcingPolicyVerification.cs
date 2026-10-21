using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private const string SourcingPoliciesPath = Root + "/configuration/policies";
    private const string PolicyMakerRole = "probe_policy_maker";
    private const string PolicyReviewerRole = "probe_policy_reviewer";

    /// <summary>Actual mapped HTTP and restricted PostgreSQL proof. All Core setup/fault/legacy
    /// fixtures use the separate bootstrap provider; the private host composes the actual SQL
    /// readiness candidate. Production host registration remains unavailable.</summary>
    public async Task VerifySourcingPolicies()
    {
        Require(distinctRuntime, "Sourcing policy proof requires separate restricted runtime and bootstrap identities.");
        using (var runtime = app.Services.CreateScope())
        {
            runtime.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("probe", company, buyer.ToString("D"), null, null, false, "actual-policy-readiness");
            var db = runtime.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(await runtime.ServiceProvider.GetRequiredService<IProcurementSourcingPolicyIdentitySchemaReadiness>().IsReadyAsync(db, default),
                "Actual owning-store P29 readiness is unavailable. Apply reviewed migrations and register the real provider; no bypass is permitted.");
        }
        await RuntimePrivileges.AssertDeniedAsync(runtimeConnection, fixtureServices, sourcingPolicies: true);
        await ConfigureFiniteSourcingPolicyActors();
        var lookupReadiness = await Get(SourcingPoliciesPath + "/lookups/sourcing-reviewers?principalIds=" + manager2.ToString("D"), buyer);
        Require(lookupReadiness.GetProperty("configurationVersion").GetInt32() == 1
            && lookupReadiness.GetProperty("total").GetInt32() == 1, "Normal-host CLI lookup lacks actual computed sourcing configuration readiness.");
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var current = await CreateSourcingPolicy(start);
        var id = current.GetProperty("id").GetGuid();
        var originalRows = await SourcingPolicyRows(id);
        Require(originalRows.Length == 1 && originalRows[0].Stage == "Created", "Create did not append exactly one origin identity.");
        var originalWorker = originalRows[0].ActorWorkerId;
        var before = await PolicyFootprint();
        await Check(await Send(HttpMethod.Put, $"{SourcingPoliciesPath}/{id}", manager1,
            new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(current.GetProperty("version").GetInt64(), SourcingPolicyDraft(start, 1))), HttpStatusCode.Forbidden);
        Require(before == await PolicyFootprint(), "Non-author edit changed persisted policy/evidence/audit.");
        current = await PolicyJson(HttpMethod.Put, $"{SourcingPoliciesPath}/{id}", buyer,
            new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(current.GetProperty("version").GetInt64(), SourcingPolicyDraft(start, 1)));
        current = await SubmitSourcingPolicy(current);
        current = await PolicyJson(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/return-for-changes", manager1,
            new ProcurementPolicyDraftRoutes.PublicationInput(current.GetProperty("version").GetInt64(), "Actual independent return for explicit correction"));
        Require(current.GetProperty("state").GetString() == "Draft" && (await SourcingPolicyRows(id)).Length == 2,
            "Return fabricated publication evidence or removed history.");
        current = await PolicyJson(HttpMethod.Put, $"{SourcingPoliciesPath}/{id}", buyer,
            new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(current.GetProperty("version").GetInt64(), SourcingPolicyDraft(start, 2)));
        current = await SubmitSourcingPolicy(current);
        before = await PolicyFootprint();
        await Check(await Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/publish", buyer,
            new ProcurementPolicyDraftRoutes.PublicationInput(current.GetProperty("version").GetInt64(), "Maker must not approve self")), HttpStatusCode.Forbidden);
        Require(before == await PolicyFootprint(), "Self-review changed policy state or audit.");
        current = await PolicyJson(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/publish", manager1,
            new ProcurementPolicyDraftRoutes.PublicationInput(current.GetProperty("version").GetInt64(), "Actual independent finite-role publication"));
        await VerifyPolicyRoundTrip(id, originalWorker);
        var effectivePath = SourcingPoliciesPath + "/effective?key=SOURCING&at=" + Uri.EscapeDataString(start.AddMinutes(1).ToString("O"));
        var effective = await Get(effectivePath, buyer);
        Require(effective.GetProperty("status").GetString() == "Valid" && !effective.GetProperty("isAuthority").GetBoolean(), "Retained config effective proof was not honest.");

        var pending = await SubmitSourcingPolicy(await CreateSourcingPolicy(start.AddDays(1)));
        var draft = await CreateSourcingPolicy(start.AddDays(2));
        await VerifySourcingPolicyWorkerSwap(pending, draft);
        await VerifySourcingPolicyActorAlias(pending, originalWorker);
        await VerifySourcingPolicyPermissionAndScope(pending);
        await VerifyOffboardedSourcingMaker(start.AddDays(3), "publish");
        await VerifyOffboardedSourcingMaker(start.AddDays(4), "return-for-changes");
        await VerifySourcingPolicyConcurrentPublish(start.AddDays(5));
        await VerifySourcingPolicyAtomicCreate(start.AddDays(6));
        await VerifySourcingPolicyRawConstraints(id, pending.GetProperty("id").GetGuid(), draft.GetProperty("id").GetGuid());
        await VerifySourcingPolicyLegacyReadAndSupersession(id, start.AddDays(100));
        await VerifySourcingPolicySchemaReadiness();
        using (var scope = Scope(buyer))
        {
            var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(!await db.SourcingEvents.AnyAsync() && !await db.SourcingDraftEvidenceRecords.AnyAsync(), "Configuration mode unexpectedly published/reserved an RFQ.");
        }
        Require(!app.Services.GetRequiredService<ProcurementSourcingDraftExecutionOptions>().Enabled,
            "Policy configuration must not enable operational RFQ capabilities.");
        var actionRejected = false;
        try { _ = ProcurementSpendingActions.Resolve("SourcingEvent.Publish"); }
        catch (InvalidOperationException) { actionRejected = true; }
        Require(actionRejected, "Publication spending action became live without its workflow contract.");
        Console.WriteLine("PASS sourcing policy actual HTTP/PG: finite roles, edited-draft return/resubmit/publish/effective, retained worker swap/alias/offboarding, scope/permission denials, legacy hold and exact database constraints. No operational RFQ publication claimed.");
    }

    private ProcurementPolicyDraftRoutes.PolicyDraft SourcingPolicyDraft(DateTimeOffset starts, int quotes = 0)
    {
        var rules = JsonSerializer.Serialize(new { schemaVersion = 1, policyKey = "SOURCING", transactionType = "SourcingPublication",
            action = "SourcingEvent.Publish", permission = "procurement-sourcing-publish", currencyCode = "ZMW", amountBasis = "ApprovedLineBase",
            rules = new[] { new { code = "EXPLICIT", priority = 0,
                when = new { minimumAmountInclusive = (decimal?)null, maximumAmountExclusive = (decimal?)null,
                    categoryCodes = Array.Empty<string>(), purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<Guid>() },
                result = new { publication = new { minimumQuoteCount = quotes, minimumInvitedSuppliers = 0,
                        requirementsPackRequired = true, evaluationCriteriaRequired = true, closingDateRequired = true },
                    approvalStages = new[] { new { code = "NAMED", assignee = "ProcurementManager", mode = "All", principalIds = new[] { manager2.ToString("D") } } } } } } }, Json);
        Require(SourcingPublicationPolicyRules.Validate(rules).Count == 0 && SourcingPublicationPolicyRules.AnalyzeCoverage(rules).Count == 0,
            "Synthetic policy input must pass the actual typed schema/coverage before consuming any fixture.");
        return new("SOURCING", starts, starts.AddHours(6), rules);
    }
    private async Task<JsonElement> CreateSourcingPolicy(DateTimeOffset starts)
        => await PolicyJson(HttpMethod.Post, SourcingPoliciesPath, buyer, SourcingPolicyDraft(starts), HttpStatusCode.Created);
    private async Task<JsonElement> SubmitSourcingPolicy(JsonElement policy)
        => await PolicyJson(HttpMethod.Post, $"{SourcingPoliciesPath}/{policy.GetProperty("id").GetGuid()}/submit-review", buyer,
            new ProcurementPolicyDraftRoutes.ReviewSubmission(policy.GetProperty("version").GetInt64(), "Actual canonical original author submits explicit policy"));
    private async Task<JsonElement> PolicyJson(HttpMethod method, string path, Guid actor, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await Send(method, path, actor, body); await Check(response, expected);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private async Task ConfigureFiniteSourcingPolicyActors()
    {
        using var scope = Scope(buyer); var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        core.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment { RoleKey = PolicyMakerRole, RoleName = "Synthetic finite policy maker", Active = true,
                PermissionsCsv = "procurement-access,procurement-configuration-read,procurement-configuration-edit" },
            new TenantRoleAssignment { RoleKey = PolicyReviewerRole, RoleName = "Synthetic finite policy reviewer", Active = true,
                PermissionsCsv = "procurement-access,procurement-configuration-read,procurement-configuration-approve" });
        (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).RolesCsv = PolicyMakerRole;
        (await core.LocalUsers.SingleAsync(x => x.Id == manager1)).RolesCsv = PolicyReviewerRole;
        (await core.LocalUsers.SingleAsync(x => x.Id == manager2)).RolesCsv = "employee";
        await core.SaveChangesAsync();
    }
    private async Task<SourcingPolicyIdentityEvidence[]> SourcingPolicyRows(Guid id)
    {
        var scope = new FinanceSchemaScope(company, buyer);
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(runtimeConnection).Options, scope);
        return await db.SourcingPolicyIdentityEvidenceRecords.AsNoTracking().Where(x => x.PolicyVersionId == id).OrderBy(x => x.PolicyRecordVersionAtAction).ToArrayAsync();
    }
    private async Task VerifyPolicyRoundTrip(Guid id, Guid originalWorker)
    {
        var rows = await SourcingPolicyRows(id);
        Require(rows.Length == 4 && rows.Count(x => x.Stage == "Created") == 1 && rows.Count(x => x.Stage == "Submitted") == 2
            && rows.Count(x => x.Stage == "Published") == 1 && rows.All(x => x.ActionAt.Ticks % 10 == 0), "PostgreSQL action history/time normalization is incomplete.");
        var origin = rows.Single(x => x.Stage == "Created"); var published = rows.Single(x => x.Stage == "Published");
        var submitted = rows.Single(x => x.Id == published.SubmittedEvidenceId);
        Require(origin.ActorWorkerId == originalWorker && origin.RulesSha256 != submitted.RulesSha256 && submitted.PolicyRecordVersionAtAction > 2,
            "Edited-draft identity/history was replaced or hard-coded to revision2.");
        await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(runtimeConnection).Options, new FinanceSchemaScope(company, buyer));
        var policy = await db.PolicyVersions.AsNoTracking().SingleAsync(x => x.Id == id);
        SourcingPolicyIdentity.VerifyPublication(policy, origin, submitted, published);
    }

    private async Task VerifySourcingPolicyWorkerSwap(JsonElement pending, JsonElement draft)
    {
        Guid? makerWorker, reviewerWorker;
        using (var scope = Scope(buyer))
        {
            var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var maker = await core.LocalUsers.SingleAsync(x => x.Id == buyer); var reviewer = await core.LocalUsers.SingleAsync(x => x.Id == manager1);
            makerWorker = maker.WorkerId; reviewerWorker = reviewer.WorkerId;
            (maker.WorkerId, reviewer.WorkerId) = (reviewerWorker, makerWorker); await core.SaveChangesAsync();
        }
        try
        {
            var before = await PolicyFootprint(); var id = pending.GetProperty("id").GetGuid(); var version = pending.GetProperty("version").GetInt64();
            foreach (var action in new[] { "publish", "return-for-changes" })
                await Check(await Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/{action}", manager1,
                    new ProcurementPolicyDraftRoutes.PublicationInput(version, "Original maker worker with another active canonical account")), HttpStatusCode.Forbidden);
            var draftId = draft.GetProperty("id").GetGuid();
            await Check(await Send(HttpMethod.Put, $"{SourcingPoliciesPath}/{draftId}", buyer,
                new ProcurementPolicyDraftRoutes.VersionedPolicyDraft(draft.GetProperty("version").GetInt64(), SourcingPolicyDraft(DateTimeOffset.UtcNow.AddDays(3)))), HttpStatusCode.Forbidden);
            await Check(await Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{draftId}/submit-review", buyer,
                new ProcurementPolicyDraftRoutes.ReviewSubmission(draft.GetProperty("version").GetInt64(), "Rebound worker cannot inherit authorship")), HttpStatusCode.Forbidden);
            Require(before == await PolicyFootprint(), "Worker-swap denials changed policy/evidence/audit or operational records.");
        }
        finally
        {
            using var scope = Scope(buyer); var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).WorkerId = makerWorker;
            (await core.LocalUsers.SingleAsync(x => x.Id == manager1)).WorkerId = reviewerWorker; await core.SaveChangesAsync();
        }
    }
    private async Task VerifySourcingPolicyActorAlias(JsonElement pending, Guid originalWorker)
    {
        Guid aliasId;
        using (var scope = Scope(buyer))
        {
            var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var alias = new LocalUser { WorkerId = originalWorker, DisplayName = "Synthetic maker alias", Email = "policy-alias@example.test",
                NormalizedEmail = "POLICY-ALIAS@EXAMPLE.TEST", PasswordHash = "synthetic-unused", RolesCsv = PolicyReviewerRole };
            aliasId = alias.Id; core.LocalUsers.Add(alias); await core.SaveChangesAsync();
        }
        try
        {
            var before = await PolicyFootprint();
            await Check(await Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{pending.GetProperty("id").GetGuid()}/publish", aliasId,
                new ProcurementPolicyDraftRoutes.PublicationInput(pending.GetProperty("version").GetInt64(), "An active alias is not an independent person")), HttpStatusCode.Forbidden);
            Require(before == await PolicyFootprint(), "Alias denial changed retained policy history.");
        }
        finally
        {
            using var scope = Scope(buyer); var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == aliasId)).IsActive = false; await core.SaveChangesAsync();
        }
    }
    private async Task VerifyOffboardedSourcingMaker(DateTimeOffset starts, string operation)
    {
        var pending = await SubmitSourcingPolicy(await CreateSourcingPolicy(starts));
        var id = pending.GetProperty("id").GetGuid(); var original = await SourcingPolicyRows(id);
        using (var scope = Scope(buyer))
        {
            var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>(); (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).IsActive = false;
            await core.SaveChangesAsync();
        }
        try
        {
            await PolicyJson(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/{operation}", manager1,
                new ProcurementPolicyDraftRoutes.PublicationInput(pending.GetProperty("version").GetInt64(), "Current independent reviewer with retained inactive maker proof"));
            var rows = await SourcingPolicyRows(id);
            foreach (var old in original) Require(rows.Any(x => x.Id == old.Id && x.EvidenceSha256 == old.EvidenceSha256), "Offboarding review modified original identity evidence.");
            Require(rows.Length == original.Length + (operation == "publish" ? 1 : 0), "Return invented published evidence or publication omitted its worker.");
        }
        finally
        {
            using var scope = Scope(buyer); var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>(); (await core.LocalUsers.SingleAsync(x => x.Id == buyer)).IsActive = true;
            await core.SaveChangesAsync();
        }
    }

    private async Task VerifySourcingPolicyPermissionAndScope(JsonElement pending)
    {
        var id = pending.GetProperty("id").GetGuid(); var input = new ProcurementPolicyDraftRoutes.PublicationInput(pending.GetProperty("version").GetInt64(), "Current scope and grants required");
        var before = await PolicyFootprint();
        await Check(await Send(HttpMethod.Get, SourcingPoliciesPath, requester), HttpStatusCode.Forbidden);
        await Check(await Send(HttpMethod.Get, SourcingPoliciesPath + "/lookups/sourcing-reviewers", requester), HttpStatusCode.Forbidden);
        Require(before == await PolicyFootprint(), "Configuration read denial changed owner data.");
        string permissions;
        using (var scope = Scope(buyer))
        {
            var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>(); var role = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == PolicyReviewerRole);
            permissions = role.PermissionsCsv; role.PermissionsCsv = "procurement-access,procurement-configuration-read"; await core.SaveChangesAsync();
        }
        try
        {
            before = await PolicyFootprint();
            await Check(await Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/publish", manager1, input), HttpStatusCode.Forbidden);
            Require(before == await PolicyFootprint(), "Revoked configuration approval granted an effect.");
        }
        finally
        {
            using var scope = Scope(buyer); var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == PolicyReviewerRole)).PermissionsCsv = permissions; await core.SaveChangesAsync();
        }
        Guid assignmentId, otherBranch;
        using (var scope = Scope(buyer))
        {
            var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var location = new WorkLocation { LegalEntityId = company, Code = "POLICY-CONFINED", Name = "Synthetic confined review branch" };
            core.WorkLocations.Add(location); otherBranch = location.Id;
            var assignment = new Mightyfin.Erp.Hrm.Application.HrUserBranchAssignment { UserId = manager1, LocationId = location.Id };
            core.UserBranchAssignments.Add(assignment); assignmentId = assignment.Id; await core.SaveChangesAsync();
        }
        try
        {
            before = await PolicyFootprint();
            // Authorized selection of B reaches the real rule-scope guard; it is not merely an unassigned-header denial.
            await Check(await Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/publish", manager1, input, selectedLocation: otherBranch), HttpStatusCode.Forbidden);
            Require(before == await PolicyFootprint(), "A branch-confined reviewer published company-wide rules.");
        }
        finally
        {
            using var scope = Scope(buyer); var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.UserBranchAssignments.SingleAsync(x => x.Id == assignmentId)).IsArchived = true; await core.SaveChangesAsync();
        }
        Guid otherCompany, foreignBranch, foreignCompanyReader;
        using (var scope = Scope(buyer))
        {
            var core = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
            var entity = new LegalEntity { Code = "POLICY-FOREIGN", RegisteredName = "Synthetic foreign policy company", Currency = "ZMW" };
            var location = new WorkLocation { LegalEntityId = entity.Id, Code = "POLICY-FOREIGN", Name = "Synthetic foreign company branch" };
            var worker = new Worker { EmployeeNo = "POLICY-FOREIGN-READER", FirstName = "Synthetic", LastName = "Company reader", Status = "active", LocationId = location.Id };
            var account = new LocalUser { WorkerId = worker.Id, DisplayName = "Synthetic company reader", Email = "policy-company-reader@example.test",
                NormalizedEmail = "POLICY-COMPANY-READER@EXAMPLE.TEST", RolesCsv = PolicyMakerRole, IsActive = true, PasswordHash = "synthetic-unused" };
            worker.SubjectId = account.Id.ToString("D");
            core.LegalEntities.Add(entity); core.WorkLocations.Add(location); core.Workers.Add(worker); core.LocalUsers.Add(account);
            otherCompany = entity.Id; foreignBranch = location.Id; foreignCompanyReader = account.Id; await core.SaveChangesAsync();
        }
        before = await PolicyFootprint();
        using (var request = new HttpRequestMessage(HttpMethod.Get, $"{SourcingPoliciesPath}/{id}"))
        {
            request.Headers.Add("X-Probe-User", buyer.ToString("D")); request.Headers.Add("X-Shell-Entity", otherCompany.ToString("D"));
            request.Headers.Add("X-Shell-Location", foreignBranch.ToString("D"));
            // This employee does not belong to the selected company: admission
            // correctly rejects before any record lookup.
            await Check(await client.SendAsync(request), HttpStatusCode.Forbidden);
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, $"{SourcingPoliciesPath}/{id}"))
        {
            // A genuine employee of that company reaches scoped record lookup,
            // but cannot discover the original company's policy by its ID.
            request.Headers.Add("X-Probe-User", foreignCompanyReader.ToString("D")); request.Headers.Add("X-Shell-Entity", otherCompany.ToString("D"));
            request.Headers.Add("X-Shell-Location", foreignBranch.ToString("D"));
            await Check(await client.SendAsync(request), HttpStatusCode.NotFound);
        }
        Require(before == await PolicyFootprint(), "Foreign-company historical read changed data or escaped scope.");
        await using (var foreign = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(databaseConnection).Options, new Tenant("policy-other-tenant")))
        {
            var entity = new LegalEntity { Code = "POLICY-OTHER-TENANT", RegisteredName = "Synthetic other tenant", Currency = "ZMW" };
            var location = new WorkLocation { Code = "POLICY-OTHER-TENANT", Name = "Synthetic other tenant", LegalEntityId = entity.Id };
            var worker = new Worker { EmployeeNo = "POLICY-OTHER-TENANT", FirstName = "Foreign", LastName = "Synthetic", Status = "active", LocationId = location.Id };
            var account = new LocalUser { WorkerId = worker.Id, DisplayName = "Foreign synthetic", Email = "foreign-policy@example.test", NormalizedEmail = "FOREIGN-POLICY@EXAMPLE.TEST",
                RolesCsv = PolicyReviewerRole, PasswordHash = "synthetic-unused" };
            worker.SubjectId = account.Id.ToString("D"); foreign.LegalEntities.Add(entity); foreign.WorkLocations.Add(location); foreign.Workers.Add(worker); foreign.LocalUsers.Add(account);
            await foreign.SaveChangesAsync(); before = await PolicyFootprint();
            await Check(await Send(HttpMethod.Get, $"{SourcingPoliciesPath}/{id}", account.Id), HttpStatusCode.Unauthorized);
            Require(before == await PolicyFootprint(), "Foreign-tenant identity affected source policy records.");
            // The probe is explicitly fixed to tenant 'probe'; this proves no foreign account login,
            // not a fabricated authenticated tenant-B session or cross-tenant membership grant.
        }
    }

    private async Task VerifySourcingPolicyConcurrentPublish(DateTimeOffset starts)
    {
        var pending = await SubmitSourcingPolicy(await CreateSourcingPolicy(starts)); var id = pending.GetProperty("id").GetGuid();
        var body = new ProcurementPolicyDraftRoutes.PublicationInput(pending.GetProperty("version").GetInt64(), "Concurrent same-revision publication must have one effect");
        var responses = await Task.WhenAll(Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/publish", manager1, body),
            Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{id}/publish", manager1, body));
        try
        {
            Require(responses.Count(x => x.StatusCode == HttpStatusCode.OK) == 1 && responses.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1,
                "Concurrent publication did not return exactly one success and one controlled CAS/transaction conflict. Observed statuses: "
                    + string.Join(',', responses.Select(x => (int)x.StatusCode)));
            Require((await SourcingPolicyRows(id)).Length == 3, "Concurrent publication appended duplicate proof.");
            using var scope = Scope(buyer); var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(await db.AuditEvents.CountAsync(x => x.RecordType == "PolicyVersion" && x.RecordId == id && x.Action == "Published") == 1,
                "Concurrent publication duplicated its consequential audit.");
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private async Task VerifySourcingPolicyAtomicCreate(DateTimeOffset starts)
    {
        var before = await PolicyFootprint();
        await using (var owner = new NpgsqlConnection(databaseConnection))
        {
            await owner.OpenAsync();
            await using var command = new NpgsqlCommand("""
                CREATE FUNCTION procurement.probe_policy_identity_fault() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                    IF NEW.stage='Created' THEN RAISE EXCEPTION 'Synthetic identity insertion failure'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER probe_policy_identity_fault BEFORE INSERT ON procurement.sourcing_policy_identity_evidence
                    FOR EACH ROW EXECUTE FUNCTION procurement.probe_policy_identity_fault();
                """, owner);
            await command.ExecuteNonQueryAsync();
        }
        try
        {
            await Check(await Send(HttpMethod.Post, SourcingPoliciesPath, buyer, SourcingPolicyDraft(starts)), HttpStatusCode.Conflict);
            Require(before == await PolicyFootprint(), "Failed identity insertion left parent policy, audit or other source effects committed.");
        }
        finally
        {
            await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
            await using var command = new NpgsqlCommand("DROP TRIGGER probe_policy_identity_fault ON procurement.sourcing_policy_identity_evidence; DROP FUNCTION procurement.probe_policy_identity_fault();", owner);
            await command.ExecuteNonQueryAsync();
        }
        // Retry follows a proved aborted DB transaction, not response loss or a new key after unknown outcome.
        var retry = await CreateSourcingPolicy(starts);
        Require((await SourcingPolicyRows(retry.GetProperty("id").GetGuid())).Length == 1, "Known-rollback retry did not create exactly one retained origin.");
    }

    private async Task VerifySourcingPolicyRawConstraints(Guid publishedPolicyId, Guid otherPolicyId, Guid unsubmittedPolicyId)
    {
        var rows = await SourcingPolicyRows(publishedPolicyId); var other = await SourcingPolicyRows(otherPolicyId);
        // The unsubmitted Created record has no dependent children, so a scope change tests
        // only its policy-owner FK rather than a competing dependent-parent NO ACTION error.
        var unsubmittedOrigin = (await SourcingPolicyRows(unsubmittedPolicyId)).Single();
        Require(unsubmittedOrigin.Stage == "Created", "Scope FK negative needs a genuine unsubmitted owner record.");
        var origin = rows.Single(x => x.Stage == "Created"); var published = rows.Single(x => x.Stage == "Published");
        var submitted = rows.Single(x => x.Id == published.SubmittedEvidenceId); var otherSubmitted = other.Single(x => x.Stage == "Submitted");
        string ownerFk, createdFk, submittedFk;
        using (var scope = Scope(buyer))
        {
            var model = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>().GetService<IDesignTimeModel>().Model;
            var type = model.FindEntityType(typeof(SourcingPolicyIdentityEvidence))!;
            ownerFk = type.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(PolicyVersion)).GetConstraintName()!;
            createdFk = type.GetForeignKeys().Single(x => x.Properties.Any(p => p.Name == nameof(SourcingPolicyIdentityEvidence.CreatedEvidenceId))).GetConstraintName()!;
            submittedFk = type.GetForeignKeys().Single(x => x.Properties.Any(p => p.Name == nameof(SourcingPolicyIdentityEvidence.SubmittedEvidenceId))).GetConstraintName()!;
        }
        var checkCases = new (string Name, Guid Row, string Set, string State, string? Constraint, string? Column)[] {
            ("schema", origin.Id, "schema_version=2", "23514", "ck_source_policy_identity", null),
            ("zero-worker", origin.Id, "actor_worker_id='00000000-0000-0000-0000-000000000000'::uuid", "23514", "ck_source_policy_identity", null),
            ("unknown-stage", origin.Id, "stage='Unknown'", "23514", "ck_source_policy_identity", null),
            ("created-revision", origin.Id, "policy_record_version_at_action=2", "23514", "ck_source_policy_identity", null),
            ("uppercase-hash", origin.Id, "evidence_sha256=repeat('A',64)", "23514", "ck_source_policy_identity", null),
            ("required-parent-stage", submitted.Id, "created_evidence_stage=NULL", "23514", "ck_source_policy_identity", null),
            ("nonfinite-time", origin.Id, "action_at='-infinity'::timestamptz", "23514", "ck_source_policy_identity", null),
            ("nonnull-rules-hash", origin.Id, "rules_sha256=NULL", "23502", null, "rules_sha256"),
            ("nonnull-evidence-hash", origin.Id, "evidence_sha256=NULL", "23502", null, "evidence_sha256"),
            ("policy-version-tuple", origin.Id, "policy_version_number=policy_version_number+1000000", "23503", ownerFk, null),
            ("company-owner", unsubmittedOrigin.Id, "legal_entity_id='00000000-0000-4000-8000-000000000001'::uuid", "23503", ownerFk, null),
            ("tenant-owner", unsubmittedOrigin.Id, "tenant_id='policy-foreign-scope'", "23503", ownerFk, null),
            ("typed-created-parent", submitted.Id, $"created_evidence_id='{submitted.Id:D}'::uuid", "23503", createdFk, null),
            ("cross-policy-submitted-parent", published.Id, $"submitted_evidence_id='{otherSubmitted.Id:D}'::uuid", "23503", submittedFk, null),
        };
        foreach (var item in checkCases)
        {
            var before = await PolicyFootprint(); var denied = false;
            await using (var owner = new NpgsqlConnection(databaseConnection))
            {
                await owner.OpenAsync(); await using var tx = await owner.BeginTransactionAsync();
                try
                {
                    await using var command = new NpgsqlCommand("UPDATE procurement.sourcing_policy_identity_evidence SET " + item.Set
                        + " WHERE id=@id AND tenant_id='probe' AND legal_entity_id=@company", owner, tx);
                    command.Parameters.AddWithValue("id", item.Row); command.Parameters.AddWithValue("company", company);
                    Require(await command.ExecuteNonQueryAsync() == 1, "Raw policy fixture did not target exactly one actual API-produced row.");
                    await using var immediate = new NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", owner, tx); await immediate.ExecuteNonQueryAsync();
                }
                catch (PostgresException error) when (error.SqlState == item.State
                    && (item.Constraint is null || error.ConstraintName == item.Constraint)
                    && (item.Column is null || error.ColumnName == item.Column)) { denied = true; }
                finally { await tx.RollbackAsync(); }
            }
            Require(denied, "Raw policy evidence invariant was not denied at the exact named constraint/column: " + item.Name);
            Require(before == await PolicyFootprint(), "Rolled-back raw invariant modified retained owner data: " + item.Name);
        }
        await VerifyPolicyRoundTrip(publishedPolicyId, origin.ActorWorkerId);
        Console.WriteLine("PASS P29 raw actual-row CHECK/NOTNULL/scoped policy/typed parent FK denials, exact names/states, forced immediate checks and unchanged full Procurement hashes after each rollback.");
    }

    private async Task VerifySourcingPolicyLegacyReadAndSupersession(Guid originalPolicyId, DateTimeOffset starts)
    {
        var legacyId = Guid.NewGuid();
        await using (var owner = new NpgsqlConnection(databaseConnection))
        {
            await owner.OpenAsync();
            await using var insert = new NpgsqlCommand("""
                INSERT INTO procurement.policy_versions
                SELECT (jsonb_populate_record(NULL::procurement.policy_versions,to_jsonb(p)||jsonb_build_object(
                    'id',@id::text,'policy_version_number',(SELECT max(policy_version_number)+1 FROM procurement.policy_versions WHERE tenant_id='probe' AND legal_entity_id=@company AND policy_key='SOURCING'),
                    'effective_from',@starts::timestamptz,'effective_to',NULL,'publication_reason','UNVERIFIED synthetic pre-P29 row, negative fixture only'))).* 
                FROM procurement.policy_versions p WHERE p.id=@original AND p.tenant_id='probe' AND p.legal_entity_id=@company
                """, owner);
            insert.Parameters.AddWithValue("id", legacyId); insert.Parameters.AddWithValue("starts", starts); insert.Parameters.AddWithValue("company", company);
            insert.Parameters.AddWithValue("original", originalPolicyId); Require(await insert.ExecuteNonQueryAsync() == 1, "Synthetic unverified legacy fixture was not inserted exactly once.");
        }
        var before = await PolicyFootprint();
        await Check(await Send(HttpMethod.Get, SourcingPoliciesPath + "/effective?key=SOURCING&at=" + Uri.EscapeDataString(starts.AddMinutes(1).ToString("O")), buyer), HttpStatusCode.Conflict);
        await Check(await Send(HttpMethod.Get, $"{SourcingPoliciesPath}/{legacyId}", buyer), HttpStatusCode.OK);
        Require((await SourcingPolicyRows(legacyId)).Length == 0 && before == await PolicyFootprint(), "Legacy effective read guessed/backfilled retained worker evidence or modified bytes.");
        var pending = await SubmitSourcingPolicy(await CreateSourcingPolicy(starts.AddDays(1)));
        before = await PolicyFootprint();
        using var response = await Send(HttpMethod.Post, $"{SourcingPoliciesPath}/{pending.GetProperty("id").GetGuid()}/publish", manager1,
            new ProcurementPolicyDraftRoutes.PublicationInput(pending.GetProperty("version").GetInt64(), "Do not adopt unverified open legacy interval"));
        await Check(response, HttpStatusCode.Conflict);
        Require((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString() == "sourcing-policy-identity-unknown"
            && before == await PolicyFootprint(), "Legacy supersession was silently adopted or failed after a partial mutation.");
        // This is deliberately not a raw successful authority fixture: legacy remains unverified,
        // no evidence row is added, and it never yields a Valid/operational publication outcome.
    }

    private async Task<string> PolicyFootprint()
    {
        await using var owner = new NpgsqlConnection(databaseConnection); await owner.OpenAsync();
        var tables = new List<string>();
        await using (var names = new NpgsqlCommand("SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='procurement' AND c.relkind='r' ORDER BY c.relname", owner))
        await using (var reader = await names.ExecuteReaderAsync()) while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        static string Identifier(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var values = tables.Select(name => "('" + name.Replace("'", "''") + "',(SELECT coalesce(jsonb_agg(j ORDER BY j::text),'[]'::jsonb) FROM (SELECT to_jsonb(r) j FROM procurement." + Identifier(name) + " r) snapshot_rows))");
        await using var query = new NpgsqlCommand("SELECT jsonb_object_agg(name,value)::text FROM (VALUES " + string.Join(',', values) + ") snapshots(name,value)", owner);
        // Only hashes are compared; full private rows are never written to tool output/logs.
        return ProcurementEvidenceEncoding.Digest((string)(await query.ExecuteScalarAsync())!);
    }
}
