using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
namespace Mightyfin.Erp.Procurement.Infrastructure.Data;
public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqPreparedPlan> PrincipalRfqPreparedPlans=>Set<PrincipalRfqPreparedPlan>();
    public DbSet<PrincipalRfqPreparationReceipt> PrincipalRfqPreparationReceipts=>Set<PrincipalRfqPreparationReceipt>();
    private void ConfigureRfqPreparation(ModelBuilder model)
    {
        RfqCommercialBase<PrincipalRfqPreparedPlan>(model,"rfq_prepared_plans","ak_pr32_prepared_scope");
        RfqCommercialBase<PrincipalRfqPreparationReceipt>(model,"rfq_preparation_receipts","ak_pr32_preparation_receipt_scope");
        var plan=model.Entity<PrincipalRfqPreparedPlan>();
        foreach(var name in new[]{"DraftSnapshotSha256","PolicyRulesSha256","PublishedIdentitySha256","DocumentSelectionDigest","EvaluationRulesSha256","SnapshotSha256"})plan.Property(name).HasMaxLength(64);
        plan.Property(x=>x.ActorPrincipalId).HasMaxLength(36);plan.Property(x=>x.PublishedStage).HasMaxLength(20);plan.Property(x=>x.SnapshotJson).HasColumnType("text");
        RfqCommercialLink<PrincipalRfqPreparedPlan,PrincipalRfqCommercialDraftRevision>(model,"DraftRevisionId","fk_pr32_prepared_revision");
        RfqCommercialLink<PrincipalRfqPreparedPlan,SourcingPolicyIdentityEvidence>(model,"PublishedPolicyIdentityId","fk_pr32_prepared_policy_identity");
        plan.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.DraftRevisionId}).IsUnique().HasDatabaseName("ux_pr32_prepared_revision");
        plan.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.DraftRevisionId,x.SourcingEventId,x.BranchId,x.ActorPrincipalId,x.SnapshotSha256}).IsUnique().HasDatabaseName("ux_pr32_prepared_owner_tuple");
        plan.ToTable(t=>t.HasCheckConstraint("ck_pr32_prepared","schema_version=1 AND draft_revision>0 AND event_version>0 AND published_stage='Published' AND document_count BETWEEN 0 AND 100 AND (NOT requirements_pack_required OR document_count>0) AND ((document_count=0 AND document_use_set_id IS NULL AND document_selection_digest IS NULL) OR (document_count>0 AND document_use_set_id IS NOT NULL AND document_use_set_id<>'00000000-0000-0000-0000-000000000000'::uuid AND document_selection_digest IS NOT NULL AND document_selection_digest ~ '^[0-9a-f]{64}$')) AND document_use_key=draft_revision_id AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(snapshot_json) BETWEEN 1 AND 1000000"));
        var receipt=model.Entity<PrincipalRfqPreparationReceipt>();
        receipt.Property(x=>x.ActorPrincipalId).HasMaxLength(36);receipt.Property(x=>x.ResponseJson).HasColumnType("text");
        foreach(var name in new[]{"RequestSha256","SnapshotSha256","ResponseSha256"})receipt.Property(name).HasMaxLength(64);
        RfqCommercialLink<PrincipalRfqPreparationReceipt,PrincipalRfqPreparedPlan>(model,"PreparedPlanId","fk_pr32_preparation_plan");
        RfqCommercialLink<PrincipalRfqPreparationReceipt,PrincipalRfqCommercialDraftReceipt>(model,"DraftCommandReceiptId","fk_pr32_preparation_draft_command");
        receipt.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CommandKey}).IsUnique().HasDatabaseName("ux_pr32_preparation_command");
        receipt.ToTable(t=>t.HasCheckConstraint("ck_pr32_preparation_receipt","schema_version=1 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ "+RfqCommercialUuid+" AND request_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$'"));
        model.Entity<PrincipalRfqCommercialDraftRevision>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.SourcingEventId,x.SourceEvidenceId,x.BranchId,x.ActorPrincipalId,x.SnapshotSha256}).IsUnique().HasDatabaseName("ux_pr32_prepared_revision_target");
        model.Entity<PrincipalRfqCommercialDraftReceipt>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.DraftRevisionId,x.SourcingEventId,x.BranchId,x.ActorPrincipalId,x.CommandKey,x.RequestSha256}).IsUnique().HasDatabaseName("ux_pr32_preparation_command_target");
        model.Entity<SourcingPolicyIdentityEvidence>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.PolicyVersionId,x.Stage,x.RulesSha256,x.EvidenceSha256}).IsUnique().HasDatabaseName("ux_pr32_published_identity_target");
    }
}
