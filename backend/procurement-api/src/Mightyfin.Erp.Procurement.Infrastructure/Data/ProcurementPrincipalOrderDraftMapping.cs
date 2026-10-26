using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PrincipalOrderSourceBinding> PrincipalOrderSourceBindings => Set<PrincipalOrderSourceBinding>();
    public DbSet<PrincipalOrderDraftReceipt> PrincipalOrderDraftReceipts => Set<PrincipalOrderDraftReceipt>();

    private void ConfigurePrincipalOrderDraft(ModelBuilder model)
    {
        ConfigureBase<PrincipalOrderSourceBinding>(model,"principal_order_source_bindings");
        ConfigureBase<PrincipalOrderDraftReceipt>(model,"principal_order_draft_receipts");
        var order=model.Entity<PurchaseOrder>();
        order.Property(x=>x.PrincipalDraftSchemaVersion).HasDefaultValue(0);
        order.Property(x=>x.OriginalDirectSourceSha256).HasMaxLength(64);
        order.ToTable(t=>t.HasCheckConstraint("ck_po_principal_draft_version",
            "(principal_draft_schema_version=0 AND original_direct_source_sha256 IS NULL) OR (principal_draft_schema_version=2 AND branch_ref IS NOT NULL AND buyer_subject_id IS NOT NULL AND buyer_subject_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_subject_id <> '00000000-0000-0000-0000-000000000000' AND original_direct_source_sha256 IS NOT NULL AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled') AND finance_commitment_ref IS NULL AND approval_instance_ref IS NULL AND submitted_at IS NULL AND issued_at IS NULL)"));
        var binding=model.Entity<PrincipalOrderSourceBinding>();
        binding.Property(x=>x.MethodCode).HasMaxLength(40);
        binding.Property(x=>x.BuyerPrincipalId).HasMaxLength(36);
        binding.Property(x=>x.CurrencyCode).HasMaxLength(3);
        foreach(var name in new[]{nameof(PrincipalOrderSourceBinding.RequestSnapshotSha256),
            nameof(PrincipalOrderSourceBinding.PolicyDecisionSha256),nameof(PrincipalOrderSourceBinding.PolicyRulesSha256),
            nameof(PrincipalOrderSourceBinding.FinanceSnapshotSha256),nameof(PrincipalOrderSourceBinding.PrincipalIdentitySha256),
            nameof(PrincipalOrderSourceBinding.OriginalDirectSourceSha256)})binding.Property<string>(name).HasMaxLength(64);
        binding.Property(x=>x.Quantity).HasPrecision(18,4);
        binding.Property(x=>x.UnitEstimate).HasPrecision(18,4);
        binding.Property(x=>x.ProposedUnitPrice).HasPrecision(18,4);
        binding.Property(x=>x.EstimatedValue).HasPrecision(38,8);
        binding.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderId,x.PurchaseRequestLineId}).IsUnique();
        binding.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseOrderLineId}).IsUnique();
        binding.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.AllocationId}).IsUnique();
        Link<PrincipalOrderSourceBinding,PurchaseOrder>(model,nameof(PrincipalOrderSourceBinding.PurchaseOrderId));
        Link<PrincipalOrderSourceBinding,PurchaseOrderLine>(model,nameof(PrincipalOrderSourceBinding.PurchaseOrderLineId));
        Link<PrincipalOrderSourceBinding,PurchaseRequestAllocation>(model,nameof(PrincipalOrderSourceBinding.AllocationId));
        Link<PrincipalOrderSourceBinding,PurchaseRequestApprovalSubmission>(model,nameof(PrincipalOrderSourceBinding.SubmissionId));
        Link<PrincipalOrderSourceBinding,PurchaseRequestApprovalOutcome>(model,nameof(PrincipalOrderSourceBinding.ApprovalOutcomeId));
        Link<PrincipalOrderSourceBinding,BudgetCheckSnapshot>(model,nameof(PrincipalOrderSourceBinding.FinanceSnapshotId));
        Link<PrincipalOrderSourceBinding,ProcurementMethod>(model,nameof(PrincipalOrderSourceBinding.MethodId));
        binding.HasOne<PurchaseRequestLine>().WithMany().HasForeignKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseRequestId,x.PurchaseRequestLineId})
            .HasPrincipalKey(x=>new{x.TenantId,x.LegalEntityId,x.PurchaseRequestId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        binding.ToTable(t=>t.HasCheckConstraint("ck_po_principal_binding",
            "schema_version=2 AND quantity>0 AND unit_estimate>=0 AND proposed_unit_price>=0 AND estimated_value=quantity*proposed_unit_price AND request_version>=submitted_business_version AND submitted_business_version>0 AND method_revision>0 AND buyer_principal_id IS NOT NULL AND buyer_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND buyer_principal_id <> '00000000-0000-0000-0000-000000000000' AND requester_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND (buyer_worker_alias_id IS NULL OR buyer_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND (requester_worker_alias_id IS NULL OR requester_worker_alias_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND original_direct_source_sha256 ~ '^[0-9a-f]{64}$'"));
        var receipt=model.Entity<PrincipalOrderDraftReceipt>();
        receipt.Property(x=>x.Operation).HasMaxLength(10);
        receipt.Property(x=>x.ActorPrincipalId).HasMaxLength(36);
        receipt.Property(x=>x.CommandSha256).HasMaxLength(64);
        receipt.Property(x=>x.ResponseSha256).HasMaxLength(64);
        receipt.Property(x=>x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x=>new{x.TenantId,x.LegalEntityId,x.Operation,x.CommandKey}).IsUnique();
        Link<PrincipalOrderDraftReceipt,PurchaseOrder>(model,nameof(PrincipalOrderDraftReceipt.PurchaseOrderId));
        receipt.ToTable(t=>t.HasCheckConstraint("ck_po_principal_receipt",
            "schema_version=2 AND operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_principal_id IS NOT NULL AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' AND actor_principal_id <> '00000000-0000-0000-0000-000000000000' AND command_sha256 ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND result_version>0 AND ((operation='Create' AND response_status=201) OR (operation='Cancel' AND response_status=200))"));
    }

    private void GuardPrincipalOrderDraft()
    {
        foreach(var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x=>x.Entity is PrincipalOrderSourceBinding or PrincipalOrderDraftReceipt))
            if(entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("PrincipalOrderEvidenceAppendOnly");
        foreach(var entry in ChangeTracker.Entries<PurchaseOrder>().Where(x=>x.State is EntityState.Added or EntityState.Modified))
        {
            var order=entry.Entity;
            if(order.PrincipalDraftSchemaVersion!=2)
            {
                if(entry.State==EntityState.Modified
                    && entry.OriginalValues.GetValue<int>(nameof(PurchaseOrder.PrincipalDraftSchemaVersion))==2)
                    throw new InvalidOperationException("PrincipalOrderDraftAuthorityInvalid");
                continue;
            }
            if(order.BuyerSubjectId!=scope.SubjectId || order.BranchRef is null || order.BranchRef==Guid.Empty
                || order.OriginalDirectSourceSha256 is not {Length:64} || order.FinanceCommitmentRef is not null
                || order.SubmittedAt is not null || order.ApprovalInstanceRef is not null || order.IssuedAt is not null
                || order.Status is not ("Draft" or "Cancelled"))
                throw new InvalidOperationException("PrincipalOrderDraftAuthorityInvalid");
            if(entry.State==EntityState.Added)
            {
                var original=PrincipalOrderSourceBindings.Local.Where(x=>x.PurchaseOrderId==order.Id).ToArray();
                var lines=PurchaseOrderLines.Local.Where(x=>x.PurchaseOrderId==order.Id&&x.IsActive).ToArray();
                PrincipalOrderDraftPins.Verify(order,lines,original);
                if(!PrincipalOrderDraftReceipts.Local.Any(x=>x.PurchaseOrderId==order.Id
                    &&x.Operation=="Create" &&x.ActorPrincipalId==scope.SubjectId))
                    throw new InvalidOperationException("PrincipalOrderCreateReceiptRequired");
            }
            if(entry.State==EntityState.Modified && (entry.OriginalValues.GetValue<int>(nameof(PurchaseOrder.PrincipalDraftSchemaVersion))!=2
                || entry.OriginalValues.GetValue<string>(nameof(PurchaseOrder.OriginalDirectSourceSha256))!=order.OriginalDirectSourceSha256
                || entry.OriginalValues.GetValue<string>(nameof(PurchaseOrder.BuyerSubjectId))!=order.BuyerSubjectId
                || entry.OriginalValues.GetValue<string>(nameof(PurchaseOrder.Number))!=order.Number
                || entry.OriginalValues.GetValue<Guid>(nameof(PurchaseOrder.SupplierId))!=order.SupplierId
                || entry.OriginalValues.GetValue<Guid?>(nameof(PurchaseOrder.BranchRef))!=order.BranchRef
                || entry.OriginalValues.GetValue<string>(nameof(PurchaseOrder.CurrencyCode))!=order.CurrencyCode
                || entry.OriginalValues.GetValue<string?>(nameof(PurchaseOrder.ShipToRef))!=order.ShipToRef
                || entry.OriginalValues.GetValue<string?>(nameof(PurchaseOrder.BillToRef))!=order.BillToRef
                || entry.OriginalValues.GetValue<string?>(nameof(PurchaseOrder.PaymentTermRef))!=order.PaymentTermRef
                || entry.OriginalValues.GetValue<DateOnly?>(nameof(PurchaseOrder.ExpectedDeliveryOn))!=order.ExpectedDeliveryOn
                || entry.OriginalValues.GetValue<int>(nameof(PurchaseOrder.CurrentRevision))!=order.CurrentRevision
                || entry.OriginalValues.GetValue<string>(nameof(PurchaseOrder.Status))!="Draft" || order.Status!="Cancelled"
                || string.IsNullOrWhiteSpace(order.ClosureReason) || order.ClosedAt is null
                || !ChangeTracker.Entries<PrincipalOrderDraftReceipt>().Any(x=>x.State==EntityState.Added
                    && x.Entity.PurchaseOrderId==order.Id && x.Entity.Operation=="Cancel")))
                throw new InvalidOperationException("PrincipalOrderDraftTransitionInvalid");
        }
        foreach(var value in ChangeTracker.Entries<PrincipalOrderSourceBinding>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==value.PurchaseOrderId);
            var line=PurchaseOrderLines.Local.SingleOrDefault(x=>x.Id==value.PurchaseOrderLineId);
            var allocation=PurchaseRequestAllocations.Local.SingleOrDefault(x=>x.Id==value.AllocationId);
            if(order is null || Entry(order).State!=EntityState.Added || order.PrincipalDraftSchemaVersion!=2
                || line is null || allocation is null || value.BuyerPrincipalId!=scope.SubjectId
                || value.OriginalDirectSourceSha256!=order.OriginalDirectSourceSha256
                || line.PurchaseOrderId!=order.Id || line.PurchaseRequestLineId!=value.PurchaseRequestLineId
                || allocation.TargetType!="PurchaseOrder" || allocation.TargetId!=order.Id
                || allocation.PurchaseRequestLineId!=value.PurchaseRequestLineId || allocation.Quantity!=value.Quantity)
                throw new InvalidOperationException("PrincipalOrderSourceBindingInvalid");
        }
        foreach(var value in ChangeTracker.Entries<PrincipalOrderDraftReceipt>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity))
        {
            var order=PurchaseOrders.Local.SingleOrDefault(x=>x.Id==value.PurchaseOrderId);
            if(order is null || value.ActorPrincipalId!=scope.SubjectId || order.BuyerSubjectId!=scope.SubjectId
                || order.PrincipalDraftSchemaVersion!=2 || value.ResponseSha256!=ProcurementEvidenceEncoding.Digest(value.ResponseJson)
                || value.Operation is not ("Create" or "Cancel")
                || value.Operation=="Create" && order.Status!="Draft"
                || value.Operation=="Cancel" && order.Status!="Cancelled"
                || value.ResultVersion!=(Entry(order).State==EntityState.Modified ? order.Version+1 : order.Version))
                throw new InvalidOperationException("PrincipalOrderReceiptInvalid");
        }
    }
}
