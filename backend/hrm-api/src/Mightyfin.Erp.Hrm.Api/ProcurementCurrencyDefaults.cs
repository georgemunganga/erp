using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Use the selected Core company currency only when a new draft omits one.
/// Persisted transaction currency remains stable if Core settings change later.</summary>
public static class ProcurementCurrencyDefaults
{
    public static async Task<string?> ForSelectedCompanyAsync(HrmDbContext core,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        var currency = await core.LegalEntities.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && x.Id == scope.LegalEntityId && !x.IsArchived)
            .Select(x => x.Currency).SingleOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
    }
}
