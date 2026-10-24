namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Proposal for the future CENTRAL generated migration; no SQL executes
/// from this class and no readiness/provider is registered. Exact same-parent
/// bindings use ordinary unique targets, never mutable EF alternate keys.</summary>
public static class RfqCommercialDraftOwnershipConstraintSql
{
    public const string Install="""
        ALTER TABLE procurement.rfq_commercial_drafts ADD CONSTRAINT fk_pr32_exact_source
          FOREIGN KEY (tenant_id,legal_entity_id,source_evidence_id,sourcing_event_id,branch_id,original_fingerprint,source_envelope_sha256,source_identity_schema_version)
          REFERENCES procurement.sourcing_draft_evidence (tenant_id,legal_entity_id,id,sourcing_event_id,branch_ref,original_draft_fingerprint,payload_sha256,schema_version)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.rfq_commercial_revisions ADD CONSTRAINT fk_pr32_exact_draft
          FOREIGN KEY (tenant_id,legal_entity_id,draft_id,sourcing_event_id,source_evidence_id,branch_id,actor_principal_id)
          REFERENCES procurement.rfq_commercial_drafts (tenant_id,legal_entity_id,id,sourcing_event_id,source_evidence_id,branch_id,owner_principal_id)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.rfq_commercial_receipts ADD CONSTRAINT fk_pr32_exact_revision
          FOREIGN KEY (tenant_id,legal_entity_id,draft_revision_id,draft_id,sourcing_event_id,source_evidence_id,branch_id,command_key,actor_principal_id,snapshot_sha256)
          REFERENCES procurement.rfq_commercial_revisions (tenant_id,legal_entity_id,id,draft_id,sourcing_event_id,source_evidence_id,branch_id,command_key,actor_principal_id,snapshot_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.rfq_commercial_drafts ADD CONSTRAINT fk_pr32_exact_current_revision
          FOREIGN KEY (tenant_id,legal_entity_id,id,revision,snapshot_sha256)
          REFERENCES procurement.rfq_commercial_revisions (tenant_id,legal_entity_id,draft_id,revision,snapshot_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        """;
}
