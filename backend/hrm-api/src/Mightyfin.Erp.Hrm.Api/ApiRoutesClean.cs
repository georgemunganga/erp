using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Application.Experience;
using Mightyfin.Erp.Hrm.Application.Analytics;
using Mightyfin.Erp.Hrm.Application.Organization;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Application.Workflow;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Hrm.Application.Performance;
using Mightyfin.Erp.Hrm.Application.Offboarding;
using Mightyfin.Erp.Hrm.Application.Integration;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
namespace Mightyfin.Erp.Hrm.Api.Routing;

// Minimal-API routes grouped by the frontend client interfaces (PeopleClient,
// TimeClient, WorkflowClient, ExperienceClient, PayrollClient,
// AdminConfigClient, RecruitmentClient, RelationsClient, DocumentsClient).
public static class Routes
{
    /// <summary>Base path for all HRM endpoints; set before RegisterAll so both
    /// the versioned /api/v1/hrm and legacy /api/hrm surfaces resolve to the
    /// same handlers.</summary>
    public static string HrmPrefix { get; set; } = "/api/hrm";

    /// <summary>Registers every HRM route group. Called twice: once with
    /// HrmPrefix="/api/v1/hrm" and once with the legacy "/api/hrm".</summary>
    public static void RegisterAll(WebApplication app)
    {
        RegisterWorkers(app);
        RegisterTime(app);
        RegisterWorkflow(app);
        RegisterExperience(app);
        RegisterPayroll(app);
        RegisterConfig(app);
        RegisterRecruitment(app);
        RegisterRelations(app);
        RegisterDocuments(app);
        RegisterDq(app);
        RegisterMasterData(app);
        RegisterIntegrations(app);
        RegisterSecurityCompliance(app);
        RegisterGoLive(app);
        RegisterStatutory(app);
        RegisterNotifications(app);
        RegisterMe(app);
        RegisterImportExport(app);
        RegisterPerformance(app);
        RegisterOffboarding(app);
        RegisterRequisitions(app);
        RegisterBenefits(app);
        RegisterSetup(app);
        RegisterIdentityAccess(app);
        RegisterShell(app);
    }

    // M44 branch scoping: echo the resolved work scope so the frontend can
    // confirm which entity/branch its requests are executing under.
    public static void RegisterShell(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/shell").RequireAuthorization();
        g.MapGet("/", (ShellContext scope) => Results.Ok(new
        {
            locationId = scope.LocationId,
            entityId = scope.EntityId,
            scopedToBranch = scope.IsScopedToBranch,
            // M45: confinement metadata — the switcher hides branches the
            // operator is not assigned to, and the badge reflects reality.
            assignedLocationIds = scope.AllowedLocationIds,
            confined = scope.IsConfined,
        }));
    }
    // M49: first-time setup wizard — state machine for a new organisation.
    // GET /hrm/setup/state is the single decision endpoint the shell polls on
    // every render (pending → show the welcome overlay; complete → dashboard).
    // Confined branch HR can never enter the wizard: top-level HR completes it.
    public static void RegisterSetup(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/setup").RequireAuthorization();
        g.MapGet("/state", async (HttpContext http, ShellContext scope, Mightyfin.Erp.Hrm.Application.Setup.ISetupService svc, CancellationToken ct) =>
        {
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot run the setup wizard — organisation-wide HR completes it.");
            return Results.Ok(await svc.GetStateAsync(ct));
        });
        g.MapGet("/steps", async (HttpContext http, ShellContext scope, Mightyfin.Erp.Hrm.Application.Setup.ISetupService svc, CancellationToken ct) =>
        {
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot run the setup wizard — organisation-wide HR completes it.");
            return Results.Ok(await svc.ListStepsAsync(ct));
        });
        // M50.18: the saved input payload of a completed wizard step — the
        // employees step reads step 3's grades and positions from here so the
        // manual-entry grid can offer them as dropdowns instead of free text.
        // Confined branch HR are refused like the rest of the wizard surface.
        g.MapGet("/steps/{key}/data", async (string key, HttpContext http, ShellContext scope, Mightyfin.Erp.Hrm.Application.Setup.ISetupService svc, CancellationToken ct) =>
        {
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot run the setup wizard — organisation-wide HR completes it.");
            return Results.Ok(new { dataJson = await svc.GetStepDataAsync(key, ct) });
        });
        g.MapPost("/steps/{key}", async (string key, HttpContext http, ShellContext scope, Mightyfin.Erp.Hrm.Application.Setup.ISetupService svc, CancellationToken ct) =>
        {
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot run the setup wizard — organisation-wide HR completes it.");
            var dataJson = (string?)null;
            if (http.Request.ContentLength > 0)
            {
                var elem = await ReadBodyAsync<System.Text.Json.JsonElement>(http, ct);
                if (elem.ValueKind != System.Text.Json.JsonValueKind.Undefined)
                {
                    // M50: the frontend client wraps the step payload in
                    // { "dataJson": "<escaped-payload>" }; unwrap when present.
                    if (elem.ValueKind == System.Text.Json.JsonValueKind.Object &&
                        elem.TryGetProperty("dataJson", out var dj) &&
                        dj.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        dataJson = dj.GetString();
                    }
                    else
                    {
                        dataJson = elem.GetRawText();
                    }
                }
            }
            await svc.CompleteStepAsync(key, dataJson, ct);
            return Results.Ok(new { key, completed = true });
        });
        g.MapPost("/finish", async (HttpContext http, ShellContext scope, Mightyfin.Erp.Hrm.Application.Setup.ISetupService svc, CancellationToken ct) =>
        {
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot run the setup wizard — organisation-wide HR completes it.");
            await svc.FinishAsync(ct);
            return Results.Ok(new { status = "complete" });
        });
        // M51: first-user auto-elevation. The first signed-in operator of a
        // fresh tenant (setup PENDING, no hr_admin holders in the realm yet)
        // claims top-HR-admin elevation for themselves. Idempotent, best-
        // effort: the caller always receives their current role set even when
        // nothing changed. Confined branch HR can never elevate themselves.
        g.MapGet("/first-user/claim", async (HttpContext http, ShellContext scope, CancellationToken ct) =>
        {
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot claim organisation elevation.");
            var principal = WorkerPrincipal.FromClaims(http.User.Claims);
            var email = http.User.FindFirst("preferred_username")?.Value ?? "";
            var svc = http.RequestServices
                .GetRequiredService<Mightyfin.Erp.Hrm.Application.Integration.IIdentityProvisioningService>();
            return Results.Ok(await svc.ClaimTopAdminAsync(
                principal.SubjectId, email, ct));
        });
        // M51: HR administrator invitation provisioning. Called by the wizard
        // "Roles & access" step after Save administrators: every listed email
        // that resolves to a Keycloak user is granted hr_admin + employee
        // realm roles. Unknown emails are reported (never failed) because
        // user creation itself is an identity-platform responsibility.
        g.MapPost("/provision-admins", async (HttpContext http, ShellContext scope, CancellationToken ct) =>
        {
            if (!WorkerPrincipal.FromClaims(http.User.Claims).IsRole("hr_admin"))
                throw new DomainException("forbidden", "Provisioning administrators requires the hr_admin role.");
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot provision organisation administrators.");
            var emails = new List<string>();
            var body = await ReadBodyAsync<System.Text.Json.JsonElement>(http, ct);
            if (body.ValueKind == System.Text.Json.JsonValueKind.Object &&
                body.TryGetProperty("emails", out var arr) &&
                arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                emails.AddRange(arr.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(s => s.Length > 0));
            }
            var svc = http.RequestServices
                .GetRequiredService<Mightyfin.Erp.Hrm.Application.Integration.IIdentityProvisioningService>();
            return Results.Ok(await svc.ProvisionAdminsAsync(emails, ct));
        });
        // DESTRUCTIVE — deliberate, guarded, explicit. Only hr_admin may call
        // it and the body must contain { "confirm": "RESET" } verbatim; this
        // keeps a stray POST from wiping an organisation by accident.
        g.MapPost("/reset", async (HttpContext http, ShellContext scope, Mightyfin.Erp.Hrm.Application.Setup.ISetupService svc, CancellationToken ct) =>
        {
            if (!WorkerPrincipal.FromClaims(http.User.Claims).IsRole("hr_admin"))
                throw new DomainException("forbidden", "Start-afresh reset requires the hr_admin role.");
            if (scope.IsConfined)
                throw new DomainException("setup-confined", "Branch-confined HR cannot reset the organisation.");
            var body = await ReadBodyAsync<System.Text.Json.JsonElement>(http, ct);
            if (body.ValueKind == System.Text.Json.JsonValueKind.Undefined
                || body.TryGetProperty("confirm", out var confirm)
                    is false || confirm.GetString() != "RESET")
                throw new DomainException("reset-not-confirmed",
                    "Reset the organisation only if you understand that ALL data will be permanently erased. Send {\"confirm\": \"RESET\"} to proceed.");
            await svc.ResetAsync(ct);
            return Results.Ok(new { reset = true });
        });
    }

    /// <summary>
    /// OIDC realm access administration. Production ERP identities live in
    /// Keycloak; these routes intentionally do not touch the optional
    /// standalone local_users tables.
    /// </summary>
    public static void RegisterIdentityAccess(WebApplication app)
    {
        var group = app.MapGroup($"{HrmPrefix}/identity/users")
            .RequireAuthorization("hrm-admin");

        group.MapGet("/", async (IIdentityProvisioningService service, CancellationToken ct) =>
            Results.Ok(await service.ListUsersAsync(ct)));

        group.MapGet("/directory", async (
            string query,
            IIdentityProvisioningService service,
            CancellationToken ct) =>
            Results.Ok(new { items = await service.SearchDirectoryAsync(query, ct) }));

        group.MapPost("/", async (
            IdentityUserInvite request,
            IIdentityProvisioningService service,
            CancellationToken ct) =>
            Results.Ok(await service.InviteUserAsync(request, ct)));

        group.MapPatch("/{id}", async (
            string id,
            IdentityUserUpdate request,
            IIdentityProvisioningService service,
            CancellationToken ct) =>
            Results.Ok(await service.UpdateUserAsync(id, request, ct)));

        group.MapPost("/{id}/send-password-link", async (
            string id,
            IIdentityProvisioningService service,
            CancellationToken ct) =>
        {
            await service.SendPasswordResetAsync(id, ct);
            return Results.Ok(new { sent = true });
        });
    }
    public static void RegisterNotifications(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/admin/notifications").RequireAuthorization();
        g.MapGet("/", async (string? eventType, string? status, int? limit,
            INotificationDeliveryService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(eventType, status, limit ?? 50, ct)));
        g.MapPost("/{id:guid}/retry", async (Guid id, INotificationDeliveryService svc, CancellationToken ct) =>
            Results.Ok(await svc.RetryAsync(id, ct)));
    }


    // Helper: read a JSON body manually inside a minimal-API handler.
    private static async Task<T?> ReadBodyAsync<T>(HttpContext http, CancellationToken ct)
    {
        http.Request.EnableBuffering();
        var stream = http.Request.Body;
        http.Request.Body.Position = 0;
        return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
    }

    // Helper: resolve the calling worker from the authenticated principal.
    // Only the explicit `worker_id` claim is trusted; the raw subject id is
    // deliberately NOT used (a Keycloak subject uuid parses as a Guid but is
    // never a worker record — the subject→worker mapping lives in M14 and is
    // resolved via IWorkerService.GetBySubjectAsync where needed).
    private static Guid? ResolveWorkerId(HttpContext http)
    {
        var raw = http.User.FindFirst("worker_id")?.Value;
        return string.IsNullOrEmpty(raw) || !System.Guid.TryParse(raw, out var id) ? null : id;
    }

