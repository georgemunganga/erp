using System.Threading;
using System.Threading.Tasks;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Setup;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>M49: first-time setup wizard — step gating, finish rules, the
/// safe refusal of web reset, and the legacy-tenant no-gate behaviour,
/// over SQLite in-memory with a fixed tenant (same harness as the rest of the
/// test suite; the InMemory provider misbehaves with Guid-V7 keys).</summary>
public class SetupWizardTests
{
    private static (SetupServiceImpl Service, HrmDbContext Ctx) Build(string tenant = "m49-test")
    {
        var ctx = TestDbContextFactory.Create(tenant);
        var repo = new SetupRepository(ctx);
        return (new SetupServiceImpl(repo), ctx);
    }

    [Fact]
    public async Task PendingStateShowsResumeStep()
    {
        var (svc, _) = Build();
        // organisation + structure done → resume points at the next open step.
        await svc.CompleteStepAsync("organisation", null, CancellationToken.None);
        await svc.CompleteStepAsync("structure", null, CancellationToken.None);
        var state = await svc.GetStateAsync(CancellationToken.None);
        Assert.Equal("pending", state.Status);
        Assert.Equal("employment", state.ResumeStepKey);
        Assert.Contains("organisation", state.CompletedSteps);
        Assert.Contains("structure", state.CompletedSteps);
        Assert.True(state.CompletionPercent > 0 && state.CompletionPercent < 100);
    }

