using Mightyfin.Erp.Hrm.Api.Routing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Application.Performance;
using Mightyfin.Erp.Hrm.Application.Offboarding;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Application.Integration;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging: structured JSON in production, console otherwise ----------
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        o.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
    });
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

builder.Services.AddControllers();
builder.Services.AddOpenApi("hrm");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

// ---------- Postgres ----------
var connStr = builder.Configuration.GetConnectionString("Hrm");
if (string.IsNullOrEmpty(connStr))
    throw new InvalidOperationException("ConnectionStrings:Hrm is not configured.");
// Npgsql does not accept libpq URI query parameters (e.g. ?sslmode=disable), so normalize
// the string to the keyword/value format it understands.
if (connStr.StartsWith("postgresql", StringComparison.OrdinalIgnoreCase) && connStr.Contains('?'))
    connStr = NpgsqlConnectionStringNormalizer.Normalize(connStr);
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddDbContext<HrmDbContext>((services, options) =>
{
    options.UseNpgsql(connStr, npgsql => npgsql.MigrationsHistoryTable("__hrm_migrations", "hrm"));
    // The repository snapshot predates several additive model changes. The
    // migration runner must still apply explicit migrations in standalone mode.
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    options.AddInterceptors(services.GetRequiredService<AuditInterceptor>());
});

// ---------- Tenant / auth principal ----------
builder.Services.AddHttpContextAccessor();
// M51: identity provisioning (first-user auto-elevation + admin invitation
// role assignment) talks to the organisation's Keycloak admin REST API
// through the internal docker network. Best-effort by design.
builder.Services.AddHttpClient("keycloak-admin");
builder.Services.AddScoped<IIdentityProvisioningService, IdentityProvisioningService>();
builder.Services.AddScoped<ITenantAccessor, PrincipalTenantAccessor>();
// M44 branch scoping: per-request work scope (entity + branch) resolved from
// X-Shell-Location / X-Shell-Entity headers before any handler runs.
builder.Services.AddScoped<ShellContext>();
builder.Services.AddScoped<IAuthzService, AuthzServiceImpl>();
builder.Services.AddSingleton<IIdProvider, IdProvider>();

