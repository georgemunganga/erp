namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Plan review stays unavailable until the additive schema, policy and finite roles are accepted.</summary>
public sealed class ProcurementPlanReviewOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}