    // Helper: read the Keycloak subject id from the current principal.
    // JwtSecurityTokenHandler maps the JWT "sub" claim to the
    // NameIdentifier claim type, so check both the raw "sub" name and the
    // mapped NameIdentifier type.
    private static string? ResolveSubjectId(HttpContext http)
        => http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    // M14 identity link: resolve the worker record bound to the caller's
    // Keycloak subject. Registered once (not per prefix) because the route is
    // identical on both surfaces.
    public static void RegisterMe(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/me").RequireAuthorization();
        g.MapGet("/", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                return Results.Ok(new { linked = false, worker = (object?)null, reason = "no-subject-claim" });
            var worker = await svc.GetBySubjectAsync(subject, ct);
            return worker is null
                ? Results.Ok(new { linked = false, worker = (object?)null, subject })
                : Results.Ok(new { linked = true, worker, subject });
        });

        // M15 self-service: workers update their own profile. The subject is
        // read from the token and merged into the request; admin-only fields
        // are not part of the request shape and can never be changed here.
        g.MapPut("/profile", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            var raw = await ReadBodyAsync<WorkerSubjectUpdateRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateOwnProfileAsync(raw with { SubjectId = subject }, ct));
        });

        // M35: self-service notification preferences — GET/PUT /me/preferences.
        // The preferences are a free-form JSON blob; the UI renders known keys
        // (email, inApp, topics) and ignores the rest.
        g.MapGet("/preferences", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
            Results.Ok(new { preferences = await svc.GetMyPreferencesAsync(ResolveSubjectId(http) ?? "", ct) }));
        g.MapPut("/preferences", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            var raw = await ReadBodyAsync<System.Text.Json.JsonElement>(http, ct);
            if (raw.ValueKind == System.Text.Json.JsonValueKind.Undefined)
                throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateMyPreferencesAsync(subject, raw.GetRawText(), ct));
        });

        // M16 self-service: the signed-in worker's own leave inbox (balances +
        // own requests + cancel) — always keyed on the token subject.
        // M35: self-service dashboard — today's punch + leave balances + identity in one call.
        g.MapGet("/dashboard", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
            Results.Ok(await svc.MyDashboardAsync(ResolveSubjectId(http) ?? "", ct)));

        g.MapGet("/leave", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            return Results.Ok(await svc.MyLeaveAsync(subject ?? "", ct));
        });
        g.MapPost("/leave", async (HttpContext http, ITimeService svc, IWorkerService workers, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            var worker = await workers.GetBySubjectAsync(subject, ct)
                ?? throw new DomainException("worker-not-linked", "Your organisation identity is not linked to an HRM worker record.");
            var request = await ReadBodyAsync<LeaveRequestCreate>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLeaveAsync(request with { WorkerId = worker.Id }, ct));
        });
        g.MapPost("/leave/{id:guid}/cancel", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            return Results.Ok(await svc.CancelLeaveAsync(id, subject, ct));
        });

        g.MapGet("/attendance/today", async (HttpContext http, ITimeService svc, IWorkerService workers, CancellationToken ct) =>
        {
            var worker = await RequireLinkedWorkerAsync(http, workers, ct);
            return Results.Ok(await svc.GetTodayAsync(worker.Id, ct));
        });
        g.MapGet("/attendance", async (HttpContext http, [FromQuery] string? from, [FromQuery] string? to, ITimeService svc, IWorkerService workers, CancellationToken ct) =>
        {
            var worker = await RequireLinkedWorkerAsync(http, workers, ct);
            return Results.Ok(await svc.ListAttendanceAsync(worker.Id, from, to, ct));
        });
        g.MapPost("/attendance/clock-in", async (HttpContext http, ITimeService svc, IWorkerService workers, CancellationToken ct) =>
        {
            var worker = await RequireLinkedWorkerAsync(http, workers, ct);
            return Results.Ok(await svc.ClockInAsync(worker.Id, ct));
        });
        g.MapPost("/attendance/clock-out", async (HttpContext http, ITimeService svc, IWorkerService workers, CancellationToken ct) =>
        {
            var worker = await RequireLinkedWorkerAsync(http, workers, ct);
            return Results.Ok(await svc.ClockOutAsync(worker.Id, ct));
        });
        g.MapPost("/attendance/corrections", async (HttpContext http, ITimeService svc, IWorkerService workers, CancellationToken ct) =>
        {
            var worker = await RequireLinkedWorkerAsync(http, workers, ct);
            var request = await ReadBodyAsync<AttendanceCorrectionCreate>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCorrectionAsync(request with { WorkerId = worker.Id }, ct));
        });


        // M25 self-service: the signed-in worker's own HR requests — keyed on
        // the token subject so an employee can never list another's inbox.
        g.MapGet("/requests", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMyRequestsAsync(ResolveSubjectId(http) ?? "", null, ct)));
        g.MapPost("/requests", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateMyRequestAsync(ResolveSubjectId(http) ?? "", request, ct));
        });
        g.MapGet("/requests/{id:guid}", async (Guid id, HttpContext http, IExperienceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMyRequestAsync(id, ResolveSubjectId(http) ?? "", ct)));
        g.MapPost("/requests/{id:guid}/messages", async (Guid id, HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestMessageCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.AddMyRequestMessageAsync(id, ResolveSubjectId(http) ?? "", request, ct));
        });

        g.MapGet("/letters", async (HttpContext http, [FromQuery] string? status, IExperienceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMyLettersAsync(ResolveSubjectId(http) ?? "", status, ct)));
        g.MapPost("/letters", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrLetterCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateMyLetterAsync(ResolveSubjectId(http) ?? "", request, ct));
        });
        g.MapGet("/letters/{id:guid}/download", async (Guid id, HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var letter = await svc.GetMyLetterAsync(id, ResolveSubjectId(http) ?? "", ct);
            if (string.IsNullOrWhiteSpace(letter.TemplateBody)) throw new DomainException("letter-not-ready", "The letter is not ready to download.");
            return Results.File(Encoding.UTF8.GetBytes(letter.TemplateBody), "text/plain; charset=utf-8", $"{letter.LetterType}-{letter.Id:N}.txt");
        });

        g.MapGet("/documents", async (HttpContext http, IDocumentsService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMyDocumentsAsync(ResolveSubjectId(http) ?? "", ct)));
        g.MapPost("/documents", async (HttpContext http, IDocumentsService svc, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new DomainException("bad-request", "No personal document was uploaded.");
            if (file.Length == 0) throw new DomainException("bad-request", "Uploaded file is empty.");
            if (file.Length > 25 * 1024 * 1024) throw new DomainException("document-too-large", "Personal documents must not exceed 25 MB.");
            var storageDir = Path.Combine(Path.GetTempPath(), "erp-docs"); Directory.CreateDirectory(storageDir);
            var storagePath = Path.Combine(storageDir, $"{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}");
            await using (var fs = File.Create(storagePath)) await file.CopyToAsync(fs, ct);
            try
            {
                return Results.Created("", await svc.UploadMyDocumentAsync(ResolveSubjectId(http) ?? "", form["category"].ToString(), form["title"].ToString(), file.FileName, file.ContentType ?? "application/octet-stream", file.Length, storagePath, ct));
            }
            catch
            {
                File.Delete(storagePath);
                throw;
            }
        }).DisableAntiforgery();
        g.MapGet("/documents/{id:guid}/download", async (Guid id, HttpContext http, IDocumentsService svc, CancellationToken ct) =>
        {
            var (document, stream) = await svc.GetMyDocumentStreamAsync(id, ResolveSubjectId(http) ?? "", ct);
            return Results.File(stream, document.ContentType, document.FileName);
        });


        // M25 self-service: the signed-in worker's own payslips — keyed on
        // the token subject so an employee can never reach another slip.
        g.MapGet("/payslips", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMyPayslipsAsync(ResolveSubjectId(http) ?? "", ct)));
        g.MapGet("/payslips/{id:guid}", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            var slip = await svc.GetMyPayslipByIdAsync(id, subject, ct);
            return slip is null ? Results.NotFound() : Results.Ok(slip);
        });
        g.MapGet("/payslips/{id:guid}/download", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            return Results.Ok(new { url = await svc.GetMyPayslipDownloadUrlAsync(id, subject, ct) });
        });
        g.MapGet("/payslips/{id:guid}/preview", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http);
            if (string.IsNullOrEmpty(subject))
                throw new DomainException("no-subject-claim", "The token carries no subject claim.");
            var bytes = await svc.GetMyPayslipPreviewAsync(id, subject, ct);
            return Results.File(bytes, "application/pdf", $"payslip-{id:D}.pdf");
        });

        g.MapGet("/notifications", async (HttpContext http, IEmployeeNotificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ResolveSubjectId(http) ?? "", ct)));
        g.MapPost("/notifications/{id:guid}/read", async (Guid id, HttpContext http, IEmployeeNotificationService svc, CancellationToken ct) =>
            Results.Ok(await svc.MarkReadAsync(id, ResolveSubjectId(http) ?? "", ct)));
        g.MapPost("/notifications/read-all", async (HttpContext http, IEmployeeNotificationService svc, CancellationToken ct) =>
            Results.Ok(new { markedRead = await svc.MarkAllReadAsync(ResolveSubjectId(http) ?? "", ct) }));

        // M36: self-service performance
        g.MapGet("/performance", async (HttpContext http, IPerformanceService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http) ?? "";
            return Results.Ok(await svc.GetMyPerformanceAsync(subject, ct));
        });
        g.MapGet("/performance/{cycleId:guid}", async (Guid cycleId, HttpContext http, IPerformanceService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http) ?? "";
            var assessment = await svc.GetMyAssessmentAsync(subject, cycleId, ct);
            return assessment is null ? Results.NotFound() : Results.Ok(assessment);
        });
        g.MapPatch("/performance/{assessmentId:guid}/self", async (Guid assessmentId, HttpContext http, IPerformanceService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http) ?? "";
            var request = await ReadBodyAsync<SelfAssessmentSubmit>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.SubmitSelfAssessmentAsync(assessmentId, subject, request, ct));
        });
        // M37: self-service offboarding
        g.MapGet("/offboarding", async (HttpContext http, IOffboardingService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http) ?? "";
            return Results.Ok(await svc.GetMyOffboardingAsync(subject, ct));
        });
        g.MapPost("/offboarding", async (HttpContext http, IOffboardingService svc, CancellationToken ct) =>
        {
            var subject = ResolveSubjectId(http) ?? "";
            var request = await ReadBodyAsync<OffboardingRequestCreate>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.SubmitMyResignationAsync(request, subject, ct));
        });
    }

    private static async Task<WorkerDto> RequireLinkedWorkerAsync(
        HttpContext http, IWorkerService workers, CancellationToken ct)
    {
        var subject = ResolveSubjectId(http);
        if (string.IsNullOrEmpty(subject))
            throw new DomainException("no-subject-claim", "The token carries no subject claim.");
        return await workers.GetBySubjectAsync(subject, ct)
            ?? throw new DomainException("worker-not-linked", "Your organisation identity is not linked to an HRM worker record.");
    }

    public static void RegisterWorkers(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/workers").RequireAuthorization();

        g.MapGet("/", async ([AsParameters] WorkerListFilters filters, IWorkerService svc, CancellationToken ct)
            => await svc.ListAsync(filters, ct));

        g.MapGet("/{id:guid}", async (Guid id, IWorkerService svc, CancellationToken ct)
            => await svc.GetByIdAsync(id, ct));

        g.MapPost("/", async (HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var errors = ValidateWorkerCreate(request);
            if (errors.Count != 0)
                return Results.UnprocessableEntity(new ApiError("validation-failed", string.Join("; ", errors), []));
            var created = await svc.CreateAsync(request, ct);
            return Results.Created($"{HrmPrefix}/workers/{created.Id}", created);
        });

        g.MapPost("/import", async (HttpContext http, IWorkerImportService svc, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new DomainException("bad-request", "No CSV file was uploaded.");
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("bad-request", "The uploaded file must be a CSV file.");
            await using var stream = file.OpenReadStream();
            return Results.Ok(await svc.ImportCsvAsync(stream, ct));
        }).DisableAntiforgery();
        g.MapPut("/{id:guid}", async (Guid id, HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateAsync(id, request, ct));
        });

        g.MapPost("/{id:guid}/archive", async (Guid id, IWorkerService svc, CancellationToken ct) =>
        {
            await svc.ArchiveAsync(id, ct);
            return Results.Ok();
        });

        // M27 P0 UX audit: admin identity-linking — ends the circular
        // dead-end where My HR / My documents / self-leave 422-ed because
        // there was no way to bind a worker to an account.
        g.MapPut("/{id:guid}/account-link", async (Guid id, HttpContext http, IWorkerService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerAccountLinkRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.LinkAccountAsync(id, request, ct));
        });

        // M2 lifecycle surface
        RegisterWorkerLifecycleRoutes(g);
    }

    private static void RegisterWorkerLifecycleRoutes(RouteGroupBuilder g)
    {
        g.MapGet("/{workerId:guid}/assignments", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListAssignmentsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/assignments", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AssignmentCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.CreateAssignmentAsync(workerId, request, ct);
            return Results.Created($"{HrmPrefix}/workers/{workerId}/assignments/{created.Id}", created);
        });
        g.MapPatch("/{workerId:guid}/assignments/{assignmentId:guid}", async (Guid workerId, Guid assignmentId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AssignmentUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateAssignmentAsync(workerId, assignmentId, request, ct));
        });
        g.MapPost("/{workerId:guid}/assignments/{assignmentId:guid}/end", async (Guid workerId, Guid assignmentId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.EndAssignmentAsync(workerId, assignmentId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/movements", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListMovementsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/movements", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<MovementCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.CreateMovementAsync(workerId, request, ct);
            return Results.Created($"{HrmPrefix}/workers/{workerId}/movements/{created.Id}", created);
        });
        g.MapGet("/{workerId:guid}/movements/{movementId:guid}", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.GetMovementAsync(workerId, movementId, ct));
        g.MapGet("/{workerId:guid}/movements/{movementId:guid}/preview", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.PreviewMovementAsync(workerId, movementId, ct));
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/submit", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.SubmitMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/approve", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.ApproveMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/reject", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.RejectMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });
        g.MapPost("/{workerId:guid}/movements/{movementId:guid}/cancel", async (Guid workerId, Guid movementId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.CancelMovementAsync(workerId, movementId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/emergency-contacts", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListEmergencyContactsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/emergency-contacts", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<EmergencyContactRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.AddEmergencyContactAsync(workerId, request, ct);
            return Results.Created("", created);
        });
        g.MapPatch("/{workerId:guid}/emergency-contacts/{contactId:guid}", async (Guid workerId, Guid contactId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<EmergencyContactRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateEmergencyContactAsync(workerId, contactId, request, ct));
        });
        g.MapDelete("/{workerId:guid}/emergency-contacts/{contactId:guid}", async (Guid workerId, Guid contactId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.DeleteEmergencyContactAsync(workerId, contactId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/bank-details", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListBankDetailsAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/bank-details", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<BankDetailRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.AddBankDetailAsync(workerId, request, ct);
            return Results.Created("", created);
        });
        g.MapPatch("/{workerId:guid}/bank-details/{bankId:guid}", async (Guid workerId, Guid bankId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<BankDetailRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateBankDetailAsync(workerId, bankId, request, ct));
        });
        g.MapDelete("/{workerId:guid}/bank-details/{bankId:guid}", async (Guid workerId, Guid bankId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.DeleteBankDetailAsync(workerId, bankId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/onboarding", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.GetOnboardingAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/offboard", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var result = await svc.OffboardAsync(workerId, ct);
            if (!result.Cleared)
                return Results.Conflict(new ApiError("offboarding-blocked", "Offboarding blocked by open clearance items.", result.OpenItems));
            return Results.Ok(result);
        });
        // ===================== M33: worker history child records =====================

        g.MapGet("/{workerId:guid}/education", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListEducationAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/education", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<EducationRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.AddEducationAsync(workerId, request, ct);
            return Results.Created("", created);
        });
        g.MapPatch("/{workerId:guid}/education/{recordId:guid}", async (Guid workerId, Guid recordId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<EducationRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateEducationAsync(workerId, recordId, request, ct));
        });
        g.MapDelete("/{workerId:guid}/education/{recordId:guid}", async (Guid workerId, Guid recordId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.DeleteEducationAsync(workerId, recordId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/external-work-history", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListExternalWorkHistoryAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/external-work-history", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ExternalWorkHistoryRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.AddExternalWorkHistoryAsync(workerId, request, ct);
            return Results.Created("", created);
        });
        g.MapPatch("/{workerId:guid}/external-work-history/{recordId:guid}", async (Guid workerId, Guid recordId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ExternalWorkHistoryRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateExternalWorkHistoryAsync(workerId, recordId, request, ct));
        });
        g.MapDelete("/{workerId:guid}/external-work-history/{recordId:guid}", async (Guid workerId, Guid recordId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.DeleteExternalWorkHistoryAsync(workerId, recordId, ct);
            return Results.Ok();
        });

        g.MapGet("/{workerId:guid}/internal-work-history", async (Guid workerId, IWorkerLifecycleService svc, CancellationToken ct)
            => await svc.ListInternalWorkHistoryAsync(workerId, ct));
        g.MapPost("/{workerId:guid}/internal-work-history", async (Guid workerId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<InternalWorkHistoryRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.AddInternalWorkHistoryAsync(workerId, request, ct);
            return Results.Created("", created);
        });
        g.MapPatch("/{workerId:guid}/internal-work-history/{recordId:guid}", async (Guid workerId, Guid recordId, HttpContext http, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<InternalWorkHistoryRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateInternalWorkHistoryAsync(workerId, recordId, request, ct));
        });
        g.MapDelete("/{workerId:guid}/internal-work-history/{recordId:guid}", async (Guid workerId, Guid recordId, IWorkerLifecycleService svc, CancellationToken ct) =>
        {
            await svc.DeleteInternalWorkHistoryAsync(workerId, recordId, ct);
            return Results.Ok();
        });
    }

    private static List<string> ValidateWorkerCreate(WorkerCreateRequest request)
    {
        var errors = new List<string>();
        // Employee number is auto-issued by the backend when the request leaves it
        // empty — the UI deliberately never asks HR to type one ("issued automatically").
        if (string.IsNullOrWhiteSpace(request.FirstName)) errors.Add("firstName is required");
        if (string.IsNullOrWhiteSpace(request.LastName)) errors.Add("lastName is required");
        if (request.WorkerType is not ("employee" or "contingent" or "intern" or "volunteer"))
            errors.Add("workerType must be employee|contingent|intern|volunteer");
        return errors;
    }

    public static void RegisterTime(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/time").RequireAuthorization();
        g.MapGet("/leave", async ([FromQuery] Guid? workerId, [FromQuery] string? status, ITimeService svc, CancellationToken ct)
            => await svc.ListLeaveAsync(workerId, status, ct));
        g.MapPost("/leave", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveRequestCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLeaveAsync(request, ct));
        });
        g.MapPost("/leave/{id:guid}/decide", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<TimeDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideLeaveAsync(id, request, ct));
        });
        g.MapGet("/leave/balances/{workerId:guid}", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => await svc.GetBalancesAsync(workerId, ct));
        g.MapGet("/corrections", async ([FromQuery] Guid? workerId, [FromQuery] string? status, ITimeService svc, CancellationToken ct)
            => await svc.ListCorrectionsAsync(workerId, status, ct));
        g.MapGet("/overtime", async ([FromQuery] Guid? workerId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? status, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.ListOvertimeAsync(workerId, from, to, status, ct)));
        g.MapPost("/overtime/{id:guid}/decide", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OvertimeDecisionRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideOvertimeAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/corrections", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AttendanceCorrectionCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCorrectionAsync(request, ct));
        });
        g.MapPost("/corrections/{id:guid}/decide", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<TimeDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideCorrectionAsync(id, request, ct));
        });

        // M3 attendance: punch, today record, range and roster
        g.MapPost("/attendance/{workerId:guid}/clock-in", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.ClockInAsync(workerId, ct)));
        g.MapPost("/attendance/{workerId:guid}/clock-out", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.ClockOutAsync(workerId, ct)));
        g.MapGet("/attendance/{workerId:guid}/today", async (Guid workerId, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.GetTodayAsync(workerId, ct)));
        g.MapGet("/attendance", async ([FromQuery] string? from, [FromQuery] string? to, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.ListAttendanceForScopeAsync(from, to, ct)));
        g.MapGet("/attendance/{workerId:guid}", async (Guid workerId, [FromQuery] string? from, [FromQuery] string? to, ITimeService svc, CancellationToken ct)
            => await svc.ListAttendanceAsync(workerId, from, to, ct));
        g.MapGet("/roster/{workerId:guid}", async (Guid workerId, [FromQuery] string? from, [FromQuery] string? to, ITimeService svc, CancellationToken ct)
            => await svc.GetRosterAsync(workerId, from, to, ct));

        // M28 attendance and leave operations
        g.MapGet("/shifts", async (ITimeService svc, CancellationToken ct) => await svc.ListShiftsAsync(ct));
        g.MapPost("/shifts", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ShiftCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateShiftAsync(request, ct));
        });
        g.MapPatch("/shifts/{id:guid}", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ShiftUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateShiftAsync(id, request, ct));
        });
        g.MapPost("/shifts/{id:guid}/close", async (Guid id, ITimeService svc, CancellationToken ct) =>
            Results.Ok(await svc.CloseShiftAsync(id, ct)));
        g.MapPost("/shifts/assign/{workerId:guid}", async (Guid workerId, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ShiftAssignmentRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.AssignShiftAsync(workerId, request, ct));
        });
        g.MapPost("/attendance/import", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<AttendanceImportRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ImportAttendanceAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/overtime/import", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OvertimeImportRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ImportOvertimeAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/leave/accruals/run", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveAccrualRunRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.RunLeaveAccrualAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/leave/balances/adjust", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveBalanceAdjustmentRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.AdjustLeaveBalanceAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/escalations/run", async (ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.EscalateOverdueAsync(ct)));
        g.MapGet("/operations/history", async (ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.GetOperationsHistoryAsync(ct)));

        // M41 Gap 6a: leave encashment — HR converts unused leave balance into
        // a cash payout at the worker's daily rate (basic monthly / 26).
        g.MapGet("/leave/encashments", async ([FromQuery] Guid? workerId, [FromQuery] string? status, ITimeService svc, CancellationToken ct)
            => await svc.ListEncashmentsAsync(workerId, status, ct));
        g.MapGet("/leave/encashments/rate/{workerId:guid}", async (Guid workerId, [FromQuery] string leaveTypeCode, [FromQuery] decimal days, ITimeService svc, CancellationToken ct)
            => Results.Ok(await svc.GetEncashmentRateAsync(workerId, leaveTypeCode, days, ct)));
        g.MapPost("/leave/encashments", async (HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveEncashmentCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateEncashmentAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/leave/encashments/{id:guid}/decide", async (Guid id, HttpContext http, ITimeService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveEncashmentDecideRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideEncashmentAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
    }

    public static void RegisterWorkflow(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/workflow").RequireAuthorization();
        g.MapGet("/queue", async (IWorkflowService svc, CancellationToken ct)
            => await svc.GetWorkQueueAsync(ct));
        g.MapGet("/requests/{id:guid}", async (Guid id, IWorkflowService svc, CancellationToken ct)
            => await svc.GetByIdAsync(id, ct));
        g.MapPost("/requests/{id:guid}/decisions", async (Guid id, HttpContext http, IWorkflowService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkflowDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var actorSubject = http.User.FindFirst("worker_id")?.Value ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(actorSubject) || !System.Guid.TryParse(actorSubject, out var actorId))
                return Results.Json(new ApiError("missing-actor", "The authenticated actor could not be resolved to a worker id; pass a 'worker_id' claim or use the actor_id query parameter.", []), statusCode: 401);
            return Results.Ok(await svc.DecideAsync(id, actorId, request, ct));
        });
        g.MapPost("/requests/{id:guid}/escalate", async (Guid id, HttpContext http, IWorkflowService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkflowEscalateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.EscalateAsync(id, request.ActorId, ct));
        });
    }

    public static void RegisterExperience(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/experience").RequireAuthorization();
        g.MapGet("/requests", async ([FromQuery] Guid? workerId, [FromQuery] string? status, IExperienceService svc, CancellationToken ct)
            => await svc.ListRequestsAsync(workerId, status, ct));
        g.MapPost("/requests", async (HttpContext http, IExperienceService svc, IWorkerService ws, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var _p = WorkerPrincipal.FromClaims(http.User.Claims);
            if (!_p.IsRole("hr_ops") && !_p.IsRole("hr_admin"))
            {
                var subject = ResolveSubjectId(http);
                if (!string.IsNullOrEmpty(subject))
                {
                    var ownWorker = await ws.GetBySubjectAsync(subject, ct);
                    if (ownWorker is not null)
                        return Results.Created("", await svc.CreateRequestAsync(ownWorker.Id, request with { WorkerId = ownWorker.Id }, ct));
                }
                if (_p.IsRole("payroll"))
                    return Results.Created("", await svc.CreateRequestAsync(null, request with { WorkerId = null }, ct));
                return Results.Created("", await svc.CreateMyRequestAsync(subject ?? "", request, ct));
            }
            var workerId = request.WorkerId ?? ResolveWorkerId(http);
            // M22: without a worker_id claim, resolve the caller via the M14
            // subject identity link instead of the raw sub Guid (a Keycloak
            // subject uuid parses as a Guid but is never a worker record).
            if (workerId is null && http.User.FindFirst("worker_id")?.Value is null)
            {
                var subject = ResolveSubjectId(http);
                if (!string.IsNullOrEmpty(subject))
                    workerId = (await ws.GetBySubjectAsync(subject, ct))?.Id;
            }
            // workerId null = HR-initiated internal request (no worker record).
            return Results.Created("", await svc.CreateRequestAsync(workerId, request, ct));
        });
        g.MapPost("/requests/{id:guid}/messages", async (Guid id, HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrRequestMessageCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var actorRole = WorkerPrincipal.FromClaims(http.User.Claims).IsRole("hr_ops", "hr_admin") ? "hr_ops" : "employee";
            if (actorRole == "employee")
                await svc.AddMyRequestMessageAsync(id, ResolveSubjectId(http) ?? "", request, ct);
            else
                await svc.AddMessageAsync(id, ResolveWorkerId(http), actorRole, request, ct);
            return Results.Ok();
        });
        g.MapPost("/requests/{id:guid}/resolve", async (Guid id, IExperienceService svc, CancellationToken ct) =>
        {
            return Results.Ok(await svc.ResolveRequestAsync(id, ct));
        });
        g.MapGet("/letters", async ([FromQuery] Guid? workerId, [FromQuery] string? status, IExperienceService svc, CancellationToken ct)
            => await svc.ListLettersAsync(workerId, status, ct));
        g.MapPost("/letters", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HrLetterCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var _p = WorkerPrincipal.FromClaims(http.User.Claims);
            if (!_p.IsRole("hr_ops") && !_p.IsRole("hr_admin"))
                return Results.Created("", await svc.CreateMyLetterAsync(ResolveSubjectId(http) ?? "", request, ct));
            var workerId = request.WorkerId ?? ResolveWorkerId(http);
            if (workerId is null)
                return Results.UnprocessableEntity(new ApiError("missing-worker", "WorkerId is required; either include worker_id in the body or authenticate as the worker.", []));
            return Results.Created("", await svc.CreateLetterAsync(workerId.Value, request, ct));
        });
        g.MapPost("/letters/{id:guid}/approve", async (Guid id, IExperienceService svc, CancellationToken ct) =>
        {
            await svc.ApproveLetterAsync(id, ct);
            return Results.Ok();
        });
        g.MapPost("/speak-up", async (HttpContext http, IExperienceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ProtectedDisclosureCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created($"{HrmPrefix}/experience/speak-up/status", await svc.SubmitDisclosureAsync(request, ct));
        }).AllowAnonymous();
        g.MapGet("/speak-up/status", async ([FromQuery] string caseReference, [FromQuery] string accessCode, IExperienceService svc, CancellationToken ct) =>
            await svc.GetDisclosureStatusAsync(caseReference, accessCode, ct)).AllowAnonymous();
    }

    public static void RegisterPayroll(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/payroll").RequireAuthorization();
        g.MapGet("/components", async ([FromQuery] string? type, IPayrollService svc, CancellationToken ct)
            => await svc.ListComponentsAsync(type, ct));
        g.MapPost("/components", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryComponentCreateRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            var created = await svc.CreateSalaryComponentAsync(request, ct);
            return Results.Created($"{HrmPrefix}/payroll/components/{created.Id}", created);
        });
        g.MapGet("/pay-groups", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListPayGroupsAsync(ct));
        g.MapGet("/pay-groups/full", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListPayGroupsFullAsync(ct));
        g.MapPatch("/pay-groups/{groupId:guid}", async (Guid groupId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayGroupUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdatePayGroupAsync(groupId, request, ct));
        });
        g.MapPatch("/tax-slabs/{slabId:guid}", async (Guid slabId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<TaxSlabUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateTaxSlabAsync(slabId, request, ct));
        });
        g.MapPatch("/contribution-rules/{ruleId:guid}", async (Guid ruleId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var body = await ReadBodyAsync<System.Text.Json.JsonElement>(http, ct);
            if (body.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null)
                throw new DomainException("bad-request", "Request body is missing or invalid.");
            decimal? ReadDecimal(string name) =>
                body.TryGetProperty(name, out var prop) && prop.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? prop.GetDecimal()
                    : null;
            var request = new ContributionRuleUpdateRequest(
                Rate: ReadDecimal("rate"),
                Ceiling: ReadDecimal("ceiling"),
                Floor: ReadDecimal("floor"),
                CeilingSpecified: body.TryGetProperty("ceiling", out _),
                FloorSpecified: body.TryGetProperty("floor", out _));
            return Results.Ok(await svc.UpdateContributionRuleAsync(ruleId, request, ct));
        });
        g.MapPatch("/components/{componentId:guid}", async (Guid componentId, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var body = await ReadBodyAsync<System.Text.Json.JsonElement>(http, ct);
            if (body.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null)
                throw new DomainException("bad-request", "Request body is missing or invalid.");
            string? ReadString(string name) =>
                body.TryGetProperty(name, out var prop) && prop.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? prop.GetString()
                    : null;
            decimal? ReadDecimal(string name) =>
                body.TryGetProperty(name, out var prop) && prop.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? prop.GetDecimal()
                    : null;
            bool? ReadBool(string name) =>
                body.TryGetProperty(name, out var prop) && prop.ValueKind != System.Text.Json.JsonValueKind.Null
                    ? prop.GetBoolean()
                    : null;
            var request = new SalaryComponentUpdateRequest(
                Name: ReadString("name"),
                CalculationBasis: ReadString("calculationBasis"),
                BasisComponentCode: ReadString("basisComponentCode"),
                Rate: ReadDecimal("rate"),
                FixedAmount: ReadDecimal("fixedAmount"),
                Ceiling: ReadDecimal("ceiling"),
                IsTaxable: ReadBool("isTaxable"),
                IsArchived: ReadBool("isArchived"),
                RateSpecified: body.TryGetProperty("rate", out _),
                FixedAmountSpecified: body.TryGetProperty("fixedAmount", out _),
                CeilingSpecified: body.TryGetProperty("ceiling", out _));
            return Results.Ok(await svc.UpdateSalaryComponentAsync(componentId, request, ct));
        });
        // M21: salary structure administration
        g.MapGet("/structures", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListStructuresAsync(ct));
        g.MapGet("/structures/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct)
            => Results.Ok(await svc.GetStructureAsync(id, ct)));
        g.MapPost("/structures", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryStructureCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateStructureAsync(request, ct));
        });
        g.MapPatch("/structures/{id:guid}", async (Guid id, IPayrollService svc, HttpContext http, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryStructureUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateStructureAsync(id, request, ct));
        });
        g.MapGet("/pay-groups/{groupId:guid}/periods", async (Guid groupId, IPayrollService svc, CancellationToken ct)
            => await svc.ListPeriodsAsync(groupId, ct));
        g.MapPost("/historical-periods", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<HistoricalPayPeriodCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateHistoricalPeriodAsync(request, ct));
        });
        g.MapGet("/tax-slabs", async ([FromQuery] string taxYear, IPayrollService svc, CancellationToken ct)
            => await svc.ListTaxSlabsAsync(taxYear, ct));
        g.MapGet("/contribution-rules", async (IPayrollService svc, CancellationToken ct)
            => await svc.ListContributionRulesAsync(ct));
        g.MapGet("/profiles", async ([FromQuery] Guid? workerId, IPayrollService svc, CancellationToken ct)
            => await svc.ListProfilesAsync(workerId, ct));
        g.MapPost("/profiles/{workerId:guid}", async (Guid workerId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerPayrollProfileCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpsertProfileAsync(workerId, request, ct));
        });
        // M41 Gap 3: pay-basis control (salary | timesheet planning flag)
        g.MapPut("/profiles/{workerId:guid}/pay-basis", async (Guid workerId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayBasisUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.SetPayBasisAsync(workerId, request, ct));
        });
        g.MapPut("/profiles/{workerId:guid}/overtime-policy", async (Guid workerId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OvertimePolicyUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.SetOvertimePolicyAsync(workerId, request, ct));
        });
        g.MapGet("/salary-advances", async ([FromQuery] Guid? workerId, [FromQuery] string? status, IPayrollService svc, CancellationToken ct)
            => Results.Ok(await svc.ListSalaryAdvancesAsync(workerId, status, ct)));
        g.MapPost("/salary-advances", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryAdvanceCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateSalaryAdvanceAsync(request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapPatch("/salary-advances/{advanceId:guid}", async (Guid advanceId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryAdvanceUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateSalaryAdvanceAsync(advanceId, request, ct));
        });
        g.MapPost("/salary-advances/{advanceId:guid}/cancel", async (Guid advanceId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<SalaryAdvanceCancelRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.CancelSalaryAdvanceAsync(advanceId, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapPost("/runs", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateRunAsync(request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapPost("/runs/preflight", async (HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.GetRunPreflightAsync(request, ct));
        });
        g.MapGet("/runs", async (IPayrollService svc, CancellationToken ct) => await svc.ListRunsAsync(ct));
        // M48: the top-HR approval queue — branch runs awaiting review with
        // control totals, branch names, and submission stamps. Confined users
        // are refused inside the service (403).
        g.MapGet("/queue", async (IPayrollService svc, CancellationToken ct) => await svc.ListPayrollQueueAsync(ct));
        g.MapGet("/runs/{id:guid}", async (Guid id, IPayrollService svc, CancellationToken ct)
            => await svc.GetRunAsync(id, ct));
        g.MapPatch("/runs/{id:guid}", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunUpdate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateRunAsync(id, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapGet("/runs/{id:guid}/calculation-readiness", async (Guid id, IPayrollService svc, CancellationToken ct)
            => Results.Ok(await svc.GetCalculationReadinessAsync(id, ct)));
        g.MapPost("/runs/{id:guid}/lock", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
            await svc.LockRunAsync(id, ct, ResolveSubjectId(http) ?? "system"));
        g.MapPost("/runs/{id:guid}/calculate", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
            await svc.CalculateRunAsync(id, ct, ResolveSubjectId(http) ?? "system"));
        g.MapGet("/runs/{id:guid}/lines", async (Guid id, IPayrollService svc, CancellationToken ct)
            => await svc.GetRunLinesAsync(id, ct));
        g.MapGet("/workers/{workerId:guid}/payslip-preview", async (Guid workerId, IPayrollService svc, CancellationToken ct)
            => await svc.PreviewWorkerPayslipAsync(workerId, ct));
        g.MapPost("/runs/{id:guid}/approve", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var note = await ReadBodyAsync<PayrollRunApprovalNote>(http, ct);
            await svc.ApproveRunAsync(id, note?.Note, ct, ResolveSubjectId(http) ?? "system");
            return Results.Ok();
        });
        // M46: branch payroll draft workflow — the branch preparer sends their
        // calculated run up for organisation-wide HR approval (draft | calculated
        // -> in-review, branch run only).
        g.MapPost("/runs/{id:guid}/submit-for-review", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
            await svc.SubmitRunAsync(id, ct, ResolveSubjectId(http) ?? "system"));
        g.MapPost("/runs/{id:guid}/release", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
            await svc.ReleaseRunAsync(id, ct, ResolveSubjectId(http) ?? "system"));
        g.MapPost("/runs/{id:guid}/lines/{lineId:guid}/exception", async (Guid id, Guid lineId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollExceptionDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideExceptionAsync(id, lineId, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapPost("/runs/{id:guid}/lines/{lineId:guid}/correction", async (Guid id, Guid lineId, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollCorrectionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ApplyCorrectionAsync(id, lineId, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapPost("/runs/{id:guid}/payments/generate", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
            Results.Ok(await svc.GeneratePaymentFileAsync(id, ct, ResolveSubjectId(http) ?? "system")));
        g.MapGet("/runs/{id:guid}/payments/readiness", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPaymentReadinessAsync(id, ct)));
        g.MapGet("/runs/{id:guid}/payments/file", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            Results.Text(await svc.DownloadPaymentFileAsync(id, ct), "text/csv", Encoding.UTF8));
        g.MapPost("/runs/{id:guid}/payments/approve", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollPaymentApprovalRequest>(http, ct) ?? new PayrollPaymentApprovalRequest();
            return Results.Ok(await svc.ApprovePaymentFileAsync(id, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapPost("/runs/{id:guid}/payments/release", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
            Results.Ok(await svc.ReleasePaymentFileAsync(id, ct, ResolveSubjectId(http) ?? "system")));
        g.MapPost("/runs/{id:guid}/reconcile", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollReconciliationRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ReconcileRunAsync(id, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapGet("/runs/{id:guid}/audit", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetRunAuditAsync(id, ct)));
        g.MapGet("/runs/{id:guid}/audit/export", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            Results.Text(await svc.ExportRunAuditAsync(id, ct), "text/csv", Encoding.UTF8));
        // M41: accounting-facing reports (JV detailed/summary, payroll by dept)
        // for the accounts team to book the salary into their own ledgers.
        g.MapGet("/runs/{id:guid}/reports/{kind}", async (Guid id, string kind,
            [FromQuery] string? format, IPayrollReportService svc,
            IPayrollReportPdfRenderer pdf, CancellationToken ct) =>
        {
            var reportKind = kind.ToLowerInvariant() switch
            {
                "jv-detailed" => PayrollReportKind.JvDetailed,
                "jv-summary" => PayrollReportKind.JvSummary,
                "dept-summary" => PayrollReportKind.DeptSummary,
                "dept-detailed" => PayrollReportKind.DeptDetailed,
                _ => throw new DomainException("report-kind-not-supported",
                    "kind must be jv-detailed, jv-summary, dept-summary or dept-detailed"),
            };
            var fmt = (format ?? "csv").ToLowerInvariant();
            var payload = await svc.GetAsync(reportKind, id, fmt, ct);
            var periodSlug = string.IsNullOrWhiteSpace(payload.PeriodLabel)
                ? id.ToString("D")[..8]
                : payload.PeriodLabel.Replace(" ", "-").Replace(",", "").ToLowerInvariant();
            if (fmt == "csv")
            {
                var filename = $"payroll-report-{kind}-{periodSlug}.csv";
                return Results.Text(PayrollReportFormatter.ToCsv(payload, reportKind), "text/csv", Encoding.UTF8);
            }
            if (fmt == "pdf")
            {
                var filename = $"payroll-report-{kind}-{periodSlug}.pdf";
                return Results.File(await pdf.RenderPdfAsync(PayrollReportFormatter.ToHtml(payload, reportKind), ct),
                    "application/pdf", filename);
            }
            throw new DomainException("report-format-not-supported", "format must be csv or pdf");
        });
        // M24: per-worker statutory identity readiness — the checklist the
        // release gate above enforces; inspectable before attempting release.
        g.MapGet("/runs/{id:guid}/statutory-readiness", async (Guid id, IPayrollService svc, CancellationToken ct) =>
            await svc.GetRunStatutoryReadinessAsync(id, ct));
        // M25: shared reads — HR keeps broad access; an employee-only caller
        // is restricted to their own payslips (ownership resolved from the
        // token subject).
        g.MapGet("/payslips/{workerId:guid}", async (Guid workerId, HttpContext http, IPayrollService svc, CancellationToken ct)
            => await svc.GetPayslipsAsync(workerId, ResolveSubjectId(http), ct));
        g.MapGet("/payslips/id/{id:guid}", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct)
            => await svc.GetPayslipByIdAsync(id, ResolveSubjectId(http), ct));

        // ---------- M6: cancellation/reversal, liability reports, payslip documents ----------
        g.MapPost("/runs/{id:guid}/cancel", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunReverseCreate>(http, ct) ?? new PayrollRunReverseCreate();
            return Results.Ok(await svc.CancelRunAsync(id, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapPost("/runs/{id:guid}/reverse", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PayrollRunReverseCreate>(http, ct) ?? new PayrollRunReverseCreate();
            return Results.Ok(await svc.ReverseRunAsync(id, request, ct, ResolveSubjectId(http) ?? "system"));
        });
        g.MapGet("/reports/employer-liability/{periodId:guid}", async (Guid periodId, IPayrollService svc, CancellationToken ct)
            => await svc.EmployerLiabilityReportAsync(periodId, ct));
        g.MapPost("/payslips/{id:guid}/generate", async (Guid id, IPayrollService svc, CancellationToken ct)
            => Results.Ok(await svc.GeneratePayslipDocumentAsync(id, ct)));

        // M34: admin payslip surface per run — list, bulk generate, and preview.
        g.MapGet("/runs/{id:guid}/payslips", async (Guid id, IPayrollService svc, CancellationToken ct)
            => await svc.ListRunPayslipsAsync(id, ct));
        g.MapPost("/runs/{id:guid}/payslips/generate-all", async (Guid id, HttpContext http, IPayrollService svc, CancellationToken ct)
            => Results.Ok(await svc.GenerateAllPayslipDocumentsAsync(id, ct)));
        g.MapGet("/payslips/{id:guid}/preview", async (Guid id, IPayrollService svc, CancellationToken ct) =>
        {
            var bytes = await svc.GetPayslipPreviewAsync(id, ct);
            return Results.File(bytes, "application/pdf", $"payslip-{id:D}.pdf");
        });
    }

    public static void RegisterConfig(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/admin").RequireAuthorization();
        g.MapGet("/config", async (IConfigService svc, CancellationToken ct) => await svc.GetConfigAsync(ct));
        g.MapGet("/branding", async (Mightyfin.Erp.Hrm.Application.Branding.ICompanyBrandingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(ct)));
        g.MapPut("/branding", async (HttpContext http, Mightyfin.Erp.Hrm.Application.Branding.ICompanyBrandingService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<Mightyfin.Erp.Hrm.Application.Branding.CompanyBrandingUpdateRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Branding settings are missing or invalid.");
            return Results.Ok(await svc.UpdateAsync(request, ct));
        });
        g.MapPost("/branding/reset", async (Mightyfin.Erp.Hrm.Application.Branding.ICompanyBrandingService svc, CancellationToken ct) =>
            Results.Ok(await svc.ResetAsync(ct)));
        g.MapGet("/leave-types", async ([FromQuery] bool includeInactive, IConfigService svc, CancellationToken ct) =>
            await svc.ListLeaveTypesAsync(includeInactive, ct));

        // ---------- M1: organization configuration CRUD ----------
        g.MapGet("/legal-entities", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListLegalEntitiesAsync(ct));
        g.MapGet("/legal-entities/{id:guid}", async (Guid id, IConfigAdminService svc, CancellationToken ct) => await svc.GetLegalEntityAsync(id, ct));
        g.MapPost("/legal-entities", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LegalEntityCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created($"{HrmPrefix}/admin/legal-entities/{request.Code}", await svc.CreateLegalEntityAsync(request, ct));
        });
        g.MapPatch("/legal-entities/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LegalEntityUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateLegalEntityAsync(id, request, ct));
        });

        g.MapGet("/locations", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListLocationsAsync(ct));
        // M45: branch access (confinement) — hr_admin maps platform users to the
        // branches they are allowed to work in. Operators WITH assignments can
        // never widen past them (middleware enforces); operators WITHOUT
        // assignments stay org-wide (top-level HR).
        g.MapGet("/branch-access", async (HrmDbContext db, CancellationToken ct) =>
        {
            var rows = await db.UserBranchAssignments.ToListAsync(ct);
            var locs = await db.WorkLocations.Select(x => new { x.Id, x.Name, x.LegalEntityId }).ToListAsync(ct);
            return Results.Ok(new
            {
                items = rows.Select(r => new { id = r.Id, userId = r.UserId, userEmail = r.UserEmail, locationId = r.LocationId,
                    locationName = locs.FirstOrDefault(l => l.Id == r.LocationId)?.Name }).ToList(),
                locations = locs,
            });
        });
        g.MapPost("/branch-access", async (HttpContext http, ShellContext scope, HrmDbContext db, CancellationToken ct) =>
        {
            if (!WorkerPrincipal.FromClaims(http.User.Claims).IsRole("hr_admin"))
                throw new DomainException("forbidden", "Branch access management requires the hr_admin role.");
            var request = await ReadBodyAsync<UserBranchAssignmentRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            if (request.UserId == Guid.Empty)
                throw new DomainException("bad-request", "userId is required.");
            var exists = await db.UserBranchAssignments.AnyAsync(x => x.UserId == request.UserId && x.LocationId == request.LocationId, ct);
            if (exists)
                return Results.Conflict(new { error = "duplicate", message = "User is already assigned to this branch." });
            if (!await db.WorkLocations.AnyAsync(x => x.Id == request.LocationId, ct))
                throw new DomainException("bad-request", "The target location does not exist.");
            var row = new HrUserBranchAssignment { UserId = request.UserId, UserEmail = request.UserEmail ?? "" };
            row.LocationId = request.LocationId;
            db.UserBranchAssignments.Add(row);
            await db.SaveChangesAsync(ct);
            return Results.Created("", new { id = row.Id, userId = row.UserId, userEmail = row.UserEmail, locationId = row.LocationId });
        });
        g.MapDelete("/branch-access/{id:guid}", async (Guid id, HttpContext http, ShellContext scope, HrmDbContext db, CancellationToken ct) =>
        {
            if (!WorkerPrincipal.FromClaims(http.User.Claims).IsRole("hr_admin"))
                throw new DomainException("forbidden", "Branch access management requires the hr_admin role.");
            var row = await db.UserBranchAssignments.FindAsync([id], ct)
                ?? throw new DomainException("not-found", "Assignment not found.");
            db.UserBranchAssignments.Remove(row);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { deleted = id });
        });
        g.MapPost("/locations", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkLocationCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLocationAsync(request, ct));
        });
        g.MapPatch("/locations/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkLocationUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateLocationAsync(id, request, ct));
        });

        g.MapGet("/org-units", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListOrgUnitsAsync(ct));
        g.MapGet("/org-units/tree", async (IConfigAdminService svc, CancellationToken ct) => await svc.GetOrgUnitTreeAsync(ct));
        g.MapGet("/org-units/entity-tree", async (IConfigAdminService svc, CancellationToken ct) => await svc.GetEntityTreeAsync(ct));
        g.MapPost("/org-units", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OrgUnitCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateOrgUnitAsync(request, ct));
        });
        g.MapPatch("/org-units/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OrgUnitUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateOrgUnitAsync(id, request, ct));
        });
        g.MapPost("/org-units/{id:guid}/close", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OrgUnitCloseRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            await svc.CloseOrgUnitAsync(id, request, ct);
            return Results.Ok();
        });

        g.MapGet("/calendars", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListCalendarsAsync(ct));
        g.MapGet("/calendars/{id:guid}", async (Guid id, IConfigAdminService svc, CancellationToken ct) => await svc.GetCalendarAsync(id, ct));
        g.MapPost("/calendars", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkCalendarCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCalendarAsync(request, ct));
        });
        g.MapPatch("/calendars/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkCalendarUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateCalendarAsync(id, request, ct));
        });
        g.MapPost("/holidays", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PublicHolidayCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.AddHolidayAsync(request, ct));
        });
        g.MapPatch("/holidays/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PublicHolidayUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateHolidayAsync(id, request, ct));
        });
        g.MapDelete("/holidays/{id:guid}", async (Guid id, IConfigAdminService svc, CancellationToken ct) =>
        {
            await svc.DeleteHolidayAsync(id, ct);
            return Results.Ok();
        });

        g.MapGet("/leave-types/full", async ([FromQuery] bool includeInactive, IConfigAdminService svc, CancellationToken ct) =>
            await svc.ListLeaveTypesAsync(includeInactive, ct));
        g.MapPost("/leave-types", async (HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveTypeCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateLeaveTypeAsync(request, ct));
        });
        g.MapPatch("/leave-types/{id:guid}", async (Guid id, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LeaveTypeUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateLeaveTypeAsync(id, request, ct));
        });

        g.MapGet("/capabilities", async (IConfigAdminService svc, CancellationToken ct) => await svc.ListCapabilitiesAsync(ct));
        // ---------- M28: jobs catalogue, tenant roles, retention rules ----------
        g.MapGet("/jobs", async ([FromQuery] bool includeInactive, IJobsAdminService svc, CancellationToken ct) =>
            await svc.ListJobsAsync(includeInactive, ct));
        g.MapPost("/jobs", async (HttpContext http, IJobsAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<JobCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateJobAsync(request, ct));
        });
        g.MapPatch("/jobs/{id:guid}", async (Guid id, HttpContext http, IJobsAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<JobUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateJobAsync(id, request, ct));
        });
        g.MapPost("/jobs/{id:guid}/close", async (Guid id, IJobsAdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.CloseJobAsync(id, ct)));

        g.MapGet("/roles", async (IJobsAdminService svc, CancellationToken ct) => await svc.ListRolesAsync(ct));
        g.MapPost("/roles", async (HttpContext http, IJobsAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RoleCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateRoleAsync(request, ct));
        });
        g.MapPatch("/roles/{roleKey}", async (string roleKey, HttpContext http, IJobsAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RoleUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateRoleAsync(roleKey, request, ct));
        });

        g.MapGet("/retention-rules", async (IJobsAdminService svc, CancellationToken ct) => await svc.ListRetentionRulesAsync(ct));
        g.MapPost("/retention-rules", async (HttpContext http, IJobsAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<DataRetentionCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateRetentionRuleAsync(request, ct));
        });
        g.MapPatch("/retention-rules/{id:guid}", async (Guid id, HttpContext http, IJobsAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<DataRetentionUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateRetentionRuleAsync(id, request, ct));
        });
        g.MapDelete("/retention-rules/{id:guid}", async (Guid id, IJobsAdminService svc, CancellationToken ct) =>
        {
            await svc.DeleteRetentionRuleAsync(id, ct);
            return Results.Ok();
        });

        g.MapPatch("/capabilities/{featureKey}", async (string featureKey, HttpContext http, IConfigAdminService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<CapabilityUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateCapabilityAsync(featureKey, request, ct));
        });
    }

    public static void RegisterRecruitment(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/recruitment").RequireAuthorization();
        g.MapGet("/vacancies", async ([FromQuery] string? status, IRecruitmentService svc, CancellationToken ct) =>
            await svc.ListVacanciesAsync(status, ct));
        g.MapPost("/vacancies", async (HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<VacancyCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateVacancyAsync(request, ct));
        });
        g.MapGet("/vacancies/{vacancyId:guid}/candidates", async (Guid vacancyId, [FromQuery] string? stage, IRecruitmentService svc, CancellationToken ct) =>
            await svc.ListCandidatesAsync(vacancyId, stage, ct));
        g.MapPost("/candidates", async (HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<CandidateCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCandidateAsync(request, ct));
        });
        g.MapGet("/candidates/{id:guid}", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.GetCandidateAsync(id, ct));
        g.MapPost("/candidates/{id:guid}/advance", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<CandidateAdvanceRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.AdvanceCandidateAsync(id, request, ct));
        });
        g.MapPost("/candidates/{id:guid}/interviews", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<InterviewCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateInterviewAsync(id, request, ct));
        });
        g.MapPost("/interviews/{id:guid}/decision", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<InterviewDecisionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DecideInterviewAsync(id, request, ct));
        });
        g.MapGet("/offers", async ([FromQuery] string? status, IRecruitmentService svc, CancellationToken ct) => await svc.ListOffersAsync(status, ct));
        g.MapPost("/offers", async (HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OfferCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateOfferAsync(request, ct));
        });
                g.MapPatch("/vacancies/{id:guid}", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<VacancyUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateVacancyAsync(id, request, ct));
        });
        g.MapPost("/vacancies/{id:guid}/publish", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.PublishVacancyAsync(id, ct));
        g.MapPost("/vacancies/{id:guid}/close", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.CloseVacancyAsync(id, ct));
        g.MapPost("/offers/{id:guid}/accept", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<OfferAcceptRequest>(http, ct) ?? new OfferAcceptRequest();
            return Results.Ok(await svc.AcceptOfferAsync(id, request, ct));
        });
        g.MapPost("/offers/{id:guid}/issue", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.IssueOfferAsync(id, ct));
        g.MapPost("/offers/{id:guid}/approve", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.ApproveOfferAsync(id, ct));
        g.MapPost("/offers/{id:guid}/decline", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
            await svc.DeclineOfferAsync(id, ct));
        g.MapGet("/preboarding", async ([FromQuery] string? status, IRecruitmentService svc, CancellationToken ct) => await svc.ListPreboardingAsync(status, ct));
        g.MapGet("/preboarding/{id:guid}", async (Guid id, IRecruitmentService svc, CancellationToken ct) => await svc.GetPreboardingAsync(id, ct));
        g.MapPost("/preboarding/{id:guid}/tasks", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PreboardingTaskCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.AddPreboardingTaskAsync(id, request, ct));
        });
        g.MapPatch("/preboarding/{caseId:guid}/tasks/{taskId:guid}", async (Guid caseId, Guid taskId, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<PreboardingTaskUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdatePreboardingTaskAsync(caseId, taskId, request, ct));
        });
        g.MapPost("/preboarding/{id:guid}/activate", async (Guid id, IRecruitmentService svc, CancellationToken ct) => await svc.ActivatePreboardingAsync(id, ct));
        g.MapPost("/candidates/{id:guid}/documents", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new DomainException("bad-request", "No candidate document was uploaded.");
            if (file.Length == 0) throw new DomainException("bad-request", "Uploaded file is empty.");
            var storageDir = Path.Combine(Path.GetTempPath(), "erp-candidate-docs"); Directory.CreateDirectory(storageDir);
            var storagePath = Path.Combine(storageDir, $"{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}");
            await using (var fs = File.Create(storagePath)) await file.CopyToAsync(fs, ct);
            return Results.Created("", await svc.AddCandidateDocumentAsync(id, form["category"].ToString(), form["title"].ToString(), file.FileName, file.ContentType ?? "application/octet-stream", file.Length, storagePath, ct));
        }).DisableAntiforgery();
        g.MapGet("/candidate-documents/{id:guid}/download", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
        {
            var (doc, stream) = await svc.GetCandidateDocumentAsync(id, ct);
            return Results.File(stream, doc.ContentType, doc.FileName);
        });
    }

    public static void RegisterRelations(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/relations").RequireAuthorization();
        g.MapGet("/cases", async ([FromQuery] string? category, IRelationsService svc, CancellationToken ct) =>
            await svc.ListCasesAsync(category, ct));
        g.MapPost("/cases", async (HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsCaseCreate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateCaseAsync(request, ct));
        });
        g.MapPatch("/cases/{id:guid}", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsCaseUpdate>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateCaseAsync(id, request, ct));
        });
        g.MapGet("/cases/{id:guid}", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetCaseAsync(id, ResolveSubjectId(http) ?? "system", ct)));
        g.MapPost("/cases/{id:guid}/access-declarations", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsAccessDeclarationRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.DeclareAccessAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/cases/{id:guid}/assign", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsCaseAssignRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.AssignCaseAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/cases/{id:guid}/transition", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsCaseTransitionRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.TransitionCaseAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/cases/{id:guid}/actions", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsActionCreateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.AddActionAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPatch("/cases/{caseId:guid}/actions/{actionId:guid}", async (Guid caseId, Guid actionId, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<RelationsActionUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateActionAsync(caseId, actionId, request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/cases/{id:guid}/evidence", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault() ?? throw new DomainException("bad-request", "No evidence file was uploaded.");
            var storageDir = Path.Combine(Path.GetTempPath(), "erp-relations-evidence"); Directory.CreateDirectory(storageDir);
            var storagePath = Path.Combine(storageDir, $"{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}");
            await using (var fs = File.Create(storagePath)) await file.CopyToAsync(fs, ct);
            return Results.Created("", await svc.AddEvidenceAsync(id, form["title"].ToString(), form["evidenceType"].ToString(), file.FileName,
                file.ContentType ?? "application/octet-stream", file.Length, storagePath, ResolveSubjectId(http) ?? "system", ct));
        }).DisableAntiforgery();
        g.MapGet("/evidence/{id:guid}/download", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var (evidence, stream) = await svc.GetEvidenceAsync(id, ResolveSubjectId(http) ?? "system", ct);
            return Results.File(stream, evidence.ContentType, evidence.FileName);
        });
        g.MapGet("/protected-disclosures", async ([FromQuery] string? status, IRelationsService svc, CancellationToken ct) =>
            await svc.ListProtectedDisclosuresAsync(status, ct));
        g.MapGet("/protected-disclosures/{id:guid}", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
            await svc.GetProtectedDisclosureAsync(id, ResolveSubjectId(http) ?? "system", ct));
        g.MapPost("/protected-disclosures/{id:guid}/transition", async (Guid id, HttpContext http, IRelationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ProtectedDisclosureUpdateRequest>(http, ct) ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.UpdateProtectedDisclosureAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
    }

    public static void RegisterDocuments(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/documents").RequireAuthorization();
        g.MapGet("/worker/{workerId:guid}", async (Guid workerId, IDocumentsService svc, CancellationToken ct) =>
            await svc.ListDocumentsAsync(workerId, ct));
        g.MapPost("/upload", async (HttpContext http, IDocumentsService svc, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault(f => f.Name.Equals("file", StringComparison.OrdinalIgnoreCase))
                ?? throw new DomainException("bad-request", "No file uploaded in the 'file' part.");
            if (file.Length == 0)
                throw new DomainException("bad-request", "Uploaded file is empty.");
            var workerId = Guid.Parse(form["workerId"].ToString());
            var category = form["category"].ToString();
            var title = form["title"].ToString();
            var storageDir = Path.Combine(Path.GetTempPath(), "erp-docs");
            Directory.CreateDirectory(storageDir);
            var storagePath = Path.Combine(storageDir, $"{Guid.NewGuid():N}-{file.FileName}");
            await using (var fs = File.Create(storagePath))
                await file.CopyToAsync(fs, ct);
            return Results.Created("", await svc.UploadDocumentAsync(
                workerId, category, title, file.FileName, file.ContentType ?? "application/octet-stream",
                file.Length, storagePath, ct));
        });
        g.MapGet("/{id:guid}/download", async (Guid id, IDocumentsService svc, CancellationToken ct) =>
        {
            var (doc, stream) = await svc.GetDocumentStreamAsync(id, ct);
            return Results.File(stream, doc.ContentType, doc.FileName);
        });
        var reports = app.MapGroup($"{HrmPrefix}/reports").RequireAuthorization();
        reports.MapGet("/", async ([AsParameters] ReportQuery query, IDocumentsService svc, CancellationToken ct) =>
            await svc.GetReportAsync(query, ct));
        reports.MapGet("/management", async ([AsParameters] ManagementReportQuery query, IManagementReportingService svc, CancellationToken ct) =>
            await svc.GetDashboardAsync(query, ct));
        reports.MapGet("/management/export/{reportType}", async (string reportType, [AsParameters] ManagementReportQuery query, IManagementReportingService svc, CancellationToken ct) =>
        {
            var file = await svc.ExportAsync(reportType, query, ct);
            return Results.File(file.Content, file.ContentType, file.FileName);
        });
    }

    public static void RegisterDq(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/dq").RequireAuthorization();
        g.MapGet("/checks", async (IDqService svc, CancellationToken ct) => await svc.RunChecksAsync(ct));
    }

    public static void RegisterMasterData(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/master-data").RequireAuthorization();
        g.MapGet("/batches", async ([FromQuery] string? batchType, [FromQuery] string? status,
            IMasterDataService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(batchType, status, ct)));
        g.MapPost("/imports/preview", async (HttpContext http, IMasterDataService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerImportPreviewRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.PreviewImportAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/bulk/preview", async (HttpContext http, IMasterDataService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerBulkPreviewRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.PreviewBulkAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/batches/{id:guid}/apply", async (Guid id, HttpContext http, IMasterDataService svc, CancellationToken ct) =>
            Results.Ok(await svc.ApplyAsync(id, ResolveSubjectId(http) ?? "system", ct)));
        g.MapPost("/batches/{id:guid}/rollback", async (Guid id, HttpContext http, IMasterDataService svc, CancellationToken ct) =>
            Results.Ok(await svc.RollbackAsync(id, ResolveSubjectId(http) ?? "system", ct)));
        g.MapPost("/workers/{id:guid}/reactivate", async (Guid id, HttpContext http, IMasterDataService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<WorkerReactivateRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ReactivateAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
    }

    public static void RegisterStatutory(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/statutory-exports").RequireAuthorization();
        g.MapGet("/", async ([AsParameters] StatutoryExportQuery q, IStatutoryExportService svc, CancellationToken ct) =>
        {
            var file = await svc.GenerateAsync(q.ExportType, q.PeriodId, ct);
            var bytes = await File.ReadAllBytesAsync(file, ct);
            File.Delete(file);
            return Results.File(bytes, "text/csv", $"{q.ExportType}-{q.PeriodId:N}.csv");
        });
        g.MapGet("/preview", async ([AsParameters] StatutoryExportQuery q, IStatutoryExportService svc, CancellationToken ct) =>
            Results.Ok(await svc.PreviewAsync(q.ExportType, q.PeriodId, ct)));
        // M23: aggregate statutory liability summary (PAYE/NAPSA/NHIMA totals)
        // for the reports UI — totals visible without downloading a file.
        g.MapGet("/summary", async (Guid periodId, IStatutoryExportService svc, CancellationToken ct) =>
            Results.Ok(await svc.SummaryAsync(periodId, ct)));
    }

    public static void RegisterIntegrations(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/integrations").RequireAuthorization();
        g.MapGet("/", async ([FromQuery] string? integrationKey, [FromQuery] string? status,
            IIntegrationOperationsService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(integrationKey, status, ct)));
        g.MapPost("/finance-postings", async (HttpContext http, IIntegrationOperationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<IntegrationSourceRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateFinancePostingAsync(request.SourceId, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/payment-handoffs", async (HttpContext http, IIntegrationOperationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<IntegrationSourceRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreatePaymentHandoffAsync(request.SourceId, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/statutory-handoffs", async (HttpContext http, IIntegrationOperationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<StatutoryHandoffRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.CreateStatutoryHandoffAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/identity-sync", async (HttpContext http, IIntegrationOperationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<IdentitySyncRequest>(http, ct) ?? new IdentitySyncRequest();
            return Results.Created("", await svc.CreateIdentitySyncAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/operations/{id:guid}/retry", async (Guid id, HttpContext http, IIntegrationOperationsService svc, CancellationToken ct) =>
            Results.Ok(await svc.RetryAsync(id, ResolveSubjectId(http) ?? "system", ct)));
        g.MapPost("/operations/{id:guid}/reconcile", async (Guid id, HttpContext http, IIntegrationOperationsService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<IntegrationReconciliationRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ReconcileAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapGet("/operations/{id:guid}/download", async (Guid id, IIntegrationOperationsService svc, CancellationToken ct) =>
        {
            var file = await svc.DownloadAsync(id, ct);
            return Results.File(Encoding.UTF8.GetBytes(file.Payload), file.ContentType, file.FileName);
        });
    }

    public static void RegisterSecurityCompliance(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/security").RequireAuthorization("hrm-admin");
        g.MapGet("/", async ([FromQuery] string? actor, [FromQuery] string? outcome,
            ISecurityComplianceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetDashboardAsync(actor, outcome, ct)));
        g.MapGet("/audit/export", async (ISecurityComplianceService svc, CancellationToken ct) =>
            Results.File(Encoding.UTF8.GetBytes(await svc.ExportAuditAsync(ct)), "text/csv", "hrm-privileged-audit.csv"));
        g.MapPost("/evidence", async (HttpContext http, ISecurityComplianceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ComplianceEvidenceRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.RecordEvidenceAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/legal-holds", async (HttpContext http, ISecurityComplianceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LegalHoldRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.PlaceLegalHoldAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/legal-holds/{id:guid}/release", async (Guid id, HttpContext http,
            ISecurityComplianceService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<LegalHoldReleaseRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ReleaseLegalHoldAsync(id, request, ResolveSubjectId(http) ?? "system", ct));
        });
    }

    public static void RegisterGoLive(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/go-live").RequireAuthorization();
        g.MapGet("/", async (IGoLiveReadinessService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(ct)));
        g.MapPost("/evidence", async (HttpContext http, IGoLiveReadinessService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<GoLiveEvidenceRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.RecordEvidenceAsync(request, ResolveSubjectId(http) ?? "system", ct));
        });
        g.MapPost("/signoffs/{roleKey}", async (string roleKey, HttpContext http,
            IGoLiveReadinessService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<GoLiveSignoffRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Created("", await svc.RecordSignoffAsync(roleKey, request, ResolveSubjectId(http) ?? "system", ct));
        });
    }

    // M31: shared import/export tool — one flow every CRUD page reuses.
    // Schemas register what fields each type accepts; preview resolves the
    // map-columns result to per-row statuses and apply persists only what HR
    // approved. Export returns round-trip-safe CSV.
    public static void RegisterImportExport(WebApplication app)
    {
        var g = app.MapGroup($"{HrmPrefix}/import").RequireAuthorization();
        g.MapGet("/schemas", (IImportExportService svc) => Results.Ok(svc.ListSchemas()));
        g.MapPost("/{typeKey}/preview", async (string typeKey, HttpContext http,
            IImportExportService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ImportPreviewRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.PreviewAsync(typeKey, request.FileName, request.Mode, request.Rows, ct));
        });
        g.MapPost("/{typeKey}/apply", async (string typeKey, HttpContext http,
            IImportExportService svc, CancellationToken ct) =>
        {
            var request = await ReadBodyAsync<ImportApplyRequest>(http, ct)
                ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
            return Results.Ok(await svc.ApplyAsync(request.PreviewId, request.RowIndexes, ct));
        });
        // M31b: format=xlsx in the filter string switches the output to XLSX.
        g.MapGet("/{typeKey}/export", async (string typeKey, string? filter,
            IImportExportService svc, CancellationToken ct) =>
        {
            var isXlsx = filter?.Contains("format=xlsx") == true;
            var bytes = await svc.ExportAsync(typeKey, filter, ct);
            if (isXlsx)
                return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{typeKey}-export.xlsx");
            return Results.File(bytes, "text/csv; charset=utf-8", $"{typeKey}-export.csv");
        });
    }

    // ===================== Performance & Goals (M36) =====================
public static void RegisterPerformance(WebApplication app)
{
    // HR admin: cycles
    var g = app.MapGroup($"{HrmPrefix}/performance").RequireAuthorization();
    g.MapGet("/cycles", async ([FromQuery] string? status, IPerformanceService svc, CancellationToken ct)
        => await svc.ListCyclesAsync(status, ct));
    g.MapPost("/cycles", async (HttpContext http, IPerformanceService svc, CancellationToken ct) =>
    {
        var req = await ReadBodyAsync<PerformanceCycleCreate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Created("", await svc.CreateCycleAsync(req, ct));
    });
    g.MapGet("/cycles/{id:guid}", async (Guid id, IPerformanceService svc, CancellationToken ct)
        => Results.Ok(await svc.GetCycleAsync(id, ct)));
    g.MapPatch("/cycles/{id:guid}", async (Guid id, HttpContext http, IPerformanceService svc, CancellationToken ct) =>
    {
        var req = await ReadBodyAsync<PerformanceCycleUpdate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.UpdateCycleAsync(id, req, ct));
    });
    g.MapPost("/cycles/{id:guid}/close", async (Guid id, IPerformanceService svc, CancellationToken ct)
        => Results.Ok(await svc.CloseCycleAsync(id, ct)));
    // HR admin: goals
    g.MapGet("/cycles/{id:guid}/goals", async (Guid id, [FromQuery] Guid? workerId, IPerformanceService svc, CancellationToken ct)
        => await svc.ListGoalsAsync(id, workerId, ct));
    g.MapPost("/cycles/{id:guid}/goals", async (Guid id, HttpContext http, IPerformanceService svc, CancellationToken ct) =>
    {
        var req = await ReadBodyAsync<PerformanceGoalCreate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Created("", await svc.CreateGoalAsync(id, req, ct));
    });
    g.MapPatch("/goals/{id:guid}", async (Guid id, HttpContext http, IPerformanceService svc, CancellationToken ct) =>
    {
        var req = await ReadBodyAsync<PerformanceGoalUpdate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.UpdateGoalAsync(id, req, ct));
    });
    g.MapDelete("/goals/{id:guid}", async (Guid id, IPerformanceService svc, CancellationToken ct) =>
    {
        await svc.DeleteGoalAsync(id, ct);
        return Results.Ok();
    });
    // HR admin: assessments
    g.MapGet("/cycles/{id:guid}/assessments", async (Guid id, IPerformanceService svc, CancellationToken ct)
        => await svc.ListAssessmentsAsync(id, ct));
    g.MapGet("/assessments/{id:guid}", async (Guid id, IPerformanceService svc, CancellationToken ct)
        => Results.Ok(await svc.GetAssessmentAsync(id, ct)));
    g.MapPost("/cycles/{id:guid}/assessments", async (Guid id, IPerformanceService svc, CancellationToken ct)
        => Results.Ok(await svc.EnsureAssessmentsAsync(id, ct)));
    g.MapPatch("/assessments/{id:guid}/manager", async (Guid id, HttpContext http, IPerformanceService svc, CancellationToken ct) =>
    {
        var req = await ReadBodyAsync<ManagerAssessmentSubmit>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.SubmitManagerAssessmentAsync(id, req, ct));
    });
    g.MapPatch("/assessments/{id:guid}/finalize", async (Guid id, HttpContext http, IPerformanceService svc, CancellationToken ct) =>
    {
        var req = await ReadBodyAsync<FinalizeAssessment>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.FinalizeAssessmentAsync(id, req, ct));
    });
    // HR admin: report
    g.MapGet("/cycles/{id:guid}/report", async (Guid id, IPerformanceService svc, CancellationToken ct)
        => Results.Ok(await svc.GetCycleReportAsync(id, ct)));
}

// M37: Offboarding & Exit Management
public static void RegisterOffboarding(WebApplication app)
{
    var g = app.MapGroup($"{HrmPrefix}/offboarding").RequireAuthorization();

    // HR admin: list and manage offboarding requests
    g.MapGet("/", async (string? status, IOffboardingService svc, CancellationToken ct) =>
        Results.Ok(await svc.ListRequestsAsync(status, ct)));

    g.MapPost("/", async (HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<OffboardingRequestCreate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        var subject = ResolveSubjectId(http) ?? "";
        return Results.Ok(await svc.CreateRequestAsync(request, subject, ct));
    });

    g.MapGet("/{id:guid}", async (Guid id, IOffboardingService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetRequestAsync(id, ct)));

    g.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var subject = ResolveSubjectId(http) ?? "";
        return Results.Ok(await svc.ApproveRequestAsync(id, subject, ct));
    });

    g.MapPost("/{id:guid}/reject", async (Guid id, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var subject = ResolveSubjectId(http) ?? "";
        var body = await ReadBodyAsync<RejectRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.RejectRequestAsync(id, body.Reason, subject, ct));
    });

    g.MapPost("/{id:guid}/cancel", async (Guid id, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var body = await ReadBodyAsync<CancelRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.CancelRequestAsync(id, body.Reason, ct));
    });

    g.MapPost("/{id:guid}/final-pay", async (Guid id, IOffboardingService svc, CancellationToken ct) =>
        Results.Ok(await svc.MarkFinalPayProcessedAsync(id, ct)));

    // Checklist items
    g.MapPost("/{id:guid}/checklist", async (Guid id, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<ChecklistItemCreate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.AddChecklistItemAsync(id, request, ct));
    });

    g.MapPatch("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<ChecklistItemUpdate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.UpdateChecklistItemAsync(id, itemId, request, ct));
    });

    g.MapPost("/{id:guid}/checklist/{itemId:guid}/complete", async (Guid id, Guid itemId, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var subject = ResolveSubjectId(http) ?? "";
        return Results.Ok(await svc.CompleteChecklistItemAsync(itemId, subject, ct));
    });

    // Exit interview
    g.MapPost("/{id:guid}/exit-interview", async (Guid id, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<ExitInterviewCreate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.CreateExitInterviewAsync(id, request, ct));
    });

    g.MapGet("/{id:guid}/exit-interview", async (Guid id, IOffboardingService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetExitInterviewAsync(id, ct)));

    g.MapPatch("/{id:guid}/exit-interview", async (Guid id, HttpContext http, IOffboardingService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<ExitInterviewUpdate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.UpdateExitInterviewAsync(id, request, ct));
    });
}

