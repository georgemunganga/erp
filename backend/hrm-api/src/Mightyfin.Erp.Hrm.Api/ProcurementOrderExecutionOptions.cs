namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explicit deployment controls. No request body can enable financial or supplier-facing execution.</summary>
public sealed class ProcurementOrderExecutionOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
    public ProcurementManualFinancePolicy[] ManualCommitmentPolicies { get; set; } = [];
    public string ApprovedManualDispatchPolicyReference { get; set; } = "";
    public string[] AllowedDeliveryChannels { get; set; } = [];
}