// ---------- Services ----------
builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();
builder.Services.AddScoped<IWorkerService, WorkerServiceImpl>();
builder.Services.AddScoped<IWorkerImportService, WorkerImportService>();
// M31: shared import/export engine — every importable type registers a schema
// here and reuses the same map-columns / preview / apply / export flow.
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Shared.IImportSchema, Mightyfin.Erp.Hrm.Application.Shared.WorkersImportSchema>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Shared.IImportSchema, Mightyfin.Erp.Hrm.Application.Shared.AttendanceImportSchema>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Shared.IImportSchema, Mightyfin.Erp.Hrm.Application.Shared.PayrollProfilesImportSchema>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Shared.IImportExportService, Mightyfin.Erp.Hrm.Application.Shared.ImportExportServiceImpl>();
builder.Services.AddScoped<IWorkerResolver, WorkerResolver>();
builder.Services.AddScoped<IWorkerLifecycleService, WorkerLifecycleServiceImpl>();
builder.Services.AddScoped<ITimeRepository, TimeRepository>();
builder.Services.AddScoped<ITimeService, TimeServiceImpl>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddSingleton<ILetterTemplates, LetterTemplatesImpl>();
builder.Services.AddScoped<IMergeDataProvider, MergeDataProviderImpl>();
builder.Services.AddScoped<ILeaveEffectApplier, LeaveEffectApplierImpl>();
builder.Services.AddScoped<IWorkflowService, WorkflowServiceImpl>();
builder.Services.AddScoped<IExperienceRepository, ExperienceRepository>();
builder.Services.AddScoped<IExperienceService, ExperienceServiceImpl>();
builder.Services.AddScoped<IPerformanceRepository, PerformanceRepository>();
builder.Services.AddScoped<IPerformanceService, PerformanceServiceImpl>();
builder.Services.AddScoped<IOffboardingRepository, OffboardingRepository>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Organization.IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Organization.IChartService, Mightyfin.Erp.Hrm.Application.Organization.ChartServiceImpl>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Analytics.IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Analytics.IAnalyticsService, Mightyfin.Erp.Hrm.Application.Analytics.AnalyticsServiceImpl>();
builder.Services.AddScoped<IOffboardingService, OffboardingServiceImpl>();
builder.Services.AddScoped<IPayrollRepository, PayrollRepository>();
builder.Services.AddScoped<IPayrollService, PayrollServiceImpl>();
// M49: first-time setup wizard — state, step completion and the destructive reset
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Setup.ISetupRepository, SetupRepository>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Setup.ISetupService, Mightyfin.Erp.Hrm.Application.Setup.SetupServiceImpl>();
// M41 Gap 6b: flexible benefit claims (types, allowances, claims)
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Benefits.IBenefitRepository, Mightyfin.Erp.Hrm.Infrastructure.Benefits.BenefitRepository>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService, Mightyfin.Erp.Hrm.Application.Benefits.BenefitServiceImpl>();
builder.Services.AddScoped<IPayslipDocumentService, PayslipDocumentServiceImpl>();
// M41: accounting-facing payroll reports (JV + payroll by department, CSV/PDF)
builder.Services.AddScoped<IPayrollReportService, PayrollReportServiceImpl>();
builder.Services.AddScoped<IPayrollReportPdfRenderer, PayrollReportPdfRendererImpl>();
builder.Services.AddScoped<IConfigRepository, ConfigRepository>();
builder.Services.AddScoped<IConfigService, ConfigServiceImpl>();
builder.Services.AddScoped<IConfigAdminService, ConfigAdminServiceImpl>();
builder.Services.AddScoped<Mightyfin.Erp.Hrm.Application.Branding.ICompanyBrandingService, CompanyBrandingService>();
builder.Services.AddScoped<IJobsAdminService, JobsAdminServiceImpl>();
builder.Services.AddScoped<IRecruitmentRepository, RecruitmentRepository>();
builder.Services.AddScoped<IRecruitmentService, RecruitmentServiceImpl>();
builder.Services.AddScoped<IRelationsRepository, RelationsRepository>();
builder.Services.AddScoped<IRelationsService, RelationsServiceImpl>();
builder.Services.AddScoped<IDocumentsRepository, DocumentsRepository>();
builder.Services.AddScoped<IDocumentsService, DocumentsServiceImpl>();
builder.Services.AddScoped<IDqService, DqServiceImpl>();
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
builder.Services.AddScoped<IIntegrationOperationsService, IntegrationOperationsService>();
builder.Services.AddScoped<ISecurityComplianceService, SecurityComplianceService>();
builder.Services.AddScoped<IManagementReportingService, ManagementReportingService>();
builder.Services.AddScoped<IGoLiveReadinessService, GoLiveReadinessService>();
builder.Services.AddScoped<IStatutoryExportService, StatutoryExportServiceImpl>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<IOutboxWriter, EfOutboxWriter>();
builder.Services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
builder.Services.AddScoped<IEmployeeNotificationService, EmployeeNotificationService>();
builder.Services.AddScoped<IOutboxPublisherStore, EfOutboxPublisherStore>();
builder.Services.AddSingleton<IHrmEventPublisher, NatsHrmEventPublisher>();
builder.Services.AddSingleton<ISmtpNotificationFallback, SmtpNotificationFallback>();

