using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<SourcingDraftEvidence> SourcingDraftEvidenceRecords => Set<SourcingDraftEvidence>();
    public DbSet<SourcingDemandBinding> SourcingDemandBindings => Set<SourcingDemandBinding>();
    public DbSet<SourcingCommandReceipt> SourcingCommandReceipts => Set<SourcingCommandReceipt>();

    private void ConfigureSourcingDraftExecution(ModelBuilder model)
    {
        ConfigureBase<SourcingDraftEvidence>(model, "sourcing_draft_evidence");
        ConfigureBase<SourcingDemandBinding>(model, "sourcing_demand_bindings");
        ConfigureBase<SourcingCommandReceipt>(model, "sourcing_command_receipts");
        var ev = model.Entity<SourcingEvent>();
        ev.Property(x => x.ExecutionVersion).HasDefaultValue(0);
        ev.Property(x => x.SourceEvidenceId).ValueGeneratedNever();
        ev.Property(x => x.OriginalDraftFingerprint).HasMaxLength(64);
        Link<SourcingEvent, ProcurementMethod>(model, nameof(SourcingEvent.MethodId));
        ev.ToTable(t => t.HasCheckConstraint("ck_sourcing_owned_draft", "execution_version IN (0,1) AND ((execution_version=0 AND source_evidence_id IS NULL AND maker_worker_id IS NULL AND method_id IS NULL AND method_revision IS NULL AND original_draft_fingerprint IS NULL AND branch_ref IS NULL) OR (execution_version=1 AND source_evidence_id IS NOT NULL AND maker_worker_id IS NOT NULL AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND method_id IS NOT NULL AND method_revision IS NOT NULL AND method_revision>0 AND original_draft_fingerprint IS NOT NULL AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND status IN ('Draft','Cancelled')))"));
        var evidence = model.Entity<SourcingDraftEvidence>();
        evidence.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).HasName("ak_rfq_evidence_scope");
        evidence.Property(x => x.PayloadJson).HasColumnType("text");
        evidence.Property(x => x.PayloadSha256).HasMaxLength(64); evidence.Property(x => x.OriginalDraftFingerprint).HasMaxLength(64);
        evidence.Property(x => x.MakerSubjectId).HasMaxLength(200);
        evidence.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId }).IsUnique().HasDatabaseName("ux_rfq_evidence_event");
        evidence.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId }).IsUnique().HasDatabaseName("ux_rfq_evidence_exact_event");
        evidence.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CreateCommandKey }).IsUnique().HasDatabaseName("ux_rfq_evidence_create_key");
        Link<SourcingDraftEvidence, SourcingEvent>(model, nameof(SourcingDraftEvidence.SourcingEventId));
        evidence.ToTable(t => t.HasCheckConstraint("ck_rfq_original_envelope", "schema_version=1 AND payload_sha256 ~ '^[0-9a-f]{64}$' AND original_draft_fingerprint ~ '^[0-9a-f]{64}$' AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND create_command_key <> '00000000-0000-0000-0000-000000000000'::uuid"));
        var binding = model.Entity<SourcingDemandBinding>();
        binding.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).HasName("ak_rfq_binding_scope");
        binding.Property(x => x.Quantity).HasPrecision(18, 4); binding.Property(x => x.UnitEstimate).HasPrecision(18, 4);
        binding.Property(x => x.EstimatedValue).HasPrecision(38, 8);
        binding.Property(x => x.RequestSha).HasMaxLength(64); binding.Property(x => x.PolicyDecisionSha).HasMaxLength(64);
        binding.Property(x => x.PolicyRulesSha).HasMaxLength(64); binding.Property(x => x.FinanceSha).HasMaxLength(64);
        binding.Property(x => x.TargetType).HasMaxLength(30); binding.Property(x => x.MethodCode).HasMaxLength(40);
        binding.Property(x => x.UnitCode).HasMaxLength(30); binding.Property(x => x.CategoryCode).HasMaxLength(60);
        binding.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingEventId, x.PurchaseRequestLineId }).IsUnique().HasDatabaseName("ux_rfq_binding_source");
        binding.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SourcingLineId }).IsUnique().HasDatabaseName("ux_rfq_binding_line");
        binding.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.AllocationId }).IsUnique().HasDatabaseName("ux_rfq_binding_allocation");
        Link<SourcingDemandBinding, SourcingEvent>(model, nameof(SourcingDemandBinding.SourcingEventId));
        Link<SourcingDemandBinding, SourcingDraftEvidence>(model, nameof(SourcingDemandBinding.SourceEvidenceId));
        Link<SourcingDemandBinding, SourcingLine>(model, nameof(SourcingDemandBinding.SourcingLineId));
        Link<SourcingDemandBinding, PurchaseRequestAllocation>(model, nameof(SourcingDemandBinding.AllocationId));
        Link<SourcingDemandBinding, PurchaseRequestApprovalSubmission>(model, nameof(SourcingDemandBinding.SubmissionId));
        Link<SourcingDemandBinding, PurchaseRequestApprovalOutcome>(model, nameof(SourcingDemandBinding.ApprovalOutcomeId));
        Link<SourcingDemandBinding, BudgetCheckSnapshot>(model, nameof(SourcingDemandBinding.FinanceSnapshotId));
        Link<SourcingDemandBinding, ProcurementMethod>(model, nameof(SourcingDemandBinding.MethodId));
        binding.HasOne<PurchaseRequestLine>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.PurchaseRequestLineId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        binding.ToTable(t => t.HasCheckConstraint("ck_rfq_binding_value", "target_type='SourcingEvent' AND quantity>0 AND unit_estimate>=0 AND estimated_value=quantity*unit_estimate AND request_version>=submitted_version AND submitted_version>0 AND method_revision>0 AND minimum_quote_count BETWEEN 0 AND 20 AND maker_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND requester_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid"));
        var receipt = model.Entity<SourcingCommandReceipt>();
        receipt.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).HasName("ak_rfq_receipt_scope");
        receipt.Property(x => x.Operation).HasMaxLength(20); receipt.Property(x => x.ActorPrincipalId).HasMaxLength(200);
        receipt.Property(x => x.CommandFingerprint).HasMaxLength(64); receipt.Property(x => x.ResponseSha256).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.CommandKey }).IsUnique().HasDatabaseName("ux_rfq_receipt_command");
        Link<SourcingCommandReceipt, SourcingEvent>(model, nameof(SourcingCommandReceipt.SourcingEventId));
        receipt.ToTable(t => t.HasCheckConstraint("ck_rfq_receipt_identity", "operation IN ('Create','Cancel') AND command_key <> '00000000-0000-0000-0000-000000000000'::uuid AND actor_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid AND response_schema_version=1 AND result_version>0 AND command_fingerprint ~ '^[0-9a-f]{64}$' AND response_sha256 ~ '^[0-9a-f]{64}$' AND ((operation='Create' AND response_status_code=201) OR (operation='Cancel' AND response_status_code=200))"));
        // Ordinary unique indexes for frozen manual exact overlays; mutable binding fields never become EF keys.
        model.Entity<SourcingLine>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SourcingEventId, x.PurchaseRequestLineId })
            .IsUnique().HasDatabaseName("ux_rfq_line_exact_source");
        model.Entity<PurchaseRequestAllocation>().HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseRequestId, x.PurchaseRequestLineId, x.TargetType, x.TargetId })
            .IsUnique().HasDatabaseName("ux_rfq_allocation_exact_target");
    }

    private void GuardSourcingDraftExecution()
    {
        foreach (var entry in ChangeTracker.Entries<ProcurementRecord>().Where(x => x.Entity is
            SourcingDraftEvidence or SourcingDemandBinding or SourcingCommandReceipt))
            if (entry.State is EntityState.Modified or EntityState.Deleted) throw new InvalidOperationException("RfqEvidenceAppendOnly");
        foreach (var value in ChangeTracker.Entries<SourcingDraftEvidence>().Where(x => x.State == EntityState.Added).Select(x => x.Entity))
        {
            var owner = RfqRow<SourcingEvent>(value.SourcingEventId);
            if (owner is null || Entry(owner).State != EntityState.Added || owner.ExecutionVersion != 1
                || owner.SourceEvidenceId != value.Id || value.MakerSubjectId != scope.SubjectId)
                throw new InvalidOperationException("RfqOriginalEvidenceOwnerRequired");
        }
        foreach (var entry in ChangeTracker.Entries<SourcingEvent>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var ev = entry.Entity;
            if (ev.ExecutionVersion == 0)
            {
                if (ev.SourceEvidenceId is not null || ev.MakerWorkerId is not null || ev.MethodId is not null
                    || ev.MethodRevision is not null || ev.OriginalDraftFingerprint is not null || ev.BranchRef is not null
                    || entry.State == EntityState.Modified && entry.OriginalValues.GetValue<int>(nameof(SourcingEvent.ExecutionVersion)) != 0)
                    throw new InvalidOperationException("RfqLegacyAuthorityCannotBeInferred");
                continue;
            }
            if (ev.ExecutionVersion != 1 || ev.SourceEvidenceId is null || ev.MakerWorkerId is null || ev.MakerWorkerId == Guid.Empty
                || ev.MethodId is null || ev.MethodRevision is not > 0 || !RfqDigest(ev.OriginalDraftFingerprint)
                || ev.OwnerSubjectId != scope.SubjectId || scope.OrgUnitId is not null
                || scope.WorkLocationId is { } selected && selected != ev.BranchRef || ev.Status is not ("Draft" or "Cancelled")) throw new InvalidOperationException("RfqOwnedDraftInvalid");
            if (entry.State == EntityState.Added && ev.Status != "Draft") throw new InvalidOperationException("RfqOwnedDraftInvalid");
            if (entry.State == EntityState.Modified && (entry.OriginalValues.GetValue<int>(nameof(SourcingEvent.ExecutionVersion)) != 1
                || entry.OriginalValues.GetValue<string>(nameof(SourcingEvent.Status)) != "Draft" || ev.Status != "Cancelled"
                || string.IsNullOrWhiteSpace(ev.CancellationReason) || ev.CancellationReason.Length > 2000
                || entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not
                    (nameof(SourcingEvent.Status) or nameof(SourcingEvent.CancellationReason) or nameof(ProcurementRecord.UpdatedAt)
                    or nameof(ProcurementRecord.UpdatedBy) or nameof(ProcurementRecord.Version))))) throw new InvalidOperationException("RfqMaterialEvidenceLocked");
            var evidence = RfqRow<SourcingDraftEvidence>(ev.SourceEvidenceId.Value) ?? throw new InvalidOperationException("RfqOriginalEvidenceRequired");
            var bindings = RfqRows(SourcingDemandBindings.Local, SourcingDemandBindings.Where(x => x.SourcingEventId == ev.Id), x => x.SourcingEventId == ev.Id);
            if (evidence.SourcingEventId != ev.Id || evidence.MakerSubjectId != scope.SubjectId || evidence.MakerWorkerId != ev.MakerWorkerId
                || evidence.BranchRef != ev.BranchRef || bindings.Count is < 1 or > 200) throw new InvalidOperationException("RfqOriginalEvidenceRequired");
            GovernedRfqRestoration.VerifyEnvelopeAndPins(SourcingDraftPersistenceProjection.Evidence(evidence), ev,
                bindings.Select(SourcingDraftPersistenceProjection.Pin).ToArray());
        }
        foreach (var binding in ChangeTracker.Entries<SourcingDemandBinding>().Where(x => x.State == EntityState.Added).Select(x => x.Entity))
            GuardRfqBinding(binding);
        foreach (var receipt in ChangeTracker.Entries<SourcingCommandReceipt>().Where(x => x.State == EntityState.Added).Select(x => x.Entity))
        {
            var ev = RfqRow<SourcingEvent>(receipt.SourcingEventId);
            if (ev is null || ev.ExecutionVersion != 1 || ev.OwnerSubjectId != scope.SubjectId || receipt.ActorPrincipalId != scope.SubjectId
                || receipt.ActorWorkerId != ev.MakerWorkerId || receipt.CommandKey == Guid.Empty || receipt.ResponseSchemaVersion != 1
                || receipt.Operation is not ("Create" or "Cancel") || receipt.ResponseStatusCode != (receipt.Operation == "Create" ? 201 : 200)
                || receipt.CompletedAt == default || receipt.ResultVersion != ev.Version
                || (receipt.Operation == "Create" ? ev.Status != "Draft" : ev.Status != "Cancelled") || !RfqDigest(receipt.CommandFingerprint)
                || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)) throw new InvalidOperationException("RfqReceiptInvalid");
        }
        GuardRfqAllocationEffects();
        foreach (var entry in ChangeTracker.Entries<SourcingLine>().Where(x => x.State is EntityState.Modified or EntityState.Added))
        {
            var ev = RfqRow<SourcingEvent>(entry.Entity.SourcingEventId);
            if (ev?.ExecutionVersion != 1) continue;
            if (entry.State == EntityState.Modified && (ev.Status != "Cancelled"
                || entry.Entity.Status != "Cancelled" || entry.OriginalValues.GetValue<string>(nameof(SourcingLine.Status)) != "Active"
                || entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not (nameof(SourcingLine.Status) or nameof(SourcingLine.StatusReason)
                    or nameof(ProcurementRecord.UpdatedAt) or nameof(ProcurementRecord.UpdatedBy) or nameof(ProcurementRecord.Version)))))
                throw new InvalidOperationException("RfqSourceLineLocked");
            if (!SourcingDemandBindings.Local.Any(x => x.SourcingEventId == ev.Id && x.SourcingLineId == entry.Entity.Id)
                && !SourcingDemandBindings.Any(x => x.SourcingEventId == ev.Id && x.SourcingLineId == entry.Entity.Id)) throw new InvalidOperationException("RfqSourceBindingRequired");
        }
    }
    private void GuardRfqBinding(SourcingDemandBinding binding)
    {
        var ev = RfqRow<SourcingEvent>(binding.SourcingEventId); var line = RfqRow<SourcingLine>(binding.SourcingLineId);
        var allocation = RfqRow<PurchaseRequestAllocation>(binding.AllocationId); var evidence = RfqRow<SourcingDraftEvidence>(binding.SourceEvidenceId);
        var submission = RfqRow<PurchaseRequestApprovalSubmission>(binding.SubmissionId); var outcome = RfqRow<PurchaseRequestApprovalOutcome>(binding.ApprovalOutcomeId);
        if (ev is null || line is null || allocation is null || evidence is null || submission is null || outcome is null
            || Entry(ev).State != EntityState.Added || ev.ExecutionVersion != 1 || ev.Status != "Draft" || ev.SourceEvidenceId != evidence.Id || evidence.SourcingEventId != ev.Id
            || ev.MethodId != binding.MethodId || ev.MethodRevision != binding.MethodRevision || ev.Method != binding.MethodCode
            || line.SourcingEventId != ev.Id || line.PurchaseRequestLineId != binding.PurchaseRequestLineId || line.Quantity != binding.Quantity
            || line.UnitCode != binding.UnitCode || line.CategoryCode != binding.CategoryCode || line.Status != "Active"
            || Entry(allocation).State != EntityState.Added || allocation.TargetType != "SourcingEvent" || allocation.TargetId != ev.Id || allocation.Status != "Reserved"
            || allocation.PurchaseRequestId != binding.PurchaseRequestId || allocation.PurchaseRequestLineId != binding.PurchaseRequestLineId
            || allocation.Method != binding.MethodCode || allocation.Quantity != binding.Quantity || allocation.EstimatedValue != binding.EstimatedValue || allocation.CurrencyCode != ev.CurrencyCode
            || allocation.IdempotencyKey != $"rfq.reserve.{evidence.CreateCommandKey:N}.{binding.PurchaseRequestLineId:N}"
            || binding.TargetType != "SourcingEvent" || binding.Quantity <= 0 || !ExactDecimalJsonCodec.IsRepresentable(binding.Quantity, 18, 4)
            || binding.UnitEstimate < 0 || !ExactDecimalJsonCodec.IsRepresentable(binding.UnitEstimate, 18, 4)
            || binding.EstimatedValue != ExactDecimalArithmetic.Multiply(binding.Quantity, binding.UnitEstimate)
            || !RfqDigest(binding.RequestSha) || !RfqDigest(binding.PolicyDecisionSha) || !RfqDigest(binding.PolicyRulesSha) || !RfqDigest(binding.FinanceSha)
            || binding.MinimumQuoteCount is < 0 or > 20 || binding.SubmittedVersion < 1 || binding.RequestVersion < binding.SubmittedVersion
            || binding.MakerWorkerId == Guid.Empty || binding.RequesterWorkerId == Guid.Empty
            || submission.PurchaseRequestId != binding.PurchaseRequestId || submission.CoreApprovalCaseId != binding.CoreApprovalCaseId
            || submission.SubmittedBusinessVersion != binding.SubmittedVersion || submission.RequestSnapshotSha256 != binding.RequestSha
            || submission.PolicyDecisionSha256 != binding.PolicyDecisionSha || submission.PolicyRulesSha256 != binding.PolicyRulesSha
            || submission.FinanceSnapshotId != binding.FinanceSnapshotId || submission.FinanceSnapshotSha256 != binding.FinanceSha
            || submission.MakerWorkerId != binding.MakerWorkerId || submission.RequesterWorkerId != binding.RequesterWorkerId
            || outcome.SubmissionId != submission.Id || outcome.PurchaseRequestId != binding.PurchaseRequestId
            || outcome.CoreApprovalCaseId != binding.CoreApprovalCaseId || outcome.CoreDecisionId != binding.CoreDecisionId || outcome.Outcome != "Approved")
            throw new InvalidOperationException("RfqBindingInvalid");
    }
    private void GuardRfqAllocationEffects()
    {
        var effects = ChangeTracker.Entries<PurchaseRequestAllocation>()
            .Where(x => (x.State is EntityState.Added or EntityState.Modified)
                && (x.Entity.TargetType == "SourcingEvent" || x.OriginalValues.GetValue<string>(nameof(PurchaseRequestAllocation.TargetType)) == "SourcingEvent"))
            .ToArray();
        var deltas = new Dictionary<Guid, decimal>();
        foreach (var entry in effects)
        {
            var row = entry.Entity;
            if (row.TargetType != "SourcingEvent" || row.TargetId is null) throw new InvalidOperationException("RfqAllocationRetargetDenied");
            var ev = RfqRow<SourcingEvent>(row.TargetId.Value);
            var binding = RfqRows(SourcingDemandBindings.Local, SourcingDemandBindings.Where(x => x.AllocationId == row.Id), x => x.AllocationId == row.Id).SingleOrDefault();
            if (ev?.ExecutionVersion != 1 || binding is null || binding.SourcingEventId != ev.Id
                || binding.PurchaseRequestId != row.PurchaseRequestId || binding.PurchaseRequestLineId != row.PurchaseRequestLineId
                || binding.Quantity != row.Quantity || binding.EstimatedValue != row.EstimatedValue)
                throw new InvalidOperationException("RfqAllocationBindingRequired");
            decimal delta;
            if (entry.State == EntityState.Added)
            {
                if (Entry(ev).State != EntityState.Added || row.Status != "Reserved") throw new InvalidOperationException("RfqReservationInvalid");
                delta = row.Quantity;
            }
            else
            {
                if (Entry(ev).State != EntityState.Modified || ev.Status != "Cancelled"
                    || entry.OriginalValues.GetValue<string>(nameof(PurchaseRequestAllocation.Status)) != "Reserved" || row.Status != "Released"
                    || entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not (nameof(PurchaseRequestAllocation.Status)
                        or nameof(PurchaseRequestAllocation.Reason) or nameof(ProcurementRecord.UpdatedAt)
                        or nameof(ProcurementRecord.UpdatedBy) or nameof(ProcurementRecord.Version))))
                    throw new InvalidOperationException("RfqReservationReleaseInvalid");
                delta = -row.Quantity;
            }
            deltas[row.PurchaseRequestLineId] = ExactDecimalArithmetic.Add(deltas.GetValueOrDefault(row.PurchaseRequestLineId), delta);
        }
        foreach (var delta in deltas)
        {
            var line = RfqRow<PurchaseRequestLine>(delta.Key) ?? throw new InvalidOperationException("RfqTrackedRequestRequired");
            var request = RfqRow<PurchaseRequest>(line.PurchaseRequestId) ?? throw new InvalidOperationException("RfqTrackedRequestRequired");
            if (Entry(request).State != EntityState.Modified || Entry(line).State != EntityState.Modified
                || line.AllocatedQuantity != ExactDecimalArithmetic.Add(Entry(line).OriginalValues.GetValue<decimal>(nameof(PurchaseRequestLine.AllocatedQuantity)), delta.Value))
                throw new InvalidOperationException("RfqTrackedRequestCounterRequired");
        }
        foreach (var entry in ChangeTracker.Entries<SourcingEvent>().Where(x => x.State == EntityState.Modified && x.Entity.ExecutionVersion == 1 && x.Entity.Status == "Cancelled"))
        {
            var bindings = RfqRows(SourcingDemandBindings.Local, SourcingDemandBindings.Where(x => x.SourcingEventId == entry.Entity.Id), x => x.SourcingEventId == entry.Entity.Id);
            if (bindings.Count == 0 || bindings.Any(binding => !effects.Any(x => x.Entity.Id == binding.AllocationId && x.State == EntityState.Modified && x.Entity.Status == "Released")))
                throw new InvalidOperationException("RfqCancellationMustReleaseEveryHold");
        }
    }
    private T? RfqRow<T>(Guid id) where T : ProcurementRecord => Set<T>().Local.SingleOrDefault(x => x.Id == id && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId)
        ?? Set<T>().SingleOrDefault(x => x.Id == id);
    private static List<T> RfqRows<T>(IEnumerable<T> local, IQueryable<T> stored, Func<T, bool> filter) where T : ProcurementRecord
        => local.Where(filter).Concat(stored.ToArray()).DistinctBy(x => x.Id).ToList();
    private static bool RfqDigest(string? value) => value is { Length: 64 } && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
}
