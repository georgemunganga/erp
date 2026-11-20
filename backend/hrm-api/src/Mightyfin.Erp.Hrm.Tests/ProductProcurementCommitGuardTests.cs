using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;
using Npgsql;
using System.Security.Claims;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductProcurementCommitGuardTests
{
    public sealed class NativeProcurementTheoryAttribute : TheoryAttribute
    {
        public NativeProcurementTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_PROCUREMENT_TEST_POSTGRES")))
                Skip = "Requires the isolated migrated Procurement PostgreSQL fixture.";
        }
    }

    [NativeProcurementTheory]
    [InlineData("authorized", 0)]
    [InlineData("revoked", 403)]
    [InlineData("outage", 503)]
    [InlineData("expired", 403)]
    public async Task Owning_native_transaction_commits_only_with_current_authority(string condition, int deniedStatus)
    {
        var connection = Environment.GetEnvironmentVariable("KUUBU_PROCUREMENT_TEST_POSTGRES")!;
        Assert.Equal("kuubu_procurement_preview", new NpgsqlConnectionStringBuilder(connection).Database);
        var clock = new CoreWorkspaceProvisionerTests.Clock();
        var now = clock.GetUtcNow(); var stamp = clock.GetTimestamp();
        var person = Guid.NewGuid(); var company = Guid.NewGuid(); var tenant = "proc-commit-" + Guid.NewGuid().ToString("N");
        var identity = new OperationalSubjectObservation("42", "391bdeaa-87b3-44b3-8e64-2110dcc1a564", now.AddSeconds(30), clock, stamp, TimeSpan.FromSeconds(30));
        var license = new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav", ["procurement"], 1, now.AddSeconds(30), clock, stamp, TimeSpan.FromSeconds(30));
        var proof = new CoreErpRequestScope(new(company, person, tenant, Guid.NewGuid(), 1, "procurement", ["procurement_manager"],
            ["procurement-access", "procurement-vendor-create"], true, [], identity, license), true, []);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant", tenant), new Claim(ClaimTypes.NameIdentifier, person.ToString("D"))], ProductRequestAuthentication.Scheme))
        };
        var rechecks = 0;
        ProductRequestContext.Set(http, proof, (_, _) =>
        {
            rechecks++;
            return Task.FromResult(new CoreProductSessionState(condition == "revoked" ? ModuleAccessStatus.Denied
                : condition == "outage" ? ModuleAccessStatus.Unavailable : ModuleAccessStatus.Authorized, proof));
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ERP:AuthMode"] = "product", ["ERP:ProductProcurement:Enabled"] = "true" }).Build();
        var guard = new ProductProcurementCommitGuard(new HttpContextAccessor { HttpContext = http }, config);
        var options = new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection,
            pg => pg.MigrationsHistoryTable("__procurement_migrations", "procurement")).AddInterceptors(guard).Options;
        var scope = new ProcurementRequestScope(); scope.Set(tenant, company, person.ToString("D"), null, null, false, "native-commit-fixture");
        var record = new Supplier { Number = "NATIVE-" + Guid.NewGuid().ToString("N"), LegalName = "Synthetic commit guard", DisplayName = "Synthetic commit guard", CountryCode = "ZM", Status = "Draft" };
        await using (var db = new ProcurementDbContext(options, scope))
        {
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
            db.AddScoped(record);
            if (condition == "expired") clock.Advance();
            if (deniedStatus == 0) await db.SaveChangesAsync();
            else Assert.Equal(deniedStatus, (await Assert.ThrowsAsync<ProductProcurementAuthorityException>(() => db.SaveChangesAsync())).Status);
        }
        // A new connection verifies the physical commit/rollback, not EF's tracked state.
        await using var check = new ProcurementDbContext(new DbContextOptionsBuilder<ProcurementDbContext>().UseNpgsql(connection).Options, scope);
        Assert.Equal(deniedStatus == 0 ? 1 : 0, await check.Suppliers.CountAsync(x => x.Id == record.Id));
        Assert.Equal(condition == "expired" ? 0 : 1, rechecks);
    }
}
