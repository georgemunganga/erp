using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<ContractReviewSubmission> ContractReviewSubmissions => Set<ContractReviewSubmission>();
    public DbSet<ContractReviewVote> ContractReviewVotes => Set<ContractReviewVote>();
    public DbSet<ContractReviewCommandReceipt> ContractReviewCommandReceipts => Set<ContractReviewCommandReceipt>();

    private void ConfigureContractReviewPersistence(ModelBuilder model)
    {
        ConfigureBase<ContractReviewSubmission>(model, "contract_review_submissions");
        ConfigureBase<ContractReviewVote>(model, "contract_review_votes");
        ConfigureBase<ContractReviewCommandReceipt>(model, "contract_review_command_receipts");
        var contractVersion = model.Entity<SupplierContractVersion>();
        contractVersion.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierContractId, x.Id })
            .HasName("ak_p35_contract_version_owner");

        var submission = model.Entity<ContractReviewSubmission>();
        submission.Property(x => x.MakerPrincipalId).HasMaxLength(36).IsRequired();
        submission.Property(x => x.PolicyKey).HasMaxLength(100).IsRequired();
        submission.Property(x => x.PolicyRulesSha256).HasMaxLength(64).IsRequired();
        submission.Property(x => x.TermsSha256).HasMaxLength(64).IsRequired();
        submission.Property(x => x.TermsJson).HasColumnType("text").IsRequired();
        submission.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.ContractId, x.ReviewBranchId, x.CoreCaseId }).HasName("ak_p35_contract_submission_owner");
        submission.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.ContractId, x.ReviewBranchId }).HasName("ak_p35_contract_submission_scope");
        submission.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.ContractId, x.SubmissionNumber })
            .IsUnique().HasDatabaseName("ux_p35_contract_submission_number");
        submission.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreCaseId })
            .IsUnique().HasDatabaseName("ux_p35_contract_core_case");
        submission.HasOne<SupplierContract>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ContractId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_submission_contract");
        submission.HasOne<SupplierContractVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.ContractId, x.ContractVersionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SupplierContractId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_submission_version");
        submission.HasOne<PolicyVersion>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PolicyVersionId, x.PolicyKey, x.PolicyVersionNumber })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PolicyKey, x.PolicyVersionNumber })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_submission_policy");
        submission.ToTable(t => t.HasCheckConstraint("ck_p35_submission", """
            schema_version=1 AND id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND contract_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND contract_version_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND review_branch_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND supplier_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND policy_version_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND policy_key='SUPPLIER_CONTRACT_REVIEW'
            AND contract_business_version>0 AND contract_version_business_version>0
            AND submission_number>0 AND policy_version_number>0
            AND maker_principal_id IS NOT NULL
            AND maker_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            AND maker_principal_id<>'00000000-0000-0000-0000-000000000000'
            AND (maker_worker_alias_id IS NULL OR maker_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid)
            AND policy_rules_sha256 IS NOT NULL AND policy_rules_sha256 ~ '^[0-9a-f]{64}$'
            AND terms_sha256 IS NOT NULL AND terms_sha256 ~ '^[0-9a-f]{64}$'
            AND terms_json IS NOT NULL AND length(terms_json)>2
            AND isfinite(submitted_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL
            """));

        var vote = model.Entity<ContractReviewVote>();
        vote.Property(x => x.ActorPrincipalId).HasMaxLength(36).IsRequired();
        vote.Property(x => x.Action).HasMaxLength(20).IsRequired();
        vote.HasAlternateKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId,
                x.Id, x.ContractId, x.ReviewBranchId }).HasName("ak_p35_contract_vote_owner");
        vote.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreDecisionId })
            .IsUnique().HasDatabaseName("ux_p35_contract_core_decision");
        vote.HasOne<ContractReviewSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId,
                x.ContractId, x.ReviewBranchId, x.CoreCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.ContractId, x.ReviewBranchId, x.CoreCaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_vote_submission");
        vote.ToTable(t => t.HasCheckConstraint("ck_p35_vote", """
            id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND core_case_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND core_stage_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND core_assignment_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND core_decision_id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND actor_principal_id IS NOT NULL
            AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            AND actor_principal_id<>'00000000-0000-0000-0000-000000000000'
            AND (actor_worker_alias_id IS NULL OR actor_worker_alias_id<>'00000000-0000-0000-0000-000000000000'::uuid)
            AND action IN ('Approve','Return','Reject')
            AND isfinite(decided_at) AND version=1 AND updated_at IS NULL AND updated_by IS NULL
            """));

        var receipt = model.Entity<ContractReviewCommandReceipt>();
        receipt.Property(x => x.Operation).HasMaxLength(20).IsRequired();
        receipt.Property(x => x.ActorPrincipalId).HasMaxLength(36).IsRequired();
        receipt.Property(x => x.CommandSha256).HasMaxLength(64).IsRequired();
        receipt.Property(x => x.ResponseSha256).HasMaxLength(64).IsRequired();
        receipt.Property(x => x.ResponseJson).HasColumnType("text").IsRequired();
        receipt.HasIndex(x => new { x.TenantId, x.ActorPrincipalId, x.CommandKey })
            .IsUnique().HasDatabaseName("ux_p35_contract_actor_key");
        receipt.HasOne<ContractReviewSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId,
                x.ContractId, x.ReviewBranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id,
                x.ContractId, x.ReviewBranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_receipt_submission");
        receipt.HasOne<ContractReviewVote>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId,
                x.VoteId, x.ContractId, x.ReviewBranchId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId,
                x.Id, x.ContractId, x.ReviewBranchId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_p35_receipt_vote");
        receipt.ToTable(t => t.HasCheckConstraint("ck_p35_receipt", """
            id<>'00000000-0000-0000-0000-000000000000'::uuid
            AND command_key<>'00000000-0000-0000-0000-000000000000'::uuid
            AND actor_principal_id IS NOT NULL
            AND actor_principal_id ~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
            AND actor_principal_id<>'00000000-0000-0000-0000-000000000000'
            AND command_sha256 IS NOT NULL AND command_sha256 ~ '^[0-9a-f]{64}$'
            AND response_sha256 IS NOT NULL AND response_sha256 ~ '^[0-9a-f]{64}$'
            AND response_json IS NOT NULL AND length(response_json)>2
            AND ((operation='Submit' AND vote_id IS NULL AND response_status=201)
                OR (operation='Decide' AND vote_id IS NOT NULL AND response_status=200))
            AND result_contract_version>0 AND isfinite(completed_at)
            AND version=1 AND updated_at IS NULL AND updated_by IS NULL
            """));
    }

    private void GuardContractReviewPersistence()
    {
        var submissions = ChangeTracker.Entries<ContractReviewSubmission>().ToArray();
        var votes = ChangeTracker.Entries<ContractReviewVote>().ToArray();
        var receipts = ChangeTracker.Entries<ContractReviewCommandReceipt>().ToArray();
        foreach (var entry in submissions.Cast<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry>()
            .Concat(votes).Concat(receipts))
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("ContractReviewEvidenceAppendOnly");
        foreach (var entry in submissions.Where(x => x.State == EntityState.Added))
        {
            var row = entry.Entity;
            var contract = ChangeTracker.Entries<SupplierContract>().SingleOrDefault(x => x.Entity.Id == row.ContractId);
            var version = ChangeTracker.Entries<SupplierContractVersion>().SingleOrDefault(x => x.Entity.Id == row.ContractVersionId);
            if (contract is null || version is null || contract.State != EntityState.Modified
                || version.State != EntityState.Modified || contract.Entity.SupplierId != row.SupplierId
                || contract.Entity.OwnerSubjectId != scope.SubjectId || row.MakerPrincipalId != scope.SubjectId
                || row.MakerWorkerAliasId != scope.CoreLinkedWorkerId
                || contract.OriginalValues.GetValue<string>(nameof(SupplierContract.Status)) != "Draft"
                || version.OriginalValues.GetValue<string>(nameof(SupplierContractVersion.Status)) != "Draft"
                || contract.Entity.Status != "PendingReview" || version.Entity.Status != "PendingReview"
                || contract.OriginalValues.GetValue<long>(nameof(SupplierContract.Version)) != row.ContractBusinessVersion
                || version.OriginalValues.GetValue<long>(nameof(SupplierContractVersion.Version)) != row.ContractVersionBusinessVersion
                || row.TermsSha256 != ProcurementEvidenceEncoding.Digest(row.TermsJson)
                || row.CreatedAt != row.SubmittedAt || row.CreatedBy != scope.SubjectId
                || ProcurementEvidenceEncoding.DatabaseTimestamp(row.SubmittedAt) != row.SubmittedAt
                || receipts.Count(x => x.State == EntityState.Added && x.Entity.SubmissionId == row.Id
                    && x.Entity.Operation == "Submit" && x.Entity.ContractId == row.ContractId
                    && x.Entity.ReviewBranchId == row.ReviewBranchId
                    && x.Entity.ResultContractVersion == row.ContractBusinessVersion + 1) != 1)
                throw new InvalidOperationException("ContractReviewSubmissionSameSaveInvalid");
            if (ChangeTracker.Entries<ContractRate>().Any(x => x.Entity.SupplierContractVersionId == row.ContractVersionId
                    && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("ContractReviewRateChangedDuringSubmission");
            var originalContract = (SupplierContract)contract.OriginalValues.ToObject();
            var originalVersion = (SupplierContractVersion)version.OriginalValues.ToObject();
            var rates = ContractRates.AsNoTracking().Where(x => x.SupplierContractVersionId == row.ContractVersionId).ToArray();
            ContractReviewTerms.VerifyCurrentDraft(row, originalContract, originalVersion, rates);
        }
        foreach (var entry in votes.Where(x => x.State == EntityState.Added))
        {
            var row = entry.Entity;
            if (row.ActorPrincipalId != scope.SubjectId || row.ActorWorkerAliasId != scope.CoreLinkedWorkerId
                || row.CreatedBy != scope.SubjectId || row.CreatedAt != row.DecidedAt
                || ProcurementEvidenceEncoding.DatabaseTimestamp(row.DecidedAt) != row.DecidedAt
                || receipts.Count(x => x.State == EntityState.Added && x.Entity.VoteId == row.Id
                    && x.Entity.Operation == "Decide" && x.Entity.SubmissionId == row.SubmissionId
                    && x.Entity.ContractId == row.ContractId && x.Entity.ReviewBranchId == row.ReviewBranchId) != 1)
                throw new InvalidOperationException("ContractReviewVoteSameSaveInvalid");
        }
        foreach (var entry in receipts.Where(x => x.State == EntityState.Added))
        {
            var row = entry.Entity;
            if (row.ActorPrincipalId != scope.SubjectId || row.CreatedBy != scope.SubjectId
                || row.CreatedAt != row.CompletedAt
                || ProcurementEvidenceEncoding.DatabaseTimestamp(row.CompletedAt) != row.CompletedAt
                || row.CommandKey == Guid.Empty || row.CommandSha256 is not { Length: 64 }
                || row.CommandSha256.Any(x => x is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
                || row.Operation is not ("Submit" or "Decide")
                || row.Operation == "Submit" && !submissions.Any(x => x.State == EntityState.Added
                    && x.Entity.Id == row.SubmissionId && x.Entity.ContractId == row.ContractId
                    && x.Entity.ReviewBranchId == row.ReviewBranchId)
                || row.Operation == "Decide" && !votes.Any(x => x.State == EntityState.Added
                    && x.Entity.Id == row.VoteId && x.Entity.SubmissionId == row.SubmissionId
                    && x.Entity.ContractId == row.ContractId && x.Entity.ReviewBranchId == row.ReviewBranchId))
                throw new InvalidOperationException("ContractReviewReceiptSameSaveInvalid");
            ContractReviewReceiptProof.Verify(row);
        }
    }
}
