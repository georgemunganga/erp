using Mightyfin.Erp.Procurement.Infrastructure.Approvals;

namespace Mightyfin.Erp.Hrm.Api;

internal static class ProcurementPrincipalApprovalCapability
{
    internal static async Task<bool> ReadyAsync(IServiceProvider services,Guid company,CancellationToken ct)
    {
        var options=services.GetService<ProcurementApprovalExecutionOptions>();
        var schema=services.GetService<IProcurementPrincipalApprovalSchemaReadiness>();
        if(options is not {Enabled:true,PrincipalExecutionEnabled:true,ExecutionDatabaseConfigured:true} || schema is null
            || !options.HasCurrentManualEvidencePolicy(company,DateTimeOffset.UtcNow))return false;
        var coordinator=services.GetService<ProcurementApprovalTransaction>();
        if(coordinator is null)return false;
        // Configured availability belongs to the real finite execution connection,
        // not the normal read context or a role/database supplied by a caller.
        return await coordinator.ExecuteAsync(async(owner,_,token)=>await schema.CanExecuteAsync(owner,token),ct);
    }
}
