using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

public class ConfigAdminServiceTests
{
    private static (ConfigAdminServiceImpl svc, HrmDbContext ctx) Build(string tenant = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new ConfigRepository(ctx);
        var svc = new ConfigAdminServiceImpl(repo, new PermissiveAuthz());
        return (svc, ctx);
    }

    [Fact]
    public async Task CreateLegalEntity_CreatesAndLists()
    {
        var (svc, ctx) = Build();
        var created = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "ZML", RegisteredName: "Zambia Mining Ltd"), default);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("ZML", created.Code);
        Assert.Equal("ZMW", created.Currency);
        Assert.Equal("ZM", created.CountryCode);
        Assert.Equal("UTC", created.TimeZoneId);

        var repo2 = new ConfigRepository(ctx);
        var svc2 = new ConfigAdminServiceImpl(repo2, new PermissiveAuthz());
        var list = await svc2.ListLegalEntitiesAsync(default);
        Assert.Single(list.Items);
        Assert.Equal("ZML", list.Items[0].Code);
    }

    [Fact]
    public async Task CompanyTimeZone_ValidatesAndPreservesOmittedPatch()
    {
        var (svc, ctx) = Build();
        var created = await svc.CreateLegalEntityAsync(new("ZONE", "Zoned company", TimeZoneId: "Pacific/Auckland"), default);
        Assert.Equal("Pacific/Auckland", created.TimeZoneId);
        var unchanged = await svc.UpdateLegalEntityAsync(created.Id, new(RegisteredName: "Renamed"), default);
        Assert.Equal("Pacific/Auckland", unchanged.TimeZoneId);
        await Assert.ThrowsAsync<DomainException>(() => svc.UpdateLegalEntityAsync(created.Id,
            new(TimeZoneId: "Not/A_Real_Zone"), default));
        Assert.Equal("Pacific/Auckland", (await ctx.LegalEntities.SingleAsync()).TimeZoneId);
        var updated = await svc.UpdateLegalEntityAsync(created.Id, new(TimeZoneId: "UTC"), default);
        Assert.Equal("UTC", updated.TimeZoneId);
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateLegalEntityAsync(
            new("BAD", "Invalid", TimeZoneId: ""), default));
        Assert.Single(await ctx.LegalEntities.ToListAsync());
    }

    [Fact]
    public async Task LegalEntity_DuplicateCode_ReturnsConflict()
    {
        var (svc, _) = Build();
        await svc.CreateLegalEntityAsync(new LegalEntityCreateRequest(Code: "DUP", RegisteredName: "One"), default);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateLegalEntityAsync(new LegalEntityCreateRequest(Code: "dup", RegisteredName: "Two"), default));
    }

    [Fact]
    public async Task OrgUnit_FutureClose_Succeeds_And_NowExcludedFromTree()
    {
        var (svc, ctx) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ1", RegisteredName: "Headquarters One"), default);
        var unit = await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "FIN", Name: "Finance", LegalEntityId: entity.Id), default);

        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        await svc.CloseOrgUnitAsync(unit.Id, new OrgUnitCloseRequest(EffectiveDate: tomorrow), default);

        var repo2 = new ConfigRepository(ctx);
        var svc2 = new ConfigAdminServiceImpl(repo2, new PermissiveAuthz());
        var list = await svc2.ListOrgUnitsAsync(default);
        Assert.Contains(list, u => u.Id == unit.Id && u.Status == "closed");
        var tree = await svc2.GetOrgUnitTreeAsync(default);
        Assert.DoesNotContain(tree, n => n.Id == unit.Id);
    }

    [Fact]
    public async Task OrgUnit_BackdatedClose_Rejected()
    {
        var (svc, _) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ2", RegisteredName: "Headquarters Two"), default);
        var unit = await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "OPS", Name: "Operations", LegalEntityId: entity.Id), default);
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CloseOrgUnitAsync(unit.Id, new OrgUnitCloseRequest(EffectiveDate: yesterday), default));
    }

    [Fact]
    public async Task Location_Create_Update_List()
    {
        var (svc, ctx) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "LOC1", RegisteredName: "Location Entity One"), default);

        var location = await svc.CreateLocationAsync(new WorkLocationCreateRequest(
            Code: "KITWE", Name: "Kitwe Branch", LegalEntityId: entity.Id, Type: "branch", City: "Kitwe"), default);
        Assert.NotEqual(Guid.Empty, location.Id);
        Assert.Equal("branch", location.Type);

        var repo2 = new ConfigRepository(ctx);
        var svc2 = new ConfigAdminServiceImpl(repo2, new PermissiveAuthz());
        var list = await svc2.ListLocationsAsync(default);
        Assert.Contains(list.Items, l => l.Id == location.Id && l.Name == "Kitwe Branch");

        var updated = await svc2.UpdateLocationAsync(location.Id,
            new WorkLocationUpdateRequest(Name: "Kitwe Main Branch"), default);
        Assert.Equal("Kitwe Main Branch", updated.Name);
    }

    [Fact]
    public async Task OrgUnit_DuplicateCode_Rejected()
    {
        var (svc, _) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "DU1", RegisteredName: "Duplicate Code Entity"), default);
        await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "DUP1", Name: "Dup One", LegalEntityId: entity.Id), default);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
                Code: "dup1", Name: "Dup Two", LegalEntityId: entity.Id), default));
    }

    [Fact]
    public async Task OrgUnit_Update_WithNewName_Persists()
    {
        var (svc, _) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "U1", RegisteredName: "Update Entity One"), default);
        var unit = await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "SLS", Name: "Sales", LegalEntityId: entity.Id), default);
        var updated = await svc.UpdateOrgUnitAsync(unit.Id,
            new OrgUnitUpdateRequest(Name: "Sales and Marketing", UnitType: "department"), default);
        Assert.Equal("Sales and Marketing", updated.Name);
        Assert.Equal("department", updated.UnitType);
    }

    [Fact]
    public async Task WorkCalendar_HolidayLifecycle()
    {
        var (svc, ctx) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ3", RegisteredName: "Headquarters Three"), default);
        var calendar = await svc.CreateCalendarAsync(new WorkCalendarCreateRequest(
            Name: "Zambia 2026", LegalEntityId: entity.Id), default);

        var holiday = await svc.AddHolidayAsync(new PublicHolidayCreateRequest(
            CalendarId: calendar.Id, Name: "Independence Day", HolidayDate: "2026-10-24"), default);

        var updated = await svc.UpdateHolidayAsync(holiday.Id, new PublicHolidayUpdateRequest(Description: "Kenneth Kaunda Day"), default);
        Assert.Equal("Kenneth Kaunda Day", updated.Description);

        var repo2 = new ConfigRepository(ctx);
        var svc2 = new ConfigAdminServiceImpl(repo2, new PermissiveAuthz());
        await svc2.DeleteHolidayAsync(holiday.Id, default);
        var calendars = await svc2.ListCalendarsAsync(default);
        Assert.Single(calendars.Items);
        Assert.Empty(calendars.Items[0].Holidays);
    }

    [Fact]
    public async Task WeekendDays_Normalized()
    {
        var (svc, _) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "HQ4", RegisteredName: "Headquarters Four"), default);
        var calendar = await svc.CreateCalendarAsync(new WorkCalendarCreateRequest(
            Name: "Mining Shifts", LegalEntityId: entity.Id, WeekendDays: "SUN, fri ,garbage"), default);
        Assert.Equal("sun,fri", calendar.WeekendDays);
    }

    [Fact]
    public async Task LeaveType_Validation_ClampsAndCodes()
    {
        var (svc, _) = Build();
        var lt = await svc.CreateLeaveTypeAsync(new LeaveTypeCreateRequest(
            Code: "AnnualLeave", Name: "Annual Leave", DefaultDaysPerYear: -5, CarryForwardDays: 10,
            MinNoticeDays: -2), default);
        Assert.Equal(0, lt.DefaultDaysPerYear);
        Assert.Equal(10, lt.CarryForwardDays);
        Assert.Equal(0, lt.MinNoticeDays);
        Assert.Equal("annualleave", lt.Code);

        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateLeaveTypeAsync(new LeaveTypeCreateRequest(
                Code: "annualleave", Name: "Dup"), default));
    }
}