// M38: Requisition Pipeline
public static void RegisterRequisitions(WebApplication app)
{
    var g = app.MapGroup($"{HrmPrefix}/requisitions").RequireAuthorization();

    g.MapGet("/", async (string? status, IRecruitmentService svc, CancellationToken ct) =>
        Results.Ok(await svc.ListRequisitionsAsync(status, ct)));

    g.MapPost("/", async (HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<RequisitionCreate>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.CreateRequisitionAsync(request, ct));
    });

    g.MapGet("/{id:guid}", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetRequisitionAsync(id, ct)));

    g.MapPatch("/{id:guid}", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<RequisitionUpdateRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.UpdateRequisitionAsync(id, request, ct));
    });

    g.MapPost("/{id:guid}/submit", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
        Results.Ok(await svc.SubmitRequisitionAsync(id, ct)));

    g.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
    {
        var body = await ReadBodyAsync<RequisitionDecisionRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.ApproveRequisitionAsync(id, body, ct));
    });

    g.MapPost("/{id:guid}/return", async (Guid id, HttpContext http, IRecruitmentService svc, CancellationToken ct) =>
    {
        var body = await ReadBodyAsync<RequisitionDecisionRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.ReturnRequisitionAsync(id, body, ct));
    });

    // Pipeline funnel stats for a vacancy (M38)
    app.MapGet($"{HrmPrefix}/vacancies/{{id:guid}}/pipeline", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetVacancyPipelineAsync(id, ct))).RequireAuthorization();

    // Offer letter generation (M38)
    app.MapGet($"{HrmPrefix}/offers/{{id:guid}}/letter", async (Guid id, IRecruitmentService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetOfferLetterAsync(id, ct))).RequireAuthorization();
    // M39: organization chart + reporting lines.
    app.MapGet($"{HrmPrefix}/org-chart", async (IChartService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetOrgChartAsync(ct))).RequireAuthorization();
    var reportingLines = app.MapGroup($"{HrmPrefix}/reporting-lines").RequireAuthorization();
    reportingLines.MapGet("/", async (Guid? orgUnitId, string? search, IChartService svc, CancellationToken ct) =>
        Results.Ok(await svc.ListReportingLinesAsync(orgUnitId, search, ct)));
    reportingLines.MapPost("/", async (HttpContext http, IChartService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<ReportingLineUpdateRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        await svc.UpdateReportingLinesAsync(request, ct);
        return Results.Ok();
    });
    // M40: HR analytics dashboard — workforce, leave, payroll cost, performance,
    // recruitment and attendance panels in a single call for HR/HRM users.
    app.MapGet($"{HrmPrefix}/analytics/dashboard", async (IAnalyticsService svc, CancellationToken ct) =>
        Results.Ok(await svc.GetDashboardAsync(ct))).RequireAuthorization();
}

