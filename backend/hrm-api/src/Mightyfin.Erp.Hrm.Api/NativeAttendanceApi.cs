using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed record NativeEmployeeGrant(CoreErpRequestScope Scope, Worker Worker, Guid Link, Guid GrantVersion,
    int GrantRevision, string CompanyName, string TimeZone, string LocationName);
internal sealed record NativeGrantResult(int Status, NativeEmployeeGrant? Grant = null);

/// <summary>Current Core/HR authorization only. No native request can create a link, membership or role.</summary>
internal sealed class NativeEmployeeAdmission(Func<string,HrmDbContext> contexts,
    Func<string,CancellationToken,Task<CommercialLicenseResult>> license, TimeProvider clock)
{
    internal async Task<NativeGrantResult> ReadAsync(OperationalSubjectObservation person, Guid company, Guid link, CancellationToken ct)
    {
        try { return await ReadCoreAsync(person,company,link,ct); }
        catch (System.Data.Common.DbException) { return new(503); }
        catch (TimeoutException) { return new(503); }
    }
    private async Task<NativeGrantResult> ReadCoreAsync(OperationalSubjectObservation person, Guid company, Guid link, CancellationToken ct)
    {
        if (!person.IsCurrent || company == Guid.Empty || link == Guid.Empty) return new(403);
        await using var db = contexts("untrusted-native-admission");
        var candidate = await new CoreCompanyAccess(db, clock).FindCandidateAsync(ProductCommercialLicense.Issuer, person.Subject, link, company, ct);
        if (candidate is null) return new(403);
        var paid = await license(candidate.EnrollmentId, ct);
        if (paid.Status != CommercialLicenseStatus.Authorized || paid.Lease is null)
            return new(paid.Status == CommercialLicenseStatus.Denied ? 403 : 503);
        var access = await new CoreModuleAccessReader(db, clock).ReadAsync(person, link, company, "hrm", paid.Lease, ct);
        if (access.Status != ModuleAccessStatus.Authorized || access.Decision is not {} grant)
            return new(access.Status == ModuleAccessStatus.Unavailable ? 503 : 403);
        // The provisioned SaaS tenant is exactly this company. Do not apply the
        // legacy tenant-wide attendance model to a multi-company tenant.
        if (await db.LegalEntities.IgnoreQueryFilters().AnyAsync(x => x.TenantId == grant.TenantId && x.Id != company, ct)) return new(403);
        var subject = grant.PrincipalId.ToString("D");
        var workers = await db.Workers.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == grant.TenantId
            && x.SubjectId == subject && !x.IsArchived).Take(2).ToArrayAsync(ct);
        if (workers.Length != 1 || workers[0].LocationId is not {} location) return new(403);
        var worker = workers[0];
        var owned = await db.WorkLocations.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == grant.TenantId
            && x.Id == location && x.LegalEntityId == company && !x.IsArchived, ct);
        if (owned is null || !grant.AllCompanyLocations && !grant.LocationIds.Contains(location)) return new(403);
        DateOnly today;
        try { today = MobileAttendanceTime.Local(clock.GetUtcNow(), candidate.TimeZoneId).Date; }
        catch (TimeZoneNotFoundException) { return new(503); }
        catch (InvalidTimeZoneException) { return new(503); }
        if (worker.Status is not ("active" or "on-leave" or "notice") || worker.StartDate > today || worker.EndDate < today
            || !grant.IsCurrent || !person.IsCurrent || !grant.AllowsAction("hrm-access")) return new(403);
        return new(200, new(new(grant, false, [location]), worker, link, grant.VersionId, grant.Revision,
            candidate.CompanyName, candidate.TimeZoneId, owned.Name));
    }

    internal async Task<object> CompaniesAsync(OperationalSubjectObservation person, Guid? after, CancellationToken ct)
    {
        await using var db = contexts("untrusted-native-directory");
        var page = await new CoreCompanyAccess(db, clock).MemberDirectoryAsync(ProductCommercialLicense.Issuer, person.Subject, after, ct);
        if (!person.IsCurrent) throw new ProductProcurementAuthorityException(503);
        return new { items = page.Rows.Select(x => new { company_id=x.CompanyId, link_id=x.LinkId, name=x.CompanyName }), next_cursor=page.Next };
    }
}