public class EntityTreeTests
{
    private static (ConfigAdminServiceImpl svc, HrmDbContext ctx) Build(string tenant = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new ConfigRepository(ctx);
        var svc = new ConfigAdminServiceImpl(repo, new PermissiveAuthz());
        return (svc, ctx);
    }

    [Fact]
    public async Task EntityTree_RootsAreEntities_WithNestedUnits()
    {
        var (svc, _) = Build();
        var entity = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "ET1", RegisteredName: "Entity Tree One"), default);
        await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "FIN", Name: "Finance", LegalEntityId: entity.Id), default);
        var dept = await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "HR", Name: "Human Resources", LegalEntityId: entity.Id), default);
        await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "HR-REC", Name: "Recruitment", LegalEntityId: entity.Id, ParentId: dept.Id), default);

        var tree = await svc.GetEntityTreeAsync(default);
        Assert.Single(tree);
        Assert.Equal("Entity Tree One", tree[0].Name);
        Assert.Equal("entity", tree[0].UnitType);
        Assert.Equal(2, tree[0].Children.Count);
        var hr = tree[0].Children.First(n => n.Code == "HR");
        Assert.Single(hr.Children);
        Assert.Equal("Recruitment", hr.Children[0].Name);
    }

    [Fact]
    public async Task EntityTree_MultipleEntities_SeparateRoots()
    {
        var (svc, _) = Build();
        var e1 = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "E1", RegisteredName: "Alpha"), default);
        var e2 = await svc.CreateLegalEntityAsync(
            new LegalEntityCreateRequest(Code: "E2", RegisteredName: "Beta"), default);
        await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "A1", Name: "A Unit", LegalEntityId: e1.Id), default);
        await svc.CreateOrgUnitAsync(new OrgUnitCreateRequest(
            Code: "B1", Name: "B Unit", LegalEntityId: e2.Id), default);

        var tree = await svc.GetEntityTreeAsync(default);
        Assert.Equal(2, tree.Count);
        Assert.All(tree, n => Assert.Contains(n.Name, new[] { "Alpha", "Beta" }));
        Assert.All(tree, n => Assert.Single(n.Children));
    }
}
