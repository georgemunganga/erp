using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Flow=Mightyfin.Erp.Hrm.Tests.ProductErpSessionFlowTests;

namespace Mightyfin.Erp.Hrm.Tests;
public sealed class ProductWorkerScopeTests
{
    private const string Path="/api/hrm/workers";
    private static async Task<HttpResponseMessage> Send(Flow.Fixture f,HttpMethod method,string path,string cookie,object? body=null,bool csrf=true,string? sessionContext=null)
    {
        using var request=new HttpRequestMessage(method,path);request.Headers.Add("Cookie",cookie);
        if (sessionContext is not null) request.Headers.Add(ProductRequestAuthentication.SessionContextHeader,sessionContext);
        if(method!=HttpMethod.Get){request.Content=JsonContent.Create(body??new{});request.Headers.Add("Origin",ProductOperationalFlowTests.Fixture.Origin);if(csrf)request.Headers.Add(ProductOperationalBrowser.Header,f.Csrf);}
        return await f.Http.Client.SendAsync(request);
    }
    private static object Create(Guid? location,string name="Created")=>new{employeeNo="",firstName=name,lastName="Worker",locationId=location,workerType="employee",bankDetails=new[]{new{bankName="Test Bank",branchCode="TEST",accountNumber="000000000001",accountName="Scoped Worker",paymentMethod="bank",isPrimary=true}}};
    private static async Task<(Guid Other,Guid Owned,Guid Hidden)> Seed(Flow.Fixture f)
    {
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);var branch=new WorkLocation{Code="OTHER-SITE",Name="Other site",LegalEntityId=f.Core.Target.Company};
        db.WorkLocations.Add(branch);await db.SaveChangesAsync();
        var owned=new Worker{EmployeeNo="EMP-0002",FirstName="Owned",LastName="Profile",LocationId=f.Core.Target.Location};
        var hidden=new Worker{EmployeeNo="EMP-0001",FirstName="Hidden",LastName="Profile",LocationId=branch.Id};
        hidden.BankDetails.Add(new(){BankName="Private bank",BranchCode="TEST",AccountNumber="synthetic-private-bank",AccountName="Hidden person"});
        db.Workers.AddRange(owned,hidden);await db.SaveChangesAsync();return(branch.Id,owned.Id,hidden.Id);
    }
    [Fact]
    public async Task Actual_worker_list_count_detail_and_includes_are_confined_before_pagination()
    {
        await using var f=await Flow.Fixture.Start(product:true);var seed=await Seed(f);var selected=await f.Select();var cookie=f.With(selected);
        var list=await Send(f,HttpMethod.Get,Path+"/?page=1&pageSize=1",cookie);Assert.Equal(HttpStatusCode.OK,list.StatusCode);
        var data=await list.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(1,data.GetProperty("totalCount").GetInt32());Assert.Equal(seed.Owned,data.GetProperty("items")[0].GetProperty("id").GetGuid());
        var detail=await Send(f,HttpMethod.Get,Path+$"/{seed.Hidden:D}",cookie);Assert.Equal(HttpStatusCode.NotFound,detail.StatusCode);
        Assert.DoesNotContain("synthetic-private-bank",await list.Content.ReadAsStringAsync());
        var filtered=await Send(f,HttpMethod.Get,Path+$"/?locationId={seed.Other:D}",cookie);Assert.Equal(HttpStatusCode.OK,filtered.StatusCode);Assert.Equal(0,(await filtered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }
    [Fact]
    public async Task Stale_browser_context_cannot_read_or_archive_after_session_replacement()
    {
        await using var f = await Flow.Fixture.Start(product:true);
        var seed = await Seed(f);
        var first = await f.Select();
        var context = await Send(f,HttpMethod.Get,"/api/core/v1/workspace-context",f.With(first));
        Assert.Equal(HttpStatusCode.OK,context.StatusCode);
        var data = await context.Content.ReadFromJsonAsync<JsonElement>();
        var marker = data.GetProperty("sessionContext").GetString()!;
        Assert.True(data.GetProperty("workerDirectory").GetBoolean());
        Assert.True(data.GetProperty("workerArchive").GetBoolean());
        Assert.Equal(HttpStatusCode.OK,(await Send(f,HttpMethod.Get,Path+"/",f.With(first),sessionContext:marker)).StatusCode);

        var second = await f.Select(f.Command());
        Assert.Equal(HttpStatusCode.Conflict,(await Send(f,HttpMethod.Get,Path+"/",f.With(second),sessionContext:marker)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(f,HttpMethod.Post,Path+$"/{seed.Owned:D}/archive",f.With(second),sessionContext:marker)).StatusCode);
        await using var db = f.Core.Core.Context(f.Core.Target.Tenant);
        Assert.False((await db.Workers.SingleAsync(x=>x.Id==seed.Owned)).IsArchived);
    }

    [Fact]
    public async Task Worker_create_update_and_archive_reuse_services_without_creating_login_or_workspace()
    {
        await using var f=await Flow.Fixture.Start(product:true);await Seed(f);var selected=await f.Select();var cookie=f.With(selected);
        var made=await Send(f,HttpMethod.Post,Path+"/",cookie,Create(f.Core.Target.Location));Assert.Equal(HttpStatusCode.Created,made.StatusCode);
        var data=await made.Content.ReadFromJsonAsync<JsonElement>();var id=data.GetProperty("id").GetGuid();Assert.Equal("EMP-0003",data.GetProperty("employeeNo").GetString());
        Assert.Equal(HttpStatusCode.OK,(await Send(f,HttpMethod.Put,Path+$"/{id:D}",cookie,new{firstName="Changed",bankDetails=new[]{new{bankName="Test Bank",branchCode="TEST",accountNumber="000000000002",accountName="Scoped Worker",paymentMethod="bank",isPrimary=true}}})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Send(f,HttpMethod.Post,Path+$"/{id:D}/archive",cookie)).StatusCode);
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);var saved=await db.Workers.SingleAsync(x=>x.Id==id);Assert.Equal("Changed",saved.FirstName);Assert.True(saved.IsArchived);Assert.Null(saved.SubjectId);
        Assert.Equal("000000000002",(await db.WorkerBankDetails.SingleAsync(x=>x.WorkerId==id)).AccountNumber);
        Assert.Single(await db.LocalUsers.ToArrayAsync());Assert.Empty(await db.LocalSessions.ToArrayAsync());Assert.Single(await db.WorkspaceBindings.ToArrayAsync());
    }
    [Theory]
    [InlineData("hidden-update")][InlineData("hidden-archive")][InlineData("move-out")][InlineData("create-out")][InlineData("unassigned")][InlineData("identity")]
    public async Task Unauthorized_source_target_or_identity_binding_cannot_mutate_workers(string fault)
    {
        await using var f=await Flow.Fixture.Start(product:true);var seed=await Seed(f);var selected=await f.Select();var cookie=f.With(selected);HttpResponseMessage response;
        if(fault=="hidden-update")response=await Send(f,HttpMethod.Put,Path+$"/{seed.Hidden:D}",cookie,new{firstName="Attack"});
        else if(fault=="hidden-archive")response=await Send(f,HttpMethod.Post,Path+$"/{seed.Hidden:D}/archive",cookie);
        else if(fault=="move-out")response=await Send(f,HttpMethod.Put,Path+$"/{seed.Owned:D}",cookie,new{locationId=seed.Other});
        else if(fault=="create-out")response=await Send(f,HttpMethod.Post,Path+"/",cookie,Create(seed.Other));
        else if(fault=="unassigned")response=await Send(f,HttpMethod.Post,Path+"/",cookie,Create(null));
        else response=await Send(f,HttpMethod.Put,Path+$"/{seed.Owned:D}",cookie,new{subjectId="forged-identity"});
        Assert.Contains(response.StatusCode,new[]{HttpStatusCode.Forbidden,HttpStatusCode.NotFound});
        await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Equal(2,await db.Workers.CountAsync());var owned=await db.Workers.SingleAsync(x=>x.Id==seed.Owned);var hidden=await db.Workers.SingleAsync(x=>x.Id==seed.Hidden);
        Assert.Equal(f.Core.Target.Location,owned.LocationId);Assert.Null(owned.SubjectId);Assert.Equal("Hidden",hidden.FirstName);Assert.False(hidden.IsArchived);
    }
    [Fact]
    public async Task Final_license_denial_rolls_back_an_already_saved_worker_change()
    {
        await using var f=await Flow.Fixture.Start(product:true);var seed=await Seed(f);var selected=await f.Select();var cookie=f.With(selected);var before=f.LicenseReads;
        f.LicenseDecision=n=>n>=before+3?CommercialLicenseStatus.Denied:CommercialLicenseStatus.Authorized;
        var response=await Send(f,HttpMethod.Put,Path+$"/{seed.Owned:D}",cookie,new{firstName="Must roll back"});Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        Assert.True(f.LicenseReads>=before+3);await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Equal("Owned",(await db.Workers.SingleAsync(x=>x.Id==seed.Owned)).FirstName);
    }
    [Fact]
    public async Task Missing_CSRF_is_denied_before_person_license_or_worker_mutation()
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var reads=f.Http.Channel.Reads;var licenses=f.LicenseReads;
        var response=await Send(f,HttpMethod.Post,Path+"/",f.With(selected),Create(f.Core.Target.Location),csrf:false);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
        Assert.Equal(reads,f.Http.Channel.Reads);Assert.Equal(licenses,f.LicenseReads);await using var db=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Empty(await db.Workers.ToArrayAsync());
    }
    [Fact]
    public async Task Company_wide_scope_can_read_other_owned_branches_and_unassigned_workers()
    {
        await using var f=await Flow.Fixture.Start(product:true);await Seed(f);await using(var db=f.Core.Core.Context(f.Core.Target.Tenant)){db.Workers.Add(new(){EmployeeNo="UNASSIGNED",FirstName="Unassigned",LastName="Profile"});await db.SaveChangesAsync();}
        var command=f.Command();command["scope"]="company";command["location_ids"]=Array.Empty<string>();command["all_company_locations_confirmed"]=true;var selected=await f.Select(command);
        var response=await Send(f,HttpMethod.Get,Path+"/",f.With(selected));Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal(3,(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
    }
    [Theory]
    [InlineData("/import")][InlineData("/00000000-0000-0000-0000-000000000001/account-link")][InlineData("/00000000-0000-0000-0000-000000000001/assignments")]
    public async Task Unreviewed_import_link_and_lifecycle_routes_remain_denied(string suffix)
    {
        await using var f=await Flow.Fixture.Start(product:true);var selected=await f.Select();var method=suffix=="/import"?HttpMethod.Post:suffix.EndsWith("account-link")?HttpMethod.Put:HttpMethod.Get;
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(f,method,Path+suffix,f.With(selected))).StatusCode);
    }
    private sealed class Scope(string tenant,Guid company,Guid[] locations):IWorkerDataScope
    {
        public bool Enabled=>true;public bool IsCurrent{get;set;}=true;public string TenantId=>tenant;public Guid CompanyId=>company;
        public bool AllLocations=>false;public Guid[] LocationIds=>locations.ToArray();
    }
    [Fact]
    public async Task Direct_child_reparenting_cannot_hide_an_inaccessible_original_parent()
    {
        await using var f=await Flow.Fixture.Start(product:true);var seed=await Seed(f);Guid bank;
        await using(var original=f.Core.Core.Context(f.Core.Target.Tenant))bank=(await original.WorkerBankDetails.SingleAsync()).Id;
        var scope=new Scope(f.Core.Target.Tenant,f.Core.Target.Company,[f.Core.Target.Location]);
        await using(var db=f.Core.Core.Context(f.Core.Target.Tenant,scope)){
            Assert.Null(await db.WorkerBankDetails.SingleOrDefaultAsync(x=>x.Id==bank));
            db.WorkerBankDetails.Update(new(){Id=bank,TenantId=f.Core.Target.Tenant,WorkerId=seed.Owned,BankName="Attack",BranchCode="TEST",AccountNumber="000000000001",AccountName="Attack"});
            var error=await Assert.ThrowsAsync<DomainException>(()=>db.SaveChangesAsync());Assert.Equal("forbidden",error.Code);
        }
        await using var check=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Equal(seed.Hidden,(await check.WorkerBankDetails.SingleAsync()).WorkerId);
    }
    [Fact]
    public async Task Expired_scope_is_empty_and_sync_save_cannot_fall_back_to_unrestricted_access()
    {
        await using var f=await Flow.Fixture.Start(product:true);var seed=await Seed(f);var scope=new Scope(f.Core.Target.Tenant,f.Core.Target.Company,[f.Core.Target.Location]);
        await using(var db=f.Core.Core.Context(f.Core.Target.Tenant,scope)){
            var worker=await db.Workers.SingleAsync();Assert.Equal(seed.Owned,worker.Id);worker.FirstName="Attack";scope.IsCurrent=false;
            Assert.Empty(await db.Workers.ToArrayAsync());Assert.Equal("forbidden",Assert.Throws<DomainException>(()=>db.SaveChanges()).Code);
        }
        await using var check=f.Core.Core.Context(f.Core.Target.Tenant);Assert.Equal("Owned",(await check.Workers.SingleAsync(x=>x.Id==seed.Owned)).FirstName);
    }
    [Fact]
    public async Task Context_model_cache_never_reuses_another_branch_scope()
    {
        await using var f=await Flow.Fixture.Start(product:true);var seed=await Seed(f);
        await using(var first=f.Core.Core.Context(f.Core.Target.Tenant,new Scope(f.Core.Target.Tenant,f.Core.Target.Company,[f.Core.Target.Location])))Assert.Equal(seed.Owned,(await first.Workers.SingleAsync()).Id);
        await using(var second=f.Core.Core.Context(f.Core.Target.Tenant,new Scope(f.Core.Target.Tenant,f.Core.Target.Company,[seed.Other])))Assert.Equal(seed.Hidden,(await second.Workers.SingleAsync()).Id);
        await using(var classic=f.Core.Core.Context(f.Core.Target.Tenant))Assert.Equal(2,await classic.Workers.CountAsync());
    }

    [Fact]
    public async Task Read_only_payroll_role_cannot_create_workers_through_product_authentication()
    {
        await using var f=await Flow.Fixture.Start(product:true);await Seed(f);var core=f.Core.Core;var clock=core.Clock;var t=f.Core.Target;
        var person=new OperationalSubjectObservation("42",ProductOperationalFlowTests.Fixture.ClientId,clock.GetUtcNow().AddSeconds(30),clock,clock.GetTimestamp(),TimeSpan.FromSeconds(30));
        var license=new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav",["hrm"],2,clock.GetUtcNow().AddMinutes(1),clock,clock.GetTimestamp(),TimeSpan.FromMinutes(1));
        var changed=await new CoreModuleAccessCommands(x=>core.Context(x),clock).ExecuteAsync(person,new(ProductCommercialLicense.Issuer,"42",t.Link),
            new(t.Company,t.Member,1,"hrm",1,Guid.NewGuid(),false,["payroll"],["hrm-access"],"company",[],true,true),license);
        Assert.Equal(ModuleAccessStatus.Changed,changed.Status);var command=f.Command();command["grant_version_id"]=changed.VersionId!.Value.ToString("D");command["grant_revision"]=2;
        var selected=await f.Select(command);Assert.Equal(HttpStatusCode.OK,selected.StatusCode);var cookie=f.With(selected);
        Assert.Equal(HttpStatusCode.OK,(await Send(f,HttpMethod.Get,Path+"/",cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(f,HttpMethod.Post,Path+"/",cookie,Create(t.Location))).StatusCode);
        await using var db=core.Context(t.Tenant);Assert.Equal(2,await db.Workers.CountAsync());
    }

    [Fact]
    public async Task Multiple_memberships_do_not_bypass_selected_company_for_actual_worker_reads_and_writes()
    {
        await using var f=await Flow.Fixture.Start(product:true);var owned=await Seed(f);var made=await f.Core.Core.Create("worker_scope_foreign_company_request_00000001");
        var target=await f.Core.Grant(made.CompanyId!.Value);Guid foreign;
        await using(var db=f.Core.Core.Context(target.Tenant)){var worker=new Worker{EmployeeNo="OTHER-COMPANY",FirstName="Foreign",LastName="Profile",LocationId=target.Location};db.Workers.Add(worker);await db.SaveChangesAsync();foreign=worker.Id;}
        var first=await f.Select();var cookie=f.With(first);
        Assert.Equal(HttpStatusCode.NotFound,(await Send(f,HttpMethod.Get,Path+$"/{foreign:D}",cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send(f,HttpMethod.Put,Path+$"/{foreign:D}",cookie,new{firstName="Attack"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(f,HttpMethod.Post,Path+"/",cookie,Create(target.Location))).StatusCode);
        var second=await f.Select(f.Command(target));Assert.Equal(HttpStatusCode.OK,second.StatusCode);var other=f.With(second);
        Assert.Equal(HttpStatusCode.OK,(await Send(f,HttpMethod.Get,Path+$"/{foreign:D}",other)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send(f,HttpMethod.Get,Path+$"/{owned.Owned:D}",other)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(f,HttpMethod.Get,Path+"/",cookie)).StatusCode);
        await using var check=f.Core.Core.Context(target.Tenant);Assert.Equal("Foreign",(await check.Workers.SingleAsync()).FirstName);
    }

}
