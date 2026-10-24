using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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
        origin.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId}).IsUnique()
            .HasDatabaseName("ux_po_draft_origin_order");
        origin.HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .HasName("ak_po_draft_origin_owner");
        origin.HasOne<PurchaseOrder>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_origin_order");
        origin.ToTable(t=>t.HasCheckConstraint("ck_po_draft_origin",$"schema_version=1 AND capture_mode IS NOT NULL AND capture_mode IN ('Create','VerifiedFirstEdit') AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{{64}}$' AND snapshot_json IS NOT NULL AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[0-9a-f]{{64}}$' AND captured_by_principal_id IS NOT NULL AND captured_by_principal_id ~ {uuid} AND captured_by_principal_id<>'00000000-0000-0000-0000-000000000000' AND captured_at IS NOT NULL"));

        var revision=model.Entity<PrincipalOrderDraftCommercialRevision>();
        revision.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        revision.Property(x=>x.SnapshotSha256).HasMaxLength(64);
        revision.Property(x=>x.CommandSha256).HasMaxLength(64);
        revision.Property(x=>x.PreviousSnapshotSha256).HasMaxLength(64);
        revision.Property(x=>x.SnapshotJson).HasColumnType("text");
        revision.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        revision.Property(x=>x.Reason).HasMaxLength(1000);
        revision.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.RevisionNumber}).IsUnique()
            .HasDatabaseName("ux_po_draft_revision_number");
        revision.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.CommandKey}).IsUnique()
            .HasDatabaseName("ux_po_draft_revision_command");
        revision.HasAlternateKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .HasName("ak_po_draft_revision_owner");
        revision.HasOne<PurchaseOrder>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_revision_order");
        revision.HasOne<PrincipalOrderCommercialOrigin>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.OriginId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_revision_origin_owner");
        revision.HasOne<PrincipalOrderDraftCommercialRevision>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.PreviousRevisionId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_revision_previous_owner");
        revision.ToTable(t=>t.HasCheckConstraint("ck_po_draft_revision",$"schema_version=1 AND revision_number>0 AND result_order_version=revision_number+1 AND reason IS NOT NULL AND length(reason)>0 AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid AND command_sha256 IS NOT NULL AND command_sha256 ~ '^[0-9a-f]{{64}}$' AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{{64}}$' AND snapshot_json IS NOT NULL AND snapshot_sha256 IS NOT NULL AND snapshot_sha256 ~ '^[0-9a-f]{{64}}$' AND ((revision_number=1 AND previous_revision_id IS NULL AND previous_snapshot_sha256 IS NULL) OR (revision_number>1 AND previous_revision_id IS NOT NULL AND previous_snapshot_sha256 IS NOT NULL AND previous_snapshot_sha256 ~ '^[0-9a-f]{{64}}$')) AND actor_principal_id IS NOT NULL AND actor_principal_id ~ {uuid} AND actor_principal_id<>'00000000-0000-0000-0000-000000000000' AND recorded_at IS NOT NULL"));

        var slice=model.Entity<PrincipalOrderEffectiveSourceSlice>();
        slice.Property(x=>x.Quantity).HasPrecision(18,4);
        slice.Property(x=>x.UnitPrice).HasPrecision(18,4);
        slice.Property(x=>x.TaxAmount).HasPrecision(18,4);
        slice.Property(x=>x.EstimatedValue).HasPrecision(38,8);
        slice.Property(x=>x.CurrencyCode).HasMaxLength(3);
        slice.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.RevisionId,x.SourceBindingId}).IsUnique()
            .HasDatabaseName("ux_po_draft_slice_binding");
        slice.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.RevisionId,x.PurchaseOrderLineId}).IsUnique()
            .HasDatabaseName("ux_po_draft_slice_line");
        slice.HasOne<PurchaseOrder>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_slice_order");
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
        slice.HasOne<PurchaseRequestAllocation>().WithMany()
            .HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.AllocationId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.Id})
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_po_draft_slice_allocation");
        slice.ToTable(t=>t.HasCheckConstraint("ck_po_draft_slice","schema_version=1 AND quantity>0 AND unit_price>=0 AND tax_amount>=0 AND estimated_value=quantity*unit_price AND currency_code IS NOT NULL AND currency_code ~ '^[A-Z]{3}$'"));
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
                || AuditEvents.Local.Count(x=>Entry(x).State==EntityState.Added
                    &&PrincipalOrderRevisionCompanions.MatchesAudit(revision,x,scope.CorrelationId))!=1
                || OutboxEvents.Local.Count(x=>Entry(x).State==EntityState.Added
                    &&PrincipalOrderRevisionCompanions.MatchesOutbox(revision,x))!=1
                )
                throw new InvalidOperationException("PrincipalOrderDraftRevisionInvalid");
        }
        foreach(var audit in AuditEvents.Local.Where(x=>Entry(x).State==EntityState.Added
            &&x.RecordType=="PurchaseOrder"&&x.Action=="PrincipalOrderEdit"))
            if(PrincipalOrderDraftCommercialRevisions.Local.Count(x=>Entry(x).State==EntityState.Added
                &&PrincipalOrderRevisionCompanions.MatchesAudit(x,audit,scope.CorrelationId))!=1)
                throw new InvalidOperationException("PrincipalOrderDraftRevisionAuditOrphan");
        foreach(var outgoing in OutboxEvents.Local.Where(x=>Entry(x).State==EntityState.Added
            &&x.AggregateType=="PurchaseOrder"
            &&x.EventType=="purchase_order.internal_unfunded_draft_revised.v2"))
            if(PrincipalOrderDraftCommercialRevisions.Local.Count(x=>Entry(x).State==EntityState.Added
                &&PrincipalOrderRevisionCompanions.MatchesOutbox(x,outgoing))!=1)
                throw new InvalidOperationException("PrincipalOrderDraftRevisionOutboxOrphan");
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

