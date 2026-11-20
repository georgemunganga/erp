using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Hrm.Infrastructure;

/// <summary>Posts configured monthly leave credits, including months missed during downtime.</summary>
public sealed class MonthlyLeaveAccrualWorker(IServiceScopeFactory scopes, ILogger<MonthlyLeaveAccrualWorker> logger, IConfiguration config)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDuePeriodsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Monthly leave accrual failed; it will retry.");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task RunDuePeriodsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmDbContext>();
        if (ProductRequestAuthentication.Enabled(config))
        {
            var options = new DbContextOptionsBuilder<HrmDbContext>().UseNpgsql(config.GetConnectionString("Hrm"),
                pg => pg.MigrationsHistoryTable("__hrm_migrations", "hrm")).Options;
            foreach (var tenantId in await RegisteredProductTenantsAsync(db, ct))
            {
                var tenant = new ScheduledTenant(tenantId);
                var audited = new DbContextOptionsBuilder<HrmDbContext>(options)
                    .AddInterceptors(new AuditInterceptor(new HttpContextAccessor(), tenant)).Options;
                await using var company = new HrmDbContext(audited, tenant);
                var service = new TimeServiceImpl(new TimeRepository(company),
                    scope.ServiceProvider.GetRequiredService<IAuthzService>(),
                    scope.ServiceProvider.GetRequiredService<IWorkflowService>(), new WorkerRepository(company),
                    unitOfWork: new EfUnitOfWork(company));
                try { await AccrueCompanyAsync(company, service, ct); }
                catch (Exception error) when (error is not OperationCanceledException)
                { logger.LogError(error, "A registered company leave accrual failed; other companies continue."); }
            }
            return;
        }
        await AccrueCompanyAsync(db, scope.ServiceProvider.GetRequiredService<ITimeService>(), ct);
    }

    // Background-only discovery of registered Core allocations. No HTTP caller
    // chooses a tenant, and each company's reads/writes use its own filtered DB.
    internal static Task<List<string>> RegisteredProductTenantsAsync(HrmDbContext db, CancellationToken ct) =>
        (from binding in db.WorkspaceBindings.IgnoreQueryFilters().AsNoTracking()
         join entity in db.LegalEntities.IgnoreQueryFilters().AsNoTracking() on binding.LegalEntityId equals entity.Id
         where !binding.IsArchived && !entity.IsArchived && binding.TenantId == entity.TenantId
         select binding.TenantId).Distinct().OrderBy(x => x).ToListAsync(ct);
    private sealed record ScheduledTenant(string Id) : ITenantAccessor { public string GetTenantId() => Id; }

    private async Task AccrueCompanyAsync(HrmDbContext db, ITimeService service, CancellationToken ct)
    {
        var starts = await db.LeaveTypes.AsNoTracking()
            .Where(t => t.IsActive && t.AutoAccrueMonthly && t.AccrualStartDate != null)
            .Select(t => t.AccrualStartDate).ToListAsync(ct);
        if (starts.Count == 0) return;

        var first = starts.Min()!.Value;
        var current = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        for (var period = first; period <= current; period = period.AddMonths(1))
        {
            try
            {
                var result = await service.RunScheduledLeaveAccrualAsync(
                    new LeaveAccrualRunRequest(period.ToString("yyyy-MM")), ct);
                logger.LogInformation("Accrued {Days} leave days for {Workers} workers in {Period}.",
                    result.TotalDaysAccrued, result.WorkerCount, result.Period);
            }
            catch (DomainException exception) when (exception.Code is "accrual-period-exists" or "accrual-no-types" or "accrual-no-workers")
            {
                // A completed period is deliberately immutable; the unique run key also guards races.
            }
            catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException
                { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
            {
                logger.LogInformation("Another instance completed leave accrual for {Period}.", period);
            }
        }
    }
}
