using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Mapped owning-context gate component; not an RFQ/P28 happy-path or PostgreSQL proof.</summary>
public sealed class ProcurementSourcingFinanceSchemaGateTests
{
    [Fact]
    public async Task Missing_Finance_schema_returns_503_before_owner_reads_or_any_effects()
    {
        var scope = new ProcurementRequestScope();
        scope.Set("finance-gate", Guid.NewGuid(), "buyer", null, null, false, "gate");
        await using var owner = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, scope);
        var readiness = new MissingSchema(owner); var reachedOwnerReads = false;
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        await using var app = builder.Build();
        app.MapGet("/gate", async () =>
        {
            try
            {
                await ProcurementSourcingExecutionRoutes.RequireFinanceSchema(owner, readiness, default);
                reachedOwnerReads = true;
                return Microsoft.AspNetCore.Http.Results.Ok();
            }
            catch (ProcurementSourcingExecutionRoutes.Problem error)
            { return ProcurementSourcingExecutionRoutes.Error(error.Message, error.Status); }
        });
        await app.StartAsync(); using var client = app.GetTestClient();
        using var response = await client.GetAsync("/gate");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SourcingFinanceCheckerSchemaUnavailable", body.GetProperty("code").GetString());
        Assert.False(reachedOwnerReads); Assert.Equal(1, readiness.Calls);
        Assert.False(owner.ChangeTracker.HasChanges());
        Assert.Empty(await owner.AuditEvents.ToArrayAsync()); Assert.Empty(await owner.OutboxEvents.ToArrayAsync());
    }

    private sealed class MissingSchema(ProcurementDbContext expected) : IProcurementFinanceCheckerSchemaReadiness
    {
        public int Calls { get; private set; }
        public Task<bool> IsReadyAsync(ProcurementDbContext actual, bool capture, CancellationToken ct)
        {
            Assert.Same(expected, actual); Assert.False(capture); Calls++;
            return Task.FromResult(false);
        }
    }
}
