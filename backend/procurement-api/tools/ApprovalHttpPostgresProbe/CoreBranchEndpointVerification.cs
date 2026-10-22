using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

sealed partial class Probe
{
    private async Task VerifyCoreBranchEndpointCoupling()
    {
        Require(OrderDistinctRuntime && new NpgsqlConnectionStringBuilder(databaseConnection).Username
            != new NpgsqlConnectionStringBuilder(runtimeConnection).Username,
            "Core writer fixture must be separate from the restricted Procurement runtime identity.");
        // A separate synthetic administrator and host use the existing bootstrap connection.
        // Never re-register the Procurement app's HrmDbContext with this writer connection.
        var administrator = new LocalUser { Email = "branch-admin@example.test", NormalizedEmail = "BRANCH-ADMIN@EXAMPLE.TEST",
            DisplayName = "Synthetic Core branch administrator", PasswordHash = "synthetic-no-login-secret", RolesCsv = "hr_admin" };
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            db.LocalUsers.Add(administrator); await db.SaveChangesAsync();
        }
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddScoped<ITenantAccessor>(_ => new Tenant());
        builder.Services.AddScoped<ShellContext>();
        builder.Services.AddDbContext<HrmDbContext>(o => o.UseNpgsql(databaseConnection));
        builder.Services.AddAuthentication("core-branch-probe")
            .AddScheme<AuthenticationSchemeOptions, CoreBranchWriterAuthentication>("core-branch-probe", _ => { });
        builder.Services.AddAuthorization();
        await using var coreApp = builder.Build();
        coreApp.UseRouting(); coreApp.UseAuthentication(); coreApp.UseAuthorization();
        // This is the same mapping entry point invoked by Routes.RegisterConfig, not a fake
        // endpoint that merely calls the lock primitive. It maps only the bounded Core surface.
        Mightyfin.Erp.Hrm.Api.Routing.Routes.RegisterBranchAccess(
            coreApp.MapGroup("/api/hrm/admin").RequireAuthorization());
        await coreApp.StartAsync();
        using var coreClient = coreApp.GetTestClient();
        coreClient.DefaultRequestHeaders.Add("X-Core-Branch-Probe-User", administrator.Id.ToString("D"));
        Guid assignment;
        await using (var reader = new NpgsqlConnection(runtimeConnection))
        {
            await reader.OpenAsync();
            var shared = await CoreBranchAuthorizationFence.AcquireAsync(reader, "probe", exclusive: false);
            try
            {
                var queued = coreClient.PostAsJsonAsync("/api/hrm/admin/branch-access", new
                    { userId = OrderBuyer, locationId = OrderBranch, userEmail = "buyer@example.test" });
                await WaitForLock("%pg_advisory_lock(%");
                Require(!queued.IsCompleted, "Mapped Core POST bypassed the execution branch fence.");
                using (var services = Scope(OrderBuyer))
                {
                    var db = services.ServiceProvider.GetRequiredService<HrmDbContext>();
                    Require(!await db.UserBranchAssignments.AnyAsync(x => x.UserId == OrderBuyer),
                        "Mapped Core POST persisted while a shared execution lease was held.");
                }
                await shared.DisposeAsync();
                var response = await queued.WaitAsync(TimeSpan.FromSeconds(15));
                await Check(response, HttpStatusCode.Created);
                assignment = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            }
            finally { await shared.DisposeAsync(); }
        }
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            Require(await db.UserBranchAssignments.AnyAsync(x => x.Id == assignment && x.UserId == OrderBuyer
                && x.LocationId == OrderBranch && x.TenantId == "probe"), "Mapped Core POST lost canonical assignment scope.");
        }
        await using (var reader = new NpgsqlConnection(runtimeConnection))
        {
            await reader.OpenAsync();
            var shared = await CoreBranchAuthorizationFence.AcquireAsync(reader, "probe", exclusive: false);
            try
            {
                var queued = coreClient.DeleteAsync($"/api/hrm/admin/branch-access/{assignment}");
                await WaitForLock("%pg_advisory_lock(%");
                Require(!queued.IsCompleted, "Mapped Core DELETE bypassed the execution branch fence.");
                using (var services = Scope(OrderBuyer))
                {
                    var db = services.ServiceProvider.GetRequiredService<HrmDbContext>();
                    Require(await db.UserBranchAssignments.AnyAsync(x => x.Id == assignment),
                        "Mapped Core DELETE changed membership while shared execution lease was held.");
                }
                await shared.DisposeAsync();
                await Check(await queued.WaitAsync(TimeSpan.FromSeconds(15)), HttpStatusCode.OK);
            }
            finally { await shared.DisposeAsync(); }
        }
        using (var services = Scope(OrderBuyer))
        {
            var db = services.ServiceProvider.GetRequiredService<HrmDbContext>();
            Require(!await db.UserBranchAssignments.AnyAsync(x => x.UserId == OrderBuyer),
                "Deleting the final Core assignment did not preserve existing organisation-wide semantics.");
        }
        await using (var runtime = new NpgsqlConnection(runtimeConnection))
        {
            await runtime.OpenAsync();
            try
            {
                await using var command = new NpgsqlCommand("DELETE FROM hrm.hr_user_branch_assignments WHERE false", runtime);
                await command.ExecuteNonQueryAsync();
                throw new InvalidOperationException("Restricted Procurement runtime gained Core branch write privileges.");
            }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.InsufficientPrivilege) { }
        }
        Console.WriteLine("PASS Core endpoints: real POST/DELETE wait for restricted reader fence; separate Core administrator writes; runtime branch DML denied");
    }
}

// Authentication exists only in this in-process synthetic TestServer. No network listener,
// password login or production authentication override is registered by the probe.
sealed class CoreBranchWriterAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, HrmDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Guid.TryParse(Request.Headers["X-Core-Branch-Probe-User"], out var principal))
            return AuthenticateResult.NoResult();
        var account = await db.LocalUsers.AsNoTracking().Where(x => x.Id == principal && x.IsActive && !x.IsArchived)
            .Select(x => new { x.Id, x.RolesCsv }).SingleOrDefaultAsync(Context.RequestAborted);
        if (account is null) return AuthenticateResult.Fail("Inactive synthetic Core administrator.");
        var claims = new List<Claim> { new("tenant", "probe"), new("sub", account.Id.ToString("D")) };
        claims.AddRange(account.RolesCsv.Split(',').Select(role => new Claim(ClaimTypes.Role, role)));
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}
