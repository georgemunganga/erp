using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    public DbSet<ProcurementApprovalCommandReceipt> ApprovalCommandReceipts => Set<ProcurementApprovalCommandReceipt>();
    public DbSet<PurchaseRequestAssignmentAuthorityEvidence> AssignmentAuthorityEvidence => Set<PurchaseRequestAssignmentAuthorityEvidence>();
    private void ConfigureApprovalExecution(ModelBuilder model)
    {
        ConfigureBase<ProcurementApprovalCommandReceipt>(model, "approval_command_receipts");
        ConfigureBase<PurchaseRequestAssignmentAuthorityEvidence>(model, "assignment_authority_evidence");
        var receipt = model.Entity<ProcurementApprovalCommandReceipt>();
        receipt.Property(x => x.Operation).HasMaxLength(20);
        receipt.Property(x => x.ActorSubjectId).HasMaxLength(200);
        receipt.Property(x => x.CommandFingerprint).HasMaxLength(64);
        receipt.Property(x => x.ResponseJson).HasColumnType("text");
        receipt.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.Operation, x.CommandKey }).IsUnique();
        receipt.HasOne<PurchaseRequest>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.PurchaseRequestId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var evidence = model.Entity<PurchaseRequestAssignmentAuthorityEvidence>();
        evidence.Property(x => x.PrincipalId).HasMaxLength(200);
        evidence.Property(x => x.AssigneeKind).HasMaxLength(50);
        evidence.Property(x => x.EvidenceJson).HasColumnType("text");
        evidence.Property(x => x.EvidenceSha256).HasMaxLength(64);
        evidence.HasIndex(x => new { x.TenantId, x.LegalEntityId, x.CoreAssignmentId }).IsUnique();
        evidence.HasOne<PurchaseRequestApprovalSubmission>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId, x.CoreApprovalCaseId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.CoreApprovalCaseId }).OnDelete(DeleteBehavior.Restrict);
        evidence.HasOne<ProcurementSpendingAuthorityGrant>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId, x.PrincipalId, x.AuthorityVersion })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id, x.PrincipalId, x.AuthorityVersion }).OnDelete(DeleteBehavior.Restrict);
        evidence.ToTable(t => t.HasCheckConstraint("ck_pr_assignment_authority_identity",
            "core_stage_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND core_assignment_id <> '00000000-0000-0000-0000-000000000000'::uuid " +
            "AND grant_revision > 0 AND authority_version > 0 AND length(btrim(principal_id)) > 0 " +
            "AND assignee_kind IN ('LineManager','ProcurementManager') AND evidence_sha256 ~ '^[a-f0-9]{64}$' " +
            "AND (assignee_worker_id IS NULL OR assignee_worker_id <> '00000000-0000-0000-0000-000000000000'::uuid)"));
    }
    private void GuardApprovalExecution()
    {
        foreach (var entry in ChangeTracker.Entries<ProcurementApprovalCommandReceipt>().Where(x => x.State == EntityState.Added))
            if (entry.Entity.ActorSubjectId != scope.SubjectId || entry.Entity.CommandKey == Guid.Empty
                || entry.Entity.CommandFingerprint.Length != 64 || entry.Entity.CompletedAt == default
                || entry.Entity.Operation is not ("Open" or "Decide" or "Release"))
                throw new InvalidOperationException("Approval command receipt is not bound to the current actor and operation.");
        foreach (var entry in ChangeTracker.Entries<PurchaseRequestAssignmentAuthorityEvidence>()
            .Where(x => x.State == EntityState.Added).ToArray())
        {
            var value = entry.Entity;
            var pin = ApprovalEvidence<PurchaseRequestApprovalSubmission>(value.SubmissionId);
            var request = pin is null ? null : PurchaseRequests.Local.SingleOrDefault(x => x.Id == pin.PurchaseRequestId)
                ?? (pin is null ? null : PurchaseRequests.Include(x => x.Lines).SingleOrDefault(x => x.Id == pin.PurchaseRequestId));
            var grant = SpendingAuthorityGrants.Local.SingleOrDefault(x => x.Id == value.GrantId)
                ?? SpendingAuthorityGrants.Include(x => x.Categories).SingleOrDefault(x => x.Id == value.GrantId);
            var retained = PurchaseRequestAssignmentIdentity.ReadVerified(value);
            var supplied = retained?.Authority;
            if (pin is null || !PurchaseRequestApprovalPinning.HasRetainedWorkerIdentity(pin)
                || request is null || pin.RequesterWorkerId != request.RequesterWorkerId || pin.MakerSubjectId != request.CreatedBy || grant is null || supplied is null
                || value.TenantId != scope.TenantId || value.LegalEntityId != scope.LegalEntityId
                || pin.CoreApprovalCaseId != value.CoreApprovalCaseId || value.CoreStageId == Guid.Empty
                || value.CoreAssignmentId == Guid.Empty || value.AssigneeKind is not ("LineManager" or "ProcurementManager")
                || value.AssigneeWorkerId == pin.MakerWorkerId || value.AssigneeWorkerId == pin.RequesterWorkerId
                || grant.PrincipalId != value.PrincipalId || grant.Version != value.GrantRevision
                || grant.AuthorityVersion != value.AuthorityVersion || supplied.EvaluatedAt != pin.PinnedAt
                || value.EvidenceSha256 != Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value.EvidenceJson))))
                throw new InvalidOperationException("ApprovalAssignmentAuthorityEvidenceInvalid");
            var evaluated = PurchaseRequestSpendingAuthority.Evaluate(request, value.PrincipalId,
                SpendingAuthorityConfiguration.ApprovalPermission, SpendingAuthorityConfiguration.ApprovalAction,
                pin.AmountBasis, pin.PinnedAt, [grant]);
            if (!evaluated.Eligible || evaluated.Evidence is null || supplied.RequestAmount != pin.EstimatedAmount
                || supplied.CurrencyCode != pin.CurrencyCode
                || JsonSerializer.Serialize(evaluated.Evidence) != JsonSerializer.Serialize(supplied))
                throw new InvalidOperationException("ApprovalAssignmentAuthorityEvidenceInvalid");
        }
        if (ChangeTracker.Entries<ProcurementApprovalCommandReceipt>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<PurchaseRequestAssignmentAuthorityEvidence>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Approval command and authority evidence are append-only.");
    }
}
