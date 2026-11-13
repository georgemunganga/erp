using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Setup;
using Mightyfin.Erp.Hrm.Application.Payroll;
using Mightyfin.Erp.Hrm.Domain.Entities;
using Mightyfin.Erp.Hrm.Infrastructure;

namespace Mightyfin.Erp.Hrm.Tests;

public class ProductHrmSetupTests
{
    private sealed class ProductRepository(ISetupRepository inner) : ISetupRepository
    {
        public Task<bool> IsProductWorkspaceAsync(CancellationToken ct) => Task.FromResult(true);
        public Task<SetupState?> GetStateAsync(CancellationToken ct) => inner.GetStateAsync(ct);
        public Task<IReadOnlySet<string>> CompletedStepKeysAsync(CancellationToken ct) => inner.CompletedStepKeysAsync(ct);
        public Task<string?> DataForStepAsync(string key, CancellationToken ct) => inner.DataForStepAsync(key, ct);
        public Task CompleteStepAsync(string key, string? json, CancellationToken ct) => inner.CompleteStepAsync(key, json, ct);
        public Task FinishAsync(SetupState state, CancellationToken ct) => inner.FinishAsync(state, ct);
        public Task WipeAllDataAsync(CancellationToken ct) => throw new InvalidOperationException();
        public Task<SetupState> SeedPendingStateAsync(CancellationToken ct) => inner.SeedPendingStateAsync(ct);
    }

    [Fact]
    public async Task Core_provisioning_does_not_claim_HR_configuration_is_complete_or_write_on_read()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new SetupServiceImpl(new ProductRepository(new SetupRepository(db)));
        var state = await svc.GetStateAsync(default);
        Assert.Equal("pending", state.Status); Assert.Equal("employment", state.ResumeStepKey);
        Assert.DoesNotContain("payroll", state.CompletedSteps);
        Assert.Empty(await db.SetupStates.ToListAsync());
        var steps = await svc.ListStepsAsync(default);
        Assert.DoesNotContain(steps, x => x.Key is "organisation" or "roles");
        Assert.False(steps.Single(x => x.Key == "employees").Mandatory);
        await Assert.ThrowsAsync<DomainException>(() => svc.FinishAsync(default));
    }

    [Fact]
    public async Task Product_configuration_finishes_without_creating_employees_or_repeating_Core_setup()
    {
        await using var db = TestDbContextFactory.Create();
        var svc = new SetupServiceImpl(new ProductRepository(new SetupRepository(db)));
        await Assert.ThrowsAsync<DomainException>(() => svc.CompleteStepAsync("organisation", null, default));
        await Assert.ThrowsAsync<DomainException>(() => svc.CompleteStepAsync("payroll", null, default));
        await svc.CompleteStepAsync("employment", null, default);
        await svc.CompleteStepAsync("leave", null, default);
        await svc.CompleteStepAsync("payroll", null, default);
        await svc.FinishAsync(default);
        Assert.Equal("complete", (await svc.GetStateAsync(default)).Status);
        Assert.Empty(await db.Workers.ToListAsync());
        Assert.Empty(await db.LocalUsers.ToListAsync());
    }

    [Theory]
    [InlineData(2026, 1, 30)]
    [InlineData(2026, 2, 27)]
    [InlineData(2026, 11, 30)]
    [InlineData(2028, 2, 29)]
    public void Last_working_day_handles_short_months_weekends_and_leap_years(int year, int month, int expected)
        => Assert.Equal(new DateOnly(year, month, expected), PayrollPayday.Resolve(year, month, 31, "last-working-day"));

    [Fact]
    public void Last_working_day_skips_configured_holidays_and_calendar_day_clamps_within_month()
    {
        Assert.Equal(new DateOnly(2026, 11, 27), PayrollPayday.Resolve(2026, 11, 31, "last-working-day", "sat,sun", [new(2026, 11, 30)]));
        Assert.Equal(new DateOnly(2026, 2, 28), PayrollPayday.Resolve(2026, 2, 31, "calendar-day"));
        Assert.Throws<DomainException>(() => PayrollPayday.Resolve(2026, 11, 31, "last-working-day", "sun,mon,tue,wed,thu,fri,sat"));
        Assert.Throws<DomainException>(() => PayrollPayday.Validate("last-working-day", "weekly"));
    }
}
