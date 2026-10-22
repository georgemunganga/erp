using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;

namespace ProcurementAuthorityHttpProbe;

internal static class Program
{
    private const string Tenant = "authority-http-probe";
    private const string Root = "/api/procurement/v1/configuration/spending-authorities";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid();
    private static readonly Guid Maker = Guid.NewGuid(), Recipient = Guid.NewGuid(), Reviewer = Guid.NewGuid();
    private static readonly Guid MakerWorker = Guid.NewGuid(), RecipientWorker = Guid.NewGuid(), ReviewerWorker = Guid.NewGuid();
    private static string ownerConnection = "";
    private static string runtimeConnection = "";
    private static string runtimeRole = "";
    private static string stage = "validate-input";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Check(args.Length == 0, "No CLI modes or arbitrary database names are accepted.");
            ownerConnection = Environment.GetEnvironmentVariable("ConnectionStrings__Procurement")
                ?? throw new ProbeFailure("An explicit fresh private owner database is required.");
            var settings = new NpgsqlConnectionStringBuilder(ownerConnection);
            Check(settings.Host is "localhost" or "127.0.0.1" or "::1" or "/authority-identity-private-socket",
                "Only private socket or loopback is allowed.");
            Check(settings.Database is not null && settings.Database.StartsWith("authority_identity_http_", StringComparison.Ordinal)
                && settings.Database.Length <= 63 && settings.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'),
                "A fresh authority_identity_http_ database is required. Production is forbidden.");
            stage = "require-fresh-database"; await RequireFreshDatabase();
            stage = "bootstrap-Core-P29-P30"; var legacy = await Bootstrap();
            stage = "provision-finite-role"; await ProvisionFiniteRole(settings);
            await using var app = await StartHost();
            using var client = app.GetTestClient();
            stage = "finite-runtime-readiness-and-SQL-denials"; await CheckRuntimeBoundary(app);
            await CheckLegacy(client, legacy);
            var first = await Create(client, "HTTP-WORKER-SWAP");
            await Actions(client, first.GetProperty("id").GetGuid(), Maker, "Edit", "Submit");
            var submitted = await Post(client, $"/{first.GetProperty("id").GetGuid()}/submit", Maker,
                new { version = first.GetProperty("version").GetInt64() }, HttpStatusCode.OK);
            var id = submitted.GetProperty("id").GetGuid();
            await Actions(client, id, Reviewer, "Return", "Activate");
            // Both accounts remain active, distinct and correctly scoped; exchanging
            // workers defeats a comparison that only consults today's maker mapping.
            await BindWorkers(ReviewerWorker, MakerWorker);
            var swapped = await Footprint();
            var denial = await Post(client, $"/{id}/activate", Reviewer,
                new { version = submitted.GetProperty("version").GetInt64(), reason = "Original maker cannot review through another account" }, HttpStatusCode.Forbidden);
            Check(denial.GetProperty("code").GetString() == "AuthorityIndependentWorkerRequired", "Swap denial came from an unrelated guard.");
            Check(await Footprint() == swapped, "Denied swap changed persisted state.");
            var hidden = await Get(client, $"/{id}", Reviewer);
            Check(!Allowed(hidden, "Activate") && !Allowed(hidden, "Return"), "Swapped maker was offered a review action.");
            await BindWorkers(MakerWorker, ReviewerWorker);
            var active = await Post(client, $"/{id}/activate", Reviewer,
                new { version = submitted.GetProperty("version").GetInt64(), reason = "Independent reviewer confirmed the exact submitted authority" }, HttpStatusCode.OK);
            Check(active.GetProperty("state").GetString() == "Active", "Restored independent activation did not succeed.");
            await Actions(client, id, Reviewer, "Revoke");
            await VerifyLineage(id, MakerWorker, RecipientWorker, ReviewerWorker);
            // A historical maker need not be reactivated for an independent reviewer.
            var second = await Create(client, "HTTP-OFFBOARDED-MAKER");
            var secondId = second.GetProperty("id").GetGuid();
            var pending = await Post(client, $"/{secondId}/submit", Maker,
                new { version = second.GetProperty("version").GetInt64() }, HttpStatusCode.OK);
            await SetMakerActive(false);
            await Actions(client, secondId, Reviewer, "Return", "Activate");
            await Post(client, $"/{secondId}/activate", Reviewer,
                new { version = pending.GetProperty("version").GetInt64(), reason = "Independent review after maker offboarding" }, HttpStatusCode.OK);
            await VerifyLineage(secondId, MakerWorker, RecipientWorker, ReviewerWorker);
            stage = "rollback-only-ownership-readiness-denials"; await OwnershipReadinessDenials(app);
            await using (var proof = Owner(Reviewer))
            {
                Check(await proof.SpendingAuthorityGrants.CountAsync() == 4 && await proof.SpendingAuthorityIdentityRecords.CountAsync() == 6,
                    "Unexpected extra grant or identity action rows.");
                Check(!await proof.PurchaseRequests.AnyAsync() && !await proof.PurchaseOrders.AnyAsync()
                    && !await proof.SourcingEvents.AnyAsync() && !await proof.SupplierInvoices.AnyAsync(), "Configuration probe caused unrelated purchasing records.");
            }
            await app.StopAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS", database = settings.Database, runtimeRole,
                finiteRuntime = true, actualMappedHttp = true, restoredWorkerSwap = true, offboardedMakerIndependentReview = true,
                newGrants = 2, retainedIdentityRows = 6, legacyReadAndRevoke = true, ownershipReadinessDenials = 2,
                recipientEligibility = "NOT EXERCISED: no bounded public eligibility endpoint; internal helper is not exposed",
                production = false, retainedForensics = true }, Json));
            return 0;
        }
        catch (Exception error)
        {
            // Do not print connection strings, SQL text or randomly generated role secrets.
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "FAIL", exception = error.GetType().Name,
                stage, sqlState = FindPostgres(error)?.SqlState, message = error is ProbeFailure ? error.Message : "See guarded execution stage; no connection/SQL data logged" }));
            return 1;
        }
    }

    private static HrmDbContext Core() => new(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(ownerConnection).Options, new ToolTenant());
    private static ProcurementDbContext Owner(Guid actor) => new(new DbContextOptionsBuilder<ProcurementDbContext>()
        .UseNpgsql(ownerConnection, n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options,
        new ToolScope(actor, actor == Reviewer ? ReviewerWorker : MakerWorker));
    private static async Task RequireFreshDatabase()
    {
        await using var connection = new NpgsqlConnection(ownerConnection); await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT NOT EXISTS(SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%' AND c.relkind IN ('r','p'))", connection);
        Check(await command.ExecuteScalarAsync() is true, "Refusing a nonempty database; this tool never deletes, resets or resumes one.");
    }

    private static async Task<(Guid Draft, Guid Active)> Bootstrap()
    {
        await using (var core = Core())
        {
            Check(await core.Database.EnsureCreatedAsync(), "Core bootstrap did not create the fresh owning schema.");
            core.LegalEntities.Add(new LegalEntity { Id = Company, Code = "HTTP", RegisteredName = "Synthetic authority company", Currency = "ZMW", IsDefault = true });
            core.WorkLocations.Add(new WorkLocation { Id = Branch, Code = "MAIN", Name = "Synthetic branch", LegalEntityId = Company });
            foreach (var (principal, worker, label) in new[] { (Maker, MakerWorker, "maker"), (Recipient, RecipientWorker, "recipient"), (Reviewer, ReviewerWorker, "reviewer") })
            {
                core.Workers.Add(new Worker { Id = worker, EmployeeNo = "AUTH-" + label, FirstName = label, LastName = "Fixture", Status = "active", LocationId = Branch });
                core.LocalUsers.Add(new LocalUser { Id = principal, WorkerId = worker, Email = label + "@example.test", NormalizedEmail = label.ToUpperInvariant() + "@EXAMPLE.TEST",
                    DisplayName = label, IsActive = true, RolesCsv = "erp_admin", PasswordHash = "synthetic-no-login-secret" });
                core.UserBranchAssignments.Add(new HrUserBranchAssignment { UserId = principal, LocationId = Branch });
            }
            await core.SaveChangesAsync();
        }
        var at = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
        ProcurementSpendingAuthorityGrant Legacy(string reference) => SpendingAuthorityConfiguration.CreateDraft(reference, Recipient.ToString("D"),
            new SpendingAuthorityDraft("ZMW", "EstimatedLineBase", 12.3456m, at.AddDays(-1), at.AddDays(1), AllBranches: false, BranchRef: Branch, AllDepartments: true, AllCategories: true),
            new(Tenant, Company, Maker.ToString("D"), true, new HashSet<string> { SpendingAuthorityConfiguration.ConfigurePermission }), at, []);
        var draft = Legacy("HTTP-LEGACY-DRAFT"); var active = Legacy("HTTP-LEGACY-ACTIVE");
        await using (var db = Owner(Maker))
        {
            await db.GetService<IMigrator>().MigrateAsync("P29SourcingPolicyIdentity");
            db.AddScoped(draft); db.AddScoped(active); await db.SaveChangesAsync();
            SpendingAuthorityConfiguration.Submit(active, active.Version,
                new(Tenant, Company, Maker.ToString("D"), true, new HashSet<string> { SpendingAuthorityConfiguration.ConfigurePermission }),
                new(Tenant, Company, Recipient.ToString("D"), true, true), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await using (var db = Owner(Reviewer))
        {
            var old = await db.SpendingAuthorityGrants.SingleAsync(x => x.Id == active.Id);
            SpendingAuthorityConfiguration.Activate(old, old.Version,
                new(Tenant, Company, Reviewer.ToString("D"), true, new HashSet<string> { SpendingAuthorityConfiguration.ReviewPermission }),
                new(Tenant, Company, Recipient.ToString("D"), true, true), "Synthetic pre-P30 domain fixture", DateTimeOffset.UtcNow, [old]);
            await db.SaveChangesAsync();
        }
        await using (var db = Owner(Maker))
        {
            var before = JsonSerializer.Serialize(await db.SpendingAuthorityGrants.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), Json);
            await db.Database.MigrateAsync();
            Check(before == JsonSerializer.Serialize(await db.SpendingAuthorityGrants.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(), Json), "P30 changed retained legacy grants.");
            Check(!await db.SpendingAuthorityIdentityRecords.AnyAsync(), "Migration fabricated legacy original worker evidence.");
        }
        return (draft.Id, active.Id);
    }

    private static async Task ProvisionFiniteRole(NpgsqlConnectionStringBuilder owner)
    {
        runtimeRole = "authority_identity_http_runtime_" + Guid.NewGuid().ToString("N")[..20];
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await using var connection = new NpgsqlConnection(ownerConnection); await connection.OpenAsync();
        await Sql(connection, "CREATE ROLE " + Quote(runtimeRole) + " LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS PASSWORD " + Literal(secret));
        await Sql(connection, "REVOKE CREATE ON SCHEMA hrm,procurement,public FROM PUBLIC");
        await Sql(connection, "GRANT CONNECT ON DATABASE " + Quote(owner.Database!) + " TO " + Quote(runtimeRole));
        await Sql(connection, "GRANT USAGE ON SCHEMA hrm,procurement TO " + Quote(runtimeRole));
        await using (var core = Core())
        {
            foreach (var type in new[] { typeof(LegalEntity), typeof(WorkLocation), typeof(OrgUnit), typeof(Worker),
                typeof(LocalUser), typeof(HrUserBranchAssignment), typeof(Mightyfin.Erp.Hrm.Application.ConfigAndExtras.TenantRoleAssignment) })
            {
                var entity = core.Model.FindEntityType(type) ?? throw new ProbeFailure("Required Core read model is missing.");
                await Sql(connection, "GRANT SELECT ON " + Quote(entity.GetSchema()!) + "." + Quote(entity.GetTableName()!) + " TO " + Quote(runtimeRole));
            }
        }
        await using (var procurement = Owner(Maker))
        {
            foreach (var type in new[] { typeof(ProcurementSpendingAuthorityGrant), typeof(ProcurementSpendingAuthorityCategory),
                typeof(SpendingAuthorityIdentityRecord), typeof(Mightyfin.Erp.Procurement.Domain.AuditEvent), typeof(OutboxEvent), typeof(ProcurementCategory) })
            {
                var entity = procurement.Model.FindEntityType(type) ?? throw new ProbeFailure("Required Procurement read model is missing.");
                await Sql(connection, "GRANT SELECT ON " + Quote(entity.GetSchema()!) + "." + Quote(entity.GetTableName()!) + " TO " + Quote(runtimeRole));
            }
        }
        await Sql(connection, "GRANT SELECT ON procurement.__procurement_migrations TO " + Quote(runtimeRole));
        await Sql(connection, "GRANT INSERT,UPDATE ON procurement.spending_authority_grants TO " + Quote(runtimeRole));
        await Sql(connection, "GRANT INSERT,DELETE ON procurement.spending_authority_categories TO " + Quote(runtimeRole));
        await Sql(connection, "GRANT INSERT ON procurement.spending_authority_identity_evidence,procurement.audit_events,procurement.outbox_events TO " + Quote(runtimeRole));
        var runtime = new NpgsqlConnectionStringBuilder(ownerConnection) { Username = runtimeRole, Password = secret };
        // Private trust socket uses no password; never reuse the owner's password/passfile.
        runtime.Remove("Passfile");
        if (runtime.Host == "/authority-identity-private-socket") runtime.Remove("Password");
        runtimeConnection = runtime.ConnectionString;
    }

    private static async Task<WebApplication> StartHost()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddScoped<ITenantAccessor>(_ => new ToolTenant());
        builder.Services.AddDbContext<HrmDbContext>(o => o.UseNpgsql(runtimeConnection));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:ProcurementApprovalExecution"] = runtimeConnection });
        ProcurementHostIntegration.AddServices(builder.Services, runtimeConnection, builder.Configuration);
        builder.Services.AddAuthentication("private-authority-probe").AddScheme<AuthenticationSchemeOptions, ToolAuthentication>("private-authority-probe", _ => { });
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseMiddleware<ProcurementScopeMiddleware>();
        ProcurementSpendingAuthorityRoutes.MapRoutes(app); ProcurementContextRoutes.MapRoutes(app);
        await app.StartAsync(); return app;
    }

    private static async Task CheckRuntimeBoundary(WebApplication app)
    {
        using var services = app.Services.CreateScope();
        services.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set(Tenant, Company, Maker.ToString("D"), Branch, null, true, "private-runtime-proof");
        var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        var provider = services.ServiceProvider.GetRequiredService<IProcurementSpendingAuthorityIdentitySchemaReadiness>();
        Check(provider is ProcurementSpendingAuthorityIdentitySchemaReadiness, "The normal actual readiness provider was replaced.");
        Check(await provider.IsReadyAsync(db, default) && await provider.CanRecordAsync(db, default), "Finite runtime owning-store readiness is false.");
        Check(!db.ChangeTracker.HasChanges(), "Readiness staged a database mutation.");
        await using var connection = new NpgsqlConnection(runtimeConnection); await connection.OpenAsync();
        await using var coreModel = Core();
        var userEntity = coreModel.Model.FindEntityType(typeof(LocalUser))!;
        var userTable = StoreObjectIdentifier.Table(userEntity.GetTableName()!, userEntity.GetSchema());
        var workerColumn = Quote(userEntity.FindProperty(nameof(LocalUser.WorkerId))!.GetColumnName(userTable)!);
        foreach (var sql in new[] {
            "UPDATE " + Quote(userTable.Schema!) + "." + Quote(userTable.Name) + " SET " + workerColumn + "=" + workerColumn + " WHERE false",
            "DELETE FROM procurement.spending_authority_identity_evidence WHERE false",
            "UPDATE procurement.spending_authority_identity_evidence SET evidence_sha256=evidence_sha256 WHERE false",
            "CREATE TABLE procurement.authority_probe_forbidden(id integer)" })
        {
            try { await Sql(connection, sql); throw new ProbeFailure("Finite runtime unexpectedly executed forbidden Core/evidence/DDL SQL."); }
            catch (PostgresException error) when (error.SqlState == "42501") { }
        }
    }

    private static async Task<JsonElement> Create(HttpClient client, string reference)
        => await Post(client, "", Maker, new { authorityReference = reference, principalId = Recipient.ToString("D"), action = "PurchaseRequest.Approve",
            draft = new { currencyCode = "ZMW", amountBasis = "EstimatedLineBase", maximumAmount = "12.3456",
                effectiveFrom = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1), TimeSpan.Zero), effectiveUntil = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1), TimeSpan.Zero),
                allBranches = false, branchRef = Branch, allDepartments = true, departmentRef = (Guid?)null,
                allCategories = true, categoryCodes = Array.Empty<string>() } }, HttpStatusCode.Created);

    private static async Task CheckLegacy(HttpClient client, (Guid Draft, Guid Active) legacy)
    {
        var draft = await Get(client, $"/{legacy.Draft}", Maker);
        Check(!Allowed(draft, "Edit") && !Allowed(draft, "Submit"), "Legacy draft was offered a worker-bound mutation.");
        var before = await Footprint();
        var denied = await Post(client, $"/{legacy.Draft}/submit", Maker, new { version = draft.GetProperty("version").GetInt64() }, HttpStatusCode.Conflict);
        Check(denied.GetProperty("code").GetString() == "AuthorityHistoricalIdentityMissing", "Legacy mutation failed for an unrelated reason.");
        Check(before == await Footprint(), "Denied legacy submission changed persisted state.");
        var active = await Get(client, $"/{legacy.Active}", Reviewer); Check(Allowed(active, "Revoke"), "Legacy authorized revoke was hidden.");
        var revoked = await Post(client, $"/{legacy.Active}/revoke", Reviewer, new { version = active.GetProperty("version").GetInt64(), reason = "Retire legacy unknown worker authority" }, HttpStatusCode.OK);
        Check(revoked.GetProperty("state").GetString() == "Revoked", "Historical revoke did not preserve its controlled outcome.");
        await using var owner = Owner(Reviewer);
        Check(!await owner.SpendingAuthorityIdentityRecords.AnyAsync(x => x.GrantId == legacy.Draft || x.GrantId == legacy.Active), "Legacy read/revoke fabricated an original worker.");
    }

    private static async Task Actions(HttpClient client, Guid id, Guid actor, params string[] required)
    {
        var detail = await Get(client, $"/{id}", actor);
        foreach (var action in required) Check(Allowed(detail, action), "Actual allowedActions omitted " + action + ".");
    }
    private static bool Allowed(JsonElement detail, string action) => detail.GetProperty("allowedActions").EnumerateArray().Any(x => x.GetString() == action);
    private static Task<JsonElement> Get(HttpClient client, string path, Guid actor) => Send(client, HttpMethod.Get, path, actor, null, HttpStatusCode.OK);
    private static Task<JsonElement> Post(HttpClient client, string path, Guid actor, object body, HttpStatusCode expected) => Send(client, HttpMethod.Post, path, actor, body, expected);
    private static async Task<JsonElement> Send(HttpClient client, HttpMethod method, string path, Guid actor, object? body, HttpStatusCode expected)
    {
        stage = $"HTTP {method} {path}";
        using var request = new HttpRequestMessage(method, Root + path);
        request.Headers.Add("X-Authority-Probe-Actor", actor.ToString("D")); request.Headers.Add("X-Shell-Entity", Company.ToString("D")); request.Headers.Add("X-Shell-Location", Branch.ToString("D"));
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await client.SendAsync(request);
        Check(response.StatusCode == expected, $"Mapped {method} {path} expected {(int)expected}, received {(int)response.StatusCode}.");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task BindWorkers(Guid makerWorker, Guid reviewerWorker)
    {
        await using var core = Core();
        (await core.LocalUsers.SingleAsync(x => x.Id == Maker)).WorkerId = makerWorker;
        (await core.LocalUsers.SingleAsync(x => x.Id == Reviewer)).WorkerId = reviewerWorker;
        await core.SaveChangesAsync();
    }
    private static async Task SetMakerActive(bool active)
    {
        await using var core = Core();
        (await core.LocalUsers.SingleAsync(x => x.Id == Maker)).IsActive = active;
        (await core.Workers.SingleAsync(x => x.Id == MakerWorker)).Status = active ? "active" : "terminated";
        await core.SaveChangesAsync();
    }
    private static async Task VerifyLineage(Guid id, Guid maker, Guid recipient, Guid reviewer)
    {
        await using var db = Owner(Reviewer);
        var grant = await db.SpendingAuthorityGrants.AsNoTracking().SingleAsync(x => x.Id == id);
        var rows = await db.SpendingAuthorityIdentityRecords.AsNoTracking().Where(x => x.GrantId == id).ToArrayAsync();
        Check(rows.Length == 3 && grant.State == "Active" && grant.Version == 3, "Actual HTTP workflow did not retain three exact stages.");
        var created = SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Created"));
        var submitted = SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Submitted"));
        var activated = SpendingAuthorityIdentityStorage.Read(rows.Single(x => x.Stage == "Activated"));
        SpendingAuthorityIdentity.VerifyActivation(grant, created, submitted, activated);
        Check(created.ActorWorkerId == maker && submitted.ActorWorkerId == maker && activated.ActorWorkerId == reviewer
            && rows.All(x => x.RecipientWorkerId == recipient) && rows.All(x => x.CreatedAt == x.ActionAt && x.Version == 1),
            "Retained worker, immutable metadata or timestamp proof differs from actual Core people.");
    }

    private static async Task OwnershipReadinessDenials(WebApplication app)
    {
        // Private owner-only rollback DDL. Revoked DML alone must not certify an
        // evidence/Core table owner that can ALTER or regrant its own privileges.
        using var core = Core(); var userType = core.Model.FindEntityType(typeof(LocalUser))!;
        var coreTable = Quote(userType.GetSchema()!) + "." + Quote(userType.GetTableName()!);
        foreach (var table in new[] { "procurement.spending_authority_identity_evidence", coreTable })
        {
            var before = await Footprint();
            await using var connection = new NpgsqlConnection(ownerConnection); await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                await Sql(connection, "ALTER TABLE " + table + " OWNER TO " + Quote(runtimeRole), transaction);
                await Sql(connection, "REVOKE INSERT,UPDATE,DELETE,TRUNCATE ON " + table + " FROM " + Quote(runtimeRole), transaction);
                await Sql(connection, "SET LOCAL ROLE " + Quote(runtimeRole), transaction);
                await using (var acl = new NpgsqlCommand("SELECT NOT (has_table_privilege(current_user," + Literal(table) + ",'INSERT') OR has_any_column_privilege(current_user," + Literal(table) + ",'INSERT') OR has_table_privilege(current_user," + Literal(table) + ",'UPDATE') OR has_any_column_privilege(current_user," + Literal(table) + ",'UPDATE') OR has_table_privilege(current_user," + Literal(table) + ",'DELETE') OR has_table_privilege(current_user," + Literal(table) + ",'TRUNCATE'))", connection, transaction))
                    Check(await acl.ExecuteScalarAsync() is true, "Ownership negative still has forbidden DML, so it does not isolate ownership.");
                await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
                    n => n.MigrationsHistoryTable("__procurement_migrations", "procurement")).Options, new ToolScope(Reviewer, ReviewerWorker));
                await db.Database.UseTransactionAsync(transaction);
                var provider = app.Services.GetRequiredService<IProcurementSpendingAuthorityIdentitySchemaReadiness>();
                Check(!await provider.IsReadyAsync(db, default), "Table ownership with revoked DML incorrectly advertised read readiness.");
            }
            finally { await transaction.RollbackAsync(); }
            Check(before == await Footprint(), "Ownership readiness rehearsal changed schema/ACL/data after rollback.");
        }
    }

    private static async Task<string> Footprint()
    {
        await using var connection = new NpgsqlConnection(ownerConnection); await connection.OpenAsync();
        var tables = new List<(string Schema, string Name)>();
        await using (var command = new NpgsqlCommand("SELECT n.nspname,c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('hrm','procurement') AND c.relkind IN ('r','p') ORDER BY n.nspname,c.relname", connection))
        await using (var rows = await command.ExecuteReaderAsync()) while (await rows.ReadAsync()) tables.Add((rows.GetString(0), rows.GetString(1)));
        var queries = tables.Select(x => "SELECT " + Literal(x.Schema + "." + x.Name) + " name,coalesce(jsonb_agg(j ORDER BY j::text),'[]'::jsonb) rows FROM (SELECT to_jsonb(r) j FROM " + Quote(x.Schema) + "." + Quote(x.Name) + " r) source_rows");
        var sql = "SELECT jsonb_build_object('data',(SELECT jsonb_object_agg(name,rows) FROM (" + string.Join(" UNION ALL ", queries) + ") all_rows),'catalog',(SELECT jsonb_agg(jsonb_build_array(n.nspname,c.relname,c.relowner,c.relacl) ORDER BY n.nspname,c.relname) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname IN ('hrm','procurement')))::text";
        await using var snapshot = new NpgsqlCommand(sql, connection);
        return ProcurementEvidenceEncoding.Digest((string)(await snapshot.ExecuteScalarAsync())!);
    }
    private static async Task Sql(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    { await using var command = new NpgsqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync(); }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    private static void Check(bool value, string message) { if (!value) throw new ProbeFailure(message); }
    private static PostgresException? FindPostgres(Exception error) => error is PostgresException pg ? pg : error.InnerException is null ? null : FindPostgres(error.InnerException);
    private sealed class ProbeFailure(string message) : InvalidOperationException(message);
    private sealed class ToolTenant : ITenantAccessor { public string GetTenantId() => Tenant; }
    private sealed class ToolScope(Guid actor, Guid worker) : IProcurementScope
    {
        public string TenantId => Tenant; public Guid LegalEntityId => Company; public string SubjectId => actor.ToString("D");
        public Guid? WorkerId => worker; public Guid? WorkLocationId => Branch; public Guid? OrgUnitId => null;
        public bool IsConfined => false; public string? CorrelationId => "private-authority-http";
    }
    private sealed class ToolAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Authority-Probe-Actor"], out var actor) || (actor != Maker && actor != Recipient && actor != Reviewer))
                return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("tenant", Tenant), new Claim("sub", actor.ToString("D")), new Claim(ClaimTypes.Role, "erp_admin") }, "private-authority-probe"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, "private-authority-probe")));
        }
    }
}
