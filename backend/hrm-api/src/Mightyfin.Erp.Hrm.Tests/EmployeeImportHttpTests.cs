using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mightyfin.Erp.Hrm.Application.Shared;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Api.Routing;

namespace Mightyfin.Erp.Hrm.Tests;

// Real mapped handlers and SQLite persistence; identity here is a test principal.
// Product authority/fences are covered by the separate ProductHrm tests.
public sealed class EmployeeImportHttpTests
{
    [Theory]
    [InlineData("Permanent")]
    [InlineData("permanent")]
    [InlineData(" PERMANENT ")]
    public async Task Raw_label_from_old_browser_survives_http_preview_apply_and_database(string label)
    {
        await using var db = TestDbContextFactory.Create($"http-import-{Guid.NewGuid():N}");
        var repo = new WorkerRepository(db);
        var service = new ImportExportServiceImpl([new WorkersImportSchema(repo,
            new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider()),
            new PermissiveAuthz(), new Application.ShellContext())]);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Configuration["ERP:AuthMode"] = "local";
        builder.Services.AddAuthentication(); builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IImportExportService>(service);
        await using var app = builder.Build();
        app.UseRouting();
        app.Use(async (context, next) => {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"synthetic-test-admin"), new Claim(ClaimTypes.Role,"hr_admin")], "test-only"));
            await next();
        });
        app.UseAuthorization(); Routes.RegisterImportExport(app); await app.StartAsync();
        using var client = app.GetTestClient();
        var prefix = Routes.HrmPrefix + "/import/workers";
        using var preview = await client.PostAsJsonAsync(prefix + "/preview", new {
            fileName="synthetic.csv", mode="insert", rows=new[]{new Dictionary<string,string>{
                ["employeeNo"]="QA-HTTP", ["firstName"]="Import", ["lastName"]="Test",
                ["email"]="preview@example.invalid", ["phone"]="0971234567", ["workerType"]=label }} });
        preview.EnsureSuccessStatusCode();
        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0,body.GetProperty("willError").GetInt32());
        Assert.Equal("employee",body.GetProperty("rows")[0].GetProperty("resolved").GetProperty("workerType").GetString());
        using var applied = await client.PostAsJsonAsync(prefix + "/apply", new {previewId=body.GetProperty("id").GetGuid(),rowIndexes=new[]{0}});
        applied.EnsureSuccessStatusCode();
        Assert.Equal(1,(await applied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32());
        Assert.Equal("employee",(await db.Workers.SingleAsync()).WorkerType);
    }
}
