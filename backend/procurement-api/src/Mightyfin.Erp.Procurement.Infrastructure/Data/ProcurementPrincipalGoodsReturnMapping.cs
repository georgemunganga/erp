using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalGoodsReturnDraft> PrincipalGoodsReturnDrafts => Set<PrincipalGoodsReturnDraft>();
    public DbSet<PrincipalGoodsReturnRevision> PrincipalGoodsReturnRevisions => Set<PrincipalGoodsReturnRevision>();
    public DbSet<PrincipalGoodsReturnCommandReceipt> PrincipalGoodsReturnCommandReceipts => Set<PrincipalGoodsReturnCommandReceipt>();

    private const string ReturnPrincipalUuid = "'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
    private const string ReturnHash = "'^[0-9a-f]{64}$'";

    private void ConfigurePrincipalGoodsReturns(ModelBuilder model)
    {
        ConfigureBase<PrincipalGoodsReturnDraft>(model, "principal_goods_return_drafts");
        ConfigureBase<PrincipalGoodsReturnRevision>(model, "principal_goods_return_revisions");
        ConfigureBase<PrincipalGoodsReturnCommandReceipt>(model, "principal_goods_return_receipts");
        var draft = model.Entity<PrincipalGoodsReturnDraft>();
        draft.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).HasName("ak_pgret_draft_scope");
        draft.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.BranchId })
            .HasName("ak_pgret_draft_branch");
        draft.Property(x => x.CreatorPrincipalId).HasMaxLength(36);
        draft.Property(x => x.SourceSha256).HasMaxLength(64);
        draft.Property(x => x.AcceptanceSha256).HasMaxLength(64);
        draft.Property(x => x.State).HasMaxLength(16);
        draft.Property(x => x.AcceptedFactQuantity).HasPrecision(18, 4);
        draft.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.BranchId, x.CreatedAt })
            .HasDatabaseName("ix_pgret_branch_created");
        ReturnLink<PrincipalGoodsReturnDraft, PrincipalReceivingSourceState>(model, nameof(PrincipalGoodsReturnDraft.SourceStateId), "fk_pgret_source");
        ReturnLink<PrincipalGoodsReturnDraft, PrincipalReceivingFact>(model, nameof(PrincipalGoodsReturnDraft.AcceptanceFactId), "fk_pgret_acceptance");
        ReturnLink<PrincipalGoodsReturnDraft, PrincipalReceivingDraftHead>(model, nameof(PrincipalGoodsReturnDraft.ReceiptId), "fk_pgret_receipt");
        ReturnLink<PrincipalGoodsReturnDraft, PurchaseOrderLine>(model, nameof(PrincipalGoodsReturnDraft.PurchaseOrderLineId), "fk_pgret_order_line");
        ReturnLink<PrincipalGoodsReturnDraft, PrincipalOrderIssuedVersion>(model, nameof(PrincipalGoodsReturnDraft.IssuedVersionId), "fk_pgret_issued");
        draft.ToTable(t => t.HasCheckConstraint("ck_pgret_draft", $"schema_version=1 AND revision>0 AND state IN ('Draft','Cancelled') AND branch_id<>'00000000-0000-0000-0000-000000000000'::uuid AND current_revision_id<>'00000000-0000-0000-0000-000000000000'::uuid AND source_sha256 IS NOT NULL AND source_sha256 ~ {ReturnHash} AND acceptance_sha256 IS NOT NULL AND acceptance_sha256 ~ {ReturnHash} AND accepted_fact_quantity>0 AND creator_principal_id IS NOT NULL AND creator_principal_id ~ {ReturnPrincipalUuid} AND creator_principal_id<>'00000000-0000-0000-0000-000000000000'"));

        var revision = model.Entity<PrincipalGoodsReturnRevision>();
        revision.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).HasName("ak_pgret_revision_scope");
        // Immutable revision identity, never the mutable header cursor.
        revision.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.DraftId,
            x.Revision, x.ActorPrincipalId, x.CommandKey }).HasName("ak_pgret_revision_command");
        revision.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        revision.Property(x => x.State).HasMaxLength(16);
        revision.Property(x => x.Reason).HasMaxLength(2000);
        revision.Property(x => x.EvidenceReference).HasMaxLength(300);
        revision.Property(x => x.ProposalSha256).HasMaxLength(64);
        revision.Property(x => x.Quantity).HasPrecision(18, 4);
        revision.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.DraftId, x.Revision }).IsUnique().HasDatabaseName("ux_pgret_revision");
        ReturnLink<PrincipalGoodsReturnRevision, PrincipalGoodsReturnDraft>(model, nameof(PrincipalGoodsReturnRevision.DraftId), "fk_pgret_revision_draft");
        revision.ToTable(t => t.HasCheckConstraint("ck_pgret_revision", $"schema_version=1 AND revision>0 AND state IN ('Draft','Cancelled') AND quantity>0 AND reason IS NOT NULL AND length(btrim(reason))>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND proposal_sha256 IS NOT NULL AND proposal_sha256 ~ {ReturnHash} AND actor_principal_id IS NOT NULL AND actor_principal_id ~ {ReturnPrincipalUuid} AND actor_principal_id<>'00000000-0000-0000-0000-000000000000'"));

        var receipt = model.Entity<PrincipalGoodsReturnCommandReceipt>();
        receipt.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).HasName("ak_pgret_receipt_scope");
        receipt.Property(x => x.Operation).HasMaxLength(12);
        receipt.Property(x => x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x => x.RequestJson).HasColumnType("text");
        receipt.Property(x => x.RequestSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CommandKey }).IsUnique().HasDatabaseName("ux_pgret_command");
        receipt.HasOne<PrincipalGoodsReturnDraft>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DraftId, x.BranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.BranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pgret_receipt_draft");
        receipt.HasOne<PrincipalGoodsReturnRevision>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.DraftId,
                x.ResultRevision, x.ActorPrincipalId, x.CommandKey })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.DraftId,
                x.Revision, x.ActorPrincipalId, x.CommandKey })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_pgret_receipt_revision");
        receipt.ToTable(t => t.HasCheckConstraint("ck_pgret_receipt", $"schema_version=1 AND operation IN ('Create','Revise','Cancel') AND ((operation='Create' AND response_status=201 AND result_revision=1) OR (operation IN ('Revise','Cancel') AND response_status=200 AND result_revision>1)) AND result_revision>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND request_sha256 IS NOT NULL AND request_sha256 ~ {ReturnHash} AND response_sha256 IS NOT NULL AND response_sha256 ~ {ReturnHash} AND actor_principal_id IS NOT NULL AND actor_principal_id ~ {ReturnPrincipalUuid} AND actor_principal_id<>'00000000-0000-0000-0000-000000000000'"));
    }

    private static void ReturnLink<TChild, TParent>(ModelBuilder model, string foreignId, string name)
        where TChild : ProcurementRecord where TParent : ProcurementRecord => model.Entity<TChild>().HasOne<TParent>().WithMany()
            .HasForeignKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), foreignId)
            .HasPrincipalKey(nameof(ProcurementRecord.TenantId), nameof(ProcurementRecord.LegalEntityId), nameof(ProcurementRecord.Id))
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName(name);
}
