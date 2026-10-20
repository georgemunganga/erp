using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private static void ConfigureFinanceCheckerIdentity(ModelBuilder model)
    {
        var finance = model.Entity<BudgetCheckSnapshot>();
        finance.Property(x => x.CheckerIdentitySchemaVersion).HasDefaultValue(0).IsRequired();
        finance.Property(x => x.CheckedByWorkerId).ValueGeneratedNever();
        finance.Property(x => x.CheckerIdentitySha256).HasMaxLength(64);
        finance.ToTable(t => t.HasCheckConstraint("ck_budget_checker_identity",
            "(checker_identity_schema_version = 0 AND checked_by_worker_id IS NULL AND checker_identity_sha256 IS NULL) OR " +
            "(checker_identity_schema_version = 1 AND checked_by_worker_id IS NOT NULL " +
            "AND checked_by_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND checker_identity_sha256 IS NOT NULL AND checker_identity_sha256 ~ '^[a-f0-9]{64}$')"));
        var submission = model.Entity<PurchaseRequestApprovalSubmission>();
        submission.Property(x => x.FinanceIdentitySchemaVersion).HasDefaultValue(0).IsRequired();
        submission.ToTable(t => t.HasCheckConstraint("ck_pr_finance_identity_version",
            "finance_identity_schema_version = 0 OR (finance_identity_schema_version = 1 AND snapshot_schema_version = 2)"));
        // No new keys/FKs. Existing scoped Finance->PR and submission->Finance source-version keys stay exact.
        // Core worker references are retained evidence, never cross-module employee/master relationships.
    }

    private void GuardFinanceCheckerIdentity()
    {
        foreach (var entry in ChangeTracker.Entries<BudgetCheckSnapshot>()
                     .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            if (entry.State != EntityState.Added)
                throw new InvalidOperationException("Finance checker evidence is append-only.");
            var f = entry.Entity;
            if (f.CheckerIdentitySchemaVersion == 0 && f.CheckedByWorkerId is null && f.CheckerIdentitySha256 is null)
                continue; // Compatibility only: Step C must deny unbound evidence at NEW positive eligibility boundaries.
            if (!FinanceCheckerIdentityEvidence.HasValidBinding(f) || f.CheckedBySubjectId != scope.SubjectId
                || f.TenantId != scope.TenantId || f.LegalEntityId != scope.LegalEntityId)
                throw new InvalidOperationException("FinanceCheckerIdentityMissingOrInvalid");
            // Current canonical identity, current server permission and requester/maker independence
            // are required application checks; an owned SHA never supplies those authorities.
        }
        foreach (var entry in ChangeTracker.Entries<PurchaseRequestApprovalSubmission>()
                     .Where(x => x.State == EntityState.Added).ToArray())
        {
            var pin = entry.Entity;
            if (pin.FinanceIdentitySchemaVersion is not (0 or 1))
                throw new InvalidOperationException("PinnedFinanceIdentityVersionInvalid");
            var finance = BudgetCheckSnapshots.Local.SingleOrDefault(x => x.Id == pin.FinanceSnapshotId)
                ?? BudgetCheckSnapshots.SingleOrDefault(x => x.Id == pin.FinanceSnapshotId);
            if (pin.FinanceIdentitySchemaVersion == 0)
            {
                // The existing FK still requires a real scoped parent in PostgreSQL. Keep old
                // synthetic/legacy test graphs compatible, but never downgrade an existing v1 parent.
                if (finance is not null && (finance.CheckerIdentitySchemaVersion != 0
                    || finance.CheckedByWorkerId is not null || finance.CheckerIdentitySha256 is not null))
                    throw new InvalidOperationException("PinnedFinanceCheckerEvidenceMismatch");
                continue;
            }
            if (pin.SnapshotSchemaVersion != 2 || finance is null
                || pin.TenantId != scope.TenantId || pin.LegalEntityId != scope.LegalEntityId
                || finance.TenantId != pin.TenantId || finance.LegalEntityId != pin.LegalEntityId
                || finance.PurchaseRequestId != pin.PurchaseRequestId || finance.RequestVersion != pin.SubmittedBusinessVersion
                || !FinanceSnapshotCodec.Matches(finance, pin.FinanceIdentitySchemaVersion,
                    pin.FinanceSnapshotJson, pin.FinanceSnapshotSha256))
                throw new InvalidOperationException("PinnedFinanceCheckerEvidenceMismatch");
        }
    }
}
