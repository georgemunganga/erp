using System.Data;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record OnPremMobileCompany(Guid Id, string Tenant, bool Searchable);
internal sealed record OnPremMobileOptions(string Origin, OnPremMobileCompany[] Companies, string? TrustedProxy = null)
{
    internal static bool Enabled(IConfiguration c) => c.GetValue<bool>("ERP:OnPremMobile:Enabled");
    internal static OnPremMobileOptions Read(IConfiguration c)
    {
        var mode = c["ERP:AuthMode"] ?? c["HRM:AuthMode"] ?? "local";
        var origin = c["ERP:OnPremMobile:PublicOrigin"] ?? "";
        if (mode != "local" || !Uri.TryCreate(origin, UriKind.Absolute, out var u)
            || u.Scheme != "https" || u.GetLeftPart(UriPartial.Authority) != origin || u.UserInfo != "")
            throw new InvalidOperationException("On-prem mobile requires local authentication and an exact HTTPS public origin.");
        var rows = c.GetSection("ERP:OnPremMobile:Companies").GetChildren().Select(x =>
            new OnPremMobileCompany(Guid.Parse(x["Id"] ?? ""), x["Tenant"] ?? "", x.GetValue<bool>("Searchable"))).ToArray();
        if (rows.Length is < 1 or > 20 || rows.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Tenant) || x.Tenant.Length > 100)
            || rows.Select(x => x.Id).Distinct().Count() != rows.Length)
            throw new InvalidOperationException("On-prem mobile requires an explicit company/tenant allowlist.");
        var proxy=c["ERP:OnPremMobile:TrustedProxyAddress"];
        if(proxy is not null && !IPAddress.TryParse(proxy,out _))throw new InvalidOperationException("An exact trusted proxy IP is required.");
        return new(origin, rows,proxy);
    }
}

/// <summary>Separate mobile-only budget; failed mobile logins cannot lock browser ERP accounts.</summary>
internal sealed class OnPremMobileLoginBudget : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter=PartitionedRateLimiter.Create<string,string>(key=>
        RateLimitPartition.GetFixedWindowLimiter(key,_=>new FixedWindowRateLimiterOptions {
            PermitLimit=5,Window=TimeSpan.FromMinutes(15),QueueLimit=0,AutoReplenishment=true
        }));
    internal bool Admit(Guid company,string identifier){using var lease=limiter.AttemptAcquire(company.ToString("D")+":"+LocalSessionTokens.Hash(identifier));return lease.IsAcquired;}
    public void Dispose()=>limiter.Dispose();
}

/// <summary>Standalone-only adapter. It never registers central identities or changes browser authentication.</summary>
internal static class OnPremMobileApi
{
    internal const string Prefix = "/mobile/v1";
    internal sealed record Login(Guid CompanyId, string Identifier, string Password);
    internal sealed record Refresh(Guid CompanyId, string RefreshToken);
    private sealed record Tenant(string Value) : ITenantAccessor { public string GetTenantId() => Value; }
    private sealed record Grant(LocalMobileSession Session, LocalUser User, Worker Worker, LegalEntity Company, WorkLocation Location);
    private static readonly string DummyHash = LocalPasswordHash.Hash("unusable-mobile-password-" + Guid.NewGuid());

