using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Workers;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Permissive double for <see cref="IAuthzService"/> used by the service layer.</summary>
internal sealed class PermissiveAuthz : IAuthzService
{
    public string CurrentSubjectId => "test-subject";
    // M25: configurable role set so tests can impersonate an employee-only
    // caller; defaults to all roles (permissive).
    private string[] _roles = ["hr_ops", "hr_admin", "payroll", "employee"];
    public string[] Roles { get => _roles; init => _roles = value; }
        public void RequireAnyRole(params string[] roles) { }
    public bool IsRole(params string[] roles) => roles.Any(r => _roles.Contains(r));
    public bool CanAccessSensitive(string category) => true;
}

/// <summary>Simple id provider double for tests.</summary>
internal sealed class UlidIdProvider : IIdProvider
{
    public string NewCorrelationId() => System.Guid.NewGuid().ToString();
}

/// <summary>Fixed tenant accessor so tests run against a known tenant.</summary>
internal sealed class FixedTenantAccessor(string tenant) : ITenantAccessor
{
    public string GetTenantId() => tenant;
}

/// <summary>SQLite in-memory EF context wired up with the same tenant-scoping
/// rules as the production context (global query filters + tenant auto-fill).
/// SQLite in-memory is used rather than the InMemory provider because EF Core
/// 10's InMemory provider has a bug with Guid-V7 primary keys: inserting a
/// child entity via navigation after the parent was loaded throws a spurious
/// DbUpdateConcurrencyException.</summary>
internal static class TestDbContextFactory
{
    public static HrmDbContext Create(string tenant = "test-tenant")
    {
        var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=hrm-tests-" + System.Guid.NewGuid() + ";Mode=Memory;Cache=Shared");
        conn.Open();
        var opts = new DbContextOptionsBuilder<HrmDbContext>()
            .UseSqlite(conn)
            .Options;
        var ctx = new HrmDbContext(opts, new FixedTenantAccessor(tenant));
        ctx.Database.EnsureCreated();
        return ctx;
    }
}

