namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Future governed approved-demand RFQ drafts; not legacy unlinked draft CRUD.
/// Technical availability never grants conversion, cancellation or publication authority.</summary>
public sealed class ProcurementSourcingDraftExecutionOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}

public interface IProcurementSourcingDraftSchemaReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
}

/// <summary>No governed RFQ mappings, constraints or runtime adapter exist yet.
/// A configured flag/connection cannot substitute for that missing owner contract.</summary>
public sealed class UnavailableProcurementSourcingDraftSchemaReadiness : IProcurementSourcingDraftSchemaReadiness
{
    public Task<bool> IsReadyAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}