    [Fact]
    public async Task MandatoryStepCannotCompleteBeforeItsPrefix()
    {
        var (svc, _) = Build();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CompleteStepAsync("payroll", null, CancellationToken.None));
        // Gating is the mandatory prefix only — optional steps may complete
        // at any time (e.g. working-time before leave or payroll).
        await svc.CompleteStepAsync("working-time", null, CancellationToken.None);
    }

    [Fact]
    public async Task FinishRequiresTheMandatoryPrefix()
    {
        var (svc, _) = Build();
        await svc.CompleteStepAsync("organisation", null, CancellationToken.None);
        await svc.CompleteStepAsync("structure", null, CancellationToken.None);
        await svc.CompleteStepAsync("employment", null, CancellationToken.None);
        await svc.CompleteStepAsync("working-time", null, CancellationToken.None);
        await svc.CompleteStepAsync("leave", null, CancellationToken.None);
        await svc.CompleteStepAsync("payroll", null, CancellationToken.None);
        // roles + employees still open → finish must refuse.
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.FinishAsync(CancellationToken.None));
        await svc.CompleteStepAsync("roles", null, CancellationToken.None);
        await svc.CompleteStepAsync("employees", null, CancellationToken.None);
        await svc.FinishAsync(CancellationToken.None);
        var state = await svc.GetStateAsync(CancellationToken.None);
        Assert.Equal("complete", state.Status);
        Assert.Null(state.ResumeStepKey);
    }

    [Fact]
    public async Task CompletedStepCanBeSavedAgainAfterSetupFinishes()
    {
        var (svc, _) = Build();
        foreach (var step in new[] { "organisation", "structure", "employment", "leave", "payroll", "roles", "employees" })
            await svc.CompleteStepAsync(step, null, CancellationToken.None);
        await svc.FinishAsync(CancellationToken.None);

        await svc.CompleteStepAsync("payroll", "{\"paydayDay\":28}", CancellationToken.None);
        Assert.Equal("{\"paydayDay\":28}", await svc.GetStepDataAsync("payroll", CancellationToken.None));
    }

    [Fact]
    public async Task FinishRefusesSecondCallAndResetPreservesCompletedDataAndAudit()
    {
        var (svc, ctx) = Build();
        await svc.CompleteStepAsync("organisation", null, CancellationToken.None);
        await svc.CompleteStepAsync("structure", null, CancellationToken.None);
        await svc.CompleteStepAsync("employment", null, CancellationToken.None);
        await svc.CompleteStepAsync("working-time", null, CancellationToken.None);
        await svc.CompleteStepAsync("leave", null, CancellationToken.None);
        await svc.CompleteStepAsync("payroll", null, CancellationToken.None);
        await svc.CompleteStepAsync("roles", null, CancellationToken.None);
        await svc.CompleteStepAsync("employees", null, CancellationToken.None);
        await svc.FinishAsync(CancellationToken.None);
        // Second finish on an already-complete org must throw, not silently
        // re-seed a pending state.
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.FinishAsync(CancellationToken.None));
        var audit = new AuditEntry
        {
            TenantId = "m49-test", EntityType = "SetupState", EntityId = "finished",
            Action = "complete", ActorSubjectId = "setup-admin"
        };
        ctx.AuditEntries.Add(audit);
        await ctx.SaveChangesAsync();
        var before = await ctx.SetupStepRecords.AsNoTracking().CountAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => svc.ResetAsync(CancellationToken.None));
        Assert.Equal("setup-maintenance-required", error.Code);
        var after = await svc.GetStateAsync(CancellationToken.None);
        Assert.Equal("complete", after.Status);
        Assert.Equal(before, await ctx.SetupStepRecords.AsNoTracking().CountAsync());
        Assert.Equal(before, after.CompletedSteps.Count);
        Assert.NotNull(await ctx.AuditEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == audit.Id));
    }

    [Fact]
    public async Task ResetRefusesBeforeAnyRepositoryReadWipeOrSeed()
    {
        var repo = new ResetCallTrap();
        var service = new SetupServiceImpl(repo);
        var error = await Assert.ThrowsAsync<DomainException>(() => service.ResetAsync(CancellationToken.None));
        Assert.Equal("setup-maintenance-required", error.Code);
        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task ResetCannotEraseAnotherTenantInTheSameDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=setup-reset-isolation-" + Guid.NewGuid() + ";Mode=Memory;Cache=Shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<HrmDbContext>().UseSqlite(connection).Options;
        await using var first = new HrmDbContext(options, new FixedTenantAccessor("tenant-a"));
        await first.Database.EnsureCreatedAsync();
        await using var other = new HrmDbContext(options, new FixedTenantAccessor("tenant-b"));
        var retained = new SetupState { Status = "complete", TenantId = "tenant-b" };
        other.SetupStates.Add(retained);
        await other.SaveChangesAsync();

        var service = new SetupServiceImpl(new SetupRepository(first));
        var error = await Assert.ThrowsAsync<DomainException>(() => service.ResetAsync(CancellationToken.None));
        Assert.Equal("setup-maintenance-required", error.Code);
        Assert.Equal("complete", (await other.SetupStates.AsNoTracking().SingleAsync(x => x.Id == retained.Id)).Status);
    }

    private sealed class ResetCallTrap : ISetupRepository
    {
        public int Calls { get; private set; }
        private T Unexpected<T>() { Calls++; throw new InvalidOperationException("Reset reached the repository."); }
        public Task<SetupState?> GetStateAsync(CancellationToken ct) => Unexpected<Task<SetupState?>>();
        public Task<IReadOnlySet<string>> CompletedStepKeysAsync(CancellationToken ct) => Unexpected<Task<IReadOnlySet<string>>>();
        public Task<string?> DataForStepAsync(string stepKey, CancellationToken ct) => Unexpected<Task<string?>>();
        public Task CompleteStepAsync(string stepKey, string? dataJson, CancellationToken ct) => Unexpected<Task>();
        public Task FinishAsync(SetupState state, CancellationToken ct) => Unexpected<Task>();
        public Task WipeAllDataAsync(CancellationToken ct) => Unexpected<Task>();
        public Task<SetupState> SeedPendingStateAsync(CancellationToken ct) => Unexpected<Task<SetupState>>();
    }

    [Fact]
    public async Task LegacyTenantWithoutSetupRowIsNeverGated()
    {
        // A tenant provisioned before M49 has no setup row; the state endpoint
        // must report complete so the welcome overlay never blocks it.
        var (svc, _) = Build();
        var state = await svc.GetStateAsync(CancellationToken.None);
        Assert.Equal("complete", state.Status);
        Assert.Null(state.ResumeStepKey);
        Assert.Equal(100, state.CompletionPercent);
    }

    [Fact]
    public async Task StepCompletionPersistsAcrossCalls()
    {
        var (svc, _) = Build();
        await svc.CompleteStepAsync("organisation", "{\"name\":\"TestCo\"}", CancellationToken.None);
        var first = await svc.GetStateAsync(CancellationToken.None);
        Assert.Contains("organisation", first.CompletedSteps);
        // A fresh resolution against the same store sees the same state.
        var second = await svc.GetStateAsync(CancellationToken.None);
        Assert.Contains("organisation", second.CompletedSteps);
    }
}
