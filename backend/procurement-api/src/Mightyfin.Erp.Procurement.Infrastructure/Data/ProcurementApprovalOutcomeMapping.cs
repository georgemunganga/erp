using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<PurchaseRequestApprovalOutcome> ApprovalOutcomes => Set<PurchaseRequestApprovalOutcome>();
    public DbSet<PurchaseRequestFinanceReleaseRequest> FinanceReleaseRequests => Set<PurchaseRequestFinanceReleaseRequest>();
    public DbSet<PurchaseRequestFinanceReleaseConfirmation> FinanceReleaseConfirmations => Set<PurchaseRequestFinanceReleaseConfirmation>();

    private void ConfigureApprovalOutcomes(ModelBuilder model)
    {
        ConfigureBase<PurchaseRequestApprovalOutcome>(model, "purchase_request_approval_outcomes");
        ConfigureBase<PurchaseRequestFinanceReleaseRequest>(model, "purchase_request_finance_release_requests");
        ConfigureBase<PurchaseRequestFinanceReleaseConfirmation>(model, "purchase_request_finance_release_confirmations");
        var outcome = model.Entity<PurchaseRequestApprovalOutcome>();
        outcome.Property(x => x.DecisionCommandKey).HasMaxLength(150);
        outcome.Property(x => x.Outcome).HasMaxLength(30);
        outcome.Property(x => x.EffectKey).HasMaxLength(150);
        outcome.Property(x => x.ActorSubjectId).HasMaxLength(200);
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId }).IsUnique();
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreApprovalCaseId }).IsUnique();
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId }).IsUnique();
        outcome.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EffectKey }).IsUnique();
        outcome.HasOne<PurchaseRequestApprovalSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.PurchaseRequestId,
                x.SubmittedBusinessVersion, x.CoreApprovalCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PurchaseRequestId,
                x.SubmittedBusinessVersion, x.CoreApprovalCaseId }).OnDelete(DeleteBehavior.Restrict);
        outcome.ToTable(t => t.HasCheckConstraint("ck_pr_approval_outcome_evidence",
            "outcome IN ('Approved','Rejected','Returned') AND submitted_business_version > 0 " +
            "AND core_decision_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND length(btrim(decision_command_key)) > 0 AND length(btrim(effect_key)) > 0 " +
            "AND length(btrim(actor_subject_id)) > 0"));

        var release = model.Entity<PurchaseRequestFinanceReleaseRequest>();
        release.Property(x => x.Outcome).HasMaxLength(30);
        release.Property(x => x.ReservationRef).HasMaxLength(500);
        release.Property(x => x.ReservedAmount).HasPrecision(18, 4);
        release.Property(x => x.CurrencyCode).HasMaxLength(3);
        release.Property(x => x.EffectKey).HasMaxLength(150);
        release.Property(x => x.OwnerQueue).HasMaxLength(100);
        release.Property(x => x.RequestedBySubjectId).HasMaxLength(200);
        release.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ApprovalOutcomeId }).IsUnique();
        release.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId }).IsUnique();
        release.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EffectKey }).IsUnique();
        release.HasOne<PurchaseRequestApprovalOutcome>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ApprovalOutcomeId, x.SubmissionId,
                x.PurchaseRequestId, x.SubmittedBusinessVersion, x.CoreApprovalCaseId, x.Outcome })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.SubmissionId,
                x.PurchaseRequestId, x.SubmittedBusinessVersion, x.CoreApprovalCaseId, x.Outcome })
            .OnDelete(DeleteBehavior.Restrict);
        release.HasOne<PurchaseRequestApprovalSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.FinanceSnapshotId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.FinanceSnapshotId })
            .OnDelete(DeleteBehavior.Restrict);
        release.HasOne<BudgetCheckSnapshot>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.FinanceSnapshotId,
                x.PurchaseRequestId, x.SubmittedBusinessVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.PurchaseRequestId, x.RequestVersion }).OnDelete(DeleteBehavior.Restrict);
        release.ToTable(t => t.HasCheckConstraint("ck_pr_finance_release_request",
            "outcome IN ('Rejected','Returned') AND reserved_amount >= 0 AND currency_code ~ '^[A-Z]{3}$' " +
            "AND length(btrim(reservation_ref)) > 0 AND length(btrim(effect_key)) > 0 " +
            "AND length(btrim(owner_queue)) > 0 AND length(btrim(requested_by_subject_id)) > 0"));

        var confirmation = model.Entity<PurchaseRequestFinanceReleaseConfirmation>();
        confirmation.Property(x => x.ReservationRef).HasMaxLength(500);
        confirmation.Property(x => x.ReleasedAmount).HasPrecision(18, 4);
        confirmation.Property(x => x.CurrencyCode).HasMaxLength(3);
        confirmation.Property(x => x.EvidenceRef).HasMaxLength(500);
        confirmation.Property(x => x.EvidenceSource).HasMaxLength(200);
        confirmation.Property(x => x.VerifiedBySubjectId).HasMaxLength(200);
        confirmation.Property(x => x.EffectKey).HasMaxLength(150);
        confirmation.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ReleaseRequestId }).IsUnique();
        confirmation.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.EffectKey }).IsUnique();
        confirmation.HasOne<PurchaseRequestFinanceReleaseRequest>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ReleaseRequestId,
                x.ReservationRef, x.CurrencyCode, x.ReleasedAmount })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.ReservationRef, x.CurrencyCode, x.ReservedAmount }).OnDelete(DeleteBehavior.Restrict);
        confirmation.ToTable(t => t.HasCheckConstraint("ck_pr_finance_release_confirmation",
            "released_amount >= 0 AND currency_code ~ '^[A-Z]{3}$' AND finance_confirmed_at <= recorded_at " +
            "AND length(btrim(evidence_ref)) > 0 AND length(btrim(evidence_source)) > 0 " +
            "AND length(btrim(verified_by_subject_id)) > 0 AND length(btrim(effect_key)) > 0"));
        // Reference identity/finance systems through owned evidence, never cross-module table FKs.
    }

    private void GuardApprovalOutcomeChanges()
    {
        foreach (var entry in ChangeTracker.Entries<ProcurementRecord>().ToArray())
        {
            if (entry.Entity is not (PurchaseRequestApprovalOutcome or PurchaseRequestFinanceReleaseRequest
                or PurchaseRequestFinanceReleaseConfirmation)) continue;
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Approval source effects and Finance release evidence are append-only.");
            if (entry.State != EntityState.Added) continue;
            if (entry.Entity.TenantId != scope.TenantId || entry.Entity.LegalEntityId != scope.LegalEntityId)
                throw new InvalidOperationException("Approval effect evidence is outside the authenticated scope.");
            switch (entry.Entity)
            {
                case PurchaseRequestApprovalOutcome effect:
                    var pin = ApprovalEvidence<PurchaseRequestApprovalSubmission>(effect.SubmissionId);
                    if (pin is null || effect.PurchaseRequestId != pin.PurchaseRequestId
                        || effect.SubmittedBusinessVersion != pin.SubmittedBusinessVersion
                        || effect.CoreApprovalCaseId != pin.CoreApprovalCaseId || effect.CoreDecisionId == Guid.Empty
                        || effect.ActorSubjectId != scope.SubjectId || effect.AppliedAt < pin.PinnedAt
                        || effect.Outcome is not ("Approved" or "Rejected" or "Returned")
                        || string.IsNullOrWhiteSpace(effect.DecisionCommandKey)
                        || effect.EffectKey != $"procurement.pr.approval.{pin.CoreApprovalCaseId:N}.{effect.Outcome.ToLowerInvariant()}.v1")
                        throw new InvalidOperationException("Approval outcome must match the pinned source and authenticated actor.");
                    break;
                case PurchaseRequestFinanceReleaseRequest request:
                    var sourceOutcome = ApprovalEvidence<PurchaseRequestApprovalOutcome>(request.ApprovalOutcomeId);
                    var sourcePin = ApprovalEvidence<PurchaseRequestApprovalSubmission>(request.SubmissionId);
                    var finance = ApprovalEvidence<BudgetCheckSnapshot>(request.FinanceSnapshotId);
                    var sourceRequest = ApprovalEvidence<PurchaseRequest>(request.PurchaseRequestId);
                    if (sourceOutcome is null || sourcePin is null || finance is null || sourceRequest is null
                        || request.BranchRef != sourceRequest.BranchRef || string.IsNullOrWhiteSpace(request.OwnerQueue)
                        || finance.Status != "Available" || finance.ReservationStatus != "Confirmed"
                        || request.Outcome is not ("Rejected" or "Returned") || request.Outcome != sourceOutcome.Outcome
                        || request.SubmissionId != sourceOutcome.SubmissionId || request.PurchaseRequestId != sourceOutcome.PurchaseRequestId
                        || request.SubmittedBusinessVersion != sourceOutcome.SubmittedBusinessVersion
                        || request.CoreApprovalCaseId != sourceOutcome.CoreApprovalCaseId
                        || request.FinanceSnapshotId != sourcePin.FinanceSnapshotId
                        || finance.PurchaseRequestId != request.PurchaseRequestId || finance.RequestVersion != request.SubmittedBusinessVersion
                        || request.ReservationRef != finance.ReservationRef || string.IsNullOrWhiteSpace(request.ReservationRef)
                        || request.CurrencyCode != finance.CurrencyCode || request.ReservedAmount != finance.CheckedAmount
                        || request.RequestedBySubjectId != sourceOutcome.ActorSubjectId || request.RequestedBySubjectId != scope.SubjectId
                        || request.RequestedAt != sourceOutcome.AppliedAt
                        || request.EffectKey != $"procurement.pr.release.{sourcePin.Id:N}.v1")
                        throw new InvalidOperationException("Release request must match the terminal outcome and exact pinned reservation.");
                    break;
                case PurchaseRequestFinanceReleaseConfirmation confirmed:
                    var release = ApprovalEvidence<PurchaseRequestFinanceReleaseRequest>(confirmed.ReleaseRequestId);
                    var budget = release is null ? null : ApprovalEvidence<BudgetCheckSnapshot>(release.FinanceSnapshotId);
                    if (release is null || budget is null || confirmed.ReservationRef != release.ReservationRef
                        || confirmed.CurrencyCode != release.CurrencyCode || confirmed.ReleasedAmount != release.ReservedAmount
                        || string.IsNullOrWhiteSpace(confirmed.EvidenceRef) || string.IsNullOrWhiteSpace(confirmed.EvidenceSource)
                        || string.IsNullOrWhiteSpace(confirmed.VerifiedBySubjectId)
                        || confirmed.FinanceConfirmedAt < budget.CheckedAt || confirmed.FinanceConfirmedAt > confirmed.RecordedAt
                        || confirmed.RecordedAt < release.RequestedAt
                        || confirmed.EffectKey != $"procurement.pr.release_confirmed.{release.Id:N}.v1")
                        throw new InvalidOperationException("Release confirmation must preserve the exact reservation and authoritative evidence.");
                    break;
            }
        }
    }

    private T? ApprovalEvidence<T>(Guid id) where T : ProcurementRecord => Set<T>().Local
        .SingleOrDefault(x => x.Id == id && x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId)
        ?? Set<T>().AsNoTracking().SingleOrDefault(x => x.Id == id);
}
