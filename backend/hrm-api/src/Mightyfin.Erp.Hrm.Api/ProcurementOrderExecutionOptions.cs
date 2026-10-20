namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Explicit deployment controls. No request body can enable financial or supplier-facing execution.</summary>
public sealed class ProcurementOrderExecutionOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionDatabaseConfigured { get; set; }
    public ProcurementManualFinancePolicy[] ManualCommitmentPolicies { get; set; } = [];
    public string ApprovedManualDispatchPolicyReference { get; set; } = "";
    public string[] AllowedDeliveryChannels { get; set; } = [];
    public bool HasCurrentManualCommitmentPolicy(Guid company, DateTimeOffset now)
    {
        var policies = ManualCommitmentPolicies.Where(x => x.Enabled && x.LegalEntityId == company
            && x.EffectiveFrom != default && x.EffectiveFrom <= now && (x.EffectiveUntil == null || now < x.EffectiveUntil)
            && !string.IsNullOrWhiteSpace(x.ApprovedPolicyReference)
            && (x.EvidenceMaximumAgeMinutes is null or > 0)).ToArray();
        return policies.Length == 1;
    }
    public bool HasApprovedDeliveryPolicy => !string.IsNullOrWhiteSpace(ApprovedManualDispatchPolicyReference)
        && AllowedDeliveryChannels.Length > 0 && AllowedDeliveryChannels.All(x => !string.IsNullOrWhiteSpace(x));
}
