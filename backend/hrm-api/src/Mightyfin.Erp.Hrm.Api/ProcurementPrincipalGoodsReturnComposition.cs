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
                { ExecutionDatabaseConfigured:true }
            || services.GetService<IProcurementPrincipalGoodsReturnReadiness>() is not
                ProcurementPrincipalGoodsReturnStoreReadiness concrete) return false;
        return await concrete.IsConfiguredAsync(ct);
    }
    internal static Task<bool> ReadReadyAsync(IServiceProvider services,bool pending,CancellationToken ct)
        =>ReadyAsync(services,pending,ct);
    internal static async Task<bool> WriteReadyAsync(IServiceProvider services,bool pending,CancellationToken ct)
        =>services.GetService<ProcurementPrincipalGoodsReturnOptions>() is {Enabled:true}
            && await ReadyAsync(services,pending,ct);
}