internal static class NativeAttendanceApi
{
    private static readonly object ProofKey = new();
    internal const string Prefix = "/api/mobile/v1";
    internal const string Scope = "product:identity product:mobile-attendance";
    private sealed record Proof(NativeEmployeeGrant Grant);
    internal static NativeEmployeeGrant? Current(HttpContext http)
        => http.Items.TryGetValue(ProofKey, out var value) && value is Proof p && p.Grant.Scope.IsCurrent ? p.Grant : null;
    internal static bool IsRequest(HttpContext http) => http.Items.ContainsKey(ProofKey);
    internal static bool Enabled(IConfiguration c) => c.GetValue<bool>("ERP:MobileAttendance:Enabled");
    private static string Origin(IConfiguration c) {
        var value=c["ERP:MobileAttendance:PublicOrigin"]??"https://api.kuubuerp.com";
        if (!Regex.IsMatch(value,@"\Ahttps://[a-z0-9]+(?:[.-][a-z0-9]+)+\z")) throw new InvalidOperationException("An exact HTTPS native API origin is required.");
        return value;
    }
    private sealed class Tenant(string id) : ITenantAccessor { public string GetTenantId() => id; }

    internal static void Register(IServiceCollection services, IConfiguration c, string connection)
    {
        if (!Enabled(c)) return;
        var client = c["ERP:MobileAttendance:ClientId"];
        if (!ProductRequestAuthentication.Enabled(c) || !c.GetValue<bool>("ERP:ProductLicensing:Enabled")
            || !Guid.TryParseExact(client, "D", out var id) || id == Guid.Empty || id.ToString("D") != client)
            throw new InvalidOperationException("Native attendance requires an explicit product/HR profile and registered public client.");
        services.AddHttpClient("kuubu-native-person", x => x.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }).RemoveAllLoggers();
        // ProductOperationalSubject is already registered for browser continuity;
        // resolve the native reader by its wrapper, never replace that registration.
        var origin=Origin(c);
        services.AddSingleton(sp => new NativeSubjectReader(sp.GetRequiredService<IHttpClientFactory>().CreateClient("kuubu-native-person"), client!,origin));
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection).Options;
        services.AddSingleton(sp => new NativeEmployeeAdmission(t => new HrmDbContext(options, new Tenant(t)),
            sp.GetRequiredService<ProductCommercialLicense>().ReadAsync, TimeProvider.System));
        services.AddScoped<IMobileAttendanceService>(sp => (IMobileAttendanceService)sp.GetRequiredService<ITimeService>());
        services.AddScoped<NativeAttendanceSaveGuard>();
    }

    internal sealed class NativeSubjectReader(HttpClient http, string client, string origin)
    {
        private readonly ProductOperationalSubject reader = new(http, TimeProvider.System, client, nativeAttendance:true,nativeOrigin:origin);
        internal Task<OperationalSubjectResult> ReadAsync(string token, CancellationToken ct) => reader.DiscoverAsync(token, ct);
    }

    internal static void Map(WebApplication app)
    {
        if (!Enabled(app.Configuration)) return;
        var group = app.MapGroup(Prefix).WithMetadata(new ProductIdentityRequestBoundary()).AllowAnonymous().RequireRateLimiting("native-attendance");
        group.MapGet("/config", () => Results.Ok(new {
            schema_version=1, api_base_url=Origin(app.Configuration)+"/v1", identity_origin=ProductCommercialLicense.Issuer,
            mobile_ready=true, client_id=app.Configuration["ERP:MobileAttendance:ClientId"],
            authorization_endpoint="https://www.kuubuerp.com/account/authorize/",
            token_endpoint=ProductCommercialLicense.Issuer+"/api/v1/product-identity/token",
            revoke_endpoint=ProductCommercialLicense.Issuer+"/api/v1/product-identity/mobile-revoke",
            redirect_uri="vcron://oauth/callback", scope=Scope, modules=new[]{new{id="hrm",attendance_ready=true}}
        }));
        group.MapGet("/companies", async (HttpContext h) => {
            if (h.Request.Query.Any(x => x.Key != "after" || x.Value.Count != 1)
                || h.Request.Query.TryGetValue("after", out var raw) && !Canonical(raw.ToString(), out _)) return Results.BadRequest();
            var identity = await Identity(h);
            if (identity.Status != OperationalSubjectStatus.Verified || identity.Observation is null) return Failure(identity.Status);
            Guid? after=h.Request.Query.TryGetValue("after",out var value)?Guid.Parse(value.ToString()):null;
            try { return Results.Ok(await h.RequestServices.GetRequiredService<NativeEmployeeAdmission>().CompaniesAsync(identity.Observation, after, h.RequestAborted)); }
            catch (System.Data.Common.DbException) { return Results.StatusCode(503); }
            catch (ProductProcurementAuthorityException e) { return Results.StatusCode(e.Status); }
        });
        group.MapGet("/hrm/me", (HttpContext h) => Execute(h, false, false, true));
        group.MapGet("/hrm/me/attendance", (HttpContext h) => Execute(h, false, false, false));
        group.MapGet("/hrm/me/attendance/today", (HttpContext h) => Execute(h, false, false, false));
        group.MapPost("/hrm/me/attendance/clock-in", (HttpContext h) => Execute(h, true, true, false));
        group.MapPost("/hrm/me/attendance/clock-out", (HttpContext h) => Execute(h, true, false, false));
    }

    private static bool Canonical(string value, out Guid id) => Guid.TryParseExact(value,"D",out id) && id != Guid.Empty && id.ToString("D") == value;
    private static IResult Failure(OperationalSubjectStatus status) => Results.Json(new{error=status==OperationalSubjectStatus.Denied?"sign-in-required":"identity-unavailable"},statusCode:status==OperationalSubjectStatus.Denied?401:503);
    private static async Task<OperationalSubjectResult> Identity(HttpContext h)
    {
        h.Response.Headers.CacheControl="no-store";
        string[] forbidden=["Cookie","X-Tenant","X-Tenant-Id","X-Shell-Entity","X-Shell-Location","X-Branch-Id","X-All-Branches","X-Kuubu-Operational-CSRF"];
        if (forbidden.Any(h.Request.Headers.ContainsKey) || h.Request.Headers.Authorization.Count != 1)
            return new(OperationalSubjectStatus.Denied);
        var match=Regex.Match(h.Request.Headers.Authorization.ToString(),@"\ABearer ([A-Za-z0-9._~-]{1,16384})\z");
        if (!match.Success) return new(OperationalSubjectStatus.Denied);
        return await h.RequestServices.GetRequiredService<NativeSubjectReader>().ReadAsync(match.Groups[1].Value,h.RequestAborted);
    }

    private static async Task<IResult> Execute(HttpContext h, bool write, bool clockIn, bool profile)
    {
        var identity=await Identity(h);
        if (identity.Status!=OperationalSubjectStatus.Verified || identity.Observation is null) return Failure(identity.Status);
        if (h.Request.Query.Count != 0 || !Canonical(h.Request.Headers["X-Kuubu-Company"].ToString(),out var company)
            || !Canonical(h.Request.Headers["X-Kuubu-Link"].ToString(),out var link)) return Results.BadRequest();
        var admission=h.RequestServices.GetRequiredService<NativeEmployeeAdmission>();
        var first=await admission.ReadAsync(identity.Observation,company,link,h.RequestAborted);
        if (first.Grant is not {} grant) return Results.Json(new{error="employee-company-access-required",message="Ask your company administrator to check your employee link, HR module and location access."},statusCode:first.Status);
        string? expectedDate=null; Guid? expectedRecord=null;
        if (write) {
            if (!h.Request.HasJsonContentType() || h.Request.ContentLength >4096) return Results.BadRequest();
            var buffer=new byte[4097];var used=0;
            while(used<buffer.Length) {
                var count=await h.Request.Body.ReadAsync(buffer.AsMemory(used),h.RequestAborted);
                if(count==0)break;used+=count;
            }
            if(used>4096)return Results.StatusCode(413);
            JsonDocument json;
            try { json=JsonDocument.Parse(buffer.AsMemory(0,used),new JsonDocumentOptions{MaxDepth=2}); }
            catch (JsonException) { return Results.BadRequest(); }
            using var parsedBody=json;
            var field=clockIn?"expected_work_date":"expected_record_id";
            if (json.RootElement.ValueKind!=JsonValueKind.Object || json.RootElement.EnumerateObject().Count()!=1 || !json.RootElement.TryGetProperty(field,out var v) || v.ValueKind!=JsonValueKind.String) return Results.BadRequest();
            if (clockIn) {expectedDate=v.GetString();if (!DateOnly.TryParseExact(expectedDate,"yyyy-MM-dd",out _)) return Results.BadRequest();}
            else {if (!Canonical(v.GetString()!,out var id)) return Results.BadRequest();expectedRecord=id;}
        }
        var scope=grant.Scope;
        h.User=new(new ClaimsIdentity([new("tenant",scope.TenantId),new("sub",scope.PrincipalId.ToString("D")),
            new(ClaimTypes.NameIdentifier,scope.PrincipalId.ToString("D")),new(ClaimTypes.Role,"employee")],ProductRequestAuthentication.Scheme));
        h.Items[ProofKey]=new Proof(grant);
        ProductRequestContext.Set(h,scope,async (_,ct) => {
            var next=await Recheck(h,admission,grant,company,link,ct);
            return new(next.Status==200?ModuleAccessStatus.Authorized:next.Status==503?ModuleAccessStatus.Unavailable:ModuleAccessStatus.Denied,next.Grant?.Scope);
        });
        var shell=h.RequestServices.GetRequiredService<ShellContext>();shell.EntityId=company;shell.LocationId=grant.Worker.LocationId;
        shell.AllowedLocationIds.Clear();shell.AllowedLocationIds.Add(grant.Worker.LocationId!.Value);
        var db=h.RequestServices.GetRequiredService<HrmDbContext>();
        if (!db.Database.IsNpgsql()) return Results.StatusCode(503);
        try {
            if (!write) {
                var data=await View(db,grant,profile,h.RequestAborted);
                var final=await Recheck(h,admission,grant,company,link,h.RequestAborted);
                return final.Status==200?Results.Ok(data):Results.StatusCode(final.Status);
            }
            await using var tx=await db.Database.BeginTransactionAsync(h.RequestAborted);
            var companyKey="core.company.team.v1\n"+scope.TenantId+"\n"+company.ToString("D");
            var workerKey="mobile.attendance.worker.v1\n"+scope.TenantId+"\n"+grant.Worker.Id.ToString("D");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared(hashtextextended({companyKey},0))",h.RequestAborted);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({workerKey},0))",h.RequestAborted);
            var before=await Recheck(h,admission,grant,company,link,h.RequestAborted);
            if (before.Status!=200) return Results.StatusCode(before.Status);
            await h.RequestServices.GetRequiredService<IMobileAttendanceService>().PunchMobileAsync(grant.Worker.Id,clockIn,
                TimeProvider.System.GetUtcNow(),grant.TimeZone,expectedDate,expectedRecord,h.RequestAborted);
            var result=await View(db,grant,false,h.RequestAborted);
            var after=await Recheck(h,admission,grant,company,link,h.RequestAborted);
            if (after.Status!=200) return Results.StatusCode(after.Status);
            await tx.CommitAsync(h.RequestAborted);
            return Results.Ok(result);
        } catch (DomainException e) {
            return Results.Json(new{error=e.Code,message=e.Message},statusCode:e.Code.StartsWith("mobile-",StringComparison.Ordinal)?409:403);
        } catch (ProductProcurementAuthorityException e) { return Results.StatusCode(e.Status); }
        catch (NpgsqlException) { return Results.Json(new{error="attendance-unavailable"},statusCode:503); }
    }

    private static async Task<NativeGrantResult> Recheck(HttpContext h, NativeEmployeeAdmission admission, NativeEmployeeGrant old, Guid company, Guid link, CancellationToken ct)
    {
        var identity=await Identity(h);
        if (identity.Status!=OperationalSubjectStatus.Verified || identity.Observation is null) return new(identity.Status==OperationalSubjectStatus.Denied?401:503);
        var next=await admission.ReadAsync(identity.Observation,company,link,ct);
        if (next.Grant is not {} g) return next;
        return old.Scope.IsCurrent && g.Scope.TenantId==old.Scope.TenantId && g.Scope.PrincipalId==old.Scope.PrincipalId
            && g.Worker.Id==old.Worker.Id && g.Worker.LocationId==old.Worker.LocationId && g.GrantVersion==old.GrantVersion
            && g.GrantRevision==old.GrantRevision && g.TimeZone==old.TimeZone ? next : new(403);
    }

    private static async Task<object> View(HrmDbContext db, NativeEmployeeGrant g, bool profile, CancellationToken ct)
    {
        var now=TimeProvider.System.GetUtcNow();var today=MobileAttendanceTime.Local(now,g.TimeZone).Date;
        var records=await db.AttendanceRecords.AsNoTracking().Where(x => x.WorkerId==g.Worker.Id && x.ClockIn!=null
            && (x.WorkDate>=today.AddDays(-90) || x.ClockOut==null)).OrderByDescending(x=>x.WorkDate).Take(100).ToArrayAsync(ct);
        var attendance=new {company_id=g.Scope.CompanyId,employee_id=g.Worker.Id,server_time=now,work_date=today.ToString("yyyy-MM-dd"),time_zone=g.TimeZone,
            records=records.Select(x=>new{id=x.Id,date=x.WorkDate.ToString("yyyy-MM-dd"),check_in=MobileAttendanceTime.Instant(x,false,g.TimeZone),check_out=MobileAttendanceTime.Instant(x,true,g.TimeZone)})};
        if (!profile) return attendance;
        var department=g.Worker.OrgUnitId is {} org?await db.OrgUnits.Where(x=>x.Id==org).Select(x=>x.Name).SingleOrDefaultAsync(ct):null;
        return new {employee=new{id=g.Worker.Id,employee_number=g.Worker.EmployeeNo,name=g.Worker.FullName,role=g.Worker.JobTitle,
            department,company=g.CompanyName,location=g.LocationName,email=g.Worker.Email,phone=g.Worker.Phone,joined=g.Worker.StartDate,status=g.Worker.Status},
            attendance,services=new[]{new{id="attendance",title="Attendance",enabled=true}},schedule=(object?)null};
    }
}

