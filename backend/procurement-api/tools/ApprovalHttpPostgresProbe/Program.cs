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
using Microsoft.Extensions.Hosting;
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
if (args.Any(x => x is not ("--serve" or "--orders" or "--settings"))
    || args.Distinct(StringComparer.Ordinal).Count() != args.Length
    || (args.Length > 1 && !(args.Length == 2 && args.Contains("--serve", StringComparer.Ordinal) && args.Contains("--orders", StringComparer.Ordinal))))
    throw new ArgumentException("Use --serve, --orders, --settings, or the supported --serve --orders browser fixture combination.");
var serve = args.Contains("--serve", StringComparer.Ordinal);
var orders = args.Contains("--orders", StringComparer.Ordinal);
var port = 5197;
if (serve && Environment.GetEnvironmentVariable("PROCUREMENT_APPROVAL_HTTP_PROBE_PORT") is string portText
    && (!int.TryParse(portText, out port) || port < 1024 || port > 65535))
    throw new ArgumentException("The sandbox loopback port must be between 1024 and 65535.");
var bootstrapConnection = Environment.GetEnvironmentVariable("PROCUREMENT_APPROVAL_HTTP_PROBE_BOOTSTRAP_CONNECTION");
if (bootstrapConnection is not null) RuntimePrivileges.ValidateConnections(connection, bootstrapConnection);
if (orders && bootstrapConnection is null) throw new InvalidOperationException("Order verification requires distinct bootstrap/runtime identities.");
await using var probe = await Probe.Start(connection, serve, port, bootstrapConnection, orders);
if (serve)
{
    var manifest = await probe.PrepareBrowserFixture(port, orders);
    Console.WriteLine($"Private synthetic Procurement sandbox listening on http://127.0.0.1:{port}. Manifest: {manifest}");
    await probe.WaitForShutdown();
    if (orders && Environment.GetEnvironmentVariable("PROCUREMENT_ORDER_BROWSER_VERIFY_ON_STOP") == "1")
        await probe.VerifyOrderBrowserCompletion();
}
else if (orders)
{
    await probe.VerifyInitialOrders();
    Console.WriteLine("PASS: actual initial PO HTTP commercial workflow and scoped/replay/allocation denial checks.");
}
else if (args.Contains("--settings", StringComparer.Ordinal))
{
    await probe.VerifySettings();
    Console.WriteLine("PASS: actual settings HTTP/PostgreSQL lookups, exact amounts, actions and restricted runtime privileges.");
}
else
{
    await probe.Verify();
    Console.WriteLine("PASS: actual mapped Procurement HTTP + PostgreSQL approval workflow, permissions, authority, Finance changes/release, replay, rollback and concurrent decisions.");
}

