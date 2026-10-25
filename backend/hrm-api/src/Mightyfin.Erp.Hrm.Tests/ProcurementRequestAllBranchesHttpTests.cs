using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

// Reuses the mapped real routes/Core authority and existing synthetic-auth,
// InMemory fixture. Normal-cookie PostgreSQL/browser proof is a separate gate.
public sealed partial class ProcurementRequestHttpTests
{
    [Fact]
    public async Task All_request_collection_filters_permitted_branches_and_owner_before_count_and_paging()
    {
        await using var fixture = await Fixture.Start();
        var data = await fixture.SeedAllRequestBranches();
        var before = await fixture.OriginalValueFootprint();
        var seen = new HashSet<Guid>();
        for (var offset = 0; offset < 3; offset++)
        {
            using var response = await fixture.SendAllRequests(HttpMethod.Get, $"{PrincipalRequests}?limit=1&offset={offset}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(3, body.GetProperty("count").GetInt32());
            var row = Assert.Single(body.GetProperty("records").EnumerateArray());
            Assert.Contains(row.GetProperty("branchRef").GetGuid(), new[] { data.First, data.Second });
            Assert.True(seen.Add(row.GetProperty("id").GetGuid()));
        }
        Assert.True(seen.SetEquals(data.Visible));
        Assert.Equal(before, await fixture.OriginalValueFootprint());
    }

    [Fact]
    public async Task All_request_collection_rechecks_current_assignments_and_preserves_selected_branch_reads()
    {
        await using var fixture = await Fixture.Start();
        var data = await fixture.SeedAllRequestBranches();
        using var selected = await fixture.SendRequestScope(HttpMethod.Get, PrincipalRequests, data.First);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        var branch = await selected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, branch.GetProperty("count").GetInt32());
        Assert.All(branch.GetProperty("records").EnumerateArray(), r => Assert.Equal(data.First, r.GetProperty("branchRef").GetGuid()));
        var before = await fixture.OriginalValueFootprint();
        await fixture.ArchiveRequestBranchAssignment(data.Second);
        using var all = await fixture.SendAllRequests(HttpMethod.Get, PrincipalRequests);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        var body = await all.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("count").GetInt32());
        Assert.All(body.GetProperty("records").EnumerateArray(), r => Assert.Equal(data.First, r.GetProperty("branchRef").GetGuid()));
        Assert.Equal(before, await fixture.OriginalValueFootprint());
    }

    [Fact]
    public async Task Explicit_All_request_writes_require_branch_and_do_not_change_source_or_audit()
    {
        await using var fixture = await Fixture.Start();
        var data = await fixture.SeedAllRequestBranches();
        var before = await fixture.OriginalValueFootprint();
        foreach (var (method, path) in new[] {
            (HttpMethod.Post, PrincipalRequests),
            (HttpMethod.Put, PrincipalRequests + "/" + data.Visible[0]),
            (HttpMethod.Post, PrincipalRequests + "/" + data.Visible[0] + "/withdraw") })
        {
            using var response = await fixture.SendAllRequests(method, path, Draft("Must select branch"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("invalid-scope", body.GetProperty("code").GetString());
        }
        Assert.Equal(before, await fixture.OriginalValueFootprint());
        using var created = await SendJson(fixture.Client, HttpMethod.Post, PrincipalRequests,
            fixture.Owner, Draft("Selected branch still works"), selectedLocation: data.Second);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(data.Second, draft.GetProperty("branchRef").GetGuid());
        Assert.Equal("ZMW", draft.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task Existing_organisation_wide_All_keeps_own_branchless_history_without_sharing_another_makers_rows()
    {
        await using var fixture = await Fixture.Start();
        var data = await fixture.SeedAllRequestBranches();
        await fixture.ArchiveAllRequestBranchAssignments();
        var before = await fixture.OriginalValueFootprint();
        using var response = await fixture.SendAllRequests(HttpMethod.Get, PrincipalRequests);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // A1,A2,B,C plus the own company-wide legacy record; inactive branch is excluded.
        Assert.Equal(5, body.GetProperty("count").GetInt32());
        var rows = body.GetProperty("records").EnumerateArray().ToArray();
        Assert.Single(rows.Where(r => r.GetProperty("branchRef").ValueKind == JsonValueKind.Null));
        Assert.Equal(before, await fixture.OriginalValueFootprint());
    }

    private sealed partial class Fixture
    {
        public async Task<(Guid First, Guid Second, Guid[] Visible)> SeedAllRequestBranches()
        {
            await ConfigurePrincipalRequester(Owner, linkedEmployee: false);
            await ConfigurePrincipalRequester(Other, linkedEmployee: false);
            Guid first, second, denied, archived;
            using (var services = app.Services.CreateScope())
            {
                var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
                first = await core.WorkLocations.Where(x => x.LegalEntityId == company).Select(x => x.Id).SingleAsync();
                var b = new WorkLocation { Code = "ALL-B", Name = "Second allowed", LegalEntityId = company };
                var c = new WorkLocation { Code = "ALL-C", Name = "Denied branch", LegalEntityId = company };
                var gone = new WorkLocation { Code = "ALL-OLD", Name = "Archived branch", LegalEntityId = company, IsArchived = true };
                core.WorkLocations.AddRange(b,c,gone);second=b.Id;denied=c.Id;archived=gone.Id;
                core.UserBranchAssignments.AddRange(new HrUserBranchAssignment { UserId = Owner, LocationId = second },
                    new HrUserBranchAssignment { UserId = Owner, LocationId = archived });
                await core.SaveChangesAsync();
            }
            var visible = new List<Guid>();
            foreach (var (tenant, entity, maker, branch, number) in new[] {
                ("tenant-a", company, Owner, (Guid?)first, "ALL-A1"),
                ("tenant-a", company, Owner, (Guid?)first, "ALL-A2"),
                ("tenant-a", company, Owner, (Guid?)second, "ALL-B"),
                ("tenant-a", company, Owner, (Guid?)denied, "DENIED-C"),
                ("tenant-a", company, Owner, (Guid?)archived, "ARCHIVED"),
                ("tenant-a", company, Other, (Guid?)first, "OTHER-MAKER"),
                ("tenant-a", company, Owner, (Guid?)null, "OWN-LEGACY-COMPANY"),
                ("tenant-a", otherCompany, Owner, (Guid?)first, "OTHER-COMPANY"),
                ("foreign-tenant", company, Owner, (Guid?)first, "OTHER-TENANT") })
            {
                var requestScope = new ProcurementRequestScope();requestScope.Set(tenant,entity,maker.ToString("D"),branch,null,false,"all-read-fixture");
                await using var db = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
                    .UseInMemoryDatabase(procurementStoreName,procurementStoreRoot).Options,requestScope);
                var row = new PurchaseRequest { Number=number, Purpose=number, CurrencyCode="ZMW",
                    RequesterPrincipalId=maker,BranchRef=branch,Status="Draft" };
                db.AddScoped(row);await db.SaveChangesAsync();
                if(number.StartsWith("ALL-",StringComparison.Ordinal))visible.Add(row.Id);
            }
            return(first,second,visible.ToArray());
        }
        public async Task ArchiveRequestBranchAssignment(Guid branch)
        {
            using var services=app.Services.CreateScope();var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var row=await core.UserBranchAssignments.SingleAsync(x=>x.UserId==Owner && x.LocationId==branch);
            row.IsArchived=true;await core.SaveChangesAsync();
        }
        public async Task ArchiveAllRequestBranchAssignments()
        {
            using var services=app.Services.CreateScope();var core=services.ServiceProvider.GetRequiredService<HrmDbContext>();
            foreach(var row in await core.UserBranchAssignments.Where(x=>x.UserId==Owner).ToArrayAsync())row.IsArchived=true;
            await core.SaveChangesAsync();
        }
        public Task<HttpResponseMessage> SendAllRequests(HttpMethod method,string path,object? body=null) => SendRequestScope(method,path,null,body,true);
        public Task<HttpResponseMessage> SendRequestScope(HttpMethod method,string path,Guid? branch,object? body=null,bool all=false)
        {
            var request=new HttpRequestMessage(method,path);request.Headers.Add("X-Test-User",Owner.ToString("D"));
            request.Headers.Add("X-Shell-Entity",company.ToString("D"));
            if(all)request.Headers.Add("X-Shell-Branch-Scope","all");
            if(branch.HasValue)request.Headers.Add("X-Shell-Location",branch.Value.ToString("D"));
            if(body is not null)request.Content=JsonContent.Create(body);
            return Client.SendAsync(request);
        }
    }
}
