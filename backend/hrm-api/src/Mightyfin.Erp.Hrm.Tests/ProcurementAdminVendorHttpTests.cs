using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Real retained-P19 vendor handlers, middleware and authorization. Only outer
/// authentication and persisted fixture records are synthetic; this is not a PG or supplier-approval proof.</summary>
public sealed class ProcurementAdminVendorHttpTests
{
    [Fact]
    public async Task Unmapped_admin_reads_actual_vendor_handlers_but_all_vendor_commands_are_denied_without_effects()
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions {EnvironmentName="Production"});
        builder.WebHost.UseTestServer();var store=Guid.NewGuid().ToString("N");
        builder.Services.AddScoped<ITenantAccessor>(_=>new FixedTenantAccessor("tenant-a"));
        builder.Services.AddDbContext<HrmDbContext>(o=>o.UseInMemoryDatabase("admin-vendor-core-"+store));
        builder.Services.AddScoped<ProcurementRequestScope>();
        builder.Services.AddScoped<IProcurementScope>(sp=>sp.GetRequiredService<ProcurementRequestScope>());
        builder.Services.AddDbContext<ProcurementDbContext>(o=>o.UseInMemoryDatabase("admin-vendor-proc-"+store));
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions,
            ProcurementRequestHttpTests.TestAuthenticationHandler>("test",_=>{});
        builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
        await using var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();
        app.UseMiddleware<ProcurementScopeMiddleware>();ProcurementContextRoutes.MapRoutes(app);ProcurementVendorRoutes.MapRoutes(app);ProcurementItemEndpoints.MapRoutes(app);
        var admin=Guid.NewGuid();var a=new LegalEntity {Code="A",RegisteredName="Same company",Currency="ZMW",IsDefault=true};
        var b=new LegalEntity {Code="B",RegisteredName="Other company",Currency="USD"};
        var branch=new WorkLocation {Code="A1",Name="Main",LegalEntityId=a.Id};
        var active=Guid.NewGuid();var foreign=Guid.NewGuid();var hiddenDraft=Guid.NewGuid();
        using(var seed=app.Services.CreateScope())
        {
            var core=seed.ServiceProvider.GetRequiredService<HrmDbContext>();core.LegalEntities.AddRange(a,b);core.WorkLocations.Add(branch);
            core.LocalUsers.Add(new LocalUser {Id=admin,Email="admin@example.test",NormalizedEmail="ADMIN@EXAMPLE.TEST",
                DisplayName="Unmapped admin",PasswordHash="synthetic",RolesCsv="erp_admin,hr_admin",IsActive=true});
            await core.SaveChangesAsync();
            seed.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a",a.Id,admin.ToString(),branch.Id,null,false,"fixture");
            var db=seed.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            // Existing records are fixture data, not evidence of a new employee-less commercial activation.
            db.AddScoped(new Supplier {Id=active,Number="VEN-A",LegalName="Existing supplier A",DisplayName="Existing supplier A",CountryCode="ZM",Status="Active",IsOrderable=true});
            await db.SaveChangesAsync();
        }
        using(var seed=app.Services.CreateScope())
        {
            seed.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a",a.Id,Guid.NewGuid().ToString(),null,null,false,"fixture");
            var db=seed.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.AddScoped(new Supplier {Id=hiddenDraft,Number="VEN-DRAFT",LegalName="Private other creator",DisplayName="Private other creator",CountryCode="ZM",Status="Draft"});
            await db.SaveChangesAsync();
        }
        using(var seed=app.Services.CreateScope())
        {
            seed.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a",b.Id,admin.ToString(),null,null,false,"fixture");
            var db=seed.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            db.AddScoped(new Supplier {Id=foreign,Number="VEN-B",LegalName="Foreign company vendor",DisplayName="Foreign company vendor",CountryCode="ZM",Status="Active",IsOrderable=true});
            await db.SaveChangesAsync();
        }
        await app.StartAsync();using var client=app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User",admin.ToString());client.DefaultRequestHeaders.Add("X-Test-Role","erp_admin");
        client.DefaultRequestHeaders.Add("X-Shell-Entity",a.Id.ToString());client.DefaultRequestHeaders.Add("X-Shell-Location",branch.Id.ToString());
        var before=await Footprint(app,a.Id,admin);
        var contextResponse=await client.GetAsync("/api/procurement/v1/context");Assert.Equal(HttpStatusCode.OK,contextResponse.StatusCode);
        var context=await contextResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(context.GetProperty("adminViewOnly").GetBoolean());Assert.Equal(JsonValueKind.Null,context.GetProperty("workerId").ValueKind);
        Assert.Equal(a.Id,context.GetProperty("legalEntityId").GetGuid());Assert.Equal(branch.Id,context.GetProperty("branch").GetProperty("id").GetGuid());
        var listResponse=await client.GetAsync("/api/procurement/v1/vendors");Assert.Equal(HttpStatusCode.OK,listResponse.StatusCode);
        var list=await listResponse.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(1,list.GetProperty("total").GetInt32());
        Assert.Equal(active,list.GetProperty("items")[0].GetProperty("id").GetGuid());
        var detailResponse=await client.GetAsync($"/api/procurement/v1/vendors/{active}");Assert.Equal(HttpStatusCode.OK,detailResponse.StatusCode);
        var detail=await detailResponse.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal(active,detail.GetProperty("id").GetGuid());
        foreach(var flag in new[]{"canReturnForInformation","canApprove","canReject","canSuspend","canReactivate"})
            Assert.False(detail.GetProperty(flag).GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/procurement/v1/vendors/{foreign}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/procurement/v1/vendors/{hiddenDraft}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync($"/api/procurement/v1/vendors/{active}/open-orders")).StatusCode);
        // Retained item maintenance GET is command-policy guarded. Do not widen it to item-read.
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/procurement/v1/items")).StatusCode);
        var id=Guid.NewGuid();var root=$"/api/procurement/v1/vendors/{active}";
        var writes=new List<(HttpMethod Method,string Path)> {(HttpMethod.Post,"/api/procurement/v1/vendors"),(HttpMethod.Put,root)};
        writes.AddRange(new[]{"submit","return","approve","reject","suspend","reactivate","withdraw","contacts","sites","qualifications"}
            .Select(action=>(HttpMethod.Post,root+"/"+action)));
        foreach(var group in new[]{"contacts","sites","qualifications"})
        {writes.Add((HttpMethod.Put,$"{root}/{group}/{id}"));writes.Add((HttpMethod.Post,$"{root}/{group}/{id}/remove"));}
        writes.Add((HttpMethod.Post,$"{root}/qualifications/{id}/review"));
        foreach(var (method,path) in writes)
        {
            using var request=new HttpRequestMessage(method,path) {Content=JsonContent.Create(new {})};
            var denied=await client.SendAsync(request);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
            Assert.Equal("forbidden",(await denied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
            Assert.Equal(before,await Footprint(app,a.Id,admin));
        }
        using(var foreignScope=new HttpRequestMessage(HttpMethod.Get,"/api/procurement/v1/context"))
        {
            foreignScope.Headers.Add("X-Shell-Entity",Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(foreignScope)).StatusCode);
        }
        Assert.Equal(before,await Footprint(app,a.Id,admin));
    }

    private static async Task<string> Footprint(WebApplication app,Guid company,Guid admin)
    {
        using var check=app.Services.CreateScope();
        check.ServiceProvider.GetRequiredService<ProcurementRequestScope>().Set("tenant-a",company,admin.ToString(),null,null,false,"owner-proof");
        var db=check.ServiceProvider.GetRequiredService<ProcurementDbContext>();var core=check.ServiceProvider.GetRequiredService<HrmDbContext>();
        Assert.Empty(await core.Workers.AsNoTracking().ToArrayAsync());
        Assert.Null((await core.LocalUsers.AsNoTracking().SingleAsync(x=>x.Id==admin)).WorkerId);
        return JsonSerializer.Serialize(new {
            suppliers=await db.Suppliers.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            contacts=await db.SupplierContacts.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            sites=await db.SupplierSites.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            qualifications=await db.SupplierQualifications.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            attributes=await db.SupplierAttributes.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            categories=await db.SupplierCategories.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            decisions=await db.SupplierDecisions.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            audits=await db.AuditEvents.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            outbox=await db.OutboxEvents.IgnoreQueryFilters().AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
            accounts=await core.LocalUsers.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync(),
        });
    }
}