sealed partial class Probe(WebApplication app, string connection, Guid company, Guid branch, Guid requester, Guid buyer,
    Guid finance, Guid manager1, Guid manager2, ProcurementApprovalExecutionOptions feature, HttpClient httpClient, ServiceProvider fixtureServices,
    string runtimeConnection, bool distinctRuntime) : IAsyncDisposable
{
    private readonly HttpClient client = httpClient;
    private readonly string databaseConnection = connection;
    private const string Root = "/api/procurement/v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<Guid, Guid> grantIds = [];
    // Explicit fixture members keep the PO partial independent of primary-parameter scope.
    private Guid OrderCompany => company;
    private Guid OrderBranch => branch;
    private Guid OrderRequester => requester;
    private Guid OrderBuyer => buyer;
    private Guid OrderFinance => finance;
    private Guid OrderManager1 => manager1;
    private Guid OrderManager2 => manager2;
    private bool OrderDistinctRuntime => distinctRuntime;

    public static async Task<Probe> Start(string connection, bool serve = false, int port = 5197, string? bootstrapConnection = null, bool orders = false)
    {
        var ownerConnection = bootstrapConnection ?? connection;
        // Deliberately separate from the app provider: fixture DDL/writes never use runtime grants.
        var fixtureCollection = new ServiceCollection();
        fixtureCollection.AddScoped<ITenantAccessor>(_ => new Tenant());
        fixtureCollection.AddScoped<ProcurementRequestScope>();
        fixtureCollection.AddScoped<IProcurementScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
        fixtureCollection.AddScoped<Mightyfin.Erp.Core.Approvals.Domain.IApprovalScope>(sp => sp.GetRequiredService<ProcurementRequestScope>());
        fixtureCollection.AddDbContext<HrmDbContext>(o => o.UseNpgsql(ownerConnection));
        fixtureCollection.AddDbContext<ProcurementDbContext>(o => o.UseNpgsql(ownerConnection,
            n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")));
        fixtureCollection.AddDbContext<ApprovalDbContext>(o => o.UseNpgsql(ownerConnection,
            n => n.MigrationsHistoryTable("__EFMigrationsHistory", "core_approval")));
        var fixtureServices = fixtureCollection.BuildServiceProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        if (serve) builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        else builder.WebHost.UseTestServer();
        builder.Services.AddScoped<ITenantAccessor>(_ => new Tenant());
        builder.Services.AddDbContext<HrmDbContext>(o => o.UseNpgsql(connection));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:ProcurementApprovalExecution"] = connection });
        ProcurementHostIntegration.AddServices(builder.Services, connection, builder.Configuration);
        // Explicit isolated fixture override is not evidence of production least privilege.
        var feature = new ProcurementApprovalExecutionOptions { Enabled = true, ExecutionDatabaseConfigured = true };
        builder.Services.AddSingleton(feature);
        var orderFeature = new ProcurementOrderExecutionOptions { Enabled = orders, ExecutionDatabaseConfigured = true,
            ApprovedManualDispatchPolicyReference = "SYNTHETIC-APPROVED-DELIVERY", AllowedDeliveryChannels = ["SecureEmail"] };
        builder.Services.AddSingleton(orderFeature);
        builder.Services.AddAuthentication("probe").AddScheme<AuthenticationSchemeOptions, ProbeAuthentication>("probe", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<ProcurementScopeMiddleware>();
        if (serve) ProcurementHostIntegration.MapRoutes(app);
        else
        {
            ProcurementOrderDraftRoutes.MapRoutes(app); ProcurementOrderExecutionRoutes.MapRoutes(app);
            ProcurementSpendingAuthorityRoutes.MapRoutes(app); ProcurementRequestRoutes.Map(app);
            ProcurementBudgetRoutes.Map(app); ProcurementApprovalReadinessRoutes.Map(app); ProcurementApprovalExecutionRoutes.MapRoutes(app);
        }
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
        using (var services = fixtureServices.CreateScope())
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
                    PermissionsCsv = "procurement-access,procurement-budget-check,procurement-request-read,procurement-approval-read,procurement-authority-read,procurement-authority-review,procurement-authority-revoke" });
            if (orders)
            {
                foreach (var role in core.TenantRoleAssignments.Local)
                    if (role.RoleKey == "procurement_manager") role.PermissionsCsv += ",procurement-po-approve,procurement-po-read,procurement-po-dispatch-confirm";
                    else if (role.RoleKey == "finance_approver") role.PermissionsCsv += ",procurement-po-finance-confirm,procurement-po-read";
            }
            await core.SaveChangesAsync();
            var scope = services.ServiceProvider.GetRequiredService<ProcurementRequestScope>();
            scope.Set("probe", company.Id, people["buyer"].User.Id.ToString("D"), null, null, false, "probe-seed");
            var procurement = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var workflow = services.ServiceProvider.GetRequiredService<ApprovalDbContext>();
            if (serve || orders)
            {
                // Browser/order proof uses the actual migration history consumed by /meta.
                // A source model ahead of committed migrations must never advertise readiness.
                if (procurement.Database.HasPendingModelChanges() || workflow.Database.HasPendingModelChanges())
                    throw new InvalidOperationException("Sandbox serve mode requires committed migrations matching both Procurement and Core approval models.");
                await procurement.Database.MigrateAsync();
                await workflow.Database.MigrateAsync();
            }
            else
            {
                await procurement.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                await workflow.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            }
            procurement.AddScoped(new ProcurementCategory { Code = "IT", Name = "Synthetic IT", Status = "Active" });
            procurement.AddScoped(new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 1,
                State = "Published", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), RulesJson = Rules(orders) });
            if (orders)
            {
                var orderRules = InitialOrderRules();
                if (Mightyfin.Erp.Procurement.Application.PolicyRuleValidator.Validate(orderRules).Count != 0)
                    throw new InvalidOperationException("Synthetic published PO policy must satisfy the production rule validator.");
                procurement.AddScoped(new ProcurementMethod { Code = "DIRECT", Name = "Synthetic approved direct buy",
                    Status = "Active", ExecutionRoute = "DirectPurchase" });
                procurement.AddScoped(new PolicyVersion { PolicyKey = "PURCHASE_ORDER", PolicyVersionNumber = 1,
                    State = "Published", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), RulesJson = orderRules });
            }
            await procurement.SaveChangesAsync();
        }
        orderFeature.ManualCommitmentPolicies = [new() { LegalEntityId = company.Id, Enabled = true,
            ApprovedPolicyReference = "SYNTHETIC-APPROVED-PO-FINANCE", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), EvidenceMaximumAgeMinutes = 60 }];
        feature.ManualFinancePolicies = [new() { LegalEntityId = company.Id, Enabled = true,
            ApprovedPolicyReference = "SYNTHETIC-APPROVED-MANUAL-POLICY", EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), EvidenceMaximumAgeMinutes = 60 }];
        if (bootstrapConnection is not null)
            await RuntimePrivileges.ProvisionAndVerifyAsync(ownerConnection, connection, fixtureServices, serve || orders, orders);
        await app.StartAsync();
        return new(app, ownerConnection, company.Id, branch.Id, people["requester"].User.Id,
            people["buyer"].User.Id, people["finance"].User.Id, people["manager1"].User.Id, people["manager2"].User.Id, feature,
            serve ? new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") } : app.GetTestClient(), fixtureServices, connection, bootstrapConnection is not null);
    }

    public async Task<string> PrepareBrowserFixture(int port, bool orders = false)
    {
        await Authority(manager1, "BROWSER-AUTH-M1");
        await Authority(manager2, "BROWSER-AUTH-M2");
        var requests = new[]
        {
            await NewFundedRequest("Browser full One and All approval"),
            await NewFundedRequest("Browser return and independent Finance release"),
        };
        // The order fixture approves its PR through real HTTP commands; it never fabricates
        // an Approved source, Core case, vote or Finance evidence row for the browser.
        if (orders) await RuntimePrivileges.AssertDeniedAsync(runtimeConnection, fixtureServices);
        var orderFixture = orders ? await PrepareOrderBrowserFixture() : null;
        // Synthetic browser-only reader and reviewer confinement. These Core rows are
        // seeded by the separate fixture owner, never through runtime write grants.
        Guid reader;
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var worker = new Worker { EmployeeNo = "HTTP-settings-reader", FirstName = "Settings", LastName = "Reader",
                Status = "active", LocationId = branch };
            var user = new LocalUser { Email = "settings-reader@example.test", NormalizedEmail = "SETTINGS-READER@EXAMPLE.TEST",
                DisplayName = "Settings reader", PasswordHash = "synthetic-no-login-secret", RolesCsv = "procurement_settings_reader", WorkerId = worker.Id };
            core.Workers.Add(worker); core.LocalUsers.Add(user);
            core.TenantRoleAssignments.Add(new TenantRoleAssignment { RoleKey = "procurement_settings_reader", RoleName = "Synthetic settings reader",
                Active = true, PermissionsCsv = "procurement-access,procurement-authority-read" });
            foreach (var principal in new[] { finance, user.Id })
                core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = principal, LocationId = branch });
            await core.SaveChangesAsync(); reader = user.Id;
        }
        if (orders)
        {
            // Purpose-scoped PO browser actors are narrowed only after real PR approval
            // and grant review. Existing --serve and --orders role fixtures stay unchanged.
            using var purpose = fixtureServices.CreateScope();
            var core = purpose.ServiceProvider.GetRequiredService<HrmDbContext>();
            core.TenantRoleAssignments.Add(
                new TenantRoleAssignment { RoleKey = "browser_po_dispatcher", RoleName = "Synthetic PO dispatcher", Active = true,
                    PermissionsCsv = "procurement-access,procurement-po-dispatch-confirm" });
            // Keep the named policy assignment while restricting its action grants.
            // Replacing it with an unrelated custom role invalidates the real
            // ProcurementManager assignment despite retaining approve permission.
            var reviewerRole = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "procurement_manager");
            reviewerRole.PermissionsCsv = "procurement-access,procurement-po-approve";
            (await core.LocalUsers.SingleAsync(x => x.Id == manager1)).RolesCsv = "browser_po_dispatcher";
            var financeRole = await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "finance_approver");
            financeRole.PermissionsCsv = string.Join(',', financeRole.PermissionsCsv.Split(',').Where(x => x != "procurement-po-read"));
            await core.SaveChangesAsync();
        }
        var actors = new Dictionary<string, object>();
        foreach (var (name, principal) in new[] { ("requester", requester), ("buyer", buyer), ("finance", finance),
            ("manager1", manager1), ("manager2", manager2), ("reader", reader) })
        {
            var context = await Get(Root + "/context", principal);
            actors[name] = new { principalId = principal, context };
        }
        var meta = await Get(Root + "/meta", buyer);
        Require(meta.GetProperty("schemaReady").GetBoolean() && meta.GetProperty("capabilities").GetProperty("approvals").GetBoolean(),
            "Migrated sandbox does not expose the actual configured approval capability.");
        if (orders) Require(meta.GetProperty("capabilities").GetProperty("orders").GetBoolean(),
            "Migrated order sandbox does not expose the actual configured orders capability.");
        var manifest = new
        {
            baseUrl = $"http://127.0.0.1:{port}", tenantId = "probe", companyId = company, branchId = branch,
            currencyCode = "ZMW", authenticationHeader = "X-Probe-User", actors, requests, orders = orderFixture,
            workflow = new[]
            {
                new { code = "NEED", assignee = "LineManager", mode = "One", principals = new[] { manager1 } },
                new { code = "PROC", assignee = "ProcurementManager", mode = "All", principals = new[] { manager1, manager2 } },
            },
            settings = new { authorityReference = "BROWSER-EXACT-SCOPED-LIMIT", principalId = manager1,
                principalDisplayName = "manager1", maximumAmount = "99999999999999.9999", branchId = branch },
            warning = "Synthetic disposable sandbox only. Header authentication and fixture database owner are not production security evidence.",
        };
        var requestedPath = Environment.GetEnvironmentVariable("PROCUREMENT_APPROVAL_HTTP_PROBE_MANIFEST");
        var directory = Path.Combine(Path.GetTempPath(), "erp-approval-browser-" + Guid.NewGuid().ToString("N"));
        if (requestedPath is not null)
        {
            if (!Path.IsPathFullyQualified(requestedPath)) throw new ArgumentException("The private manifest path must be absolute.");
            directory = Path.GetDirectoryName(requestedPath)!;
            if (Directory.Exists(directory))
            {
                if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(directory) &
                    (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                     UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
                    throw new ArgumentException("An existing manifest directory must be private to its owner.");
            }
        }
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = requestedPath ?? Path.Combine(directory, "manifest.json");
        var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var output = new FileStream(path, fileOptions);
        await JsonSerializer.SerializeAsync(output, manifest, new JsonSerializerOptions(Json) { WriteIndented = true });
        return path;
    }

    public Task WaitForShutdown() => app.WaitForShutdownAsync();

    public async Task Verify()
    {
        if (distinctRuntime) await RuntimePrivileges.AssertDeniedAsync(runtimeConnection, fixtureServices);
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
        var approvedSource = await Get($"{Root}/requests/{id}", requester);
        Require(approvedSource.GetProperty("approvalInstanceRef").GetGuid() == caseId,
            "Scoped request detail lost its real approval case reference.");
        Require(approvedSource.GetProperty("lines").EnumerateArray().All(line =>
            line.GetProperty("approvedQuantity").GetDecimal() == line.GetProperty("requestedQuantity").GetDecimal()
            && line.GetProperty("approvalStatus").GetString() == "Approved"),
            "Scoped request detail does not expose persisted approved line quantities.");
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
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.TenantRoleAssignments.SingleAsync(x => x.RoleKey == "procurement_manager")).IsArchived = true;
            await core.SaveChangesAsync();
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager1, firstBody, voteKey);
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.TenantRoleAssignments.IgnoreQueryFilters().SingleAsync(x => x.TenantId == "probe"
                && x.RoleKey == "procurement_manager")).IsArchived = false;
            await core.SaveChangesAsync();
        }
        await Revoke(manager1);
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, DecisionPath(caseId, firstAssignment), manager1, firstBody, voteKey);
        await Authority(manager1, "AUTH-M1-RESTORED");
        await OtherAssigneeRevocationHidesApprove();
        await FinanceChangedReturnAndRelease();
        await ReceiptFailureRollsBackBothContexts();
        await FinalFinanceRace();
        await OffboardedHistoricalWorkersDoNotStrandFinanceRelease();
    }

    private async Task OtherAssigneeRevocationHidesApprove()
    {
        var request = await NewFundedRequest("Another assignee authority revoked");
        var opened = await Open(request); var caseId = opened.GetProperty("caseId").GetGuid();
        await Revoke(manager2);
        var detail = await Get($"{Root}/approvals/{caseId}", manager1);
        var assignment = ActiveAssignment(detail, manager1);
        var actions = assignment.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).ToArray();
        Require(!actions.Contains("Approve") && actions.Contains("Return") && actions.Contains("Reject")
            && detail.GetProperty("blockers").GetArrayLength() != 0,
            "Detail advertised approval despite a revoked other-stage assignment authority.");
        await Expect(HttpStatusCode.Conflict, HttpMethod.Post, DecisionPath(caseId, assignment), manager1,
            Decision(detail, assignment, "Approve"), Guid.NewGuid());
        await Expect(HttpStatusCode.OK, HttpMethod.Post, DecisionPath(caseId, assignment), manager1,
            Decision(detail, assignment, "Return", "Other-stage authority needs independent reconfiguration"), Guid.NewGuid());
        await Authority(manager2, "AUTH-M2-RESTORED");
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
        Guid aliasId;
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var requesterWorker = await core.LocalUsers.Where(x => x.Id == requester).Select(x => x.WorkerId).SingleAsync();
            var alias = new LocalUser { Email = "requester-alias@example.test", NormalizedEmail = "REQUESTER-ALIAS@EXAMPLE.TEST",
                DisplayName = "Requester second account", PasswordHash = "synthetic-no-login-secret",
                RolesCsv = "finance_approver", WorkerId = requesterWorker };
            core.LocalUsers.Add(alias); await core.SaveChangesAsync(); aliasId = alias.Id;
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", aliasId, body, Guid.NewGuid());
        using (var services = Scope(finance))
        {
            Require(!await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().FinanceReleaseConfirmations
                .AnyAsync(x => x.ReleaseRequestId == release.GetProperty("releaseRequestId").GetGuid()),
                "Ambiguous/requester-linked Finance account recorded a release confirmation.");
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == aliasId)).IsArchived = true;
            await core.SaveChangesAsync();
        }
        Guid decisionAliasId;
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var decisionWorker = await core.LocalUsers.Where(x => x.Id == manager1).Select(x => x.WorkerId).SingleAsync();
            var alias = new LocalUser { Email = "decision-maker-alias@example.test", NormalizedEmail = "DECISION-MAKER-ALIAS@EXAMPLE.TEST",
                DisplayName = "Decision maker second account", PasswordHash = "synthetic-no-login-secret",
                RolesCsv = "finance_approver", WorkerId = decisionWorker };
            core.LocalUsers.Add(alias); await core.SaveChangesAsync(); decisionAliasId = alias.Id;
        }
        await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", decisionAliasId, body, Guid.NewGuid());
        using (var services = Scope(finance))
        {
            Require(!await services.ServiceProvider.GetRequiredService<ProcurementDbContext>().FinanceReleaseConfirmations
                .AnyAsync(x => x.ReleaseRequestId == release.GetProperty("releaseRequestId").GetGuid()),
                "Decision-maker worker alias recorded its own release confirmation.");
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            (await core.LocalUsers.SingleAsync(x => x.Id == decisionAliasId)).IsArchived = true;
            await core.SaveChangesAsync();
        }
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

    private async Task OffboardedHistoricalWorkersDoNotStrandFinanceRelease()
    {
        var request = await NewFundedRequest("Release after historical employees leave");
        var id = request.GetProperty("id").GetGuid(); var opened = await Open(request);
        var caseId = opened.GetProperty("caseId").GetGuid();
        var detail = await Get($"{Root}/approvals/{caseId}", manager1); var assignment = ActiveAssignment(detail, manager1);
        var returned = await Send(HttpMethod.Post, DecisionPath(caseId, assignment), manager1,
            Decision(detail, assignment, "Return", "Historical worker offboarding fixture"), Guid.NewGuid());
        await Check(returned, HttpStatusCode.OK); var result = await returned.Content.ReadFromJsonAsync<JsonElement>();
        var queue = await Get(Root + "/requests/finance-release-queue", finance);
        var release = queue.GetProperty("records").EnumerateArray().Single(x => x.GetProperty("requestId").GetGuid() == id);
        var body = new { expectedRequestVersion = result.GetProperty("requestVersion").GetInt64(),
            reservationReference = release.GetProperty("reservationReference").GetString(),
            releasedAmount = release.GetProperty("reservedAmount").GetDecimal(), currencyCode = "ZMW",
            evidenceReference = "SYNTHETIC-OFFBOARD-RELEASE", confirmedAt = DateTimeOffset.UtcNow,
            reason = "Independent Finance verified full release despite historical employee offboarding" };
        using (var services = Scope(finance))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            var pin = await db.ApprovalSubmissions.AsNoTracking().SingleAsync(x => x.CoreApprovalCaseId == caseId);
            // Deliberate corrupt/legacy fixture rows exercise read-side rejection, not application update permissions.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.purchase_request_approval_submissions SET worker_identity_sha256={new string('f', 64)} WHERE id={pin.Id}");
            await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", finance, body, Guid.NewGuid());
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.purchase_request_approval_submissions SET snapshot_schema_version=1,maker_worker_id=NULL,requester_worker_id=NULL,worker_identity_sha256=NULL WHERE id={pin.Id}");
            await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", finance, body, Guid.NewGuid());
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.purchase_request_approval_submissions SET snapshot_schema_version=2,maker_worker_id={pin.MakerWorkerId},requester_worker_id={pin.RequesterWorkerId},worker_identity_sha256={pin.WorkerIdentitySha256} WHERE id={pin.Id}");
            var terminal = await db.AssignmentAuthorityEvidence.AsNoTracking().SingleAsync(x => x.CoreAssignmentId == assignment.GetProperty("id").GetGuid());
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.assignment_authority_evidence SET assignee_worker_id={Guid.NewGuid()} WHERE id={terminal.Id}");
            await Expect(HttpStatusCode.Conflict, HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", finance, body, Guid.NewGuid());
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE procurement.assignment_authority_evidence SET assignee_worker_id={terminal.AssigneeWorkerId} WHERE id={terminal.Id}");
        }
        var aliases = new List<Guid>();
        var workerIds = new List<Guid>();
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            foreach (var principal in new[] { requester, manager1 })
            {
                var user = await core.LocalUsers.SingleAsync(x => x.Id == principal); workerIds.Add(user.WorkerId!.Value);
                user.IsActive = false;
                var alias = new LocalUser { Email = "offboard-alias-" + principal + "@example.test",
                    NormalizedEmail = ("OFFBOARD-ALIAS-" + principal + "@EXAMPLE.TEST").ToUpperInvariant(),
                    DisplayName = "Historical worker active alias", PasswordHash = "synthetic-no-login-secret",
                    RolesCsv = "finance_approver", WorkerId = user.WorkerId };
                core.LocalUsers.Add(alias); aliases.Add(alias.Id);
            }
            await core.SaveChangesAsync();
        }
        foreach (var alias in aliases)
            await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", alias, body, Guid.NewGuid());
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            foreach (var worker in await core.Workers.Where(x => workerIds.Contains(x.Id)).ToArrayAsync()) worker.Status = "terminated";
            await core.SaveChangesAsync();
        }
        var confirmation = await Send(HttpMethod.Post, $"{Root}/requests/{id}/finance-release-confirmations", finance, body, Guid.NewGuid());
        await Check(confirmation, HttpStatusCode.OK);
        Require((await confirmation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestState").GetString() == "ReturnedForChanges",
            "Historical offboarding stranded independent Finance release.");
        // Restore only this disposable fixture so later operator/browser scenarios can reuse its actors.
        using (var services = fixtureServices.CreateScope())
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            foreach (var user in await core.LocalUsers.Where(x => x.Id == requester || x.Id == manager1).ToArrayAsync()) user.IsActive = true;
            foreach (var user in await core.LocalUsers.Where(x => aliases.Contains(x.Id)).ToArrayAsync()) user.IsArchived = true;
            foreach (var worker in await core.Workers.Where(x => workerIds.Contains(x.Id)).ToArrayAsync()) worker.Status = "active";
            await core.SaveChangesAsync();
        }
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

    private async Task Authority(Guid principal, string reference, string action = "PurchaseRequest.Approve")
    {
        var path = Root + "/configuration/spending-authorities";
        var created = await Send(HttpMethod.Post, path, buyer, new { authorityReference = reference, principalId = principal.ToString("D"), action,
            draft = new { currencyCode = "ZMW", amountBasis = action == "PurchaseOrder.Approve" ? "ApprovedLineBase" : "EstimatedLineBase", maximumAmount = 1000,
                effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1), effectiveUntil = DateTimeOffset.UtcNow.AddDays(1), allBranches = true,
                allDepartments = true, allCategories = true } }, selectedBranch: false);
        await Check(created, HttpStatusCode.Created); var grant = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = grant.GetProperty("id").GetGuid();
        var submitted = await Send(HttpMethod.Post, path + "/" + id + "/submit", buyer, new { version = grant.GetProperty("version").GetInt64() }, selectedBranch: false);
        await Check(submitted, HttpStatusCode.OK); var pending = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        if (reference == "AUTH-M1")
        {
            foreach (var participant in new[] { principal, buyer })
            {
                Guid aliasId;
                using (var services = fixtureServices.CreateScope())
                {
                    var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
                    var workerId = await core.LocalUsers.Where(x => x.Id == participant).Select(x => x.WorkerId).SingleAsync();
                    var alias = new LocalUser { Email = "review-alias-" + participant + "@example.test",
                        NormalizedEmail = ("REVIEW-ALIAS-" + participant + "@EXAMPLE.TEST").ToUpperInvariant(),
                        DisplayName = "Authority participant second account", PasswordHash = "synthetic-no-login-secret",
                        RolesCsv = "finance_approver", WorkerId = workerId };
                    core.LocalUsers.Add(alias); await core.SaveChangesAsync(); aliasId = alias.Id;
                }
                foreach (var operation in new[] { "activate", "return", "supersede" })
                    await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, path + "/" + id + "/" + operation, aliasId,
                        new { version = pending.GetProperty("version").GetInt64(), reason = "Alias must not review its own worker",
                            previousGrantId = id, previousVersion = pending.GetProperty("version").GetInt64() }, selectedBranch: false);
                await Expect(HttpStatusCode.Forbidden, HttpMethod.Post, path + "/" + id + "/activate", finance,
                    new { version = pending.GetProperty("version").GetInt64(), reason = "Ambiguous participant identity must block review" }, selectedBranch: false);
                using var cleanup = fixtureServices.CreateScope();
                var cleanupCore = cleanup.ServiceProvider.GetRequiredService<HrmDbContext>();
                (await cleanupCore.LocalUsers.SingleAsync(x => x.Id == aliasId)).IsArchived = true;
                await cleanupCore.SaveChangesAsync();
            }
        }
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
    private async Task<JsonElement> NewFundedRequest(string purpose, decimal quantity = 1m,
        decimal unitPrice = 100m, decimal checkedAmount = 100m)
    {
        var created = await Send(HttpMethod.Post, Root + "/requests", requester, new { purpose, currencyCode = "ZMW",
            neededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), lines = new[] { new { description = "Synthetic item", unitCode = "each",
                requestedQuantity = quantity, estimatedUnitPrice = unitPrice, categoryCode = "IT" } } });
        await Check(created, HttpStatusCode.Created); var draft = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = draft.GetProperty("id").GetGuid();
        var submitted = await Send(HttpMethod.Post, $"{Root}/requests/{id}/submit", requester, new { version = draft.GetProperty("version").GetInt64() });
        await Check(submitted, HttpStatusCode.OK); var request = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        await Check(await BudgetResponse(id, request.GetProperty("version").GetInt64(), "Available", checkedAmount), HttpStatusCode.Created); return request;
    }
    private async Task<JsonElement> Open(JsonElement request)
    {
        var response = await Send(HttpMethod.Post, $"{Root}/requests/{request.GetProperty("id").GetGuid()}/approval-cases", buyer,
            new { expectedRequestVersion = request.GetProperty("version").GetInt64() }, Guid.NewGuid());
        await Check(response, HttpStatusCode.Created); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private Task<HttpResponseMessage> BudgetResponse(Guid id, long version, string status, decimal checkedAmount = 100m)
        => Send(HttpMethod.Post, $"{Root}/requests/{id}/budget-checks", finance, new
        {
            requestVersion = version, financeCheckRef = "CHECK-" + Guid.NewGuid(), status, checkedAmount,
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
        var services = fixtureServices.CreateScope(); services.ServiceProvider.GetRequiredService<ProcurementRequestScope>()
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
    private static string Rules(bool direct = false) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, transactionType = "PurchaseRequest", currencyCode = "ZMW", amountBasis = "EstimatedLineBase",
        rules = new[] { new { code = "FALLBACK", priority = 0, when = new { categoryCodes = Array.Empty<string>(), purchaseTypes = Array.Empty<string>(), branchIds = Array.Empty<Guid>() },
            result = new { budgetReservation = "Required", sourcingMethodCode = direct ? "DIRECT" : (string?)null, minimumQuoteCount = direct ? 0 : (int?)null,
                approvalStages = new[] { new { code = "NEED", assignee = "LineManager", mode = "One" }, new { code = "PROC", assignee = "ProcurementManager", mode = "All" } } } } },
    });
    public async ValueTask DisposeAsync() { client.Dispose(); await app.DisposeAsync(); await fixtureServices.DisposeAsync(); }
}
sealed class Tenant : ITenantAccessor { public string GetTenantId() => "probe"; }
sealed class ProbeAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, HrmDbContext core) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Probe-User"], out var id)) return AuthenticateResult.NoResult();
        var user = await core.LocalUsers.AsNoTracking().Where(x => x.Id == id && x.IsActive && !x.IsArchived)
            .Select(x => new { x.RolesCsv }).SingleOrDefaultAsync();
        if (user is null) return AuthenticateResult.Fail("Inactive fixture account.");
        var actions = await ProcurementPermissions.ResolveConfiguredActionsAsync(core, user.RolesCsv, Context.RequestAborted);
        var claims = new List<Claim> { new("tenant", "probe"), new("sub", id.ToString("D")), new(ProcurementPermissions.ConfiguredClaim, "true") };
        claims.AddRange(user.RolesCsv.Split(',').Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(actions.Select(action => new Claim(ProcurementPermissions.ActionClaim, action)));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "probe")), "probe"));
    }
}