// ---------- AuthN: local PostgreSQL sessions by default; OIDC is optional ----------
var authMode = builder.Configuration["ERP:AuthMode"] ?? builder.Configuration["HRM:AuthMode"] ?? "local";
if (authMode.Equals("local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddAuthentication(LocalAuthenticationHandler.Scheme)
        .AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(LocalAuthenticationHandler.Scheme, _ => { });
}
else if (authMode.Equals("disabled", StringComparison.OrdinalIgnoreCase))
{
    // Development-only compatibility mode. Production standalone deployments must use local mode.
    builder.Services.AddAuthentication("dev")
        .AddScheme<DeveloperAuthOptions, DeveloperAuthHandler>("dev", _ => { });
}
else
{
    var authority = builder.Configuration["ERP:OidcAuthority"] ?? builder.Configuration["HRM:OidcAuthority"];
    if (string.IsNullOrWhiteSpace(authority))
        throw new InvalidOperationException("ERP:AuthMode=oidc requires ERP:OidcAuthority or HRM:OidcAuthority.");
    var authentication = authMode.Equals("hybrid", StringComparison.OrdinalIgnoreCase)
        ? builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "hybrid";
            options.DefaultChallengeScheme = "hybrid";
        }).AddPolicyScheme("hybrid", "OIDC or HRMS local session", options =>
        {
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.Authorization.ToString()
                    .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? JwtBearerDefaults.AuthenticationScheme
                    : LocalAuthenticationHandler.Scheme;
        }).AddScheme<LocalAuthOptions, LocalAuthenticationHandler>(
            LocalAuthenticationHandler.Scheme, _ => { })
        : builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
    authentication.AddJwtBearer(o =>
        {
            o.Authority = authority;
            o.RequireHttpsMetadata = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                NameClaimType = "preferred_username",
                RoleClaimType = "realm_access.roles",
            };
        });
}
builder.Services.AddAuthorization(options =>
{
    // Authentication proves the shared platform identity. Admission to HRM
    // is a separate product decision: only explicit workforce roles pass.
    // Roles belonging to another platform/tenant (such as tenant_owner) must
    // never open ERP endpoints merely because they share the same IdP realm.
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(context => HrmStaffAccess.IsStaff(context.User.Claims))
        .Build();
    options.AddPolicy("hrm-admin", policy => policy.RequireAuthenticatedUser()
        .RequireAssertion(context => WorkerPrincipal.FromClaims(context.User.Claims).IsRole("hr_admin")));
});

// ---------- Health: readiness probes the database ----------
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString: connStr ?? throw new InvalidOperationException("ConnectionStrings:Hrm is not configured."));

// ---------- API versioning + CORS for the React frontend ----------
// The React HRM UI (TanStack Start) is served from a separate origin; CORS is
// enabled only for the configured origin list so the API refuses stray origins.
var allowedOrigins = (builder.Configuration["HRM:AllowedOrigins"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()
     .WithExposedHeaders("X-Request-Id", "X-Correlation-Id")));

// Map every route under /api/v{version}/hrm (current = v1) while keeping the
// legacy /api/hrm/* routes available for existing clients. Version resolution
// uses the URL path when present; handlers shared between versions.
builder.Services.AddSingleton(new ApiVersioning { CurrentVersion = 1 });

var app = builder.Build();

// ---------- Apply migrations at startup ----------
// A dedicated "migrate" container can also launch the same binary with
// --apply-migrations-only to run migrations before the API starts serving.
// The API itself applies any pending migrations on startup as a safety net,
// exactly like the Go ERP stack's migrate service + API pair.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<HrmDbContext>().Database.Migrate();
}
if (args.Contains("--apply-migrations-only"))
{
    Console.WriteLine("Migrations applied. Exiting.");
    return;
}
await LocalIdentityBootstrap.EnsureAsync(app.Services, builder.Configuration);

if (args.Contains("--run-outbox-publisher"))
{
    await RunOutboxPublisherAsync(app.Services, builder.Configuration);
    return;
}

// ---------- Cross-cutting middleware ----------
app.UseCors();

// Assign a per-request id (client-supplied X-Request-Id preferred) and log
// every request with method/path/status/duration for observability.
app.Use(async (ctx, next) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? System.Guid.NewGuid().ToString("N")[..12];
    var sw = System.Diagnostics.Stopwatch.StartNew();
    ctx.Response.Headers["X-Request-Id"] = requestId;
    var logger = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
    using var scope = logger.BeginScope(new Dictionary<string, object?>
    {
        ["RequestId"] = requestId,
        ["Method"] = ctx.Request.Method,
        ["Path"] = ctx.Request.Path.Value,
    });
    await next(ctx);
    sw.Stop();
    logger.LogInformation("{Method} {Path} -> {Status} in {ElapsedMs}ms",
        ctx.Request.Method, ctx.Request.Path.Value, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health/live");

// Readiness probes the database so orchestrators can delay routing traffic
// until migrations have run and Postgres is reachable.
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        var status = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy ? "healthy" : "degraded";
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { status, checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), duration = e.Value.Duration.TotalMilliseconds }) });
    },
});

