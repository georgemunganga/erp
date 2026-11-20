using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>Only constructed from current server-side employee/company admission.</summary>
public sealed record MobileWorkdayScope(string TenantId,Guid CompanyId,Guid WorkerId,Guid PrincipalId,string TimeZone);
public sealed record MobileWorkdayView(Guid AttendanceId,int Revision,string State,MobileWorkdayItem[] Events,
    string MetricsAvailability="not_connected",bool AffectsPayroll=false);
public sealed record MobileWorkdayItem(int Revision,string Kind,string StateAfter,DateTimeOffset At,
    string Reason,string Summary,string Blockers);
public sealed record MobileWorkdayResult(Guid RequestId,int AcceptedRevision,MobileWorkdayView Workday,bool Replayed);

/// <summary>Caller owns transaction, current authority rechecks and the shared attendance-worker lock.</summary>
public sealed class MobileWorkdayStore(HrmDbContext db,TimeProvider clock)
{
    private const int EventLimit=1000;
    private async Task<(AttendanceRecord Record,MobileWorkdayEvent[] Events)> Load(MobileWorkdayScope scope,Guid attendance,CancellationToken ct)
    {
        if(scope.CompanyId==Guid.Empty || scope.WorkerId==Guid.Empty || scope.PrincipalId==Guid.Empty || string.IsNullOrWhiteSpace(scope.TenantId))
            throw new DomainException("workday-denied","Workday access could not be confirmed.");
        var row=await db.AttendanceRecords.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==attendance
            && x.TenantId==scope.TenantId && x.WorkerId==scope.WorkerId && !x.IsArchived,ct);
        if(row is null || row.ClockIn is null || row.Source!="mobile" || row.LocationId is not {} location
            || !await db.WorkLocations.AsNoTracking().AnyAsync(x=>x.Id==location && x.TenantId==scope.TenantId
                && x.LegalEntityId==scope.CompanyId && !x.IsArchived,ct))
            throw new DomainException("workday-denied","This mobile attendance record is not available in your company access.");
        var events=await db.MobileWorkdayEvents.AsNoTracking().Where(x=>x.TenantId==scope.TenantId
            && x.AttendanceRecordId==attendance).OrderBy(x=>x.Revision).Take(EventLimit+1).ToArrayAsync(ct);
        if(events.Length>EventLimit)throw new DomainException("workday-needs-review","Ask HR to review this workday history.");
        var state="working";DateTimeOffset? last=null;
        for(var i=0;i<events.Length;i++) {
            var e=events[i];
            if(e.IsArchived || e.LegalEntityId!=scope.CompanyId || e.WorkerId!=scope.WorkerId || e.LocationId!=location
                || e.Revision!=i+1 || last is not null && e.RecordedAt<last)
                throw new DomainException("workday-needs-review","Ask HR to review this workday history.");
            state=MobileWorkdayRules.Apply(state,new(e.RequestId,attendance,e.Revision-1,e.Kind,e.Reason,e.Summary,e.Blockers),false);
            if(state!=e.StateAfter)throw new DomainException("workday-needs-review","Ask HR to review this workday history.");
            last=e.RecordedAt;
        }
        return(row,events);
    }
    private static MobileWorkdayView View(AttendanceRecord record,MobileWorkdayEvent[] events)=>new(record.Id,events.Length,
        record.ClockOut is not null?"closed":events.LastOrDefault()?.StateAfter??"working",
        events.Select(e=>new MobileWorkdayItem(e.Revision,e.Kind,e.StateAfter,e.RecordedAt,e.Reason,e.Summary,e.Blockers)).ToArray());
    public async Task<MobileWorkdayView> ReadAsync(MobileWorkdayScope scope,Guid attendance,CancellationToken ct)
    {
        var data=await Load(scope,attendance,ct);return View(data.Record,data.Events);
    }
    public async Task<MobileWorkdayResult> ApplyAsync(MobileWorkdayScope scope,MobileWorkdayCommand command,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Workday commands require an owning transaction.");
        MobileWorkdayRules.Validate(command);
        if(db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM hrm.attendance_records WHERE id={command.AttendanceId} AND tenant_id={scope.TenantId} AND worker_id={scope.WorkerId} FOR UPDATE",ct);
        var hash=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(command)));
        var prior=await db.MobileWorkdayEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==scope.TenantId
            && x.WorkerId==scope.WorkerId && x.RequestId==command.RequestId,ct);
        var data=await Load(scope,command.AttendanceId,ct);
        if(prior is not null) {
            if(prior.RequestHash!=hash || prior.ActorSubjectId!=scope.PrincipalId.ToString("D")
                || prior.LegalEntityId!=scope.CompanyId || prior.AttendanceRecordId!=command.AttendanceId || prior.IsArchived)
                throw new DomainException("workday-conflict","This request identifier was already used for another action.");
            return new(command.RequestId,prior.Revision,View(data.Record,data.Events),true);
        }
        if(data.Events.Length!=command.ExpectedRevision)
            throw new DomainException("workday-conflict","Your workday changed. Refresh before trying again.");
        if(data.Events.Length>=EventLimit)throw new DomainException("workday-needs-review","Ask HR to review this workday history.");
        if(command.Kind!="report"){
            var started=MobileAttendanceTime.Instant(data.Record,false,scope.TimeZone);
            if(started is null || clock.GetUtcNow()<started || clock.GetUtcNow()-started>TimeSpan.FromHours(24))
                throw new DomainException("workday-needs-review","Ask HR to review this old or future-dated shift.");
        }
        var state=MobileWorkdayRules.Apply(data.Events.LastOrDefault()?.StateAfter??"working",command,data.Record.ClockOut is not null);
        var now=clock.GetUtcNow();
        if(data.Events.LastOrDefault() is {} last && now<last.RecordedAt)
            throw new DomainException("workday-needs-review","The server clock needs review. Please try again later.");
        var actor=scope.PrincipalId.ToString("D");
        var row=new MobileWorkdayEvent{TenantId=scope.TenantId,LegalEntityId=scope.CompanyId,WorkerId=scope.WorkerId,
            AttendanceRecordId=command.AttendanceId,LocationId=data.Record.LocationId!.Value,RequestId=command.RequestId,
            RequestHash=hash,Revision=data.Events.Length+1,Kind=command.Kind,StateAfter=state,RecordedAt=now,
            Reason=command.Reason,Summary=command.Summary,Blockers=command.Blockers,ActorSubjectId=actor,CreatedBy=actor};
        db.MobileWorkdayEvents.Add(row);
        db.AuditEntries.Add(new AuditEntry{EntityType="MobileWorkdayEvent",EntityId=row.Id.ToString("D"),
            Action="mobile.workday."+command.Kind,ActorSubjectId=actor,CreatedBy=actor,
            CorrelationId=command.RequestId.ToString("D"),AfterJson=JsonSerializer.Serialize(new{
                attendance_id=command.AttendanceId,company_id=scope.CompanyId,worker_id=scope.WorkerId,
                revision=row.Revision,kind=row.Kind,state=row.StateAfter,affects_payroll=false})});
        await db.SaveChangesAsync(ct);
        return new(command.RequestId,row.Revision,View(data.Record,[..data.Events,row]),false);
    }
}