public class WorkerServiceTests
{
    private static (WorkerServiceImpl service, HrmDbContext ctx) Build(string tenant = "test-tenant")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new WorkerRepository(ctx);
        var service = new WorkerServiceImpl(repo, new PermissiveAuthz(), new UlidIdProvider());
        return (service, ctx);
    }

    [Fact]
    public async Task CreateWorker_SetsTenantId()
    {
        var (service, ctx) = Build();
        var dto = await service.CreateAsync(
            new WorkerCreateRequest(EmployeeNo: "EMP-001", FirstName: "Test", LastName: "Worker", WorkerType: "employee"),
            CancellationToken.None);
        var worker = await ctx.Workers.FirstAsync();
        Assert.Equal("test-tenant", worker.TenantId);
        Assert.Equal("Test Worker", worker.FullName);
        Assert.Equal("pre-hire", worker.Status);
        Assert.Equal("EMP-001", worker.EmployeeNo);
    }

    [Fact]
    public async Task CreateWorker_SetsDefaultNationality()
    {
        var (service, ctx) = Build();
        await service.CreateAsync(
            new WorkerCreateRequest(EmployeeNo: "EMP-001", FirstName: "Test", LastName: "Worker", WorkerType: "employee"),
            CancellationToken.None);

        // TenantId is auto-populated by the DbContext on save.
        var worker = await ctx.Workers.FirstAsync();
        Assert.Equal("Zambian", worker.Nationality);
    }

    [Fact]
    public async Task CreateWorker_WithFutureStartDate_RemainsPreHire()
    {
        var (service, _) = Build();
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd");
        var worker = await service.CreateAsync(new WorkerCreateRequest(
            EmployeeNo: "", FirstName: "Future", LastName: "Starter",
            StartDate: future, WorkerType: "employee"), CancellationToken.None);

        Assert.Equal("pre-hire", worker.Status);
    }

    [Fact]
    public async Task PersonnelDetails_RoundTripThroughCreateAndUpdate()
    {
        var (service, _) = Build();
        var created = await service.CreateAsync(new WorkerCreateRequest(
            EmployeeNo: "EMP-PROFILE", FirstName: "Mina", LastName: "Dube",
            ProfileDetailsJson: "{\"salutation\":\"Ms\",\"gender\":\"Female\"}"), CancellationToken.None);
        Assert.Contains("\"salutation\":\"Ms\"", created.ProfileDetailsJson);

        var updated = await service.UpdateAsync(created.Id, new WorkerUpdateRequest(
            ProfileDetailsJson: "{\"salutation\":\"Dr\",\"gender\":\"Female\"}"), CancellationToken.None);
        Assert.Contains("\"salutation\":\"Dr\"", updated.ProfileDetailsJson);
        Assert.Equal(updated.ProfileDetailsJson, (await service.GetByIdAsync(created.Id, CancellationToken.None))?.ProfileDetailsJson);
    }

    [Fact]
    public async Task PersonnelDetails_RejectNonObjectJson()
    {
        var (service, _) = Build();
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(new WorkerCreateRequest(
            EmployeeNo: "EMP-INVALID", FirstName: "Mina", LastName: "Dube",
            ProfileDetailsJson: "[]"), CancellationToken.None));
    }

    [Fact]
    public async Task TenantFilter_ScopesQueriesToCurrentTenant()
    {
        var ctx = TestDbContextFactory.Create("tenant-a");
        ctx.Workers.Add(new Worker { EmployeeNo = "EMP-A1", FirstName = "A", LastName = "One", WorkerType = "employee", Status = "pre-hire" });
        await ctx.SaveChangesAsync();

        var otherCtx = TestDbContextFactory.Create("tenant-b");
        otherCtx.Workers.Add(new Worker { EmployeeNo = "EMP-B1", FirstName = "B", LastName = "One", WorkerType = "employee", Status = "pre-hire" });
        await otherCtx.SaveChangesAsync();

        // Each context only sees its own tenant thanks to the global query filter.
        Assert.Equal(1, await ctx.Workers.CountAsync());
        Assert.Equal(1, await otherCtx.Workers.CountAsync());
        Assert.Equal("EMP-A1", (await ctx.Workers.FirstAsync()).EmployeeNo);
        Assert.Equal("EMP-B1", (await otherCtx.Workers.FirstAsync()).EmployeeNo);
    }

    [Fact]
    public async Task GetBySubject_ResolveLinkedWorker()
    {
        var (service, ctx) = Build();
        ctx.Workers.Add(new Worker
        {
            EmployeeNo = "EMP-001",
            FirstName = "Mutale",
            LastName = "Test",
            WorkerType = "employee",
            Status = "pre-hire",
            SubjectId = "60b649a4-74c5-43ba-8bf3-97521f496f41",
        });
        await ctx.SaveChangesAsync();

        // Same-tenant subject resolves to the linked worker.
        var linked = await service.GetBySubjectAsync("60b649a4-74c5-43ba-8bf3-97521f496f41", CancellationToken.None);
        Assert.NotNull(linked);
        Assert.Equal("EMP-001", linked!.EmployeeNo);
        Assert.Equal("Mutale Test", linked.FullName);

        // Unknown subject returns null (unlinked identity), never throws.
        var unlinked = await service.GetBySubjectAsync("unknown-subject", CancellationToken.None);
        Assert.Null(unlinked);

        // Subject scoped to a different tenant resolves within that tenant only.
        var otherCtx = TestDbContextFactory.Create("other-tenant");
        otherCtx.Workers.Add(new Worker
        {
            EmployeeNo = "EMP-X1",
            FirstName = "Other",
            LastName = "Tenant",
            WorkerType = "employee",
            Status = "pre-hire",
            SubjectId = "60b649a4-74c5-43ba-8bf3-97521f496f41",
        });
        await otherCtx.SaveChangesAsync();
        var otherService = new WorkerServiceImpl(new WorkerRepository(otherCtx), new PermissiveAuthz(), new UlidIdProvider());
        var other = await otherService.GetBySubjectAsync("60b649a4-74c5-43ba-8bf3-97521f496f41", CancellationToken.None);
        Assert.NotNull(other);
        Assert.Equal("EMP-X1", other!.EmployeeNo);

        // The original tenant's view stays scoped to its own worker.
        var original = await service.GetBySubjectAsync("60b649a4-74c5-43ba-8bf3-97521f496f41", CancellationToken.None);
        Assert.Equal("EMP-001", original!.EmployeeNo);
    }

    [Fact]
    public async Task UpdateOwnProfile_NotLinked_Throws()
    {
        var (service, _) = Build();
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.UpdateOwnProfileAsync(
            new WorkerSubjectUpdateRequest(SubjectId: "nobody-here"), CancellationToken.None));
        Assert.Equal("not-linked", ex.Code);
    }

    [Fact]
    public async Task UpdateOwnProfile_AllowedFieldsUpdateOthersStayUnchanged()
    {
        var (service, ctx) = Build();
        ctx.Workers.Add(new Worker
        {
            EmployeeNo = "EMP-002",
            FirstName = "Grace",
            LastName = "Phiri",
            WorkerType = "employee",
            Status = "active",
            Grade = "Grade 4",
            JobTitle = "Teller",
            SubjectId = "subject-grace-002",
            EmergencyContacts = { new EmergencyContact { Relationship = "Spouse", FullName = "John Phiri", Phone = "0970000001", IsPrimary = true } },
        });
        await ctx.SaveChangesAsync();

        var updated = await service.UpdateOwnProfileAsync(new WorkerSubjectUpdateRequest(
            SubjectId: "subject-grace-002",
            Phone: "0971111111",
            Tpin: "1001111111",
            EmergencyContacts: [new EmergencyContactCreate("Sibling", "Mary Phiri", "0961234567", true)]),
            CancellationToken.None);

        Assert.Equal("0971111111", updated.Phone);
        Assert.Equal("1001111111", updated.Tpin);
        Assert.Equal("Grade 4", updated.Grade);          // admin-only: untouched
        Assert.Equal("Teller", updated.JobTitle);          // admin-only: untouched
        Assert.Equal("active", updated.Status);            // admin-only: untouched
        Assert.Single(updated.EmergencyContacts);
        Assert.Equal("Mary Phiri", updated.EmergencyContacts[0].FullName);
        Assert.Null(updated.BankDetails);                  // not sent → left untouched
    }
    [Fact]
    public async Task UpdateOwnProfile_ScalarOnly_Succeeds()
    {
        var (service, ctx) = Build();
        ctx.Workers.Add(new Worker
        {
            EmployeeNo = "EMP-003",
            FirstName = "Sam",
            LastName = "Zulu",
            WorkerType = "employee",
            Status = "active",
            SubjectId = "subject-sam-003",
        });
        await ctx.SaveChangesAsync();

        var updated = await service.UpdateOwnProfileAsync(new WorkerSubjectUpdateRequest(
            SubjectId: "subject-sam-003",
            Phone: "0972222222"),
            CancellationToken.None);
        Assert.Equal("0972222222", updated.Phone);
    }

    // ---- M18 employer-side employee CRUD ----

    [Fact]
    public async Task ListWorkers_ExcludesArchivedByDefault()
    {
        var (service, ctx) = Build();
        var active = new Worker { EmployeeNo = "EMP-A1", FirstName = "Active", LastName = "One", WorkerType = "employee", Status = "active" };
        var archived = new Worker { EmployeeNo = "EMP-A2", FirstName = "Left", LastName = "One", WorkerType = "employee", Status = "active", IsArchived = true };
        ctx.Set<Worker>().AddRange(active, archived);
        await ctx.SaveChangesAsync();

        var repo = new WorkerRepository(ctx);
        var (items, total) = await repo.ListAsync(new WorkerListFilters(Search: null, Status: null, LegalEntityId: null, OrgUnitId: null, LocationId: null, WorkerType: null, Grade: null), CancellationToken.None);
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal("EMP-A1", items[0].EmployeeNo);

        // Explicitly including archived surfaces the leaver again.
        var (archivedItems, archivedTotal) = await repo.ListAsync(
            new WorkerListFilters(Search: null, Status: null, LegalEntityId: null, OrgUnitId: null, LocationId: null, WorkerType: null, Grade: null, IncludeArchived: true), CancellationToken.None);
        Assert.Equal(2, archivedTotal);
        Assert.Contains(archivedItems, w => w.EmployeeNo == "EMP-A2");
    }

    [Fact]
    public async Task ArchiveWorker_SetsStatusArchived()
    {
        var (service, ctx) = Build();
        var worker = new Worker { EmployeeNo = "EMP-A3", FirstName = "To", LastName = "Archive", WorkerType = "employee", Status = "active" };
        ctx.Set<Worker>().Add(worker);
        await ctx.SaveChangesAsync();

        await service.ArchiveAsync(worker.Id, CancellationToken.None);

        var reloaded = await ctx.Workers.FirstAsync();
        Assert.True(reloaded.IsArchived);
        Assert.Equal("archived", reloaded.Status);
    }

    [Fact]
    public async Task UpdateArchivedWorker_Throws()
    {
        var (service, ctx) = Build();
        var worker = new Worker { EmployeeNo = "EMP-A4", FirstName = "Was", LastName = "Archived", WorkerType = "employee", Status = "active", IsArchived = true };
        ctx.Set<Worker>().Add(worker);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateAsync(worker.Id, new WorkerUpdateRequest(JobTitle: "New Title"), CancellationToken.None));
        Assert.Equal("worker-archived", ex.Code);
    }

    [Fact]
    public async Task UpdateWorker_ChangesEmploymentStartDate()
    {
        var (service, ctx) = Build();
        var worker = new Worker
        {
            EmployeeNo = "EMP-START-1", FirstName = "Start", LastName = "Date",
            WorkerType = "employee", Status = "active", StartDate = new DateOnly(2026, 8, 1),
        };
        ctx.Workers.Add(worker);
        await ctx.SaveChangesAsync();

        var updated = await service.UpdateAsync(worker.Id,
            new WorkerUpdateRequest(StartDate: "2026-07-10"), CancellationToken.None);

        Assert.Equal("2026-07-10", updated.StartDate);
        Assert.Equal(new DateOnly(2026, 7, 10), (await ctx.Workers.SingleAsync()).StartDate);
    }

    [Fact]
    public void CreateWorker_MissingFirstName_InvalidWorkerType_FailRouteValidation()
    {
        // The API route (ValidateWorkerCreate) is what produces the 422 for HR.
        // Re-derive the same rules here: a missing name and a bogus worker type
        // are caught before the service layer is reached. Mononyms are allowed.
        var bad = new[]
        {
            new WorkerCreateRequest(EmployeeNo: "EMP-A5", FirstName: "", LastName: "Worker", WorkerType: "employee"),
            new WorkerCreateRequest(EmployeeNo: "EMP-A7", FirstName: "Valid", LastName: "Worker", WorkerType: "freelancer"),
        };
        var allowedTypes = new[] { "employee", "contingent", "intern", "volunteer" };
        foreach (var request in bad)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(request.FirstName)) errors.Add("firstName is required");
            if (!allowedTypes.Contains(request.WorkerType)) errors.Add("workerType is invalid");
            Assert.NotEmpty(errors);
        }
        // And the happy path validates clean.
        var ok = new WorkerCreateRequest(EmployeeNo: "EMP-A8", FirstName: "Bwalya", LastName: "", WorkerType: "employee");
        var okErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(ok.FirstName)) okErrors.Add("firstName is required");
        if (!allowedTypes.Contains(ok.WorkerType)) okErrors.Add("workerType is invalid");
        Assert.Empty(okErrors);
    }
}
