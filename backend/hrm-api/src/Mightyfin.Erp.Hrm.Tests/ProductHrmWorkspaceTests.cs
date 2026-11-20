using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductHrmWorkspaceTests
{
    [Fact]
    public async Task Employee_numbers_are_unique_within_a_tenant_and_reusable_across_tenants()
    {
        await using var db = Database();
        db.Workers.Add(new Worker { EmployeeNo = "EMP-0001", FirstName = "First", LastName = "Company" });
        await db.SaveChangesAsync();
        await using (var foreign = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseSqlite(db.Database.GetDbConnection()).Options, new Tenant("tenant-b")))
        {
            foreign.Workers.Add(new Worker { EmployeeNo = "EMP-0001", FirstName = "Second", LastName = "Company" });
            await foreign.SaveChangesAsync();
            Assert.Single(await foreign.Workers.ToListAsync());
        }
        Assert.Single(await db.Workers.ToListAsync());
        db.Workers.Add(new Worker { EmployeeNo = "EMP-0001", FirstName = "Duplicate", LastName = "Same company" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private sealed record Tenant(string Id) : ITenantAccessor { public string GetTenantId() => Id; }
    private static HrmDbContext Database()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:"); connection.Open();
        var db = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options, new Tenant("tenant-a"));
        db.Database.EnsureCreated(); return db;
    }
    private static CoreErpRequestScope Proof(Guid company, Guid[] locations, bool all = false,
        string module = "hrm", string[]? roles = null, string[]? actions = null)
    {
        var clock = TimeProvider.System; var now = clock.GetUtcNow(); var stamp = clock.GetTimestamp();
        return new(new(company, Guid.NewGuid(), "tenant-a", Guid.NewGuid(), 1, module, roles ?? ["hr_admin"],
            actions ?? ["hrm-access", "hrm-admin"], all, locations,
            new OperationalSubjectObservation("42", "391bdeaa-87b3-44b3-8e64-2110dcc1a564", now.AddMinutes(1), clock, stamp, TimeSpan.FromMinutes(1)),
            new CommercialLicenseLease("01arz3ndektsv4rrffq69g5fav", [module], 1, now.AddMinutes(1), clock, stamp, TimeSpan.FromMinutes(1))), all, locations);
    }
    private static DefaultHttpContext Request(CoreErpRequestScope proof)
    {
        var http = new DefaultHttpContext { User = new(new ClaimsIdentity([
            new("tenant", proof.TenantId), new(ClaimTypes.NameIdentifier, proof.PrincipalId.ToString("D"))], "kuubu-erp")) };
        ProductRequestContext.Set(http, proof, (_, _) => Task.FromResult(new CoreProductSessionState(ModuleAccessStatus.Authorized, proof)));
        return http;
    }

    [Theory]
    [InlineData("/api/hrm/identity/users", "POST")]
    [InlineData("/api/hrm/IDENTITY/users", "GET")]
    [InlineData("/api/hrm/auth/users", "POST")]
    [InlineData("/api/hrm/admin/branch-access", "POST")]
    [InlineData("/api/hrm/admin/roles/owner", "PATCH")]
    [InlineData("/api/hrm/setup/reset", "POST")]
    [InlineData("/api/hrm/admin/legal-entities", "POST")]
    [InlineData("/api/v1/hrm/integrations", "GET")]
    [InlineData("/api/core/v1/roles", "GET")]
    public void Compatibility_never_opens_credential_grant_reset_or_company_provisioning_routes(string path, string method)
        => Assert.False(ProductHrmWorkspace.PathAllowed(path, method));

    [Theory]
    [InlineData("/api/hrm/payroll/periods", "POST")]
    [InlineData("/api/hrm/admin/leave-types", "GET")]
    [InlineData("/api/v1/hrm/recruitment/vacancies", "GET")]
    [InlineData("/api/hrm/time/leave", "POST")]
    public void Existing_business_routes_can_be_reviewed_separately_from_identity(string path, string method)
        => Assert.True(ProductHrmWorkspace.PathAllowed(path, method));

    [Fact]
    public void Selected_role_and_explicit_actions_are_all_required()
    {
        var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        Assert.True(ProductHrmWorkspace.RoleAllows(Proof(company, [branch])));
        Assert.False(ProductHrmWorkspace.RoleAllows(Proof(company, [branch], roles: ["employee"])));
        Assert.False(ProductHrmWorkspace.RoleAllows(Proof(company, [branch], actions: ["hrm-access"])));
        Assert.False(ProductHrmWorkspace.RoleAllows(Proof(company, [branch], module: "procurement")));
    }

    [Fact]
    public async Task Company_wide_legacy_routes_never_widen_a_partial_location_grant()
    {
        await using var db = Database();
        var company = new LegalEntity { Code = "C", RegisteredName = "Selected" };
        var hq = new WorkLocation { Code = "HQ", Name = "Head office", LegalEntityId = company.Id };
        var owner = LocalUser.Federated("Synthetic owner");
        db.LocalUsers.Add(owner);
        db.LegalEntities.Add(company); db.WorkLocations.Add(hq);
        db.WorkspaceBindings.Add(new CoreWorkspaceBinding { LegalEntityId = company.Id,
            Issuer = ProductCommercialLicense.Issuer, Subject = "42", ClientId = "test", CustomerId = "test",
            EnrollmentId = "01arz3ndektsv4rrffq69g5fav", Slot = 1, PurchasedLimit = 1,
            OwnerLocalUserId = owner.Id, AuthorityUntil = DateTimeOffset.UtcNow.AddMinutes(1), LicenseUntil = DateTimeOffset.UtcNow.AddMinutes(1),
            IdempotencyHash = new string('a', 64), RequestHash = new string('b', 64) });
        await db.SaveChangesAsync();
        var http = Request(Proof(company.Id, [hq.Id]));
        Assert.True(await ProductHrmWorkspace.CoversCompanyAsync(http, db, default));
        var other = new WorkLocation { Code = "OLD", Name = "Historical branch", LegalEntityId = company.Id, IsArchived = true };
        db.WorkLocations.Add(other); await db.SaveChangesAsync();
        Assert.False(await ProductHrmWorkspace.CoversCompanyAsync(http, db, default));
        Assert.True(await ProductHrmWorkspace.CoversCompanyAsync(Request(Proof(company.Id, [], all: true)), db, default));
        db.LegalEntities.Add(new LegalEntity { Code = "OTHER", RegisteredName = "Second company" }); await db.SaveChangesAsync();
        Assert.False(await ProductHrmWorkspace.CoversCompanyAsync(Request(Proof(company.Id, [], all: true)), db, default));
    }

    [Fact]
    public async Task Caller_tenant_headers_cannot_override_selected_workspace()
    {
        var http = new DefaultHttpContext(); http.Request.Path = "/api/hrm/admin/leave-types";
        http.Request.Headers["HRM-Default-TenantId"] = "foreign-tenant";
        var reached = false;
        await ProductHrmWorkspace.Boundary(http, _ => { reached = true; return Task.CompletedTask; });
        Assert.False(reached); Assert.Equal(403, http.Response.StatusCode);
    }

    [Fact]
    public async Task Logical_worker_references_and_detached_foreign_updates_are_denied_before_sql()
    {
        await using var db = Database();
        var own = new Worker { EmployeeNo = "OWN", FirstName = "Own", LastName = "Worker" };
        db.Workers.Add(own); await db.SaveChangesAsync();
        var other = new Worker { EmployeeNo = "OTHER", FirstName = "Other", LastName = "Worker" };
        await using (var foreign = new HrmDbContext(new DbContextOptionsBuilder<HrmDbContext>()
            .UseSqlite(db.Database.GetDbConnection()).Options, new Tenant("tenant-b")))
        { foreign.Workers.Add(other); await foreign.SaveChangesAsync(); }
        var http = Request(Proof(Guid.NewGuid(), [], all: true));
        var guard = new ProductHrmWriteGuard(new HttpContextAccessor { HttpContext = http }, new ConfigurationBuilder().Build());
        var document = new WorkerDocument { TenantId = "tenant-a", WorkerId = own.Id, Category = "test", Title = "Synthetic", FileName = "test.pdf", ContentType = "application/pdf", Classification = "internal", StoragePath = "/private/test.pdf" };
        db.WorkerDocuments.Add(document);
        await guard.ValidateAsync(db, http, default);
        document.WorkerId = other.Id;
        Assert.Equal(403, (await Assert.ThrowsAsync<ProductProcurementAuthorityException>(() => guard.ValidateAsync(db, http, default))).Status);
        db.ChangeTracker.Clear();
        var retarget = new Worker { Id = other.Id, TenantId = "tenant-a", EmployeeNo = "SPOOF", FirstName = "Attempt", LastName = "Retarget" };
        db.Workers.Attach(retarget); db.Entry(retarget).State = EntityState.Modified;
        Assert.Equal(403, (await Assert.ThrowsAsync<ProductProcurementAuthorityException>(() => guard.ValidateAsync(db, http, default))).Status);
    }
}
