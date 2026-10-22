using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
using Npgsql;
sealed partial class Probe
{
    private async Task VerifyUnitReviewMetadataExecutionStore(Guid actor)
    {
        string ownerConnection;
        using (var services = Scope(OrderBuyer)) ownerConnection = services.ServiceProvider.GetRequiredService<ProcurementDbContext>().Database.GetDbConnection().ConnectionString;
        Require(OrderDistinctRuntime && InvoiceDocumentStoreBinding.SamePhysicalDatabase(ownerConnection, runtimeConnection)
            && new NpgsqlConnectionStringBuilder(ownerConnection).Username != new NpgsqlConnectionStringBuilder(runtimeConnection).Username,
            "Metadata boundary proof requires distinct actual owner/runtime identities in the same private store.");
        var before = await UnitReviewFootprint();
        // Same genuine DB with deliberately different default/execution roles: neither
        // the read role's owner privilege nor its finite privilege may stand in for execution.
        foreach (var (readConnection, executionConnection, expected) in new[] {
            (ownerConnection, runtimeConnection, true), (runtimeConnection, ownerConnection, false) })
        {
            var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:ProcurementApprovalExecution"] = executionConnection,
                ["ConnectionStrings:CoreDocuments"] = runtimeConnection,
                ["CoreDocuments:Enabled"] = "true", ["CoreDocuments:PrivateDirectory"] = matchingDocumentDirectory,
                ["CoreDocuments:ScannerSocket"] = Path.Combine(matchingDocumentDirectory!, "unused-test-only-scanner.sock") });
            builder.Services.AddScoped<ITenantAccessor>(_ => new Tenant());
            builder.Services.AddDbContext<HrmDbContext>(o => o.UseNpgsql(runtimeConnection));
            ProcurementHostIntegration.AddServices(builder.Services, readConnection, builder.Configuration);
            builder.Services.AddSingleton(new ProcurementInvoiceUnitReviewOptions { Enabled = true, ExecutionDatabaseConfigured = true });
            builder.Services.AddAuthentication("probe").AddScheme<AuthenticationSchemeOptions, ProbeAuthentication>("probe", _ => { });
            builder.Services.AddAuthorization(ProcurementPermissions.AddPolicies);
            await using var host = builder.Build(); host.UseRouting(); host.UseAuthentication(); host.UseMiddleware<ProcurementCorePrincipalRefreshMiddleware>(); host.UseAuthorization(); host.UseMiddleware<ProcurementScopeMiddleware>();
            ProcurementHostIntegration.MapMetadataRoute(host); await host.StartAsync(); using var api = host.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, Root + "/meta"); request.Headers.Add("X-Probe-User", actor.ToString("D"));
            request.Headers.Add("X-Shell-Entity", OrderCompany.ToString("D")); request.Headers.Add("X-Shell-Location", OrderBranch.ToString("D"));
            using var response = await api.SendAsync(request); await Check(response, HttpStatusCode.OK); var meta = await response.Content.ReadFromJsonAsync<JsonElement>();
            Require(meta.GetProperty("capabilities").GetProperty("invoiceOriginalUnitReview").GetBoolean() == expected,
                "Unit-review metadata used the default role/store instead of the actual execution connection.");
            await host.StopAsync();
        }
        Require(before == await UnitReviewFootprint(), "Metadata availability probes created business/case/evidence effects.");
        Console.WriteLine("PASS actual unit-review metadata finite execution connection=true, owner execution connection=false; no business/case/evidence effects.");
    }
}
