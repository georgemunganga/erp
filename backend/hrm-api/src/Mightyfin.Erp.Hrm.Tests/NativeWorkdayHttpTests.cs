using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Team=Mightyfin.Erp.Hrm.Tests.CoreCompanyTeamCommandsTests;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class NativeWorkdayHttpTests
{
    private const string Client="391bdeaa-87b3-44b3-8e64-2110dcc1a564",Enrollment="01arz3ndektsv4rrffq69g5fav",Origin="https://api.kuubuerp.com";
    [Fact]
    public async Task SaaS_identity_Core_employee_and_license_admission_reach_workday_persistence_and_fail_closed()
    {
        await using var f=await Team.Fixture.Start();
        OperationalSubjectObservation Person()=>new(f.Owner.Subject,Client,f.Clock.GetUtcNow().AddSeconds(30),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(30));
        CommercialLicenseLease License()=>new(Enrollment,["hrm"],2,f.Clock.GetUtcNow().AddSeconds(60),f.Clock,f.Clock.GetTimestamp(),TimeSpan.FromSeconds(60));
        Guid attendance,worker;
        await using(var db=f.Context()){
            (await db.LegalEntities.SingleAsync()).TimeZoneId="Africa/Lusaka";var location=await db.WorkLocations.SingleAsync();
            var w=new Worker{EmployeeNo="NATIVE-WORKDAY-QA",FirstName="Isolated",LastName="Employee",Status="active",StartDate=new(2020,1,1),LocationId=location.Id,SubjectId=f.Owner.User.ToString("D")};
            db.Workers.Add(w);worker=w.Id;var local=MobileAttendanceTime.Local(f.Clock.GetUtcNow().AddMinutes(-5),"Africa/Lusaka");
            var row=new AttendanceRecord{WorkerId=w.Id,LocationId=location.Id,Source="mobile",WorkDate=local.Date,ClockIn=local.Time,TotalHours=2.5m};db.AttendanceRecords.Add(row);attendance=row.Id;await db.SaveChangesAsync();
            var command=new ModuleAccessCommand(f.Company,f.Owner.Member,1,"hrm",0,Guid.NewGuid(),false,["employee"],["hrm-access"],"locations",[location.Id],false,true);
            Assert.Equal(ModuleAccessStatus.Changed,(await new CoreModuleAccessCommands(t=>f.Context(t),f.Clock).ExecuteAsync(Person(),f.Owner.Proof,command,License())).Status);
        }
        var licensing=CommercialLicenseStatus.Authorized;var identity=new SubjectHandler(f.Owner.Subject);
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{{"ERP:MobileAttendance:Enabled","true"},{"ERP:MobileWorkday:Enabled","true"},{"ERP:MobileAttendance:ClientId",Client}});
        builder.Services.AddSingleton<TimeProvider>(f.Clock);
        builder.Services.AddSingleton(new NativeAttendanceApi.NativeSubjectReader(new HttpClient(identity),Client,Origin));
        builder.Services.AddSingleton(new NativeEmployeeAdmission(t=>f.Context(t),(_,_)=>Task.FromResult(licensing==CommercialLicenseStatus.Authorized?new CommercialLicenseResult(licensing,License()):new CommercialLicenseResult(licensing)),f.Clock));
        builder.Services.AddRateLimiter(o=>o.AddFixedWindowLimiter("native-attendance",x=>{x.PermitLimit=100;x.Window=TimeSpan.FromMinutes(1);x.QueueLimit=0;}));
        await using var app=builder.Build();app.UseRouting();app.UseRateLimiter();NativeAttendanceApi.Map(app);await app.StartAsync();
        using var http=app.GetTestClient();http.DefaultRequestHeaders.Add("Authorization","Bearer isolated.native.workday.token");http.DefaultRequestHeaders.Add("X-Kuubu-Company",f.Company.ToString("D"));http.DefaultRequestHeaders.Add("X-Kuubu-Link",f.Owner.Link.ToString("D"));
        var path=NativeAttendanceApi.Prefix+"/hrm/me/workdays/"+attendance;
        var commandId=Guid.NewGuid();var first=new{request_id=commandId,expected_revision=0,kind="break-start"};
        using(var response=await http.PostAsJsonAsync(path+"/commands",first)){
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);var body=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("on_break",body.GetProperty("workday").GetProperty("state").GetString());
        }
        using(var response=await http.PostAsJsonAsync(path+"/commands",first))Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replayed").GetBoolean());
        identity.Status=HttpStatusCode.Unauthorized;Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync(path)).StatusCode);
        identity.Status=HttpStatusCode.ServiceUnavailable;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await http.GetAsync(path)).StatusCode);identity.Status=HttpStatusCode.OK;
        licensing=CommercialLicenseStatus.Denied;Assert.Equal(HttpStatusCode.Forbidden,(await http.GetAsync(path)).StatusCode);
        licensing=CommercialLicenseStatus.Unavailable;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await http.GetAsync(path)).StatusCode);licensing=CommercialLicenseStatus.Authorized;
        var revision=1;
        foreach(var kind in new[]{"break-end","departure","return","report"}){
            using var response=await http.PostAsJsonAsync(path+"/commands",new{request_id=Guid.NewGuid(),expected_revision=revision,kind,reason=kind=="departure"?"Supplier visit":"",summary=kind=="report"?"Completed delivery review":""});
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);revision++;
        }
        await using(var db=f.Context()){var row=await db.AttendanceRecords.SingleAsync();row.ClockOut=MobileAttendanceTime.Local(f.Clock.GetUtcNow(),"Africa/Lusaka").Time;await db.SaveChangesAsync();}
        using(var response=await http.PostAsJsonAsync(path+"/commands",new{request_id=Guid.NewGuid(),expected_revision=revision,kind="report",summary="Completed handover after checkout"})){
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("closed",(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("workday").GetProperty("state").GetString());
        }
        http.DefaultRequestHeaders.Remove("X-Kuubu-Company");http.DefaultRequestHeaders.Add("X-Kuubu-Company",Guid.NewGuid().ToString("D"));Assert.Equal(HttpStatusCode.Forbidden,(await http.GetAsync(path)).StatusCode);
        http.DefaultRequestHeaders.Remove("X-Kuubu-Company");http.DefaultRequestHeaders.Add("X-Kuubu-Company",f.Company.ToString("D"));
        await using(var db=f.Context()){(await db.Workers.SingleAsync(x=>x.Id==worker)).SubjectId="unlinked";await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.Forbidden,(await http.GetAsync(path)).StatusCode);
        await using(var db=f.Context()){Assert.Equal(6,await db.MobileWorkdayEvents.CountAsync());Assert.Equal(2.5m,(await db.AttendanceRecords.SingleAsync()).TotalHours);Assert.Empty(await db.LocalSessions.ToArrayAsync());}
    }
    private sealed class SubjectHandler(string subject):HttpMessageHandler
    {
        internal HttpStatusCode Status=HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(ProductCommercialLicense.Issuer+"/api/v1/product-identity/mobile-subject",request.RequestUri!.AbsoluteUri);
            Assert.Equal("isolated.native.workday.token",request.Headers.Authorization!.Parameter);
            if(Status!=HttpStatusCode.OK)return Task.FromResult(new HttpResponseMessage(Status));
            var now=DateTimeOffset.UtcNow;
            return Task.FromResult(new HttpResponseMessage(Status){Content=JsonContent.Create(new{data=new{schema_version=1,issuer=ProductCommercialLicense.Issuer,subject,client_id=Client,product_code="kuubu",purpose="mobile-attendance-v1",verified=true,observed_at=now,proof_until=now.AddSeconds(25),operational_access_granted=false,api_origin=Origin}})});
        }
    }
}
