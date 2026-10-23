using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Owning-store successor readiness, separate from historic worker evidence.
/// The actual provider requires genuine generated migration and PostgreSQL manifests.</summary>
public interface IProcurementPrincipalApprovalSchemaReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner,CancellationToken ct);
    // Procurement-only configuration/funding writes; never requires Core identity DML.
    Task<bool> CanRecordAsync(ProcurementDbContext owner,CancellationToken ct);
    // Coordinated approval execution additionally needs exact finite Core workflow privileges.
    Task<bool> CanExecuteAsync(ProcurementDbContext owner,CancellationToken ct);
}

// Default-off provider; enabling the successor selects a separate scoped
// checker whose actual migration, schema and finite-role tests still decide.
// No setting, caller body, model presence or synthetic status alone establishes readiness.
public sealed class UnavailableProcurementPrincipalApprovalSchemaReadiness : IProcurementPrincipalApprovalSchemaReadiness
{
    public Task<bool> IsReadyAsync(ProcurementDbContext owner,CancellationToken ct)=>Task.FromResult(false);
    public Task<bool> CanRecordAsync(ProcurementDbContext owner,CancellationToken ct)=>Task.FromResult(false);
    public Task<bool> CanExecuteAsync(ProcurementDbContext owner,CancellationToken ct)=>Task.FromResult(false);
}

internal static class ProcurementPrincipalApprovalSchemaGuard
{
    internal static async Task<bool> ExecutionReadyAsync(ProcurementDbContext owner,IServiceProvider services,CancellationToken ct)
    {
        var provider=services.GetService<IProcurementPrincipalApprovalSchemaReadiness>();
        return provider is not null && await provider.CanExecuteAsync(owner,ct);
    }

    internal static async Task<bool> ReadyAsync(ProcurementDbContext owner,IServiceProvider services,bool write,CancellationToken ct)
    {
        var provider=services.GetService<IProcurementPrincipalApprovalSchemaReadiness>();
        return provider is not null && (write ? await provider.CanRecordAsync(owner,ct) : await provider.IsReadyAsync(owner,ct));
    }
}

/// <summary>The physical P25 schema is a strict, finite-role-checked superset
/// of the retained worker evidence. This does not opt a request into principal1,
/// nor does it replace the old exact P21 check on a pre-P25 database.</summary>
internal static class ProcurementLegacyP25SchemaGuard
{
    internal static async Task<bool> ReadyAsync(ProcurementDbContext owner,IServiceProvider services,
        bool write,CancellationToken ct)
    {
        var actual=services.GetService<ProcurementPrincipalApprovalStoreReadiness>();
        return actual is not null && (write ? await actual.CanRecordAsync(owner,ct)
            : await actual.IsReadyAsync(owner,ct));
    }
}

/// <summary>Retained worker authority on P21 or the exact physical P25
/// superset. The principal interface/feature flag is never consulted here;
/// worker account, grant and SOD checks remain with the caller.</summary>
internal static class ProcurementWorkerAuthoritySchemaGuard
{
    internal static async Task<bool> ReadyAsync(ProcurementDbContext owner,
        ProcurementRequestScope scope,Mightyfin.Erp.Hrm.Infrastructure.Data.HrmDbContext core,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(await new ProcurementSpendingAuthorityIdentitySchemaReadiness().IsReadyAsync(owner,ct))return true;
        return await new ProcurementPrincipalApprovalStoreReadiness(scope,core).IsReadyAsync(owner,ct);
    }
}
