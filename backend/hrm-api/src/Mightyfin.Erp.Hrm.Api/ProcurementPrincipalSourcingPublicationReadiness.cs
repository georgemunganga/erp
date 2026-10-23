using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Separate default-off P30 gate. Root's genuine owning migration and captured
/// PostgreSQL constraints/privileges must replace this implementation before activation.</summary>
public sealed class ProcurementPrincipalSourcingPublicationOptions
{
    public bool Enabled { get; set; }
}

public interface IProcurementPrincipalSourcingPublicationReadiness
{
    Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct);
    Task<bool> IsReadyAsync(CancellationToken ct) => Task.FromResult(false);
    Task<bool> IsReadyAsync(ProcurementDbContext owner, bool write, CancellationToken ct) => IsReadyAsync(owner, ct);
}

public sealed class UnavailablePrincipalSourcingPublicationReadiness : IProcurementPrincipalSourcingPublicationReadiness
{
    public Task<bool> IsReadyAsync(ProcurementDbContext owner, CancellationToken ct) => Task.FromResult(false);
}
