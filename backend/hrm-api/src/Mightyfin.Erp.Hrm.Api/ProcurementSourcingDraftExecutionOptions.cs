namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Future governed approved-demand RFQ drafts; not legacy unlinked draft CRUD.
/// Technical availability never grants conversion, cancellation or publication authority.</summary>
public sealed class ProcurementSourcingDraftExecutionOptions
{
    public bool Enabled { get; set; }
    // Separate P29 authority. Defaults off until generated migration and finite-role acceptance.
    public bool PrincipalSourceEnabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}

public interface IProcurementSourcingDraftSchemaReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
    // Existing metadata-only test doubles cannot authorize an owning-store command.
    Task<bool> IsReadyAsync(Mightyfin.Erp.Procurement.Infrastructure.Data.ProcurementDbContext owner, bool write, CancellationToken ct)
        => Task.FromResult(false);
}

/// <summary>Explicit unavailable provider retained for negative/test contracts.
/// A configured flag/connection cannot substitute for that missing owner contract.</summary>
public sealed class UnavailableProcurementSourcingDraftSchemaReadiness : IProcurementSourcingDraftSchemaReadiness
{
    public Task<bool> IsReadyAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
