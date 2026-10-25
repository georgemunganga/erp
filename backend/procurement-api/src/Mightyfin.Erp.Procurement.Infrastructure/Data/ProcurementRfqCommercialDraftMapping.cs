using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalRfqCommercialDraftHead> PrincipalRfqCommercialDrafts=>Set<PrincipalRfqCommercialDraftHead>();
    public DbSet<PrincipalRfqCommercialDraftRevision> PrincipalRfqCommercialDraftRevisions=>Set<PrincipalRfqCommercialDraftRevision>();
    public DbSet<PrincipalRfqCommercialDraftReceipt> PrincipalRfqCommercialDraftReceipts=>Set<PrincipalRfqCommercialDraftReceipt>();
    private const string RfqCommercialUuid="'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
    private void ConfigureRfqCommercialDraft(ModelBuilder model)
    {
        RfqCommercialBase<PrincipalRfqCommercialDraftHead>(model,"rfq_commercial_drafts","ak_pr32_draft_scope");
        RfqCommercialBase<PrincipalRfqCommercialDraftRevision>(model,"rfq_commercial_revisions","ak_pr32_revision_scope");
        RfqCommercialBase<PrincipalRfqCommercialDraftReceipt>(model,"rfq_commercial_receipts","ak_pr32_receipt_scope");
        var head=model.Entity<PrincipalRfqCommercialDraftHead>();
        head.Property(x=>x.OwnerPrincipalId).HasMaxLength(36);head.Property(x=>x.OriginalFingerprint).HasMaxLength(64);
        head.Property(x=>x.SourceEnvelopeSha256).HasMaxLength(64);head.Property(x=>x.SnapshotSha256).HasMaxLength(64);
        RfqCommercialLink<PrincipalRfqCommercialDraftHead,SourcingEvent>(model,nameof(PrincipalRfqCommercialDraftHead.SourcingEventId),"fk_pr32_draft_event");
        RfqCommercialLink<PrincipalRfqCommercialDraftHead,SourcingDraftEvidence>(model,nameof(PrincipalRfqCommercialDraftHead.SourceEvidenceId),"fk_pr32_draft_source");
        head.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.SourcingEventId}).IsUnique().HasDatabaseName("ux_pr32_one_event_draft");
        head.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.SourcingEventId,x.SourceEvidenceId,x.BranchId,x.OwnerPrincipalId}).IsUnique().HasDatabaseName("ux_pr32_draft_owner_tuple");
        head.ToTable(t=>t.HasCheckConstraint("ck_pr32_draft","schema_version=1 AND source_identity_schema_version=2 AND revision>0 AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND owner_principal_id ~ "+RfqCommercialUuid+" AND owner_principal_id<>'00000000-0000-0000-0000-000000000000' AND (owner_worker_alias_id IS NULL OR owner_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid) AND original_fingerprint ~ '^[0-9a-f]{64}$' AND source_envelope_sha256 ~ '^[0-9a-f]{64}$' AND snapshot_sha256 ~ '^[0-9a-f]{64}$'"));
        var row=model.Entity<PrincipalRfqCommercialDraftRevision>();
        row.Property(x=>x.ActorPrincipalId).HasMaxLength(36);row.Property(x=>x.SnapshotJson).HasColumnType("text");row.Property(x=>x.SnapshotSha256).HasMaxLength(64);
        row.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.DraftId,x.Revision}).IsUnique().HasDatabaseName("ux_pr32_draft_revision");
        row.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.DraftId,x.Revision,x.SnapshotSha256}).IsUnique().HasDatabaseName("ux_pr32_head_revision_target");
        row.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.DraftId,x.SourcingEventId,x.SourceEvidenceId,x.BranchId,x.CommandKey,x.ActorPrincipalId,x.SnapshotSha256}).IsUnique().HasDatabaseName("ux_pr32_revision_owner_tuple");
        RfqCommercialLink<PrincipalRfqCommercialDraftRevision,PrincipalRfqCommercialDraftHead>(model,nameof(PrincipalRfqCommercialDraftRevision.DraftId),"fk_pr32_revision_draft");
        row.ToTable(t=>t.HasCheckConstraint("ck_pr32_revision","schema_version=1 AND revision>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ "+RfqCommercialUuid+" AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid) AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND octet_length(snapshot_json) BETWEEN 1 AND 262144"));
        var receipt=model.Entity<PrincipalRfqCommercialDraftReceipt>();
        receipt.Property(x=>x.Operation).HasMaxLength(10);receipt.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x=>x.RequestSha256).HasMaxLength(64);receipt.Property(x=>x.SnapshotSha256).HasMaxLength(64);receipt.Property(x=>x.ResponseSha256).HasMaxLength(64);receipt.Property(x=>x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CommandKey}).IsUnique().HasDatabaseName("ux_pr32_original_command");
        RfqCommercialLink<PrincipalRfqCommercialDraftReceipt,PrincipalRfqCommercialDraftRevision>(model,nameof(PrincipalRfqCommercialDraftReceipt.DraftRevisionId),"fk_pr32_receipt_revision");
        receipt.ToTable(t=>t.HasCheckConstraint("ck_pr32_receipt","schema_version=1 AND result_revision>0 AND operation IN ('Create','Revise') AND response_status IN (200,201) AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id ~ "+RfqCommercialUuid+" AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND snapshot_sha256 ~ '^[0-9a-f]{64}$' AND request_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$'"));
        // Ordinary exact indexes, NOT EF keys on nullable/mutable existing fields.
        model.Entity<SourcingDraftEvidence>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.SourcingEventId,x.BranchRef,x.OriginalDraftFingerprint,x.PayloadSha256,x.SchemaVersion})
            .IsUnique().HasDatabaseName("ux_pr32_original_source_tuple");
    }
    private void RfqCommercialBase<T>(ModelBuilder model,string table,string key) where T:ProcurementRecord
    {ConfigureBase<T>(model,table);model.Entity<T>().HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.Id}).HasName(key);model.Entity<T>().Property(x=>x.Id).ValueGeneratedNever();}
    private static void RfqCommercialLink<TChild,TParent>(ModelBuilder model,string foreign,string name) where TChild:ProcurementRecord where TParent:ProcurementRecord
        =>model.Entity<TChild>().HasOne<TParent>().WithMany().HasForeignKey("TenantId","LegalEntityId",foreign)
            .HasPrincipalKey("TenantId","LegalEntityId","Id").OnDelete(DeleteBehavior.Restrict).HasConstraintName(name);
}
