namespace Mightyfin.Erp.Hrm.Api;

using Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Plan review stays unavailable until the additive schema, policy and finite roles are accepted.</summary>
public sealed class ProcurementPlanReviewOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}

public interface IProcurementPlanReviewReadiness
{
    Task<bool> CanReadPlanAsync(ProcurementDbContext db, CancellationToken cancellationToken);
    Task<bool> CanReadReviewAsync(ProcurementDbContext db, CancellationToken cancellationToken);
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
