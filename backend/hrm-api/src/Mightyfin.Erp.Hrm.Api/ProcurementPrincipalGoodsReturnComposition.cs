using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mightyfin.Erp.Hrm.Api;

// Intentionally not called by the live composition yet. Wire this seam and route
// mapping only after actual capture/finite-role proof and independent review.
internal static class ProcurementPrincipalGoodsReturnComposition
{
    internal static void AddFoundation(IServiceCollection services,IConfiguration configuration)
    {
        var options=configuration.GetSection("Procurement:PrincipalGoodsReturnDrafts")
            .Get<ProcurementPrincipalGoodsReturnOptions>() ?? new();
        // A caller cannot assert connection readiness via business configuration.
        options.ExecutionDatabaseConfigured=!string.IsNullOrWhiteSpace(
            configuration.GetConnectionString("ProcurementApprovalExecution"));
        services.AddSingleton(options);
        services.TryAddScoped<IProcurementPrincipalGoodsReturnReadiness,
            ProcurementPrincipalGoodsReturnStoreReadiness>();
    }
}