// Root welcome endpoint: confirms a successful deployment and lists supported versions.
app.MapGet("/", () => Results.Ok(new
{
    service = "newworldcargo-hrm-api",
    versions = new[] { "v1" },
    authentication = "local-postgresql",
    documentation = app.Environment.IsDevelopment() ? "/openapi/hrm.json" : null,
}));

// Standalone identity surface. These routes use the application database and
// same-origin HttpOnly sessions; no external provider redirect is involved.
LocalIdentityRoutes.Map(app);

// Global error handler: DomainException -> structured ApiError
app.Use(async (ctx, next) =>
{
    try
    {
        await next(ctx);
    }
    catch (DomainException ex)
    {
            var code = ex.Code switch
        {
            "forbidden" => StatusCodes.Status403Forbidden,
            "unauthorized" => StatusCodes.Status401Unauthorized,
            "not-found" or "worker-not-found" or "payroll-run-not-found" or "candidate-not-found" or "pay-period-not-found" or "letter-not-found" or "hr-request-not-found" => StatusCodes.Status404NotFound,
            "conflict" or "employee-no-exists" or "run-already-exists" or "movement-not-allowed" or "offboarding-blocked" => StatusCodes.Status409Conflict,
            "legal-entity-code-taken" or "location-code-taken" or "org-unit-code-taken" or "leave-type-code-taken"
                or "unit-close-backdated" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ApiError(ex.Code, ex.Message, []));
    }
    catch (Exception ex)
    {
        var logger2 = ctx.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
        logger2.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path.Value);
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new ApiError("internal-error", "An unexpected error occurred.", []));
    }
});

app.UseAuthentication();

