using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalReceivingSourceState> PrincipalReceivingSourceStates => Set<PrincipalReceivingSourceState>();
    public DbSet<PrincipalReceivingFact> PrincipalReceivingFacts => Set<PrincipalReceivingFact>();
    public DbSet<PrincipalReceivingDraftHead> PrincipalReceivingDraftHeads => Set<PrincipalReceivingDraftHead>();
    public DbSet<PrincipalReceivingDraftRevision> PrincipalReceivingDraftRevisions => Set<PrincipalReceivingDraftRevision>();
    public DbSet<PrincipalReceivingCommandReceipt> PrincipalReceivingCommandReceipts => Set<PrincipalReceivingCommandReceipt>();
    public DbSet<PrincipalReceivingDraftLineBinding> PrincipalReceivingDraftLineBindings => Set<PrincipalReceivingDraftLineBinding>();
    private const string PrincipalReceivingUuid = "'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";

    private void ConfigurePrincipalReceiving(ModelBuilder model)
    {
        PrincipalReceivingBase<PrincipalReceivingSourceState>(model,"principal_receiving_sources","ak_prcv_source_scope");
        PrincipalReceivingBase<PrincipalReceivingFact>(model,"principal_receiving_facts","ak_prcv_fact_scope");
        PrincipalReceivingBase<PrincipalReceivingDraftHead>(model,"principal_receiving_drafts","ak_prcv_draft_scope");
        PrincipalReceivingBase<PrincipalReceivingDraftRevision>(model,"principal_receiving_revisions","ak_prcv_revision_scope");
        PrincipalReceivingBase<PrincipalReceivingCommandReceipt>(model,"principal_receiving_receipts","ak_prcv_receipt_scope");
        PrincipalReceivingBase<PrincipalReceivingDraftLineBinding>(model,"principal_receiving_line_bindings","ak_prcv_line_scope");
        var source=model.Entity<PrincipalReceivingSourceState>();
        source.Property(x=>x.OriginalSourceSha256).HasMaxLength(64);source.Property(x=>x.IssuedSnapshotSha256).HasMaxLength(64);
        source.Property(x=>x.CategoryCode).HasMaxLength(64);source.Property(x=>x.PurchaseType).HasMaxLength(64);
        source.Property(x=>x.Kind).HasMaxLength(7);source.Property(x=>x.CurrencyCode).HasMaxLength(3);
        PrincipalReceivingLink<PrincipalReceivingSourceState,PrincipalOrderSourceBinding>(model,nameof(PrincipalReceivingSourceState.SourceBindingId),"fk_prcv_source_original");
        source.Property(x=>x.SourceJson).HasColumnType("text");source.Property(x=>x.SourceSha256).HasMaxLength(64);
        source.Property(x=>x.AcceptedQuantity).HasPrecision(18,4);source.Property(x=>x.AcceptedNetAmount).HasPrecision(38,8);
        source.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.IssuedVersionId,x.PurchaseOrderLineId}).IsUnique().HasDatabaseName("ux_prcv_source_line");
        PrincipalReceivingLink<PrincipalReceivingSourceState,PurchaseOrder>(model,nameof(PrincipalReceivingSourceState.PurchaseOrderId),"fk_prcv_source_order");
        PrincipalReceivingLink<PrincipalReceivingSourceState,PurchaseOrderLine>(model,nameof(PrincipalReceivingSourceState.PurchaseOrderLineId),"fk_prcv_source_line");
        PrincipalReceivingLink<PrincipalReceivingSourceState,PrincipalOrderIssuedVersion>(model,nameof(PrincipalReceivingSourceState.IssuedVersionId),"fk_prcv_source_issued");
        PrincipalReceivingLink<PrincipalReceivingSourceState,PrincipalOrderSubmission>(model,nameof(PrincipalReceivingSourceState.SubmissionId),"fk_prcv_source_submission");
        PrincipalReceivingLink<PrincipalReceivingSourceState,PrincipalOrderFundingEvidence>(model,nameof(PrincipalReceivingSourceState.FundingEvidenceId),"fk_prcv_source_funding");
        PrincipalReceivingLink<PrincipalReceivingSourceState,PrincipalOrderAcknowledgment>(model,nameof(PrincipalReceivingSourceState.AcknowledgmentId),"fk_prcv_source_ack");
        PrincipalReceivingLink<PrincipalReceivingSourceState,PurchaseRequestLine>(model,nameof(PrincipalReceivingSourceState.PurchaseRequestLineId),"fk_prcv_source_request_line");
        source.ToTable(t=>t.HasCheckConstraint("ck_prcv_source","schema_version=2 AND kind IN ('Goods','Service') AND length(category_code)>0 AND length(purchase_type)>0 AND currency_code ~ '^[A-Z]{3}$' AND original_source_sha256 ~ '^[0-9a-f]{64}$' AND issued_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND sequence>=0 AND accepted_quantity>=0 AND accepted_net_amount>=0 AND source_sha256 ~ '^[0-9a-f]{64}$' AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid"));
        var draft=model.Entity<PrincipalReceivingDraftHead>();
        draft.Property(x=>x.Kind).HasMaxLength(12);draft.Property(x=>x.State).HasMaxLength(20);
        draft.Property(x=>x.CreatorPrincipalId).HasMaxLength(36);draft.Property(x=>x.SnapshotSha256).HasMaxLength(64);
        PrincipalReceivingLink<PrincipalReceivingDraftHead,PurchaseOrder>(model,nameof(PrincipalReceivingDraftHead.PurchaseOrderId),"fk_prcv_draft_order");
        PrincipalReceivingLink<PrincipalReceivingDraftHead,PrincipalOrderIssuedVersion>(model,nameof(PrincipalReceivingDraftHead.IssuedVersionId),"fk_prcv_draft_issued");
        draft.ToTable(t=>t.HasCheckConstraint("ck_prcv_draft",$"schema_version=2 AND revision>0 AND kind IN ('Goods','Service') AND state IN ('Draft','Recorded','Certified','Cancelled') AND (kind='Goods' OR state<>'Recorded') AND (kind='Service' OR state<>'Certified') AND snapshot_sha256 ~ '^[0-9a-f]{{64}}$' AND creator_principal_id ~ {PrincipalReceivingUuid} AND creator_principal_id<>'00000000-0000-0000-0000-000000000000'"));
        var revision=model.Entity<PrincipalReceivingDraftRevision>();
        revision.Property(x=>x.ActorPrincipalId).HasMaxLength(36);revision.Property(x=>x.SnapshotJson).HasColumnType("text");revision.Property(x=>x.SnapshotSha256).HasMaxLength(64);
        revision.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.DraftId,x.Revision}).IsUnique().HasDatabaseName("ux_prcv_draft_revision");
        PrincipalReceivingLink<PrincipalReceivingDraftRevision,PrincipalReceivingDraftHead>(model,nameof(PrincipalReceivingDraftRevision.DraftId),"fk_prcv_revision_draft");
        revision.ToTable(t=>t.HasCheckConstraint("ck_prcv_revision",$"schema_version=2 AND revision>0 AND snapshot_sha256 ~ '^[0-9a-f]{{64}}$' AND actor_principal_id ~ {PrincipalReceivingUuid} AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid"));
        var line=model.Entity<PrincipalReceivingDraftLineBinding>();
        line.Property(x=>x.Quantity).HasPrecision(18,4);line.Property(x=>x.RejectedQuantity).HasPrecision(18,4);
        line.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.DraftRevisionId,x.LineId}).IsUnique().HasDatabaseName("ux_prcv_revision_line");
        line.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.DraftRevisionId,x.SourceStateId}).IsUnique().HasDatabaseName("ux_prcv_revision_source");
        PrincipalReceivingLink<PrincipalReceivingDraftLineBinding,PrincipalReceivingDraftHead>(model,nameof(PrincipalReceivingDraftLineBinding.DraftId),"fk_prcv_line_draft");
        PrincipalReceivingLink<PrincipalReceivingDraftLineBinding,PrincipalReceivingDraftRevision>(model,nameof(PrincipalReceivingDraftLineBinding.DraftRevisionId),"fk_prcv_line_revision");
        PrincipalReceivingLink<PrincipalReceivingDraftLineBinding,PrincipalReceivingSourceState>(model,nameof(PrincipalReceivingDraftLineBinding.SourceStateId),"fk_prcv_line_source");
        line.ToTable(t=>t.HasCheckConstraint("ck_prcv_line","schema_version=2 AND quantity>0 AND rejected_quantity>=0 AND rejected_quantity<=quantity AND line_id<>'00000000-0000-0000-0000-000000000000'::uuid"));
        var fact=model.Entity<PrincipalReceivingFact>();
        PrincipalReceivingLink<PrincipalReceivingFact,PrincipalReceivingDraftLineBinding>(model,nameof(PrincipalReceivingFact.DraftLineBindingId),"fk_prcv_fact_line");
        fact.Property(x=>x.Operation).HasMaxLength(20);fact.Property(x=>x.Kind).HasMaxLength(12);fact.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        fact.Property(x=>x.PayloadJson).HasColumnType("text");fact.Property(x=>x.PayloadSha256).HasMaxLength(64);
        fact.Property(x=>x.Quantity).HasPrecision(18,4);fact.Property(x=>x.NetAmount).HasPrecision(38,8);
        fact.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.SourceStateId,x.Sequence}).IsUnique().HasDatabaseName("ux_prcv_fact_sequence");
        fact.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.SourceStateId,x.CommandKey}).IsUnique().HasDatabaseName("ux_prcv_fact_command");
        PrincipalReceivingLink<PrincipalReceivingFact,PrincipalReceivingSourceState>(model,nameof(PrincipalReceivingFact.SourceStateId),"fk_prcv_fact_source");
        PrincipalReceivingLink<PrincipalReceivingFact,PrincipalReceivingDraftHead>(model,nameof(PrincipalReceivingFact.DraftId),"fk_prcv_fact_draft");
        PrincipalReceivingLink<PrincipalReceivingFact,PolicyVersion>(model,nameof(PrincipalReceivingFact.PolicyVersionId),"fk_prcv_fact_policy");
        fact.ToTable(t=>t.HasCheckConstraint("ck_prcv_fact",$"schema_version=2 AND sequence>0 AND kind IN ('Observation','Inspection','Acceptance') AND ((kind='Observation' AND operation='Record') OR (kind='Inspection' AND operation='Inspect') OR (kind='Acceptance' AND operation IN ('Accept','Certify'))) AND quantity>=0 AND net_amount>=0 AND (kind='Inspection' OR quantity>0) AND (kind='Acceptance' OR net_amount=0) AND payload_sha256 ~ '^[0-9a-f]{{64}}$' AND actor_principal_id ~ {PrincipalReceivingUuid} AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid"));
        var receipt=model.Entity<PrincipalReceivingCommandReceipt>();
        receipt.Property(x=>x.Operation).HasMaxLength(20);receipt.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x=>x.RequestSha256).HasMaxLength(64);receipt.Property(x=>x.ResponseSha256).HasMaxLength(64);receipt.Property(x=>x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CommandKey}).IsUnique().HasDatabaseName("ux_prcv_receipt_command");
        PrincipalReceivingLink<PrincipalReceivingCommandReceipt,PrincipalReceivingDraftHead>(model,nameof(PrincipalReceivingCommandReceipt.DraftId),"fk_prcv_receipt_draft");
        receipt.ToTable(t=>t.HasCheckConstraint("ck_prcv_receipt",$"schema_version=2 AND operation IN ('Create','Revise','Cancel','Record','Inspect','Accept','Certify') AND response_status IN (200,201) AND result_revision>0 AND request_sha256 ~ '^[0-9a-f]{{64}}$' AND response_sha256 ~ '^[0-9a-f]{{64}}$' AND actor_principal_id ~ {PrincipalReceivingUuid} AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid"));
        ConfigurePrincipalReceivingOwnershipIndexes(model);
    }
    private static void ConfigurePrincipalReceivingOwnershipIndexes(ModelBuilder model)
    {
        // Ordinary unique indexes support exact migration-owned deferred FKs. None
        // of these mutable legacy binding columns become EF alternate keys.
        model.Entity<PrincipalOrderIssuedVersion>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.PurchaseOrderId,x.SubmissionId,x.FundingEvidenceId,x.CommercialSnapshotSha256,x.OriginalDirectSourceSha256}).IsUnique().HasDatabaseName("ux_prcv_issued_parent");
        model.Entity<PrincipalOrderIssuedVersion>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.PurchaseOrderId}).IsUnique().HasDatabaseName("ux_prcv_issued_order");
        model.Entity<PrincipalOrderAcknowledgment>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.IssuedVersionId,x.PurchaseOrderId}).IsUnique().HasDatabaseName("ux_prcv_ack_parent");
        model.Entity<PrincipalOrderSourceBinding>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.PurchaseOrderId,x.PurchaseOrderLineId,x.PurchaseRequestLineId,x.BranchId,x.OriginalDirectSourceSha256}).IsUnique().HasDatabaseName("ux_prcv_original_parent");
        model.Entity<PurchaseOrderLine>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.PurchaseOrderId,x.PurchaseRequestLineId}).IsUnique().HasDatabaseName("ux_prcv_po_line_parent");
        model.Entity<PrincipalReceivingSourceState>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.BranchId,x.PurchaseOrderId,x.IssuedVersionId,x.PurchaseOrderLineId}).IsUnique().HasDatabaseName("ux_prcv_source_parent");
        model.Entity<PrincipalReceivingDraftRevision>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.DraftId}).IsUnique().HasDatabaseName("ux_prcv_revision_parent");
        model.Entity<PrincipalReceivingDraftLineBinding>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.SourceStateId,x.DraftId,x.BranchId}).IsUnique().HasDatabaseName("ux_prcv_line_parent");
        model.Entity<PrincipalReceivingCommandReceipt>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CommandKey,x.DraftId,x.ActorPrincipalId,x.BranchId,x.Operation}).IsUnique().HasDatabaseName("ux_prcv_receipt_fact_parent");
        model.Entity<PrincipalReceivingDraftHead>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.BranchId}).IsUnique().HasDatabaseName("ux_prcv_draft_branch");
        model.Entity<PrincipalReceivingDraftHead>().HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Id,x.BranchId,x.PurchaseOrderId,x.IssuedVersionId}).IsUnique().HasDatabaseName("ux_prcv_draft_parent");
    }
    private void PrincipalReceivingBase<T>(ModelBuilder model,string table,string key) where T:ProcurementRecord
    {
        ConfigureBase<T>(model,table);model.Entity<T>().HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.Id}).HasName(key);
        model.Entity<T>().Property(x=>x.Id).ValueGeneratedNever();
    }
    private static void PrincipalReceivingLink<TChild,TParent>(ModelBuilder model,string foreignId,string name)
        where TChild:ProcurementRecord where TParent:ProcurementRecord => model.Entity<TChild>().HasOne<TParent>().WithMany()
            .HasForeignKey(nameof(ProcurementRecord.TenantId),nameof(ProcurementRecord.LegalEntityId),foreignId)
            .HasPrincipalKey(nameof(ProcurementRecord.TenantId),nameof(ProcurementRecord.LegalEntityId),nameof(ProcurementRecord.Id))
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName(name);
}
