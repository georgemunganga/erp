using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

sealed partial class Probe
{
    private async Task VerifyConversionReplayTargetScope(Guid orderId, object originalInput, Guid key, string exactReply)
    {
        Guid otherBranch; Guid administrator;
        using (var services = Scope(OrderBuyer))
        {
            var core = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            var other = new WorkLocation { Code = "REPLAY-B", Name = "Synthetic replay context B", LegalEntityId = OrderCompany };
            core.WorkLocations.Add(other); await core.SaveChangesAsync(); otherBranch = other.Id;
            administrator = await core.LocalUsers.Where(x => x.NormalizedEmail == "BRANCH-ADMIN@EXAMPLE.TEST").Select(x => x.Id).SingleAsync();
        }
        var path = Root + "/orders/from-approved-sources";
        async Task<HttpResponseMessage> ReplayAt(Guid selected)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("X-Probe-User", OrderBuyer.ToString("D"));
            request.Headers.Add("X-Shell-Entity", OrderCompany.ToString("D"));
            request.Headers.Add("X-Shell-Location", selected.ToString("D"));
            request.Headers.Add("Idempotency-Key", key.ToString("D"));
            request.Content = JsonContent.Create(originalInput, options: Json);
            return await client.SendAsync(request);
        }
        // With no branch assignments the actor is organisation-wide. Explicit B context
        // still cannot retrieve/replay the actual A target's historical response.
        await Check(await ReplayAt(otherBranch), HttpStatusCode.NotFound);
        var allowed = await ReplayAt(OrderBranch); await Check(allowed, HttpStatusCode.Created);
        Require(await allowed.Content.ReadAsStringAsync() == exactReply, "Authorized A replay changed the exact conversion response.");

        // Revoke A using the same real Core endpoints/fence exercised by the existing
        // Core integration probe, with the separate bootstrap writer identity. The
        // restricted Procurement app never receives branch-table write privileges.
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddScoped<ITenantAccessor>(_ => new Tenant());
        builder.Services.AddScoped<ShellContext>();
        builder.Services.AddDbContext<HrmDbContext>(options => options.UseNpgsql(databaseConnection));
        builder.Services.AddAuthentication("core-branch-replay-probe")
            .AddScheme<AuthenticationSchemeOptions, CoreBranchWriterAuthentication>("core-branch-replay-probe", _ => { });
        builder.Services.AddAuthorization();
        await using var coreApp = builder.Build();
        coreApp.UseRouting(); coreApp.UseAuthentication(); coreApp.UseAuthorization();
        Mightyfin.Erp.Hrm.Api.Routing.Routes.RegisterBranchAccess(coreApp.MapGroup("/api/hrm/admin").RequireAuthorization());
        await coreApp.StartAsync();
        using var writer = coreApp.GetTestClient();
        writer.DefaultRequestHeaders.Add("X-Core-Branch-Probe-User", administrator.ToString("D"));
        Guid first = Guid.Empty, second = Guid.Empty;
        try
        {
            var response = await writer.PostAsJsonAsync("/api/hrm/admin/branch-access", new { userId = OrderBuyer, locationId = OrderBranch });
            await Check(response, HttpStatusCode.Created);
            first = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            response = await writer.PostAsJsonAsync("/api/hrm/admin/branch-access", new { userId = OrderBuyer, locationId = otherBranch });
            await Check(response, HttpStatusCode.Created);
            second = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            await Check(await writer.DeleteAsync($"/api/hrm/admin/branch-access/{first:D}"), HttpStatusCode.OK); first = Guid.Empty;
            await Check(await ReplayAt(otherBranch), HttpStatusCode.NotFound);
            await Check(await ReplayAt(OrderBranch), HttpStatusCode.Forbidden);
        }
        finally
        {
            if (first != Guid.Empty) await Check(await writer.DeleteAsync($"/api/hrm/admin/branch-access/{first:D}"), HttpStatusCode.OK);
            if (second != Guid.Empty) await Check(await writer.DeleteAsync($"/api/hrm/admin/branch-access/{second:D}"), HttpStatusCode.OK);
        }
        var restored = await ReplayAt(OrderBranch); await Check(restored, HttpStatusCode.Created);
        Require(await restored.Content.ReadAsStringAsync() == exactReply, "Restored authorized scope lost the stable conversion receipt.");
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<ProcurementDbContext>();
            Require(await db.PurchaseOrders.CountAsync() == 1 && await db.PurchaseOrders.AnyAsync(x => x.Id == orderId)
                && await db.PurchaseRequestAllocations.CountAsync(x => x.TargetId == orderId) == 1
                && await db.CommercialCommandReceipts.CountAsync(x => x.Operation == "Convert" && x.CommandKey == key) == 1,
                "Scoped replay denial/restoration duplicated the PO, allocation or command effect.");
        }
        Console.WriteLine("PASS order conversion replay: unconfined selected B denies A; real Core A revoke denies old key; authorized A preserves exact reply/effect counts");
    }
}
