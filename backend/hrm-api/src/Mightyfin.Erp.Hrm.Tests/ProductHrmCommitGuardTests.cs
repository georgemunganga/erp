using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Npgsql;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductHrmCommitGuardTests
{
    private sealed class Clock : TimeProvider
    {
        private readonly DateTimeOffset start = DateTimeOffset.UtcNow;
        private TimeSpan offset;
        public override DateTimeOffset GetUtcNow() => start + offset;
        public override long GetTimestamp() => offset.Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance() => offset += TimeSpan.FromMinutes(1);
    }
    private sealed record Tenant(string Id) : ITenantAccessor { public string GetTenantId() => Id; }
    public sealed class NativeHrmTheoryAttribute : TheoryAttribute
    {
        public NativeHrmTheoryAttribute()
        { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KUUBU_HRM_WORKSPACE_TEST_POSTGRES"))) Skip = "Requires isolated migrated HRM PostgreSQL fixture."; }
    }

    [NativeHrmTheory]
    [InlineData("authorized", 0)]
    [InlineData("revoked", 403)]
    [InlineData("outage", 503)]
    [InlineData("expired", 403)]
    [InlineData("location-expanded", 403)]
    public async Task Every_native_save_rechecks_authority_and_rolls_back_uncovered_locations(string condition, int denied)
    {
        var connection = Environment.GetEnvironmentVariable("KUUBU_HRM_WORKSPACE_TEST_POSTGRES")!;
        Assert.Equal("kuubu_procurement_preview", new NpgsqlConnectionStringBuilder(connection).Database);
        var tenant = new Tenant("hrm-commit-" + Guid.NewGuid().ToString("N"));
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection).Options;
        var company = new LegalEntity { TenantId = tenant.Id, Code = "MAIN", RegisteredName = "Synthetic HR commit" };
        var owner = LocalUser.Federated("Synthetic HR owner"); owner.TenantId = tenant.Id;
        var hq = new WorkLocation { TenantId = tenant.Id, Code = "HQ", Name = "Head office", LegalEntityId = company.Id };
        await using (var seed = new HrmDbContext(options, tenant))
        {
            seed.AddRange(company, owner, hq);
            seed.WorkspaceBindings.Add(new CoreWorkspaceBinding { TenantId = tenant.Id, Issuer = ProductCommercialLicense.Issuer,
                Subject = "42", ClientId = Guid.NewGuid().ToString("D"), CustomerId = "01arz3ndektsv4rrffq69g5fav",
                EnrollmentId = "01" + Guid.NewGuid().ToString("N")[..24], Slot = 1, PurchasedLimit = 1, LegalEntityId = company.Id,
                OwnerLocalUserId = owner.Id, IdempotencyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant(), RequestHash = new string('b', 64),
                AuthorityUntil = DateTimeOffset.UtcNow.AddMinutes(1), LicenseUntil = DateTimeOffset.UtcNow.AddMinutes(1) });
            await seed.SaveChangesAsync();
        }
        var clock = new Clock(); var now = clock.GetUtcNow(); var stamp = clock.GetTimestamp();
        var all = condition != "location-expanded";
        var proof = new CoreErpRequestScope(new(company.Id, owner.Id, tenant.Id, Guid.NewGuid(), 1, "hrm", ["hr_admin"],
            ["hrm-access", "hrm-admin"], all, all ? [] : [hq.Id],
            new OperationalSubjectObservation("42", "391bdeaa-87b3-44b3-8e64-2110dcc1a564", now.AddSeconds(30), clock, stamp, TimeSpan.FromSeconds(30)),
            new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav", ["hrm"], 1, now.AddSeconds(30), clock, stamp, TimeSpan.FromSeconds(30))), all, all ? [] : [hq.Id]);
        var http = new DefaultHttpContext { User = new(new ClaimsIdentity([new("tenant", tenant.Id),
            new(ClaimTypes.NameIdentifier, owner.Id.ToString("D"))], "kuubu-erp")) };
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ProductHrmWorkspacePolicy()), "native HRM"));
        ProductRequestContext.Set(http, proof, (_, _) => Task.FromResult(new CoreProductSessionState(
            condition == "revoked" ? ModuleAccessStatus.Denied : condition == "outage" ? ModuleAccessStatus.Unavailable : ModuleAccessStatus.Authorized, proof)));
        ProductHrmWorkspace.MarkAdmission(http);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ERP:AuthMode"] = "product", ["ERP:ProductHrmWorkspace:Enabled"] = "true" }).Build();
        var guard = new ProductHrmCommitGuard(new HttpContextAccessor { HttpContext = http }, config);
        var record = new WorkLocation { TenantId = tenant.Id, Code = "NEW", Name = "Synthetic new location", LegalEntityId = company.Id };
        await using (var db = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(connection).AddInterceptors(guard).Options, tenant))
        {
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.True(await ProductHrmWorkspace.CoversCompanyAsync(http, db, default));
            db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
            db.WorkLocations.Add(record);
            if (condition == "expired") clock.Advance();
            if (denied == 0) await db.SaveChangesAsync();
            else Assert.Equal(denied, (await Assert.ThrowsAsync<ProductProcurementAuthorityException>(() => db.SaveChangesAsync())).Status);
        }
        await using var check = new HrmDbContext(options, tenant);
        Assert.Equal(denied == 0, await check.WorkLocations.AnyAsync(x => x.Id == record.Id));
    }
}