/// <summary>Native self-service can persist only its own attendance and corresponding audit evidence.</summary>
internal sealed class NativeAttendanceSaveGuard(IHttpContextAccessor accessor) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct=default)
    {
        if (data.Context is HrmDbContext db && accessor.HttpContext is {} h && NativeAttendanceApi.IsRequest(h)) {
            var proof=NativeAttendanceApi.Current(h) ?? throw new ProductProcurementAuthorityException(503);
            foreach (var e in db.ChangeTracker.Entries<Entity>().Where(x=>x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)) {
                if (e.Entity.TenantId!=proof.Scope.TenantId || e.State==EntityState.Deleted) throw new ProductProcurementAuthorityException(403);
                if (e.Entity is AttendanceRecord row) {
                    if (row.WorkerId!=proof.Worker.Id || e.State==EntityState.Added && (row.Source!="mobile" || row.LocationId!=proof.Worker.LocationId)
                        || e.State==EntityState.Modified && (e.Property(nameof(AttendanceRecord.WorkerId)).IsModified || e.Property(nameof(AttendanceRecord.LocationId)).IsModified
                            || e.Property(nameof(AttendanceRecord.WorkDate)).IsModified || e.Property(nameof(AttendanceRecord.Source)).IsModified))
                        throw new ProductProcurementAuthorityException(403);
                } else if (e.Entity is not AuditEntry audit || e.State!=EntityState.Added
                    || audit.ActorSubjectId!=proof.Scope.PrincipalId.ToString("D") && audit.ActorSubjectId!="system") throw new ProductProcurementAuthorityException(403);
            }
        }
        return ValueTask.FromResult(result);
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result)
    {
        if (accessor.HttpContext is {} h && NativeAttendanceApi.IsRequest(h)) throw new ProductProcurementAuthorityException(503);
        return result;
    }
}
