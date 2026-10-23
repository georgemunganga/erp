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

// Deliberately unavailable until the later owned migration and finite-role acceptance.
// No setting, caller body, model presence or synthetic status can establish readiness.
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
