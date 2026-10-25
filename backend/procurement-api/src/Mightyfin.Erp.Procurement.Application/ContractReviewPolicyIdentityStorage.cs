using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Reuses the shared Core-principal policy lineage rules; persistence must
/// independently prove same-save ownership and actor binding.</summary>
public static class ContractReviewPolicyIdentityStorage
{
    public static ContractReviewPolicyIdentityRecord ToRecord(OperationalPolicyWorkerIdentityEvidence value)
    {
        OperationalPolicyWorkerIdentity.Verify(value);
        if (value.PolicyKey != ProcurementContractReviewPolicyRules.PolicyKey || value.SchemaVersion != 2)
            throw new InvalidOperationException("ContractReviewPolicyIdentityInvalid");
        return new()
        {
            Id = value.Id, TenantId = value.TenantId, LegalEntityId = value.LegalEntityId,
            SchemaVersion = value.SchemaVersion, PolicyKey = value.PolicyKey,
            PolicyVersionId = value.PolicyVersionId, PolicyVersionNumber = value.PolicyVersionNumber,
            Stage = value.Stage, ActorPrincipalId = value.ActorPrincipalId, ActorWorkerId = value.ActorWorkerId,
            PolicyRecordVersionAtAction = value.PolicyRecordVersionAtAction, RulesSha256 = value.RulesSha256,
            ActionAt = value.ActionAt, CreatedEvidenceId = value.CreatedEvidenceId,
            CreatedEvidenceStage = value.CreatedEvidenceStage,
            SubmittedEvidenceId = value.SubmittedEvidenceId, SubmittedEvidenceStage = value.SubmittedEvidenceStage,
            EvidenceSha256 = value.EvidenceSha256, CreatedAt = value.ActionAt,
            CreatedBy = value.ActorPrincipalId, Version = 1,
        };
    }

    public static OperationalPolicyWorkerIdentityEvidence Read(ContractReviewPolicyIdentityRecord row)
    {
        if (row.PolicyKey != ProcurementContractReviewPolicyRules.PolicyKey || row.SchemaVersion != 2
            || row.Version != 1 || row.UpdatedAt is not null || row.UpdatedBy is not null
            || row.CreatedAt != row.ActionAt || row.CreatedBy != row.ActorPrincipalId)
            throw new InvalidOperationException("ContractReviewPolicyIdentityStorageInvalid");
        var value = new OperationalPolicyWorkerIdentityEvidence
        {
            Id = row.Id, TenantId = row.TenantId, LegalEntityId = row.LegalEntityId,
            SchemaVersion = row.SchemaVersion, PolicyKey = row.PolicyKey,
            PolicyVersionId = row.PolicyVersionId, PolicyVersionNumber = row.PolicyVersionNumber,
            Stage = row.Stage, ActorPrincipalId = row.ActorPrincipalId, ActorWorkerId = row.ActorWorkerId,
            PolicyRecordVersionAtAction = row.PolicyRecordVersionAtAction, RulesSha256 = row.RulesSha256,
            ActionAt = row.ActionAt, CreatedEvidenceId = row.CreatedEvidenceId,
            CreatedEvidenceStage = row.CreatedEvidenceStage,
            SubmittedEvidenceId = row.SubmittedEvidenceId,
            SubmittedEvidenceStage = row.SubmittedEvidenceStage,
            EvidenceSha256 = row.EvidenceSha256,
        };
        OperationalPolicyWorkerIdentity.Verify(value);
        return value;
    }
}
