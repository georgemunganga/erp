using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mightyfin.Erp.Hrm.Api;

// Default-off composition; an explicit option alone cannot assert physical
// readiness or grant a current principal any return action.
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

    internal static async Task<bool> ReadyAsync(IServiceProvider services,bool pending,CancellationToken ct)
    {
        if(pending || services.GetService<ProcurementPrincipalGoodsReturnOptions>() is not
                { Enabled:true, ExecutionDatabaseConfigured:true }
            || services.GetService<ProcurementApprovalExecutionOptions>() is not
                { Enabled:true, PrincipalExecutionEnabled:true, ExecutionDatabaseConfigured:true }
            || services.GetService<IProcurementPrincipalGoodsReturnReadiness>() is not
                ProcurementPrincipalGoodsReturnStoreReadiness concrete) return false;
        return await concrete.IsConfiguredAsync(ct);
    }
}