    internal static void Register(IServiceCollection services, IConfiguration c, string connection)
    {
        if (!OnPremMobileOptions.Enabled(c)) return;
        services.AddSingleton(OnPremMobileOptions.Read(c));
        services.AddSingleton<OnPremMobileLoginBudget>();
        services.AddSingleton<Func<string, HrmDbContext>>(_ => tenant => new HrmDbContext(
            new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection,
                pg => pg.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options, new Tenant(tenant)));
    }
    internal static string RateKey(HttpContext h)
    {
        var connected=h.Connection.RemoteIpAddress;
        var proxy=h.RequestServices.GetService<OnPremMobileOptions>()?.TrustedProxy;
        var value=h.Request.Headers["X-Real-IP"];
        if(proxy is not null && connected is not null && IPAddress.Parse(proxy).MapToIPv6().Equals(connected.MapToIPv6())
            && value.Count==1 && IPAddress.TryParse(value[0],out var caller))return caller.ToString();
        return connected?.ToString()??"unknown";
    }
    internal static void Map(WebApplication app)
    {
        if (!OnPremMobileOptions.Enabled(app.Configuration)) return;
        var options = app.Services.GetRequiredService<OnPremMobileOptions>();
        var g = app.MapGroup(Prefix).AllowAnonymous().RequireRateLimiting("onprem-mobile-read");
        // No cookie/JWT tenant selector can contribute authority to this boundary.
        g.AddEndpointFilter(async (context, next) => {
            var h = context.HttpContext;
            h.Response.Headers.CacheControl = "no-store";
            h.Response.Headers["Pragma"] = "no-cache";
            if (h.Request.Headers.ContentLength > 4096) return Error(413, "request_too_large");
            string[] forbidden = ["Cookie", "X-Tenant", "X-Tenant-Id", "X-Shell-Entity", "X-Shell-Location", "X-Branch-Id", "X-All-Branches"];
            if (forbidden.Any(h.Request.Headers.ContainsKey) || h.Request.Query.Keys.Any(x => x != "query")) return Error(400, "invalid_request");
            h.User = new ClaimsPrincipal(new ClaimsIdentity());
            try { return await next(context); }
            catch (DbUpdateConcurrencyException) { return Error(401, "session_changed"); }
            catch (System.Data.Common.DbException) { return Error(503, "service_unavailable"); }
            catch (TimeoutException) { return Error(503, "service_unavailable"); }
        });
        g.MapGet("/profile", () => Results.Ok(new { schema_version = 1, authentication = "local-employee-v1",
            api_origin = options.Origin, api_path = Prefix, attendance_mode = "server-online", central_registration = false }));
        g.MapGet("/companies", (Func<HttpContext,Task<IResult>>)Search);
        g.MapPost("/auth/sign-in", SignIn).RequireRateLimiting("onprem-mobile-login");
        g.MapPost("/auth/refresh", Rotate).RequireRateLimiting("onprem-mobile-login");
        g.MapPost("/auth/sign-out", SignOut);
        g.MapGet("/me", (Func<HttpContext, Task<IResult>>)(h => Execute(h, null)));
        g.MapPost("/attendance/clock-in", (Func<HttpContext, Task<IResult>>)(h => Execute(h, true)));
        g.MapPost("/attendance/clock-out", (Func<HttpContext, Task<IResult>>)(h => Execute(h, false)));
    }
    internal static void UseBoundary(WebApplication app)
    {
        if (!OnPremMobileOptions.Enabled(app.Configuration)) return;
        var origin = app.Services.GetRequiredService<OnPremMobileOptions>().Origin;
        app.Use(async (h,next) => {
            if (!h.Request.Path.StartsWithSegments(Prefix)) { await next(h);return; }
            h.Response.Headers.CacheControl="no-store";
            string[] forbidden=["Cookie","X-Tenant","X-Tenant-Id","X-Shell-Entity","X-Shell-Location","X-Branch-Id","X-All-Branches"];
            if (forbidden.Any(h.Request.Headers.ContainsKey) || h.Request.Host.Value != new Uri(origin).Authority
                || h.Request.Headers.TryGetValue("Origin",out var site) && site.ToString()!=origin) {
                await Error(400,"invalid_request").ExecuteAsync(h);return;
            }
            var size=h.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if(size is {IsReadOnly:false})size.MaxRequestBodySize=4096;
            await next(h);
        });
    }
    private static IResult Error(int status, string code) => Results.Json(new { error = new { code } }, statusCode: status);
    private static Func<string,HrmDbContext> Contexts(HttpContext h) => h.RequestServices.GetRequiredService<Func<string,HrmDbContext>>();
    private static OnPremMobileOptions Options(HttpContext h) => h.RequestServices.GetRequiredService<OnPremMobileOptions>();
    private static DateTimeOffset Now(HttpContext h) => (h.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();
    private static OnPremMobileCompany? Company(HttpContext h, Guid id) => Options(h).Companies.SingleOrDefault(x => x.Id == id);
    private static bool Token(string? t) => t is {Length:43} && Regex.IsMatch(t, "\\A[A-Za-z0-9_-]{43}\\z");
    private static string? Bearer(HttpContext h)
    {
        var a = h.Request.Headers.Authorization;
        return a.Count == 1 && a[0] is {} s && s.StartsWith("Bearer ", StringComparison.Ordinal) && Token(s[7..]) ? s[7..] : null;
    }
    private static object PublicCompany(LegalEntity c, string origin) => new { id = c.Id, name = c.TradingName ?? c.RegisteredName,
        code = c.Code, domain = new Uri(origin).Host };
    private static async Task<IResult> Search(HttpContext h)
    {
        var q = h.Request.Query["query"];
        if (q.Count != 1 || q.ToString().Trim().Length is < 2 or > 100) return Error(400, "invalid_query");
        var query = q.ToString().Trim(); var items = new List<object>();
        foreach (var allowed in Options(h).Companies)
        {
            await using var db = Contexts(h)(allowed.Tenant);
            var c = await db.LegalEntities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == allowed.Id && !x.IsArchived, h.RequestAborted);
            if (c is null) continue;
            if (string.Equals(c.Code, query, StringComparison.OrdinalIgnoreCase) || allowed.Searchable &&
                new[]{c.RegisteredName, c.TradingName ?? "", new Uri(Options(h).Origin).Host}.Any(x => x.Contains(query, StringComparison.OrdinalIgnoreCase)))
                items.Add(PublicCompany(c, Options(h).Origin));
            if (items.Count == 10) break;
        }
        return Results.Ok(new {items});
    }
    private static async Task<Grant?> Admit(HrmDbContext db, OnPremMobileCompany allowed, LocalMobileSession session, HttpContext h, bool issuing = false)
    {
        var ct = h.RequestAborted; var now = Now(h);
        if (!issuing) {
            var current = await db.LocalMobileSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.Id, ct);
            if (current is null || current.AccessHash != session.AccessHash || current.RefreshHash != session.RefreshHash
                || current.RevokedAt != null || current.IsArchived || current.ExpiresAt <= now) return null;
        }
        if (session.TenantId != allowed.Tenant || session.LegalEntityId != allowed.Id || session.Origin != Options(h).Origin
            || session.IsArchived || session.RevokedAt != null || session.ExpiresAt <= now) return null;
        var u = await db.LocalUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.LocalUserId && !x.IsArchived && x.IsActive, ct);
        if (u is null || u.CredentialMode != LocalCredentialModes.Local || !LocalCredentialModes.IsValid(u)
            || u.MustChangePassword || u.LockedUntil > now || u.WorkerId != session.WorkerId
            || session.PasswordVersion != LocalSessionTokens.Hash(u.PasswordHash!)) return null;
        var roles = await LocalIdentityRoutes.ResolveGrantedRolesAsync(db, u.RolesCsv, ct);
        if (!roles.Contains("employee", StringComparer.OrdinalIgnoreCase)) return null;
        var c = await db.LegalEntities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == allowed.Id && !x.IsArchived, ct);
        var w = await db.Workers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.WorkerId && !x.IsArchived, ct);
        if (c is null || w?.LocationId is not {} location || w.SubjectId != u.Id.ToString("D")
            || w.Status is not ("active" or "on-leave" or "notice")) return null;
        DateOnly today;
        try { today = MobileAttendanceTime.Local(now, c.TimeZoneId).Date; }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
        if (w.StartDate > today || w.EndDate < today) return null;
        var l = await db.WorkLocations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == location && x.LegalEntityId == c.Id && !x.IsArchived, ct);
        if (l is null) return null;
        var branches = await db.UserBranchAssignments.AsNoTracking().Where(x => x.UserId == u.Id && !x.IsArchived).Select(x => x.LocationId).ToArrayAsync(ct);
        return branches.Length != 0 && !branches.Contains(location) ? null : new(session, u, w, c, l);
    }
    private static async Task<IResult> SignIn(Login r, HttpContext h)
    {
        if (Company(h, r.CompanyId) is not {} allowed || r.Identifier is null || r.Identifier.Length is < 1 or > 254
            || r.Password is null || r.Password.Length is < 1 or > 256 || h.Request.Headers.ContainsKey("Authorization")) return Error(401, "invalid_credentials");
        await using var db = Contexts(h)(allowed.Tenant);
        var identifier = r.Identifier.Trim().ToUpperInvariant();
        if(!h.RequestServices.GetRequiredService<OnPremMobileLoginBudget>().Admit(r.CompanyId,identifier))return Error(429,"mobile_login_limited");
        // An identifier looks up an already linked principal, never links matching contact details.
        var users = await db.LocalUsers.AsNoTracking().Where(x => x.CredentialMode == LocalCredentialModes.Local
            && x.NormalizedEmail == identifier && !x.IsArchived).Take(2).ToArrayAsync(h.RequestAborted);
        var u = users.Length == 1 ? users[0] : null;
        var verified = LocalPasswordHash.Verify(r.Password, u?.PasswordHash ?? DummyHash);
        if (!verified || u?.WorkerId is not {} worker) return Error(401, "invalid_credentials");
        var now = Now(h); var access = LocalSessionTokens.Create(); var refresh = LocalSessionTokens.Create();
        var session = new LocalMobileSession { Id = Guid.CreateVersion7(), LocalUserId = u.Id, WorkerId = worker,
            LegalEntityId = allowed.Id, Origin = Options(h).Origin, PasswordVersion = LocalSessionTokens.Hash(u.PasswordHash!),
            AccessHash = LocalSessionTokens.Hash(access), RefreshHash = LocalSessionTokens.Hash(refresh), AccessExpiresAt = now.AddMinutes(10),
            ExpiresAt = now.AddDays(7), TenantId = allowed.Tenant, CreatedBy = u.Id.ToString("D") };
        var grant = await Admit(db, allowed, session, h, issuing:true);
        if (grant is null) return Error(401, "invalid_credentials");
        db.LocalMobileSessions.Add(session);
        db.AuditEntries.Add(Audit(allowed.Tenant, u.Id, "mobile-session-issued", session.Id));
        await db.SaveChangesAsync(h.RequestAborted);
        return Results.Ok(new {accessToken = access, refreshToken = refresh, expiresAt = session.AccessExpiresAt,
            company = PublicCompany(grant.Company, Options(h).Origin)});
    }
    private static AuditEntry Audit(string tenant, Guid user, string action, Guid target) => new() {
        TenantId = tenant, ActorSubjectId = user.ToString("D"), Action = action, EntityType = "local-mobile-session", EntityId = target.ToString("D") };
    private static async Task<IResult> Rotate(Refresh r, HttpContext h)
    {
        if (Company(h, r.CompanyId) is not {} allowed || !Token(r.RefreshToken) || h.Request.Headers.ContainsKey("Authorization")) return Error(401, "session_expired");
        await using var db = Contexts(h)(allowed.Tenant);
        var hash = LocalSessionTokens.Hash(r.RefreshToken);
        var s = await db.LocalMobileSessions.SingleOrDefaultAsync(x => x.RefreshHash == hash && x.LegalEntityId == allowed.Id, h.RequestAborted);
        if (s is null || await Admit(db, allowed, s, h) is null) return Error(401, "session_expired");
        var access = LocalSessionTokens.Create(); var refresh = LocalSessionTokens.Create();
        s.AccessHash = LocalSessionTokens.Hash(access); s.RefreshHash = LocalSessionTokens.Hash(refresh); s.AccessExpiresAt = Now(h).AddMinutes(10);
        await db.SaveChangesAsync(h.RequestAborted);
        return Results.Ok(new {accessToken = access, refreshToken = refresh, expiresAt = s.AccessExpiresAt});
    }
    private static async Task<IResult> SignOut(Refresh r, HttpContext h)
    {
        if (Company(h,r.CompanyId) is not {} allowed || !Token(r.RefreshToken)) return Results.NoContent();
        await using var db = Contexts(h)(allowed.Tenant);
        var hash = LocalSessionTokens.Hash(r.RefreshToken);
        var s = await db.LocalMobileSessions.SingleOrDefaultAsync(x => x.RefreshHash == hash && x.LegalEntityId == allowed.Id, h.RequestAborted);
        if (s is not null && s.RevokedAt is null) {
            s.RevokedAt = Now(h); db.AuditEntries.Add(Audit(allowed.Tenant,s.LocalUserId,"mobile-session-revoked",s.Id));
            await db.SaveChangesAsync(h.RequestAborted);
        }
        return Results.NoContent();
    }
    private static async Task<IResult> Execute(HttpContext h, bool? clockIn)
    {
        if (!Guid.TryParseExact(h.Request.Headers["X-Workplace-Company"], "D", out var company)
            || Company(h, company) is not {} allowed || Bearer(h) is not {} token || h.Request.Query.Count != 0) return Error(401,"session_expired");
        await using var db = Contexts(h)(allowed.Tenant);
        var hash = LocalSessionTokens.Hash(token);
        var s = await db.LocalMobileSessions.AsNoTracking().SingleOrDefaultAsync(x => x.AccessHash == hash && x.LegalEntityId == company, h.RequestAborted);
        if (s is null || s.AccessExpiresAt <= Now(h) || await Admit(db, allowed, s, h) is not {} grant) return Error(401,"session_expired");
        if (clockIn is null) {
            var view = await View(db, grant, h);
            return await Admit(db,allowed,s,h) is null ? Error(401,"session_expired") : Results.Ok(view);
        }
        string? date = null; Guid? record = null;
        JsonDocument body;
        try {
            var bytes = new byte[4097]; var used = 0;
            while (used < bytes.Length) { var n = await h.Request.Body.ReadAsync(bytes.AsMemory(used),h.RequestAborted); if(n==0)break;used+=n; }
            if (used > 4096) return Error(413,"request_too_large");
            body = JsonDocument.Parse(bytes.AsMemory(0,used),new JsonDocumentOptions{MaxDepth=2});
        }
        catch (JsonException) { return Error(400,"invalid_request"); }
        using (body) {
            var field = clockIn.Value ? "expected_work_date" : "expected_record_id";
            var root = body.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1
                || !root.TryGetProperty(field,out var value) || value.ValueKind != JsonValueKind.String) return Error(400,"invalid_request");
            if (clockIn.Value) { date = value.GetString(); if (!DateOnly.TryParseExact(date,"yyyy-MM-dd",out _)) return Error(400,"invalid_request"); }
            else { if (!Guid.TryParseExact(value.GetString(),"D",out var id)) return Error(400,"invalid_request"); record=id; }
        }
        // Serializable transaction also rechecks account/worker changes at commit.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,h.RequestAborted);
        if (db.Database.IsNpgsql()) {
            var key = "mobile.attendance.worker.v1\n" + allowed.Tenant + "\n" + grant.Worker.Id.ToString("D");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))",h.RequestAborted);
        }
        var before = await Admit(db,allowed,s,h);
        if (before is null || before.Worker.LocationId != grant.Worker.LocationId) return Error(401,"session_expired");
        h.User = new(new ClaimsIdentity([new("tenant",allowed.Tenant),new("sub",grant.User.Id.ToString("D")),
            new(ClaimTypes.NameIdentifier,grant.User.Id.ToString("D")),new(ClaimTypes.Role,"employee")],"onprem-mobile"));
        var shell = new ShellContext {EntityId = company,LocationId = grant.Location.Id}; shell.AllowedLocationIds.Add(grant.Location.Id);
        var service = new TimeServiceImpl(new TimeRepository(db), new AuthzServiceImpl(new HttpContextAccessor{HttpContext=h},h.RequestServices.GetRequiredService<IConfiguration>()),
            h.RequestServices.GetRequiredService<IWorkflowService>(), new WorkerRepository(db), shell,
            new PayrollRepository(db),unitOfWork:new EfUnitOfWork(db));
        try {
            await service.PunchMobileAsync(grant.Worker.Id,clockIn.Value,Now(h),grant.Company.TimeZoneId,date,record,h.RequestAborted);
            if (await Admit(db,allowed,s,h) is null) return Error(401,"session_expired");
            var result = await View(db,grant,h);
            await tx.CommitAsync(h.RequestAborted);
            return Results.Ok(result);
        } catch (DomainException) { return Error(409,"attendance_needs_review"); }
    }
    private static async Task<object> View(HrmDbContext db, Grant g, HttpContext h)
    {
        var now = Now(h);var today = MobileAttendanceTime.Local(now,g.Company.TimeZoneId).Date;
        var rows = await db.AttendanceRecords.AsNoTracking().Where(x => x.WorkerId == g.Worker.Id && !x.IsArchived && x.ClockIn != null
            && (x.WorkDate >= today.AddDays(-90) || x.ClockOut == null)).OrderByDescending(x => x.WorkDate).Take(100).ToArrayAsync(h.RequestAborted);
        var department = g.Worker.OrgUnitId is {} id ? await db.OrgUnits.Where(x => x.Id == id && !x.IsArchived).Select(x=>x.Name).SingleOrDefaultAsync(h.RequestAborted) : null;
        return new {employee = new {id=g.Worker.Id,employee_number=g.Worker.EmployeeNo,name=g.Worker.FullName,role=g.Worker.JobTitle,
            department,company=g.Company.TradingName??g.Company.RegisteredName,location=g.Location.Name,email=g.Worker.Email,phone=g.Worker.Phone,
            joined=g.Worker.StartDate,status=g.Worker.Status},
            attendance=new {company_id=g.Company.Id,employee_id=g.Worker.Id,server_time=now,work_date=today.ToString("yyyy-MM-dd"),time_zone=g.Company.TimeZoneId,
                records=rows.Select(x=>new{id=x.Id,date=x.WorkDate.ToString("yyyy-MM-dd"),check_in=MobileAttendanceTime.Instant(x,false,g.Company.TimeZoneId),check_out=MobileAttendanceTime.Instant(x,true,g.Company.TimeZoneId)})}};
    }
}
