using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class MobileWorkdayStoreTests
{
    [Fact]
    public void Compiled_PostgreSQL_model_matches_the_owning_migration_snapshot()
    {
        using var db=new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=isolated_model_check;Username=unused").Options,new ModelTenant());
        Assert.False(db.Database.HasPendingModelChanges());
    }
    private sealed class ModelTenant:ITenantAccessor{public string GetTenantId()=>"model-check";}
    [Fact]
    public async Task Fresh_reads_recover_break_state_and_retries_do_not_duplicate_history_or_change_attendance_totals()
    {
        await using var f=await Fixture.Start();var c=f.Command("break-start");
        var first=await f.Run(c);var retry=await f.Run(c);
        Assert.Equal(1,first.AcceptedRevision);Assert.True(retry.Replayed);Assert.Equal("on_break",retry.Workday.State);
        await using var read=f.Db();var view=await new MobileWorkdayStore(read,TimeProvider.System).ReadAsync(f.Scope,f.Attendance,default);
        Assert.Equal("on_break",view.State);Assert.False(view.AffectsPayroll);Assert.Equal("not_connected",view.MetricsAvailability);
        Assert.Single(await read.MobileWorkdayEvents.ToArrayAsync());Assert.Single(await read.AuditEntries.ToArrayAsync());
        var row=await read.AttendanceRecords.SingleAsync();Assert.Null(row.ClockOut);Assert.Equal(3m,row.TotalHours);Assert.Equal("pending",row.OvertimeStatus);
    }
    [Fact]
    public async Task Stale_actions_and_request_key_reuse_are_rejected_without_side_effects()
    {
        await using var f=await Fixture.Start();var c=f.Command("departure","Supplier meeting");await f.Run(c);
        await Assert.ThrowsAsync<DomainException>(()=>f.Run(c with{Kind="break-start"}));
        await Assert.ThrowsAsync<DomainException>(()=>f.Run(f.Command("return")));
        Assert.Equal("working",(await f.Run(f.Command("return") with{ExpectedRevision=1})).Workday.State);
        await using var db=f.Db();Assert.Equal(2,await db.MobileWorkdayEvents.CountAsync());Assert.Equal(2,await db.AuditEntries.CountAsync());
    }
    [Fact]
    public async Task A_work_report_after_checkout_does_not_reopen_the_shift_or_change_payroll()
    {
        await using var f=await Fixture.Start();var end=MobileAttendanceTime.Local(DateTimeOffset.UtcNow.AddMinutes(-1),"Africa/Lusaka").Time;
        await using(var db=f.Db()){
            var a=await db.AttendanceRecords.SingleAsync();a.ClockOut=end;await db.SaveChangesAsync();
        }
        var c=f.Command("report") with{Summary="Reviewed supplier documents",Blockers="Waiting for manager response"};
        var result=await f.Run(c);Assert.Equal("closed",result.Workday.State);Assert.Equal(c.Summary,Assert.Single(result.Workday.Events).Summary);
        await Assert.ThrowsAsync<DomainException>(()=>f.Run(f.Command("break-start") with{ExpectedRevision=1}));
        await using var verify=f.Db();Assert.Equal(end,(await verify.AttendanceRecords.SingleAsync()).ClockOut);
    }
    [Fact]
    public async Task Different_worker_company_and_tenant_cannot_read_or_append_to_the_record()
    {
        await using var f=await Fixture.Start();await f.Run(f.Command("break-start"));
        foreach(var scope in new[]{f.Scope with{WorkerId=Guid.NewGuid()},f.Scope with{CompanyId=Guid.NewGuid()},f.Scope with{TenantId="other"}}){
            await using var db=f.Db();var store=new MobileWorkdayStore(db,TimeProvider.System);
            await Assert.ThrowsAsync<DomainException>(()=>store.ReadAsync(scope,f.Attendance,default));
            await using var tx=await db.Database.BeginTransactionAsync();
            await Assert.ThrowsAsync<DomainException>(()=>store.ApplyAsync(scope,f.Command("break-start"),default));
        }
        await using var verify=f.Db();Assert.Single(await verify.MobileWorkdayEvents.ToArrayAsync());
    }
    [Fact]
    public async Task Transaction_rollback_and_append_only_guard_preserve_evidence()
    {
        await using var f=await Fixture.Start();await using(var db=f.Db()){
            await using(var tx=await db.Database.BeginTransactionAsync())
                await new MobileWorkdayStore(db,TimeProvider.System).ApplyAsync(f.Scope,f.Command("break-start"),default);
        }
        await using(var verify=f.Db()){Assert.Empty(await verify.MobileWorkdayEvents.ToArrayAsync());Assert.Empty(await verify.AuditEntries.ToArrayAsync());}
        await f.Run(f.Command("break-start"));
        await using(var db=f.Db()){(await db.MobileWorkdayEvents.SingleAsync()).StateAfter="working";await Assert.ThrowsAsync<DomainException>(()=>db.SaveChangesAsync());}
        await using(var db=f.Db()){db.MobileWorkdayEvents.Remove(await db.MobileWorkdayEvents.SingleAsync());await Assert.ThrowsAsync<DomainException>(()=>db.SaveChangesAsync());}
    }
    private sealed class Fixture(SqliteConnection connection,DbContextOptions<HrmDbContext> options):IAsyncDisposable
    {
        internal MobileWorkdayScope Scope=null!;internal Guid Attendance;
        internal HrmDbContext Db()=>new(options,new Tenant());
        internal MobileWorkdayCommand Command(string kind,string reason="")=>new(Guid.NewGuid(),Attendance,0,kind,reason);
        internal async Task<MobileWorkdayResult> Run(MobileWorkdayCommand command){
            await using var db=Db();await using var tx=await db.Database.BeginTransactionAsync();
            var result=await new MobileWorkdayStore(db,TimeProvider.System).ApplyAsync(Scope,command,default);await tx.CommitAsync();return result;
        }
        internal static async Task<Fixture> Start(){
            var c=new SqliteConnection("Data Source=:memory:");await c.OpenAsync();
            var f=new Fixture(c,new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(c).Options);
            await using var db=f.Db();await db.Database.EnsureCreatedAsync();
            var company=new LegalEntity{Code="QA",RegisteredName="Isolated workday company"};db.LegalEntities.Add(company);
            var location=new WorkLocation{Code="HQ",Name="Head office",LegalEntityId=company.Id};db.WorkLocations.Add(location);
            var worker=new Worker{EmployeeNo="WORKDAY-QA",FirstName="Isolated",LastName="Employee",WorkerType="employee",Status="active",LocationId=location.Id};db.Workers.Add(worker);
            var local=MobileAttendanceTime.Local(DateTimeOffset.UtcNow.AddMinutes(-30),"Africa/Lusaka");
            var a=new AttendanceRecord{WorkerId=worker.Id,WorkDate=local.Date,ClockIn=local.Time,Source="mobile",LocationId=location.Id,TotalHours=3m,OvertimeStatus="pending"};db.AttendanceRecords.Add(a);await db.SaveChangesAsync();
            f.Scope=new("workday-test",company.Id,worker.Id,Guid.NewGuid(),"Africa/Lusaka");f.Attendance=a.Id;return f;
        }
        public async ValueTask DisposeAsync()=>await connection.DisposeAsync();
        private sealed class Tenant:ITenantAccessor{public string GetTenantId()=>"workday-test";}
    }
}
