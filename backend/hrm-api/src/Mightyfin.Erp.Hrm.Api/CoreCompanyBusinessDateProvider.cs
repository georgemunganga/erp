using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mightyfin.Erp.Hrm.Application.ConfigAndExtras;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Core organisation services composed regardless of optional ERP modules.</summary>
public static class CoreCompanyBusinessDateServices
{
    public static IServiceCollection AddCoreCompanyBusinessDate(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<ICoreCompanyBusinessDateProvider, CoreCompanyBusinessDateProvider>();
        return services;
    }
}

/// <summary>A trusted Core date observation for a tenant and legal entity selected by the host.</summary>
public sealed record CoreCompanyBusinessDate(DateOnly Date, string TimeZoneId, DateTimeOffset ObservedAtUtc);

public interface ICoreCompanyBusinessDateProvider
{
    Task<CoreCompanyBusinessDate> GetAsync(string tenantId, Guid legalEntityId, CancellationToken ct);
}

/// <summary>Reads the current persisted company setting on every call; no browser or payroll clock is involved.</summary>
public sealed class CoreCompanyBusinessDateProvider(HrmDbContext db, TimeProvider clock) : ICoreCompanyBusinessDateProvider
{
    public async Task<CoreCompanyBusinessDate> GetAsync(string tenantId, Guid legalEntityId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || legalEntityId == Guid.Empty)
            throw new InvalidOperationException("A selected tenant and company are required for business date.");
        var timeZoneId = await db.LegalEntities.AsNoTracking()
            .Where(entity => entity.TenantId == tenantId && entity.Id == legalEntityId && !entity.IsArchived)
            .Select(entity => entity.TimeZoneId)
            .SingleOrDefaultAsync(ct);
        if (timeZoneId is null)
            throw new InvalidOperationException("The selected Core company is unavailable for business date.");
        var observedAt = clock.GetUtcNow();
        return new CoreCompanyBusinessDate(CompanyTimeZone.BusinessDate(observedAt, timeZoneId),
            timeZoneId, observedAt);
    }
}
