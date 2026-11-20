using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Api;

internal sealed class MobileWorkdayAccessException(int status):Exception
{
    internal int Status{get;}=status;
}
internal static class MobileWorkdayApi
{
    internal static bool Enabled(IConfiguration c)=>c.GetValue<bool>("ERP:MobileWorkday:Enabled");
    private static IResult Error(int status,string code)=>Results.Json(new{error=new{code}},statusCode:status);
    internal static async Task<IResult> Execute(HttpContext h,Guid attendance,bool write,Func<string,HrmDbContext> contexts,
        Func<HrmDbContext?,CancellationToken,Task<MobileWorkdayScope?>> authority)
    {
        if(!Enabled(h.RequestServices.GetRequiredService<IConfiguration>()))return Results.NotFound();
        h.Response.Headers.CacheControl="no-store";
        var ct=h.RequestAborted;
        try {
            var actor=await authority(null,ct);if(actor is null)return Error(401,"workday_sign_in_required");
            if(attendance==Guid.Empty || h.Request.Query.Count!=0)return Error(400,"workday_invalid");
            MobileWorkdayCommand? command=null;
            if(write){command=await ReadCommand(h,attendance,ct);if(command is null)return Error(400,"workday_invalid");}
            await using var db=contexts(actor.TenantId);var store=new MobileWorkdayStore(db,h.RequestServices.GetService<TimeProvider>()??TimeProvider.System);
            if(!write){
                var view=await store.ReadAsync(actor,attendance,ct);
                return await authority(db,ct)==actor?Results.Ok(Transport(view)):Error(401,"workday_access_changed");
            }
            // PostgreSQL must take its business read snapshot after the shared
            // attendance-worker lock, including when a concurrent checkout waited.
            await using var tx=await db.Database.BeginTransactionAsync(db.Database.IsNpgsql()?IsolationLevel.ReadCommitted:IsolationLevel.Serializable,ct);
            if(db.Database.IsNpgsql()){
                var company="core.company.team.v1\n"+actor.TenantId+"\n"+actor.CompanyId.ToString("D");
                var worker="mobile.attendance.worker.v1\n"+actor.TenantId+"\n"+actor.WorkerId.ToString("D");
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared(hashtextextended({company},0))",ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({worker},0))",ct);
            }
            if(await authority(db,ct)!=actor)return Error(401,"workday_access_changed");
            var result=await store.ApplyAsync(actor,command!,ct);
            if(await authority(db,ct)!=actor)return Error(401,"workday_access_changed");
            await tx.CommitAsync(ct);
            return Results.Ok(new{request_id=result.RequestId,accepted_revision=result.AcceptedRevision,replayed=result.Replayed,workday=Transport(result.Workday)});
        }catch(MobileWorkdayAccessException e){return Error(e.Status,e.Status==503?"workday_unavailable":"workday_access_denied");}
        catch(DomainException e){return Error(e.Code=="workday-invalid"||e.Code=="workday-reason-required"?400:e.Code=="workday-denied"?403:409,e.Code);}
        catch(DbUpdateException e)when(e.InnerException is PostgresException p && p.SqlState==PostgresErrorCodes.UniqueViolation){return Error(409,"workday-conflict");}
        catch(PostgresException e)when(e.SqlState==PostgresErrorCodes.SerializationFailure){return Error(409,"workday-conflict");}
        catch(Exception e)when(e is System.Data.Common.DbException or DbUpdateException or TimeoutException){return Error(503,"workday_unavailable");}
    }
    private static object Transport(MobileWorkdayView v)=>new{attendance_id=v.AttendanceId,revision=v.Revision,state=v.State,
        affects_payroll=v.AffectsPayroll,metrics_availability=v.MetricsAvailability,
        events=v.Events.Select(e=>new{revision=e.Revision,kind=e.Kind,state_after=e.StateAfter,at=e.At,reason=e.Reason,summary=e.Summary,blockers=e.Blockers})};
    private static async Task<MobileWorkdayCommand?> ReadCommand(HttpContext h,Guid attendance,CancellationToken ct)
    {
        if(!h.Request.HasJsonContentType() || h.Request.ContentLength>16384)return null;
        var buffer=new byte[16385];var used=0;
        while(used<buffer.Length){var n=await h.Request.Body.ReadAsync(buffer.AsMemory(used),ct);if(n==0)break;used+=n;}
        if(used>16384)return null;
        try{
            using var json=JsonDocument.Parse(buffer.AsMemory(0,used),new JsonDocumentOptions{MaxDepth=2});var r=json.RootElement;
            if(r.ValueKind!=JsonValueKind.Object)return null;
            var keys=r.EnumerateObject().Select(x=>x.Name).ToArray();string[] allowed=["request_id","expected_revision","kind","reason","summary","blockers"];
            if(keys.Length!=keys.Distinct(StringComparer.Ordinal).Count() || keys.Any(x=>!allowed.Contains(x)))return null;
            if(!r.TryGetProperty("request_id",out var id) || id.ValueKind!=JsonValueKind.String || !Guid.TryParseExact(id.GetString(),"D",out var request)
                || request==Guid.Empty || !r.TryGetProperty("expected_revision",out var revision) || revision.ValueKind!=JsonValueKind.Number || !revision.TryGetInt32(out var expected)
                || !r.TryGetProperty("kind",out var kind) || kind.ValueKind!=JsonValueKind.String)return null;
            string? Text(string name)=>!r.TryGetProperty(name,out var value)?"":value.ValueKind==JsonValueKind.String?value.GetString():null;
            var reason=Text("reason");var summary=Text("summary");var blockers=Text("blockers");
            if(reason is null || summary is null || blockers is null)return null;
            return new(request,attendance,expected,kind.GetString()!,reason,summary,blockers);
        }catch(JsonException){return null;}
    }
}