public static void RegisterBenefits(WebApplication app)
{
    var g = app.MapGroup($"{HrmPrefix}/benefits").RequireAuthorization();

    // Benefit types (hr_admin configures claim categories)
    g.MapGet("/types", async (Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
        Results.Ok(await svc.ListBenefitTypesAsync(ct)));
    g.MapPost("/types", async (HttpContext http,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<Application.Benefits.BenefitTypeCreateRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.CreateBenefitTypeAsync(request, ct));
    });
    g.MapPut("/types/{id:guid}", async (Guid id, HttpContext http,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<Application.Benefits.BenefitTypeUpdateRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.UpdateBenefitTypeAsync(id, request, ct));
    });
    g.MapDelete("/types/{id:guid}", async (Guid id,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        await svc.DeleteBenefitTypeAsync(id, ct);
        return Results.Ok();
    });

    // Per-worker annual allowances (hr_admin / hr_ops)
    g.MapGet("/allowances", async (Guid? workerId,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
        Results.Ok(await svc.ListAllowancesAsync(workerId, ct)));
    g.MapPost("/allowances", async (HttpContext http,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<Application.Benefits.AllowanceSetRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        await svc.SetAllowanceAsync(request, ct);
        return Results.Ok();
    });
    g.MapDelete("/allowances/{id:guid}", async (Guid id,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        await svc.DeleteAllowanceAsync(id, ct);
        return Results.Ok();
    });

    // Claims (submit by employee/hr, decide by HR, pay by payroll)
    g.MapGet("/claims", async (Guid? workerId, string? status, int? page, int? pageSize,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        var (items, total) = await svc.ListClaimsAsync(workerId, status, page ?? 1, pageSize ?? 50, ct);
        return Results.Ok(new { Items = items, Total = total, Page = page ?? 1, PageSize = pageSize ?? 50 });
    });
    g.MapPost("/claims", async (HttpContext http,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<Application.Benefits.BenefitClaimCreateRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.CreateClaimAsync(request, ct));
    });
    g.MapPost("/claims/{id:guid}/decide", async (Guid id, HttpContext http,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
    {
        var request = await ReadBodyAsync<Application.Benefits.ClaimDecideRequest>(http, ct)
            ?? throw new DomainException("bad-request", "Request body is missing or invalid.");
        return Results.Ok(await svc.DecideClaimAsync(id, request, ct));
    });
    g.MapPost("/claims/{id:guid}/pay", async (Guid id,
        Mightyfin.Erp.Hrm.Application.Benefits.IBenefitService svc, CancellationToken ct) =>
        Results.Ok(await svc.PayClaimAsync(id, ct)));
}
}

// Route-local binding types.
public sealed record RejectRequest(string Reason);
public sealed record CancelRequest(string Reason);
public sealed record PayrollRunApprovalNote(string? Note);
public sealed record StatutoryExportQuery(string ExportType, Guid PeriodId);
// M31 import/export: the UI sends client-mapped rows (file column → canonical
// field key already resolved by the Map Columns step) plus the desired mode.
public sealed record ImportPreviewRequest(string FileName, string Mode, List<Dictionary<string, string>> Rows);
public sealed record ImportApplyRequest(Guid PreviewId, List<int> RowIndexes);
/// <summary>Current API version resolved from the URL path by Program.cs.</summary>
public sealed class ApiVersioning
{
    public int CurrentVersion { get; set; }
}
