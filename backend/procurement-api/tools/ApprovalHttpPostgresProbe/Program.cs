using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("PROCUREMENT_APPROVAL_HTTP_PROBE_CONNECTION")
    ?? throw new InvalidOperationException("A private disposable PostgreSQL connection is required.");
if (!(new NpgsqlConnectionStringBuilder(connection).Database?.StartsWith("proc_approval_http_", StringComparison.Ordinal) ?? false))
    throw new InvalidOperationException("Only databases named proc_approval_http_<unique> are allowed. Production is forbidden.");
await using var probe = await Probe.Start(connection);
await probe.Verify();
Console.WriteLine("PASS: actual mapped Procurement HTTP + PostgreSQL approval workflow, permissions, authority, Finance changes/release, replay, rollback and concurrent decisions.");

sealed class Probe(WebApplication app, string connection, Guid company, Guid branch, Guid requester, Guid buyer,
    Guid finance, Guid manager1, Guid manager2, ProcurementApprovalExecutionOptions feature) : IAsyncDisposable
{
    private readonly HttpClient client = app.GetTestClient();
    private readonly string databaseConnection = connection;
    private const string Root = "/api/procurement/v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<Guid, Guid> grantIds = [];

    public static async Task<Probe> Start(string connection)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddScoped<ITenantAccessor>(_ => new Tenant());
        builder.Services.AddDbContext<HrmDbContext>(o => o.UseNpgsql(connection));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:ProcurementApprovalExecution"] = connection });
        ProcurementHostIntegration.AddServices(builder.Services, connection, builder.Configuration);
        // Explicit isolated fixture override is not evidence of production least privilege.
        var feature = new ProcurementApprovalExecutionOptions { Enabled = true, ExecutionDatabaseConfigured = true };
        builder.Services.AddSingleton(feature);
        builder.Services.AddAuthentication("probe").AddScheme<AuthenticationSchemeOptions, ProbeAuthentication>("probe", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<ProcurementScopeMiddleware>();
        ProcurementSpendingAuthorityRoutes.MapRoutes(app); ProcurementRequestRoutes.Map(app);
        ProcurementBudgetRoutes.Map(app); ProcurementApprovalReadinessRoutes.Map(app); ProcurementApprovalExecutionRoutes.MapRoutes(app);
        var company = new LegalEntity { Code = "HTTP-PROBE", RegisteredName = "Synthetic probe company", Currency = "ZMW" };
        var branch = new WorkLocation { Code = "HTTP-BRANCH", Name = "Synthetic branch", LegalEntityId = company.Id };
        var people = new Dictionary<string, (Worker Worker, LocalUser User)>();
        foreach (var (name, role) in new[] { ("requester", "employee"), ("buyer", "erp_admin"),
            ("finance", "finance_approver"), ("manager1", "procurement_manager"), ("manager2", "procurement_manager") })
        {
            var worker = new Worker { EmployeeNo = "HTTP-" + name, FirstName = name, LastName = "Fixture", Status = "active", LocationId = branch.Id };
            var user = new LocalUser { Email = name + "@example.test", NormalizedEmail = name.ToUpperInvariant() + "@EXAMPLE.TEST",
                DisplayName = name, PasswordHash = "synthetic-no-login-secret", RolesCsv = role, WorkerId = worker.Id };
            people[name] = (worker, user);
        }
        people["requester"].Worker.ManagerId = people["manager1"].Worker.Id;
        using (var services = app.Services.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            await core.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            core.LegalEntities.Add(company); core.WorkLocations.Add(branch);
            foreach (var person in people.Values) { core.Workers.Add(person.Worker); core.LocalUsers.Add(person.User); }
            core.TenantRoleAssignments.AddRange(
                new TenantRoleAssignment { RoleKey = "employee", RoleName = "Employee", Active = true,
                    PermissionsCsv = "procurement-access,procurement-request-read,procurement-request-create,procurement-request-edit,procurement-request-submit" },
                new TenantRoleAssignment { RoleKey = "procurement_manager", RoleName = "Procurement manager", Active = true,
                    PermissionsCsv = "procurement-access,procurement-request-read,procurement-approval-read,procurement-approval-decide" },
                new TenantRoleAssignment { RoleKey = "finance_approver", RoleName = "Finance", Active = true,
                    PermissionsCsv = "procurement-access,procurement-budget-check,procurement-request-read,procurement-approval-read,procurement-authority-review,procurement-authority-revoke" });
            await core.SaveChangesAsync();
            var scope = services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
            scope.Set("probe", company.Id, people["buyer"].User.Id.ToString("D"), null, null, false, "probe-seed");
            var procurement = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            await procurement.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            var workflow = services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
            await workflow.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            procurement.AddScoped(new ProcurementCategory { Code = "IT", Name = "Synthetic IT", Status = "Active" });
            procurement.AddScoped(new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 1,
                State = "Published", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), RulesJson = Rules() });
            await procurement.SaveChangesAsync();
        }
        feature.ManualFinancePolicies = [new() { LegalEntityId = company.Id, Enabled = true,
            ApprovedPolicyReference = "SYNTHETIC-APPROVED-MANUAL-POLICY", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), EvidenceMaximumAgeMinutes = 60 }];
        await app.StartAsync();
        return new(app, connection, company.Id, branch.Id, people["requester"].User.Id,
            people["buyer"].User.Id, people["finance"].User.Id, people["manager1"].User.Id, people["manager2"].User.Id, feature);
    }

    public async Task Verify()
    {
        Require((await client.GetAsync(Root + "/approvals")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous queue access.");
        feature.Enabled = false;
        await Expect(HttpStatusCode.ServiceUnavailable, HttpMethod.Get, Root + "/approvals", manager1);
        feature.Enabled = true;
        feature.ExecutionDatabaseConfigured = false;
        await Expect(HttpStatusCode.ServiceUnavailable, HttpMethod.Get, Root + "/approvals", manager1);
        feature.ExecutionDatabaseConfigured = true;
        var request = await NewFundedRequest("Happy complete route");
        var requestId = request.GetProperty("id").GetGuid();
        var manualPolicies = feature.ManualFinancePolicies; feature.ManualFinancePolicies = [];
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/requests/{requestId}/approval-cases", buyer,
            new { expectedRequestVersion = request.GetProperty("version").GetInt64() }, Guid.NewGuid());
        feature.ManualFinancePolicies = manualPolicies;
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/requests/{requestId}/approval-cases", buyer,
            new { expectedRequestVersion = request.GetProperty("version").GetInt64() }, Guid.NewGuid());
        using (var services = Scope(buyer))
            Require(await services.ServiceProvider.GetRequiredService<ApprovalDbContext>().Cases.CountAsync() == 0,
                "Blocked prerequisites left a partial Core case.");
        await Authority(manager1, "AUTH-M1"); await Authority(manager2, "AUTH-M2");
        var readiness = await Get($"{Root}/requests/{requestId}/approval-readiness", buyer);
        Require(readiness.GetProperty("canOpenCase").GetBoolean()
            && !readiness.GetProperty("isAuthority").GetBoolean(), "Enabled canonical readiness disagrees with actual opening eligibility.");
        var openingQueue = await Get(Root + "/requests/approval-opening-queue", buyer);
        Require(openingQueue.GetProperty("records").EnumerateArray().Any(x => x.GetProperty("requestId").GetGuid() == requestId),
            "Designated buyer cannot discover requests needing case opening.");
        var id = request.GetProperty("id").GetGuid();
        await Expect(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"{Root}/requests/{id}/approval-cases", buyer,
            new { expectedRequestVersion = request.GetProperty("version").GetInt64() });
        var openKey = Guid.NewGuid(); var openBody = new { expectedRequestVersion = request.GetProperty("version").GetInt64() };
        var originalOpen = await Send(HttpMethod.Post, $"{Root}/requests/{id}/approval-cases", buyer, openBody, openKey);
        await Check(originalOpen, HttpStatusCode.Created); var openText = await originalOpen.Content.ReadAsStringAsync();
        var opened = JsonDocument.Parse(openText).RootElement.Clone(); var caseId = opened.GetProperty("caseId").GetGuid();
        var replay = await Send(HttpMethod.Post, $"{Root}/requests/{id}/approval-cases", buyer, openBody, openKey);
        await Check(replay, HttpStatusCode.Created);
        Require(await replay.Content.ReadAsStringAsync() == openText, "Open retry did not recover exact response.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/requests/{id}/approval-cases", buyer,
            new { expectedRequestVersion = 999 }, openKey);
        await Expect(HttpStatusCode.NotFound, HttpMethod.Get, $"{Root}/approvals/{caseId}", finance);
        var queue = await Get($"{Root}/approvals?limit=1&offset=0", manager1);
        Require(queue.GetProperty("count").GetInt32() == 1 && queue.GetProperty("records").GetArrayLength() == 1, "Assigned first page missing.");
        var emptyPage = await Get($"{Root}/approvals?limit=1&offset=1", manager1);
        Require(emptyPage.GetProperty("records").GetArrayLength() == 0, "Queue offset ignored.");
        var detail = await Get($"{Root}/approvals/{caseId}", manager1);
        var firstAssignment = ActiveAssignment(detail, manager1);
        await Expect(HttpStatusCode.NotFound, HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager2,
            Decision(detail, firstAssignment, "Approve"), Guid.NewGuid());
        await Expect(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager1,
            Decision(detail, firstAssignment, "Return", " "), Guid.NewGuid());
        var voteKey = Guid.NewGuid(); var firstBody = Decision(detail, firstAssignment, "Approve");
        var firstResponse = await Send(HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager1, firstBody, voteKey);
        await Check(firstResponse, HttpStatusCode.OK); var firstText = await firstResponse.Content.ReadAsStringAsync();
        detail = await Get($"{Root}/approvals/{caseId}", manager1);
        var a1 = ActiveAssignment(detail, manager1); var a2 = ActiveAssignment(detail, manager2);
        var votes = await Task.WhenAll(Send(HttpMethod.Post, DecisionPath(caseId, a1), manager1, Decision(detail, a1, "Approve"), Guid.NewGuid()),
            Send(HttpMethod.Post, DecisionPath(caseId, a2), manager2, Decision(detail, a2, "Approve"), Guid.NewGuid()));
        Require(votes.Count(x => x.StatusCode == HttpStatusCode.OK) == 1 && votes.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1,
            "Concurrent expected-revision votes did not serialize safely.");
        var finalDetail = await Get($"{Root}/approvals/{caseId}", manager1);
        var remaining = finalDetail.GetProperty("stages").EnumerateArray().SelectMany(s => s.GetProperty("assignments").EnumerateArray())
            .Single(a => a.GetProperty("state").GetString() == "Pending");
        var remainingActor = Guid.Parse(remaining.GetProperty("principalId").GetString()!);
        var completed = await Send(HttpMethod.Post, DecisionPath(caseId, remaining), remainingActor,
            Decision(finalDetail, remaining, "Approve"), Guid.NewGuid()); await Check(completed, HttpStatusCode.OK);
        var state = await completed.Content.ReadFromJsonAsync<JsonElement>();
        Require(state.GetProperty("requestState").GetString() == "Approved", "Final approval did not update source.");
        var history = await Get($"{Root}/requests/{id}/approval-history", requester);
        Require(history.GetProperty("records")[0].GetProperty("decisions").GetArrayLength() == 3, "History lost stage decisions.");
        var finalReplay = await Send(HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager1, firstBody, voteKey);
        await Check(finalReplay, HttpStatusCode.OK);
        Require(await finalReplay.Content.ReadAsStringAsync() == firstText, "Terminal retry did not recover original intermediate response.");
        using (var services = Scope(buyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(await db.ApprovalSubmissions.CountAsync() == 1 && await db.ApprovalOutcomes.CountAsync() == 1,
                "Retries duplicated source approval evidence.");
            var lines = await db.PurchaseRequestLines.Where(x => x.PurchaseRequestId == id).ToArrayAsync();
            Require(lines.All(x => x.ApprovedQuantity == x.RequestedQuantity && x.ApprovalStatus == "Approved"), "Approved line quantities not persisted.");
        }
        using (var services = app.Services.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "procurement_manager")).IsArchived = true;
            await core.SaveChangesAsync();
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager1, firstBody, voteKey);
        using (var services = app.Services.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.TenantRoleAssignments.IgnoreQueryFilters().SingleAsync(x => x.TenantId == "probe"
                && x.RoleKey == "procurement_manager")).IsArchived = false;
            await core.SaveChangesAsync();
        }
        await Revoke(manager1);
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager1, firstBody, voteKey);
        await Authority(manager1, "AUTH-M1-RESTORED");
        await FinanceChangedReturnAndRelease();
        await ReceiptFailureRollsBackBothContexts();
        await FinalFinanceRace();
    }

    private async Task FinanceChangedReturnAndRelease()
    {
        var request = await NewFundedRequest("Return on changed Finance"); var id = request.GetProperty("id").GetGuid();
        var opened = await Open(request); var caseId = opened.GetProperty("caseId").GetGuid();
        await Budget(id, opened.GetProperty("requestVersion").GetInt64(), "Insufficient");
        var detail = await Get($"{Root}/approvals/{caseId}", manager1); var assignment = ActiveAssignment(detail, manager1);
        var allowed = assignment.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).ToArray();
        Require(!allowed.Contains("Approve") && allowed.Contains("Return") && allowed.Contains("Reject"), "Changed Finance hid non-orderable outcomes.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, DecisionPath(caseId, assignment), manager1, Decision(detail, assignment, "Approve"), Guid.NewGuid());
        var returned = await Send(HttpMethod.Post, DecisionPath(caseId, assignment), manager1,
            Decision(detail, assignment, "Return", "Correct the specification"), Guid.NewGuid()); await Check(returned, HttpStatusCode.OK);
        var result = await returned.Content.ReadFromJsonAsync<JsonElement>();
        Require(result.GetProperty("requestState").GetString() == "ReturnPendingFinanceRelease", "Return falsely released Finance reservation.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/requests/{id}/withdraw", requester,
            new { version = result.GetProperty("requestVersion").GetInt64(), reason = "Withdraw" });
        var releases = await Get(Root + "/requests/finance-release-queue", finance);
        var release = releases.GetProperty("records").EnumerateArray().Single(x => x.GetProperty("requestId").GetGuid() == id);
        var body = new { expectedRequestVersion = result.GetProperty("requestVersion").GetInt64(),
            reservationReference = release.GetProperty("reservationReference").GetString(),
            releasedAmount = release.GetProperty("reservedAmount").GetDecimal(), currencyCode = "ZMW",
            evidenceReference = "SYNTHETIC-RELEASE-EVIDENCE", confirmedAt = DateTimeOffset.UtcNow, reason = "Finance verified full release under the fixture manual policy" };
        await Expect(HttpStatusCode.UnprocessableEntity, HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", finance,
            new { body.expectedRequestVersion, body.reservationReference, releasedAmount = body.releasedAmount + 1,
                body.currencyCode, body.evidenceReference, body.confirmedAt, body.reason }, Guid.NewGuid());
        var key = Guid.NewGuid(); var confirmation = await Send(HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", finance, body, key);
        await Check(confirmation, HttpStatusCode.OK); var text = await confirmation.Content.ReadAsStringAsync();
        Require(JsonDocument.Parse(text).RootElement.GetProperty("requestState").GetString() == "ReturnedForChanges", "Release did not allow correction.");
        var retry = await Send(HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", finance, body, key);
        await Check(retry, HttpStatusCode.OK); Require(await retry.Content.ReadAsStringAsync() == text, "Release replay changed response.");
        var current = await Get($"{Root}/requests/{id}", requester);
        var submitted = await Send(HttpMethod.Post, $"{Root}/requests/{id}/submit", requester,
            new { version = current.GetProperty("version").GetInt64() }); await Check(submitted, HttpStatusCode.OK);
        var newSource = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Require(newSource.GetProperty("submittedVersion").GetInt64() > request.GetProperty("submittedVersion").GetInt64(), "Resubmission reused submitted version.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, DecisionPath(caseId, assignment), manager1,
            Decision(detail, assignment, "Approve"), Guid.NewGuid());
        var history = await Get($"{Root}/requests/{id}/approval-history", requester);
        Require(history.GetProperty("records")[0].GetProperty("financeRelease").GetProperty("state").GetString() == "Confirmed", "Release recovery missing from history.");
    }

    private async Task ReceiptFailureRollsBackBothContexts()
    {
        var request = await NewFundedRequest("Atomic receipt failure"); var opened = await Open(request);
        var caseId = opened.GetProperty("caseId").GetGuid(); var detail = await Get($"{Root}/approvals/{caseId}", manager1);
        var assignment = ActiveAssignment(detail, manager1); var body = Decision(detail, assignment, "Approve"); var key = Guid.NewGuid();
        long auditBefore; long outboxBefore;
        using (var services = Scope(buyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            auditBefore = await db.AuditEvents.LongCountAsync(); outboxBefore = await db.OutboxEvents.LongCountAsync();
        }
        await using (var connection = new NpgsqlConnection(databaseConnection))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                CREATE FUNCTION procurement.probe_fail_receipt() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                    IF NEW.operation='Decide' THEN RAISE EXCEPTION 'Synthetic receipt failure'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER probe_receipt_failure BEFORE INSERT ON procurement.approval_command_receipts
                    FOR EACH ROW EXECUTE FUNCTION procurement.probe_fail_receipt();
                """, connection);
            await command.ExecuteNonQueryAsync();
        }
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, DecisionPath(caseId, assignment), manager1, body, key);
        var after = await Get($"{Root}/approvals/{caseId}", manager1);
        Require(after.GetProperty("caseVersion").GetInt64() == detail.GetProperty("caseVersion").GetInt64()
            && after.GetProperty("decisions").GetArrayLength() == 0, "Core decision survived failed receipt commit.");
        using (var services = Scope(buyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(await db.AuditEvents.LongCountAsync() == auditBefore && await db.OutboxEvents.LongCountAsync() == outboxBefore,
                "A lone audit or outbox record survived whole-command rollback.");
        }
        await using (var connection = new NpgsqlConnection(databaseConnection))
        {
            await connection.OpenAsync(); await using var command = new NpgsqlCommand("DROP TRIGGER probe_receipt_failure ON procurement.approval_command_receipts; DROP FUNCTION procurement.probe_fail_receipt();", connection);
            await command.ExecuteNonQueryAsync();
        }
        await Expect(HttpStatusCode.OK, HttpMethod.Post, DecisionPath(caseId, assignment), manager1, body, key);
    }

    private async Task FinalFinanceRace()
    {
        var request = await NewFundedRequest("Final approval versus Finance write"); var id = request.GetProperty("id").GetGuid();
        var opened = await Open(request); var caseId = opened.GetProperty("caseId").GetGuid();
        foreach (var actor in new[] { manager1, manager1 })
        {
            var d = await Get($"{Root}/approvals/{caseId}", actor); var a = ActiveAssignment(d, actor);
            await Expect(HttpStatusCode.OK, HttpMethod.Post, DecisionPath(caseId, a), actor, Decision(d, a, "Approve"), Guid.NewGuid());
        }
        var detail = await Get($"{Root}/approvals/{caseId}", manager2); var assignment = ActiveAssignment(detail, manager2);
        var final = Send(HttpMethod.Post, DecisionPath(caseId, assignment), manager2, Decision(detail, assignment, "Approve"), Guid.NewGuid());
        var budget = BudgetResponse(id, detail.GetProperty("requestVersion").GetInt64(), "Insufficient");
        var responses = await Task.WhenAll(final, budget);
        Require(responses.Count(x => x.StatusCode == HttpStatusCode.OK || x.StatusCode == HttpStatusCode.Created) == 1
            && responses.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1, "Finance race did not produce one serialized winner.");
        using var services = Scope(buyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var source = await db.PurchaseRequests.SingleAsync(x => x.Id == id);
        var latest = await db.BudgetCheckSnapshots.Where(x => x.PurchaseRequestId == id).OrderByDescending(x => x.CheckedAt).ThenByDescending(x => x.Id).FirstAsync();
        Require(source.Status != "Approved" || latest.Status == "Available", "Final approval committed against changed Finance evidence.");
    }

    private async Task Authority(Guid principal, string reference)
    {
        var path = Root + "/configuration/spending-authorities";
        var created = await Send(HttpMethod.Post, path, buyer, new { authorityReference = reference, principalId = principal.ToString("D"),
            draft = new { currencyCode = "ZMW", amountBasis = "EstimatedLineBase", maximumAmount = 1000,
                effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), effectiveUntil = DateTimeOffset.UtcNow.AddDays(1), allBranches = true,
                allDepartments = true, allCategories = true } }, selectedBranch: false);
        await Check(created, HttpStatusCode.Created); var grant = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = grant.GetProperty("id").GetGuid();
        var submitted = await Send(HttpMethod.Post, path + "/" + id + "/submit", buyer, new { version = grant.GetProperty("version").GetInt64() }, selectedBranch: false);
        await Check(submitted, HttpStatusCode.OK); var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        await Expect(HttpStatusCode.OK, HttpMethod.Post, path + "/" + id + "/activate", finance,
            new { version = pending.GetProperty("version").GetInt64(), reason = "Independent synthetic grant review" }, selectedBranch: false);
        grantIds[principal] = id;
    }
    private async Task Revoke(Guid principal)
    {
        using var services = Scope(buyer); var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var grant = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == grantIds[principal]);
        await Expect(HttpStatusCode.OK, HttpMethod.Post, Root + "/configuration/spending-authorities/" + grant.Id + "/revoke", finance,
            new { version = grant.Version, reason = "Synthetic authority withdrawn" }, selectedBranch: false);
    }
    private async Task<JsonElement> NewFundedRequest(string purpose)
    {
        var created = await Send(HttpMethod.Post, Root + "/requests", requester, new { purpose, currencyCode = "ZMW",
            neededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), lines = new[] { new { description = "Synthetic item", unitCode = "each",
                requestedQuantity = 1, estimatedUnitPrice = 100, categoryCode = "IT" } } });
        await Check(created, HttpStatusCode.Created); var draft = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = draft.GetProperty("id").GetGuid();
        var submitted = await Send(HttpMethod.Post, $"{Root}/requests/{id}/submit", requester, new { version = draft.GetProperty("version").GetInt64() });
        await Check(submitted, HttpStatusCode.OK); var request = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        await Budget(id, request.GetProperty("version").GetInt64(), "Available"); return request;
    }
    private async Task<JsonElement> Open(JsonElement request)
    {
        var response = await Send(HttpMethod.Post, $"{Root}/requests/{request.GetProperty("id").GetGuid()}/approval-cases", buyer,
            new { expectedRequestVersion = request.GetProperty("version").GetInt64() }, Guid.NewGuid());
        await Check(response, HttpStatusCode.Created); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private Task<HttpResponseMessage> BudgetResponse(Guid id, long version, string status)
        => Send(HttpMethod.Post, $"{Root}/requests/{id}/budget-checks", finance, new
        {
            requestVersion = version, financeCheckRef = "CHECK-" + Guid.NewGuid(), status, checkedAmount = 100,
            currencyCode = "ZMW", evidenceRef = "SYNTHETIC-FINANCE-EVIDENCE", decisionReason = "Synthetic controlled manual Finance attestation",
            reservationRef = status == "Available" ? "RESERVE-" + Guid.NewGuid() : null,
            reservationStatus = status == "Available" ? "Confirmed" : null,
        });
    private async Task Budget(Guid id, long version, string status) => await Check(await BudgetResponse(id, version, status), HttpStatusCode.Created);
    private async Task<JsonElement> Get(string path, Guid actor)
    { var response = await Send(HttpMethod.Get, path, actor); await Check(response, HttpStatusCode.OK); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private async Task Expect(HttpStatusCode status, HttpMethod method, string path, Guid actor, object? body = null, Guid? key = null, bool selectedBranch = true)
        => await Check(await Send(method, path, actor, body, key, selectedBranch), status);
    private Task<HttpResponseMessage> Send(HttpMethod method, string path, Guid actor, object? body = null, Guid? key = null, bool selectedBranch = true)
    {
        var request = new HttpRequestMessage(method, path); request.Headers.Add("X-Probe-User", actor.ToString("D"));
        request.Headers.Add("X-Shell-Entity", company.ToString("D"));
        if (selectedBranch) request.Headers.Add("X-Shell-Location", branch.ToString("D"));
        if (key.HasValue) request.Headers.Add("Idempotency-Key", key.Value.ToString("D"));
        if (body is not null) request.Content = JsonContent.Create(body, options: Json); return client.SendAsync(request);
    }
    private IServiceScope Scope(Guid actor)
    {
        var services = app.Services.CreateScope(); services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
            .Set("probe", company, actor.ToString("D"), null, null, false, "probe-inspect"); return services;
    }
    private static JsonElement ActiveAssignment(JsonElement detail, Guid actor) => detail.GetProperty("stages").EnumerateArray()
        .Where(s => s.GetProperty("state").GetString() == "Active").SelectMany(s => s.GetProperty("assignments").EnumerateArray())
        .Single(a => a.GetProperty("principalId").GetString() == actor.ToString("D") && a.GetProperty("state").GetString() == "Pending");
    private static object Decision(JsonElement detail, JsonElement assignment, string action, string? reason = null) => new
    {
        action, reason, expectedCaseVersion = detail.GetProperty("caseVersion").GetInt64(),
        expectedAssignmentVersion = assignment.GetProperty("version").GetInt64(), expectedRequestVersion = detail.GetProperty("requestVersion").GetInt64(),
    };
    private static string DecisionPath(Guid caseId, JsonElement assignment) => $"{Root}/approvals/{caseId}/assignments/{assignment.GetProperty("id").GetGuid()}/decisions";
    private static async Task Check(HttpResponseMessage response, HttpStatusCode expected)
    { if (response.StatusCode != expected) throw new InvalidOperationException($"Expected HTTP {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string Rules() => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, transactionType = "PurchaseRequest", currencyCode = "ZMW", amountBasis = "EstimatedLineBase",
        rules = new[] { new { code = "FALLBACK", priority = 0, when = new { categoryCodes = Array.Empty<string>(), purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<Guid>() },
            result = new { budgetReservation = "Required", sourcingMethodCode = (string?)null, minimumQuoteCount = (int?)null,
                approvalStages = new[] { new { code = "NEED", assignee = "LineManager", mode = "One" }, new { code = "PROC", assignee = "ProcurementManager", mode = "All" } } } } },
    });
    public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); }
}
sealed class Tenant : ITenantAccessor { public string GetTenantId() => "probe"; }
sealed class ProbeAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, HrmDbContext core) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Probe-User"], out var id)) return AuthenticateResult.NoResult();
        var user = await core.LocalUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive && !x.IsArchived);
        if (user is null) return AuthenticateResult.Fail("Inactive fixture account.");
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, user.RolesCsv, Context.RequestAborted);
        var claims = new List<Claim> { new("tenant", "probe"), new("sub", id.ToString("D")), new(ProcurementPermissions.ConfiguredClaim, "true") };
        claims.AddRange(user.RolesCsv.Split(',').Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(actions.Select(action => new Claim(ProcurementPermissions.ActionClaim, action)));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "probe")), "probe"));
    }
}
