namespace Mightyfin.Erp.Procurement.Infrastructure.Data;
/// <summary>Ownership companion for the P32 owning migration. No runtime execution/readiness/provider.</summary>
public static class RfqPreparationOwnershipConstraintSql
{
    public const string Install="""
        ALTER TABLE procurement.rfq_prepared_plans ADD CONSTRAINT fk_pr32_exact_prepared_revision
          FOREIGN KEY (tenant_id,legal_entity_id,draft_revision_id,draft_id,sourcing_event_id,source_evidence_id,branch_id,actor_principal_id,draft_snapshot_sha256)
          REFERENCES procurement.rfq_commercial_revisions (tenant_id,legal_entity_id,id,draft_id,sourcing_event_id,source_evidence_id,branch_id,actor_principal_id,snapshot_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.rfq_prepared_plans ADD CONSTRAINT fk_pr32_exact_published_identity
          FOREIGN KEY (tenant_id,legal_entity_id,published_policy_identity_id,policy_version_id,published_stage,policy_rules_sha256,published_identity_sha256)
          REFERENCES procurement.sourcing_policy_identity_evidence (tenant_id,legal_entity_id,id,policy_version_id,stage,rules_sha256,evidence_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.rfq_preparation_receipts ADD CONSTRAINT fk_pr32_exact_preparation_plan
          FOREIGN KEY (tenant_id,legal_entity_id,prepared_plan_id,draft_revision_id,sourcing_event_id,branch_id,actor_principal_id,snapshot_sha256)
          REFERENCES procurement.rfq_prepared_plans (tenant_id,legal_entity_id,id,draft_revision_id,sourcing_event_id,branch_id,actor_principal_id,snapshot_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        ALTER TABLE procurement.rfq_preparation_receipts ADD CONSTRAINT fk_pr32_exact_preparation_command
          FOREIGN KEY (tenant_id,legal_entity_id,draft_command_receipt_id,draft_revision_id,sourcing_event_id,branch_id,actor_principal_id,command_key,request_sha256)
          REFERENCES procurement.rfq_commercial_receipts (tenant_id,legal_entity_id,id,draft_revision_id,sourcing_event_id,branch_id,actor_principal_id,command_key,request_sha256)
          ON DELETE NO ACTION DEFERRABLE INITIALLY DEFERRED;
        """;
    // Remove authored cross-table constraints before generated table/index drops.
    public const string Remove = """
        ALTER TABLE procurement.rfq_preparation_receipts DROP CONSTRAINT fk_pr32_exact_preparation_command;
        ALTER TABLE procurement.rfq_preparation_receipts DROP CONSTRAINT fk_pr32_exact_preparation_plan;
        ALTER TABLE procurement.rfq_prepared_plans DROP CONSTRAINT fk_pr32_exact_published_identity;
        ALTER TABLE procurement.rfq_prepared_plans DROP CONSTRAINT fk_pr32_exact_prepared_revision;
        """;
}
