using System.Security.Claims;
using System.Text.Json;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Domain.Entities;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementContextRoutesTests
{
    [Fact]
    public async Task Context_contains_only_verified_current_company_and_safe_workforce_fields()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var company = new LegalEntity { Code = "A", RegisteredName = "Company A" };
        var otherCompany = new LegalEntity { Code = "B", RegisteredName = "Company B" };
        db.LegalEntities.AddRange(company, otherCompany);
        var branch = new WorkLocation { Code = "A1", Name = "Main branch", LegalEntityId = company.Id };
        var unit = new OrgUnit { Code = "IT", Name = "IT", LegalEntityId = company.Id,
            EffectiveFrom = new DateOnly(2025, 1, 1), Status = "active" };
        db.WorkLocations.Add(branch);
        db.OrgUnits.Add(unit);
        var worker = new Worker { EmployeeNo = "EMP-1", FirstName = "Pat", LastName = "Test",
            Nrc = "private-nrc", PersonalEmail = "private@example.test", Status = "active",
            LocationId = branch.Id, OrgUnitId = unit.Id };
        db.Workers.Add(worker);
        await db.SaveChangesAsync();

        var scope = Scope(company.Id, worker.Id);
        var response = await ProcurementContextRoutes.BuildAsync(db, scope, Claims("employee"));

        Assert.NotNull(response);
        Assert.Equal("tenant-a", response.TenantId);
        Assert.Equal(company.Id, response.LegalEntityId);
        Assert.Equal(worker.Id, response.WorkerId);
        Assert.Equal("Company A", response.Company.Name);
        Assert.Equal(branch.Id, response.Branch?.Id);
        Assert.Equal(unit.Id, response.OrgUnit?.Id);
        Assert.Single(response.AllowedCompanies);
        Assert.Equal(company.Id, response.AllowedCompanies[0].Id);
        Assert.Contains("procurement-request-create", response.Permissions);
        Assert.DoesNotContain("procurement-item-manage", response.Permissions);
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("private-nrc", json);
        Assert.DoesNotContain("private@example.test", json);
        Assert.DoesNotContain("Company B", json);
    }

    [Fact]
    public async Task Context_rejects_worker_from_another_company_or_no_active_worker()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var company = new LegalEntity { Code = "A", RegisteredName = "Company A" };
        var otherCompany = new LegalEntity { Code = "B", RegisteredName = "Company B" };
        db.LegalEntities.AddRange(company, otherCompany);
        var otherBranch = new WorkLocation { Code = "B1", Name = "Other branch", LegalEntityId = otherCompany.Id };
        db.WorkLocations.Add(otherBranch);
        var worker = new Worker { EmployeeNo = "EMP-2", FirstName = "Lee", LastName = "Test",
            Status = "active", LocationId = otherBranch.Id };
        db.Workers.Add(worker);
        await db.SaveChangesAsync();

        Assert.Null(await ProcurementContextRoutes.BuildAsync(db, Scope(company.Id, worker.Id), Claims("employee")));
        worker.Status = "terminated";
        await db.SaveChangesAsync();
        Assert.Null(await ProcurementContextRoutes.BuildAsync(db, Scope(otherCompany.Id, worker.Id), Claims("employee")));
    }

    [Fact]
    public async Task Context_uses_procurement_roles_and_never_exposes_unrelated_roles()
    {
        await using var db = TestDbContextFactory.Create("tenant-a");
        var company = new LegalEntity { Code = "A", RegisteredName = "Company A" };
        db.LegalEntities.Add(company);
        var branch = new WorkLocation { Code = "A1", Name = "Branch", LegalEntityId = company.Id };
        db.WorkLocations.Add(branch);
        var worker = new Worker { EmployeeNo = "EMP-3", FirstName = "Sam", LastName = "Test",
            Status = "active", LocationId = branch.Id };
        db.Workers.Add(worker);
        await db.SaveChangesAsync();

        var response = await ProcurementContextRoutes.BuildAsync(db, Scope(company.Id, worker.Id),
            Claims("procurement_buyer", "hr_admin"));

        Assert.NotNull(response);
        Assert.Equal(["procurement_buyer"], response.Roles);
        Assert.Contains("procurement-item-manage", response.Permissions);
        Assert.Contains("procurement-vendor-manage", response.Permissions);
        Assert.DoesNotContain("hr_admin", response.Roles);
    }

    private static ProcurementRequestScope Scope(Guid companyId, Guid workerId)
    {
        var scope = new ProcurementRequestScope();
        scope.Set("tenant-a", companyId, Guid.NewGuid().ToString(), null, null, false, "test");
        scope.AssignWorker(workerId);
        return scope;
    }

    private static Claim[] Claims(params string[] roles) =>
        roles.Select(x => new Claim(ClaimTypes.Role, x)).ToArray();
}
