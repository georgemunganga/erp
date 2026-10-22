using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Lossless typed storage conversion only; integrity verification does not authenticate
/// arbitrary caller evidence. The owning context must verify canonical actor and same-save action.</summary>
public static class SpendingAuthorityIdentityStorage
{
    public static SpendingAuthorityIdentityRecord ToRecord(SpendingAuthorityIdentityEvidence value)
    {
        SpendingAuthorityIdentity.Verify(value);
        return new()
        {
            Id = value.Id, TenantId = value.TenantId, LegalEntityId = value.LegalEntityId,
            SchemaVersion = value.SchemaVersion, GrantId = value.GrantId,
            AuthorityReference = value.AuthorityReference, AuthorityVersion = value.AuthorityVersion,
            Permission = value.Permission, Action = value.Action,
            ConfigurationOwnerPrincipalId = value.ConfigurationOwnerPrincipalId,
            RecipientPrincipalId = value.RecipientPrincipalId, RecipientWorkerId = value.RecipientWorkerId,
            Stage = value.Stage, ActorPrincipalId = value.ActorPrincipalId, ActorWorkerId = value.ActorWorkerId,
            GrantRecordVersionAtAction = value.GrantRecordVersionAtAction,
            BusinessFingerprint = value.BusinessFingerprint, ActionAt = value.ActionAt,
            CreatedEvidenceId = value.CreatedEvidenceId,
            CreatedEvidenceStage = value.CreatedEvidenceId.HasValue ? "Created" : null,
            SubmittedEvidenceId = value.SubmittedEvidenceId,
            SubmittedEvidenceStage = value.SubmittedEvidenceId.HasValue ? "Submitted" : null,
            EvidenceSha256 = value.EvidenceSha256, CreatedAt = value.ActionAt,
            CreatedBy = value.ActorPrincipalId, Version = 1,
        };
    }

    public static SpendingAuthorityIdentityEvidence Read(SpendingAuthorityIdentityRecord row)
    {
        if (row.Version != 1 || row.UpdatedAt is not null || row.UpdatedBy is not null
            || row.CreatedAt != row.ActionAt || row.CreatedBy != row.ActorPrincipalId
            || row.CreatedEvidenceStage != (row.CreatedEvidenceId.HasValue ? "Created" : null)
            || row.SubmittedEvidenceStage != (row.SubmittedEvidenceId.HasValue ? "Submitted" : null))
            throw new InvalidOperationException("AuthorityIdentityStorageInvalid");
        var value = new SpendingAuthorityIdentityEvidence
        {
            Id = row.Id, TenantId = row.TenantId, LegalEntityId = row.LegalEntityId,
            SchemaVersion = row.SchemaVersion, GrantId = row.GrantId,
            AuthorityReference = row.AuthorityReference, AuthorityVersion = row.AuthorityVersion,
            Permission = row.Permission, Action = row.Action,
            ConfigurationOwnerPrincipalId = row.ConfigurationOwnerPrincipalId,
            RecipientPrincipalId = row.RecipientPrincipalId, RecipientWorkerId = row.RecipientWorkerId,
            Stage = row.Stage, ActorPrincipalId = row.ActorPrincipalId, ActorWorkerId = row.ActorWorkerId,
            GrantRecordVersionAtAction = row.GrantRecordVersionAtAction,
            BusinessFingerprint = row.BusinessFingerprint, ActionAt = row.ActionAt,
            CreatedEvidenceId = row.CreatedEvidenceId, SubmittedEvidenceId = row.SubmittedEvidenceId,
            EvidenceSha256 = row.EvidenceSha256,
        };
        SpendingAuthorityIdentity.Verify(value);
        return value;
    }
}
