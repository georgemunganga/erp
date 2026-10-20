using Microsoft.EntityFrameworkCore;
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
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.SubmissionId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        evidence.HasOne<ProcurementSpendingAuthorityGrant>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.LegalEntityId, x.GrantId })
            .HasPrincipalKey(x => new { x.TenantId, x.LegalEntityId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
    private void GuardApprovalExecution()
    {
        foreach (var entry in ChangeTracker.Entries<ProcurementApprovalCommandReceipt>().Where(x => x.State == EntityState.Added))
            if (entry.Entity.ActorSubjectId != scope.SubjectId || entry.Entity.CommandKey == Guid.Empty
                || entry.Entity.CommandFingerprint.Length != 64 || entry.Entity.CompletedAt == default
                || entry.Entity.Operation is not ("Open" or "Decide" or "Release"))
                throw new InvalidOperationException("Approval command receipt is not bound to the current actor and operation.");
        if (ChangeTracker.Entries<ProcurementApprovalCommandReceipt>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<PurchaseRequestAssignmentAuthorityEvidence>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Approval command and authority evidence are append-only.");
    }
}