// M34: append-only request evidence for every authenticated privileged
// mutation. Entity-level before/after evidence is written independently by
// AuditInterceptor; this row also retains denied and failed attempts.
app.Use(async (ctx, next) =>
{
    var mutating = ctx.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE";
    var hrmApi = ctx.Request.Path.StartsWithSegments("/api/hrm")
        || ctx.Request.Path.StartsWithSegments("/api/v1/hrm");
    if (!mutating || !hrmApi || ctx.User.Identity?.IsAuthenticated != true)
    {
        await next(ctx);
        return;
    }

    Exception? failure = null;
    try { await next(ctx); }
    catch (Exception ex) { failure = ex; throw; }
    finally
    {
        try
        {
            var principal = WorkerPrincipal.FromClaims(ctx.User.Claims);
            var actor = !string.IsNullOrWhiteSpace(principal.SubjectId)
                ? principal.SubjectId
                : ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var requestId = ctx.Response.Headers["X-Request-Id"].FirstOrDefault() ?? ctx.TraceIdentifier;
            var status = failure is null ? ctx.Response.StatusCode : StatusCodes.Status500InternalServerError;
            await using var auditScope = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
            var db = auditScope.ServiceProvider.GetRequiredService<HrmDbContext>();
            db.PrivilegedActionEvents.Add(new Mightyfin.Erp.Hrm.Domain.Entities.PrivilegedActionEvent
            {
                ActorSubjectId = string.IsNullOrWhiteSpace(actor) ? "unknown" : actor,
                ActorRoles = string.Join(',', principal.Roles.OrderBy(x => x)),
                Method = ctx.Request.Method,
                Path = ctx.Request.Path.Value ?? "",
                Outcome = status is >= 200 and < 300 ? "succeeded" : status is 401 or 403 ? "denied" : "failed",
                StatusCode = status,
                RequestId = requestId,
                // Network addresses are deliberately not persisted. The
                // authenticated subject and request id provide traceability
                // without retaining additional personal data.
                SourceAddressHash = null,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception auditError)
        {
            ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("PrivilegedAudit")
                .LogError(auditError, "Unable to persist privileged action evidence for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
        }
    }
});
app.UseAuthorization();

// M44 branch scoping + M45 branch confinement: populate ShellContext from
// frontend shell-state headers and restrict confined operators to their
// assigned branches. MUST run after UseAuthentication — it reads the token
// subject from http.User to load branch assignments; registered before auth
// it always saw an anonymous principal and confinement was silently skipped
// (M45 smoke test root cause).
app.UseMiddleware<Mightyfin.Erp.Hrm.Api.ShellContextMiddleware>();

// ---------- Route registrations ----------
// URL-based API versioning: the current version is served at /api/v{n}/hrm
// while the legacy /api/hrm prefix stays available for existing clients.
// Both surfaces register the same handlers (shared service methods).
var versioning = app.Services.GetRequiredService<ApiVersioning>();
Routes.HrmPrefix = $"/api/v{versioning.CurrentVersion}/hrm";
Routes.RegisterAll(app);
Routes.HrmPrefix = "/api/hrm"; // legacy prefix, kept for existing clients
Routes.RegisterAll(app);

// M28: seed tenant role assignments for the known HRM roles if this tenant has none yet.
{
    try
    {
        using var seedScope = app.Services.CreateScope();
        var seedRepo = seedScope.ServiceProvider.GetRequiredService<IConfigRepository>();
        if (!(await seedRepo.ListRoleAssignmentsAsync(CancellationToken.None)).Any())
        {
            foreach (var key in new (string Key, string Name, string Cat)[]
            {
                ("employee", "Employee", "hrm"),
                ("manager", "Manager", "hrm"),
                ("hr_ops", "HR Operations", "hrm"),
                ("payroll", "Payroll", "payroll"),
                ("finance_approver", "Finance Approver", "payroll"),
                ("hr_admin", "HR Administrator", "system"),
                ("investigator", "Relations Investigator", "hrm"),
            })
            {
                await seedRepo.CreateRoleAssignmentAsync(new TenantRoleAssignment { RoleKey = key.Key, RoleName = key.Name, Category = key.Cat, PermissionsCsv = key.Key, Active = true }, CancellationToken.None);
            }
        }
    }
    catch (Exception)
    {
        // Seeding failures must never crash startup; roles can be created later.
    }
}

app.Run();

static async Task RunOutboxPublisherAsync(IServiceProvider services, IConfiguration configuration)
{
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("HrmOutboxPublisher");
    var smtpFallback = services.GetRequiredService<ISmtpNotificationFallback>();
    var transport = (configuration["HRM:NotificationTransport"] ?? "nats").Trim().ToLowerInvariant();
    var smtpPrimary = transport == "smtp";
    var publisher = smtpPrimary ? null : services.GetRequiredService<IHrmEventPublisher>();
    var deliveryTimeoutSeconds = int.TryParse(configuration["HRM:NotificationDeliveryTimeoutSeconds"], out var configuredDeliveryTimeout)
        ? Math.Clamp(configuredDeliveryTimeout, 5, 120)
        : 20;
    var pollSeconds = int.TryParse(configuration["HRM:OutboxPollSeconds"], out var configuredPoll)
        ? Math.Clamp(configuredPoll, 1, 60)
        : 1;
    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        stopping.Cancel();
    };
    System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += _ => stopping.Cancel();

    var streamReady = false;

    while (!stopping.IsCancellationRequested)
    {
        try
        {
            if (!streamReady)
            {
                if (smtpPrimary)
                {
                    if (!smtpFallback.Enabled)
                        throw new InvalidOperationException("HRM:NotificationTransport=smtp requires HRM:NotificationFallback=smtp and HRM:Smtp settings.");
                    logger.LogInformation("HRM outbox publisher using SMTP as primary notification transport");
                }
                else
                {
                    await publisher!.EnsureStreamAsync(stopping.Token);
                    logger.LogInformation("HRM outbox publisher connected to HRM_EVENTS; SMTP fallback enabled={SmtpFallbackEnabled}", smtpFallback.Enabled);
                }
                streamReady = true;
            }
            await using var scope = services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IOutboxPublisherStore>();
            var rows = await store.ClaimAsync(50, stopping.Token);
            foreach (var row in rows)
            {
                try
                {
                    using var rowDelivery = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
                    rowDelivery.CancelAfter(TimeSpan.FromSeconds(deliveryTimeoutSeconds));
                    if (smtpPrimary)
                    {
                        if (!smtpFallback.CanDeliver(row))
                        {
                            await store.CompleteAsync(row.Id, true, "smtp-skipped", "No SMTP template for this event type.", rowDelivery.Token);
                            logger.LogInformation("Skipped non-email HRM event {EventId} {EventType} in SMTP transport mode", row.PublicId, row.EventType);
                            continue;
                        }
                        await smtpFallback.DeliverAsync(row, rowDelivery.Token);
                        await store.CompleteAsync(row.Id, true, "smtp", null, rowDelivery.Token);
                        logger.LogInformation("Delivered HRM event {EventId} {EventType} by SMTP", row.PublicId, row.EventType);
                    }
                    else
                    {
                        await publisher!.PublishAsync(row, rowDelivery.Token);
                        await store.CompleteAsync(row.Id, true, "nats", null, rowDelivery.Token);
                        logger.LogInformation("Published HRM event {EventId} {EventType}", row.PublicId, row.EventType);
                    }
                }
                catch (Exception publishError) when (!stopping.IsCancellationRequested)
                {
                    if (smtpPrimary)
                    {
                        await store.CompleteAsync(row.Id, false, "smtp", publishError.Message, stopping.Token);
                        logger.LogError(publishError, "SMTP delivery failed for HRM event {EventId}; retry scheduled", row.PublicId);
                        continue;
                    }
                    if (!smtpPrimary && smtpFallback.Enabled)
                    {
                        if (!smtpFallback.CanDeliver(row))
                        {
                            await store.CompleteAsync(row.Id, true, "smtp-skipped", "No SMTP template or recipient for this event.", stopping.Token);
                            logger.LogInformation("Skipped non-email HRM event {EventId} {EventType} after NATS failure", row.PublicId, row.EventType);
                            continue;
                        }
                        try
                        {
                            await smtpFallback.DeliverAsync(row, stopping.Token);
                            await store.CompleteAsync(row.Id, true, "smtp", publishError.Message, stopping.Token);
                            logger.LogWarning(publishError, "NATS publish failed; explicitly enabled SMTP fallback delivered event {EventId}", row.PublicId);
                            continue;
                        }
                        catch (Exception smtpError) when (!stopping.IsCancellationRequested)
                        {
                            await store.CompleteAsync(row.Id, false, "smtp", $"NATS: {publishError.Message}; SMTP: {smtpError.Message}", stopping.Token);
                            logger.LogError(smtpError, "Both NATS and SMTP fallback failed for event {EventId}", row.PublicId);
                            continue;
                        }
                    }
                    await store.CompleteAsync(row.Id, false, "nats", publishError.Message, stopping.Token);
                    logger.LogError(publishError, "NATS publish failed for event {EventId}; retry scheduled", row.PublicId);
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HRM outbox claim cycle failed");
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stopping.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
    logger.LogInformation("HRM outbox publisher stopped");
}

/// <summary>In-process JWT verification bypass for local development only:
/// mirrors the Go skeleton's ERP_AUTH_MODE=disabled behaviour — never enable in production.</summary>
internal sealed class DeveloperAuthOptions : Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions;

internal sealed class DeveloperAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<DeveloperAuthOptions>
{
    public DeveloperAuthHandler(Microsoft.Extensions.Options.IOptionsMonitor<DeveloperAuthOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "dev-user-001"),
                new System.Security.Claims.Claim("preferred_username", "developer"),
                new System.Security.Claims.Claim("tenant", Context.RequestServices.GetRequiredService<ITenantAccessor>().GetTenantId()),
                new System.Security.Claims.Claim("realm_access.roles", "hr_admin"),
                new System.Security.Claims.Claim("worker_id", Context.RequestServices.GetRequiredService<IWorkerResolver>().ResolveDev()),
            ], "dev"));
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(
            new Microsoft.AspNetCore.Authentication.AuthenticationTicket(claims, "dev")));
    }
}
