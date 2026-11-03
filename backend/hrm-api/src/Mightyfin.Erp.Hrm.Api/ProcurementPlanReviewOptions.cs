namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Plan review stays unavailable until the additive schema, policy and finite roles are accepted.</summary>
public sealed class ProcurementPlanReviewOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}

public interface IProcurementPlanReviewReadiness
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}

/// <summary>No nominal readiness before the genuine additive schema and finite roles are captured.</summary>
public sealed class UnavailableProcurementPlanReviewReadiness : IProcurementPlanReviewReadiness
{
    public Task<bool> IsReadyAsync(CancellationToken cancellationToken) => Task.FromResult(false);
}
