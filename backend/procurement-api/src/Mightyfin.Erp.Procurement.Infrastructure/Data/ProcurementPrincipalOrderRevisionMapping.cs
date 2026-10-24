using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalOrderCommercialOrigin> PrincipalOrderCommercialOrigins => Set<PrincipalOrderCommercialOrigin>();
    public DbSet<PrincipalOrderDraftCommercialRevision> PrincipalOrderDraftCommercialRevisions => Set<PrincipalOrderDraftCommercialRevision>();
    public DbSet<PrincipalOrderEffectiveSourceSlice> PrincipalOrderEffectiveSourceSlices => Set<PrincipalOrderEffectiveSourceSlice>();

    private void ConfigurePrincipalOrderRevision(ModelBuilder model)
    {
        ConfigureBase<PrincipalOrderCommercialOrigin>(model,"principal_order_commercial_origins");
        ConfigureBase<PrincipalOrderDraftCommercialRevision>(model,"principal_order_draft_commercial_revisions");
        ConfigureBase<PrincipalOrderEffectiveSourceSlice>(model,"principal_order_effective_source_slices");
        const string uuid="'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'";
        var origin=model.Entity<PrincipalOrderCommercialOrigin>();
        origin.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        origin.Property(x=>x.SnapshotSha256).HasMaxLength(64);
        origin.Property(x=>x.SnapshotJson).HasColumnType("text");
        origin.Property(x=>x.CapturedByPrincipalId).HasMaxLength(36);
        origin.Property(x=>x.CaptureMode).HasMaxLength(20);
        origin.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId}).IsUnique();
        origin.HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .HasName("ak_po_draft_origin_owner");
        Link<PrincipalOrderCommercialOrigin,PurchaseOrder>(model,nameof(PrincipalOrderCommercialOrigin.PurchaseOrderId));
        origin.ToTable(t=>t.HasCheckConstraint("ck_po_draft_origin",$"schema_version=1 AND capture_mode IN ('Create','VerifiedFirstEdit') AND original_direct_source_sha256 ~ '^[0-9a-f]{{64}}$' AND snapshot_sha256 ~ '^[0-9a-f]{{64}}$' AND captured_by_principal_id IS NOT NULL AND captured_by_principal_id ~ {uuid} AND captured_by_principal_id<>'00000000-0000-0000-0000-000000000000' AND captured_at IS NOT NULL"));

        var revision=model.Entity<PrincipalOrderDraftCommercialRevision>();
        revision.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        revision.Property(x=>x.SnapshotSha256).HasMaxLength(64);
        revision.Property(x=>x.CommandSha256).HasMaxLength(64);
        revision.Property(x=>x.PreviousSnapshotSha256).HasMaxLength(64);
        revision.Property(x=>x.SnapshotJson).HasColumnType("text");
        revision.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        revision.Property(x=>x.Reason).HasMaxLength(1000);
        revision.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.RevisionNumber}).IsUnique();
        revision.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CommandKey}).IsUnique();
        revision.HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .HasName("ak_po_draft_revision_owner");
        Link<PrincipalOrderDraftCommercialRevision,PurchaseOrder>(model,nameof(PrincipalOrderDraftCommercialRevision.PurchaseOrderId));
        revision.HasOne<PrincipalOrderCommercialOrigin>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.OriginId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_revision_origin_owner");
        revision.HasOne<PrincipalOrderDraftCommercialRevision>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.PreviousRevisionId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_revision_previous_owner");
        revision.ToTable(t=>t.HasCheckConstraint("ck_po_draft_revision",$"schema_version=1 AND revision_number>0 AND result_order_version=revision_number+1 AND length(reason)>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND command_sha256 ~ '^[0-9a-f]{{64}}$' AND original_direct_source_sha256 ~ '^[0-9a-f]{{64}}$' AND snapshot_sha256 ~ '^[0-9a-f]{{64}}$' AND ((revision_number=1 AND previous_revision_id IS NULL AND previous_snapshot_sha256 IS NULL) OR (revision_number>1 AND previous_revision_id IS NOT NULL AND previous_snapshot_sha256 ~ '^[0-9a-f]{{64}}$')) AND actor_principal_id IS NOT NULL AND actor_principal_id ~ {uuid} AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND recorded_at IS NOT NULL"));

        var slice=model.Entity<PrincipalOrderEffectiveSourceSlice>();
        slice.Property(x=>x.Quantity).HasPrecision(18,4);
        slice.Property(x=>x.UnitPrice).HasPrecision(18,4);
        slice.Property(x=>x.TaxAmount).HasPrecision(18,4);
        slice.Property(x=>x.EstimatedValue).HasPrecision(38,8);
        slice.Property(x=>x.CurrencyCode).HasMaxLength(3);
        slice.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.RevisionId,x.SourceBindingId}).IsUnique();
        slice.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.RevisionId,x.PurchaseOrderLineId}).IsUnique();
        Link<PrincipalOrderEffectiveSourceSlice,PurchaseOrder>(model,nameof(PrincipalOrderEffectiveSourceSlice.PurchaseOrderId));
        slice.HasOne<PrincipalOrderDraftCommercialRevision>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.RevisionId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_slice_revision_owner");
        slice.HasOne<PrincipalOrderSourceBinding>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.SourceBindingId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_slice_binding_owner");
        slice.HasOne<PurchaseOrderLine>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.PurchaseOrderLineId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_slice_line_owner");
        Link<PrincipalOrderEffectiveSourceSlice,PurchaseRequestAllocation>(model,nameof(PrincipalOrderEffectiveSourceSlice.AllocationId));
        slice.ToTable(t=>t.HasCheckConstraint("ck_po_draft_slice","schema_version=1 AND quantity>0 AND unit_price>=0 AND tax_amount>=0 AND estimated_value=quantity*unit_price AND currency_code ~ '^[A-Z]{3}$'"));
    }

    private void GuardPrincipalOrderRevision()
    {
        foreach(var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.Entity is
            PrincipalOrderCommercialOrigin or PrincipalOrderDraftCommercialRevision or PrincipalOrderEffectiveSourceSlice))
            if(entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("PrincipalOrderRevisionEvidenceAppendOnly");
        foreach(var origin in ChangeTracker.Entries<PrincipalOrderCommercialOrigin>()
            .Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==origin.PurchaseOrderId);
            var lines=PurchaseOrderLines.Local.Where(x=>x.PurchaseOrderId==origin.PurchaseOrderId&&x.IsActive).ToArray();
            var bindings=PrincipalOrderSourceBindings.Local.Where(x=>x.PurchaseOrderId==origin.PurchaseOrderId).ToArray();
            if(order is null || order.PrincipalDraftSchemaVersion!=2 || order.Status!="Draft"
                || order.CurrentRevision!=0 || order.FinanceCommitmentRef is not null
                || origin.CapturedByPrincipalId!=scope.SubjectId
                || origin.SnapshotSha256!=PrincipalOrderCommercialStates.Digest(origin.SnapshotJson)
                || (origin.CaptureMode=="Create" && (Entry(order).State!=EntityState.Added
                    || origin.SnapshotJson!=PrincipalOrderCommercialStates.Capture(order,lines)))
                || (origin.CaptureMode=="VerifiedFirstEdit" && (Entry(order).State!=EntityState.Modified
                    || !PrincipalOrderDraftCommercialRevisions.Local.Any(x=>x.OriginId==origin.Id
                        &&Entry(x).State==EntityState.Added))))
                throw new InvalidOperationException("PrincipalOrderOriginalCommercialInvalid");
            PrincipalOrderCommercialStates.VerifyOrigin(origin,order,bindings);
        }
        foreach(var revision in ChangeTracker.Entries<PrincipalOrderDraftCommercialRevision>()
            .Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==revision.PurchaseOrderId);
            var origin=PrincipalOrderCommercialOrigins.Local.SingleOrDefault(x=>x.Id==revision.OriginId);
            var lines=PurchaseOrderLines.Local.Where(x=>x.PurchaseOrderId==revision.PurchaseOrderId&&x.IsActive).ToArray();
            var slices=PrincipalOrderEffectiveSourceSlices.Local.Where(x=>x.RevisionId==revision.Id).ToArray();
            if(order is null || origin is null || order.PrincipalDraftSchemaVersion!=2 || order.Status!="Draft"
                || order.CurrentRevision!=0 || order.FinanceCommitmentRef is not null
                || PrincipalOrderFundingEvidence.Any(x=>x.PurchaseOrderId==order.Id)
                || PrincipalOrderFundingEvidence.Local.Any(x=>x.PurchaseOrderId==order.Id)
                || PrincipalOrderSubmissions.Any(x=>x.PurchaseOrderId==order.Id)
                || PrincipalOrderSubmissions.Local.Any(x=>x.PurchaseOrderId==order.Id)
                || PrincipalOrderIssuedVersions.Any(x=>x.PurchaseOrderId==order.Id)
                || PrincipalOrderIssuedVersions.Local.Any(x=>x.PurchaseOrderId==order.Id)
                || PrincipalReceivingSourceStates.Any(x=>x.PurchaseOrderId==order.Id)
                || PrincipalReceivingSourceStates.Local.Any(x=>x.PurchaseOrderId==order.Id)
                || revision.ActorPrincipalId!=scope.SubjectId || revision.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || revision.ResultOrderVersion!=checked(order.Version+1)
                || revision.CommandSha256 is not {Length:64}
                || revision.SnapshotSha256!=PrincipalOrderCommercialStates.Digest(revision.SnapshotJson)
                || revision.SnapshotJson!=PrincipalOrderCommercialStates.Capture(order,lines)
                || slices.Length!=lines.Length
                || slices.Any(x=>x.PurchaseOrderId!=order.Id || x.RevisionId!=revision.Id)
                || !PrincipalOrderDraftReceipts.Local.Any(x=>x.PurchaseOrderId==order.Id
                    &&x.Operation=="Edit"&&x.CommandKey==revision.CommandKey&&x.ActorPrincipalId==scope.SubjectId
                    &&Entry(x).State==EntityState.Added)
                )
                throw new InvalidOperationException("PrincipalOrderDraftRevisionInvalid");
        }
        foreach(var slice in ChangeTracker.Entries<PrincipalOrderEffectiveSourceSlice>()
            .Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var revision=PrincipalOrderDraftCommercialRevisions.Local.SingleOrDefault(x=>x.Id==slice.RevisionId);
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==slice.PurchaseOrderId);
            var binding=PrincipalOrderSourceBindings.Local.SingleOrDefault(x=>x.Id==slice.SourceBindingId);
            var allocation=PurchaseRequestAllocations.Local.SingleOrDefault(x=>x.Id==slice.AllocationId);
            var line=PurchaseOrderLines.Local.SingleOrDefault(x=>x.Id==slice.PurchaseOrderLineId);
            if(revision is null || order is null || Entry(revision).State!=EntityState.Added
                || binding is null || allocation is null || line is null
                || revision.PurchaseOrderId!=slice.PurchaseOrderId
                || binding.PurchaseOrderId!=slice.PurchaseOrderId
                || binding.PurchaseOrderLineId!=line.Id || binding.AllocationId!=allocation.Id
                || slice.Quantity!=line.Quantity || slice.UnitPrice!=line.UnitPrice
                || slice.TaxAmount!=line.TaxAmount
                || slice.EstimatedValue!=ExactDecimalArithmetic.Multiply(line.Quantity,line.UnitPrice)
                || allocation.Quantity!=slice.Quantity || allocation.EstimatedValue!=slice.EstimatedValue
                || slice.CurrencyCode!=order.CurrencyCode || allocation.CurrencyCode!=slice.CurrencyCode)
                throw new InvalidOperationException("PrincipalOrderDraftSliceInvalid");
        }
        foreach(var line in ChangeTracker.Entries<PurchaseOrderLine>()
            .Where(x=>x.State==EntityState.Modified))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==line.Entity.PurchaseOrderId);
            if(order?.PrincipalDraftSchemaVersion!=2 || order.Status!="Draft")continue;
            if(!PrincipalOrderDraftCommercialRevisions.Local.Any(x=>x.PurchaseOrderId==order.Id
                &&Entry(x).State==EntityState.Added)
                || !PrincipalOrderDraftReceipts.Local.Any(x=>x.PurchaseOrderId==order.Id
                    &&x.Operation=="Edit"&&Entry(x).State==EntityState.Added))
                throw new InvalidOperationException("PrincipalOrderLineEditRequiresRevision");
        }
        foreach(var allocation in ChangeTracker.Entries<PurchaseRequestAllocation>()
            .Where(x=>x.State==EntityState.Modified&&x.Entity.TargetType=="PurchaseOrder"
                &&x.Entity.TargetId is not null))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==allocation.Entity.TargetId);
            if(order?.PrincipalDraftSchemaVersion!=2 || order.Status!="Draft")continue;
            if(!PrincipalOrderDraftCommercialRevisions.Local.Any(x=>x.PurchaseOrderId==order.Id
                &&Entry(x).State==EntityState.Added)
                || !PrincipalOrderDraftReceipts.Local.Any(x=>x.PurchaseOrderId==order.Id
                    &&x.Operation=="Edit"&&Entry(x).State==EntityState.Added))
                throw new InvalidOperationException("PrincipalOrderReservationEditRequiresRevision");
        }
    }
}