public static class PrincipalOrderRevisionCompanions
{
    public static bool MatchesAudit(PrincipalOrderDraftCommercialRevision revision, AuditEvent audit,
        string? correlationId)
    {
        if(audit.TenantId!=revision.TenantId||audit.LegalEntityId!=revision.LegalEntityId
            ||audit.RecordType!="PurchaseOrder"||audit.RecordId!=revision.PurchaseOrderId
            ||audit.Action!="PrincipalOrderEdit"||audit.ActorSubjectId!=revision.ActorPrincipalId
            ||audit.CorrelationId!=correlationId||audit.ChangedFieldsJson is null)return false;
        try
        {
            using var document=JsonDocument.Parse(audit.ChangedFieldsJson);
            var root=document.RootElement;
            if(!root.TryGetProperty("schemaVersion",out var schema)||schema.GetInt32()!=2
                ||!root.TryGetProperty("commandKey",out var key)||key.GetGuid()!=revision.CommandKey
                ||!root.TryGetProperty("detail",out var detail)
                ||!detail.TryGetProperty("revisionNumber",out var number)
                ||number.GetInt32()!=revision.RevisionNumber
                ||!detail.TryGetProperty("effectiveSnapshotSha256",out var digest)
                ||digest.GetString()!=revision.SnapshotSha256
                ||!detail.TryGetProperty("reason",out var reason)
                ||reason.GetString()!=revision.Reason)return false;
            return true;
        }
        catch(JsonException){return false;}
        catch(InvalidOperationException){return false;}
        catch(FormatException){return false;}
    }

    public static bool MatchesOutbox(PrincipalOrderDraftCommercialRevision revision, OutboxEvent outgoing)
    {
        if(outgoing.TenantId!=revision.TenantId||outgoing.LegalEntityId!=revision.LegalEntityId
            ||outgoing.AggregateType!="PurchaseOrder"||outgoing.AggregateId!=revision.PurchaseOrderId
            ||outgoing.EventType!="purchase_order.internal_unfunded_draft_revised.v2"
            ||outgoing.EventVersion!=1||outgoing.State!="Pending"
            ||outgoing.IdempotencyKey!=$"po.principal.Edit.{revision.CommandKey:N}.v2")return false;
        try
        {
            using var document=JsonDocument.Parse(outgoing.PayloadJson);
            var root=document.RootElement;
            return root.TryGetProperty("orderId",out var order)&&order.GetGuid()==revision.PurchaseOrderId
                &&root.TryGetProperty("commandKey",out var key)&&key.GetGuid()==revision.CommandKey
                &&root.TryGetProperty("internalVersionAuthorized",out var authorized)
                &&authorized.ValueKind==JsonValueKind.False
                &&root.TryGetProperty("supplierSendPerformed",out var sent)
                &&sent.ValueKind==JsonValueKind.False
                &&root.TryGetProperty("financeReservationChanged",out var finance)
                &&finance.ValueKind==JsonValueKind.False;
        }
        catch(JsonException){return false;}
        catch(InvalidOperationException){return false;}
        catch(FormatException){return false;}
    }
}
