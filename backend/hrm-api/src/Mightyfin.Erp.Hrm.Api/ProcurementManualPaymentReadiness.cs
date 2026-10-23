namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Owning P28/Core store readiness; separate from PR budget/Finance gates.
/// No implementation is registered until the owning migration and finite-role gates pass.</summary>
public interface IProcurementManualPaymentGovernanceReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
}

/// <summary>Explicit host opt-in. Schema availability never enables a payment command.</summary>
public sealed class ProcurementManualPaymentOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
}
