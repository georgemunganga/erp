using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Fixture=Mightyfin.Erp.Hrm.Tests.OnPremMobileHttpTests.Fixture;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class MobileWorkdayHttpTests
{
    [Fact]
    public async Task Feature_off_keeps_ordinary_attendance_and_publishes_no_workday_routes()
    {
        await using var f=await Fixture.Start();var t=await f.Login();
        Assert.Equal(HttpStatusCode.NotFound,(await f.Raw("GET","/workdays/"+Guid.NewGuid(),t)).StatusCode);
        var p=await f.Client.GetFromJsonAsync<JsonElement>("/mobile/v1/profile");Assert.False(p.GetProperty("workday_ready").GetBoolean());
        Assert.Equal(HttpStatusCode.OK,(await f.Raw("GET","/me",t)).StatusCode);
    }
    [Fact]
    public async Task Existing_login_breaks_departure_return_checkout_and_report_are_persisted_without_payroll_changes()
    {
        await using var f=await Fixture.Start(workday:true);var token=await f.Login();
        var p=await f.Client.GetFromJsonAsync<JsonElement>("/mobile/v1/profile");Assert.True(p.GetProperty("workday_ready").GetBoolean());
        var me=await f.Send("GET","/me",token);var date=me.GetProperty("attendance").GetProperty("work_date").GetString();
        me=await f.Send("POST","/attendance/clock-in",token,new{expected_work_date=date});
        var id=me.GetProperty("attendance").GetProperty("records")[0].GetProperty("id").GetGuid();var path="/workdays/"+id;
        var revision=0;
        foreach(var kind in new[]{"break-start","break-end","departure","return"}){
            f.Clock.Value=f.Clock.Value.AddMinutes(5);
            if(f.Clock.Value>=token.GetProperty("expiresAt").GetDateTimeOffset()){
                using var rotated=await f.Client.PostAsJsonAsync("/mobile/v1/auth/refresh",new{companyId=f.A,refreshToken=token.GetProperty("refreshToken").GetString()});
                Assert.Equal(HttpStatusCode.OK,rotated.StatusCode);token=await rotated.Content.ReadFromJsonAsync<JsonElement>();
            }
            var cmd=new{request_id=Guid.NewGuid(),expected_revision=revision,kind,reason=kind=="departure"?"Visit supplier":""};
            var result=await f.Send("POST",path+"/commands",token,cmd);
            Assert.Equal(++revision,result.GetProperty("accepted_revision").GetInt32());
            var replay=await f.Send("POST",path+"/commands",token,cmd);Assert.True(replay.GetProperty("replayed").GetBoolean());
        }
        await f.Send("POST","/attendance/clock-out",token,new{expected_record_id=id});
        // Valid Unicode reports exceed the old 4KB punch limit when JSON-escaped.
        var summary=new string('\u6587',1800);
        var report=await f.Send("POST",path+"/commands",token,new{request_id=Guid.NewGuid(),expected_revision=revision,kind="report",summary,blockers="Waiting for supplier response"});
        Assert.Equal("closed",report.GetProperty("workday").GetProperty("state").GetString());
        Assert.False(report.GetProperty("workday").GetProperty("affects_payroll").GetBoolean());
        Assert.Equal(summary,report.GetProperty("workday").GetProperty("events")[4].GetProperty("summary").GetString());
        var reread=await f.Send("GET",path,token);Assert.Equal(5,reread.GetProperty("events").GetArrayLength());
        await using var db=f.Db("tenant-a");Assert.Equal(5,await db.MobileWorkdayEvents.CountAsync());Assert.Single(await db.AttendanceRecords.ToArrayAsync());
        Assert.Equal(5,await db.AuditEntries.CountAsync(x=>x.Action.StartsWith("mobile.workday.")));
    }
    [Theory]
    [InlineData("worker")][InlineData("revision")][InlineData("duplicate")][InlineData("null")]
    public async Task Forged_or_malformed_command_fields_are_rejected_before_any_workday_write(string mode)
    {
        await using var f=await Fixture.Start(workday:true);var token=await f.Login();
        var me=await f.Send("GET","/me",token);me=await f.Send("POST","/attendance/clock-in",token,new{expected_work_date=me.GetProperty("attendance").GetProperty("work_date").GetString()});
        var id=me.GetProperty("attendance").GetProperty("records")[0].GetProperty("id").GetGuid();
        var request=Guid.NewGuid();var body=mode switch{
            "worker"=>$"{{\"request_id\":\"{request}\",\"expected_revision\":0,\"kind\":\"break-start\",\"worker_id\":\"{f.WorkerB}\"}}",
            "revision"=>$"{{\"request_id\":\"{request}\",\"expected_revision\":\"0\",\"kind\":\"break-start\"}}",
            "duplicate"=>$"{{\"request_id\":\"{request}\",\"expected_revision\":0,\"kind\":\"break-start\",\"kind\":\"departure\"}}",
            _=>$"{{\"request_id\":\"{request}\",\"expected_revision\":0,\"kind\":\"report\",\"summary\":null}}"};
        using var h=new HttpRequestMessage(HttpMethod.Post,"/mobile/v1/workdays/"+id+"/commands");
        h.Headers.Add("Authorization","Bearer "+token.GetProperty("accessToken").GetString());h.Headers.Add("X-Workplace-Company",f.A.ToString());h.Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json");
        using var response=await f.Client.SendAsync(h);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        await using var db=f.Db("tenant-a");Assert.Empty(await db.MobileWorkdayEvents.ToArrayAsync());
    }
    [Fact]
    public async Task Workday_routes_recheck_employee_link_and_deny_cross_company_access()
    {
        await using var f=await Fixture.Start(workday:true);var token=await f.Login();
        var me=await f.Send("GET","/me",token);me=await f.Send("POST","/attendance/clock-in",token,new{expected_work_date=me.GetProperty("attendance").GetProperty("work_date").GetString()});
        var id=me.GetProperty("attendance").GetProperty("records")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/workdays/"+id,token,company:f.B)).StatusCode);
        await using(var db=f.Db("tenant-a")){var u=await db.LocalUsers.SingleAsync();u.WorkerId=null;await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Raw("GET","/workdays/"+id,token)).StatusCode);
    }
}
