using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class CoreOrganisationAuthorityTests
{
    private sealed class RoleAuthz(params string[] assigned) : IAuthzService
    {
        public string CurrentSubjectId => "synthetic-core-admin";
        public bool IsRole(params string[] roles) => roles.Any(assigned.Contains);
        public void RequireAnyRole(params string[] roles)
        {
            if (!IsRole(roles)) throw new DomainException("forbidden", "Access denied.");
        }
        public bool CanAccessSensitive(string category) => false;
    }

    private static ConfigAdminServiceImpl Service(HrmDbContext db, string role) =>
        new(new ConfigRepository(db), new RoleAuthz(role));

    [Fact]
    public async Task ErpAdminOnly_ManagesExistingCoreMasters_WithoutHrmAuthority()
    {
        await using var db = TestDbContextFactory.Create();
        var service = Service(db, "erp_admin");
        var entity = await service.CreateLegalEntityAsync(new("CORE", "Core Company"), default);
        var updated = await service.UpdateLegalEntityAsync(entity.Id, new(Currency: "USD", RegisteredName: "Core Profile"), default);
        Assert.Equal("USD", updated.Currency);
        Assert.Equal("Core Profile", (await service.GetLegalEntityAsync(entity.Id, default)).RegisteredName);
        Assert.Single((await service.ListLegalEntitiesAsync(default)).Items);
        var location = await service.CreateLocationAsync(new("HQ", "Headquarters", entity.Id), default);
        await service.UpdateLocationAsync(location.Id, new(Name: "Main office"), default);
        Assert.Equal("Main office", Assert.Single((await service.ListLocationsAsync(default)).Items).Name);
        var unit = await service.CreateOrgUnitAsync(new("OPS", "Operations", entity.Id), default);
        await service.UpdateOrgUnitAsync(unit.Id, new(Name: "Shared Operations"), default);
        Assert.Equal("Shared Operations", Assert.Single(await service.ListOrgUnitsAsync(default)).Name);
        Assert.Single(await service.GetOrgUnitTreeAsync(default));
        Assert.Single(await service.GetEntityTreeAsync(default));
        await service.CloseOrgUnitAsync(unit.Id, new(DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd")), default);
        Assert.Equal("closed", (await db.OrgUnits.SingleAsync()).Status);
        await Assert.ThrowsAsync<DomainException>(() => service.CreateCalendarAsync(new("Payroll calendar", entity.Id), default));
        Assert.Empty(await db.WorkCalendars.ToListAsync());
    }

    [Fact]
    public async Task Employee_CannotMutateCoreMasters_ExistingReadsRemainAllowed()
    {
        await using var db = TestDbContextFactory.Create();
        var admin = Service(db, "erp_admin");
        var entity = await admin.CreateLegalEntityAsync(new("CORE", "Core Company"), default);
        var location = await admin.CreateLocationAsync(new("HQ", "Headquarters", entity.Id), default);
        var unit = await admin.CreateOrgUnitAsync(new("OPS", "Operations", entity.Id), default);
        var employee = Service(db, "employee");
        await Assert.ThrowsAsync<DomainException>(() => employee.CreateLegalEntityAsync(new("BAD", "Denied"), default));
        await Assert.ThrowsAsync<DomainException>(() => employee.UpdateLegalEntityAsync(entity.Id, new(Currency: "USD"), default));
        await Assert.ThrowsAsync<DomainException>(() => employee.CreateLocationAsync(new("BAD", "Denied", entity.Id), default));
        await Assert.ThrowsAsync<DomainException>(() => employee.UpdateLocationAsync(location.Id, new(Name: "Denied"), default));
        await Assert.ThrowsAsync<DomainException>(() => employee.CreateOrgUnitAsync(new("BAD", "Denied", entity.Id), default));
        await Assert.ThrowsAsync<DomainException>(() => employee.UpdateOrgUnitAsync(unit.Id, new(Name: "Denied"), default));
        await Assert.ThrowsAsync<DomainException>(() => employee.CloseOrgUnitAsync(unit.Id, new(DateTime.UtcNow.ToString("yyyy-MM-dd")), default));
        Assert.Single((await employee.ListLocationsAsync(default)).Items);
        Assert.Single(await employee.ListOrgUnitsAsync(default));
        Assert.Single(await db.LegalEntities.ToListAsync());
        Assert.Equal("ZMW", (await db.LegalEntities.SingleAsync()).Currency);
        Assert.Equal("Headquarters", (await db.WorkLocations.SingleAsync()).Name);
        Assert.Equal("Operations", (await db.OrgUnits.SingleAsync()).Name);
        Assert.Null((await db.OrgUnits.SingleAsync()).EffectiveTo);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task ErpAdmin_DoesNotReadOrMutateForeignTenantReferences()
    {
        await using var foreign = TestDbContextFactory.Create("foreign");
        var owner = Service(foreign, "erp_admin");
        var entity = await owner.CreateLegalEntityAsync(new("PRIVATE", "Foreign Company"), default);
        var location = await owner.CreateLocationAsync(new("PRIVATE", "Foreign Location", entity.Id), default);
        var unit = await owner.CreateOrgUnitAsync(new("PRIVATE", "Foreign Department", entity.Id), default);
        // Same relational database, different current tenant: an empty independent database would not test isolation.
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(foreign.Database.GetDbConnection()).Options;
        await using var current = new HrmDbContext(options, new FixedTenantAccessor("current"));
        var admin = Service(current, "erp_admin");
        Assert.Empty((await admin.ListLegalEntitiesAsync(default)).Items);
        Assert.Empty((await admin.ListLocationsAsync(default)).Items);
        Assert.Empty(await admin.ListOrgUnitsAsync(default));
        await Assert.ThrowsAsync<DomainException>(() => admin.GetLegalEntityAsync(entity.Id, default));
        await Assert.ThrowsAsync<DomainException>(() => admin.UpdateLegalEntityAsync(entity.Id, new(Currency: "USD"), default));
        await Assert.ThrowsAsync<DomainException>(() => admin.UpdateLocationAsync(location.Id, new(Name: "Denied"), default));
        await Assert.ThrowsAsync<DomainException>(() => admin.UpdateOrgUnitAsync(unit.Id, new(Name: "Denied"), default));
        await Assert.ThrowsAsync<DomainException>(() => admin.CloseOrgUnitAsync(unit.Id, new(DateTime.UtcNow.ToString("yyyy-MM-dd")), default));
        await Assert.ThrowsAsync<DomainException>(() => admin.CreateLocationAsync(new("BAD", "Denied", entity.Id), default));
        await Assert.ThrowsAsync<DomainException>(() => admin.CreateOrgUnitAsync(new("BAD", "Denied", entity.Id), default));
        Assert.False(current.ChangeTracker.HasChanges());
        foreign.ChangeTracker.Clear();
        Assert.Equal("ZMW", (await foreign.LegalEntities.SingleAsync()).Currency);
        Assert.Equal("Foreign Location", (await foreign.WorkLocations.SingleAsync()).Name);
        Assert.Equal("Foreign Department", (await foreign.OrgUnits.SingleAsync()).Name);
        Assert.Null((await foreign.OrgUnits.SingleAsync()).EffectiveTo);
    }
    private sealed class WorkforceQueries : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"workers\"", StringComparison.OrdinalIgnoreCase)) Reads++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task CoreOnlyDepartmentProjections_NeverQueryWorkers_OrDiscloseManagerIdentity()
    {
        await using var seed = TestDbContextFactory.Create("core-projection");
        var hr = Service(seed, "hr_admin");
        var entity = await hr.CreateLegalEntityAsync(new("ORG", "Organization"), default);
        var manager = new Worker { EmployeeNo = "PRIVATE", FirstName = "Private", LastName = "Manager", Nrc = "restricted-id" };
        seed.Workers.Add(manager);
        await seed.SaveChangesAsync();
        var unit = await hr.CreateOrgUnitAsync(new("OPS", "Operations", entity.Id, ManagerId: manager.Id), default);
        var counter = new WorkforceQueries();
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(seed.Database.GetDbConnection())
            .AddInterceptors(counter).Options;
        await using var current = new HrmDbContext(options, new FixedTenantAccessor("core-projection"));
        var core = Service(current, "erp_admin");
        var listed = Assert.Single(await core.ListOrgUnitsAsync(default));
        Assert.Null(listed.ManagerId);
        Assert.Null(listed.ManagerName);
        var tree = Assert.Single(await core.GetOrgUnitTreeAsync(default));
        Assert.Null(tree.ManagerId);
        Assert.Null(tree.ManagerName);
        var entityTree = Assert.Single(await core.GetEntityTreeAsync(default));
        Assert.Null(Assert.Single(entityTree.Children).ManagerId);
        Assert.Null(Assert.Single(entityTree.Children).ManagerName);
        var updated = await core.UpdateOrgUnitAsync(unit.Id, new(Name: "Shared Operations"), default);
        Assert.Null(updated.ManagerId);
        Assert.Null(updated.ManagerName);
        var created = await core.CreateOrgUnitAsync(new("NEW", "New Department", entity.Id), default);
        Assert.Null(created.ManagerName);
        Assert.Equal(0, counter.Reads);
        Assert.Empty(current.ChangeTracker.Entries<Worker>());
        await Assert.ThrowsAsync<DomainException>(() => core.CreateOrgUnitAsync(new("DENIED", "Denied", entity.Id, ManagerId: manager.Id), default));
        await Assert.ThrowsAsync<DomainException>(() => core.UpdateOrgUnitAsync(unit.Id, new(Name: "Must not mutate", ManagerId: manager.Id), default));
        Assert.False(current.ChangeTracker.HasChanges());
        Assert.Equal(0, counter.Reads);
        seed.ChangeTracker.Clear();
        Assert.Equal(manager.Id, (await seed.OrgUnits.SingleAsync(x => x.Id == unit.Id)).ManagerId);
        Assert.Equal("Shared Operations", (await seed.OrgUnits.SingleAsync(x => x.Id == unit.Id)).Name);
        var hrProjection = (await Service(current, "hr_ops").ListOrgUnitsAsync(default)).Single(x => x.Id == unit.Id);
        Assert.Equal(manager.Id, hrProjection.ManagerId);
        Assert.Equal("Private Manager", hrProjection.ManagerName);
        Assert.True(counter.Reads > 0);
    }

}
