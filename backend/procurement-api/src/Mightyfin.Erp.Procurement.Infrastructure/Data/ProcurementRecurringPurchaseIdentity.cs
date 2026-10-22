using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardRecurringPurchaseIdentity()
    {
        foreach (var entry in ChangeTracker.Entries<RecurringPurchaseCommitment>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            var record = entry.Entity;
            if (record.OwnerPrincipalId == Guid.Empty || record.OwnerWorkerId == Guid.Empty
                || record.OwnerSubjectId != record.OwnerPrincipalId.ToString("D"))
                throw new InvalidOperationException("RecurringPurchaseIdentityInvalid");
            if (entry.State == EntityState.Added)
            {
                if (record.OwnerSubjectId != scope.SubjectId || record.OwnerWorkerId != scope.WorkerId)
                    throw new InvalidOperationException("RecurringPurchaseIdentityScopeMismatch");
                continue;
            }
            // OriginalValues can be fabricated by attaching a detached DTO. The scoped
            // persisted identity and CAS version are authoritative, including closed history.
            var stored = RecurringPurchaseCommitments.AsNoTracking().Where(x => x.Id == record.Id)
                .Select(x => new { x.OwnerPrincipalId, x.OwnerSubjectId, x.OwnerWorkerId, x.WorkLocationId, x.CreatedBy, x.Version })
                .SingleOrDefault();
            if (stored is null || stored.Version != entry.Property(x => x.Version).OriginalValue)
                throw new DbUpdateConcurrencyException("The recurring purchase changed or is outside this scope.");
            if (entry.Property(x => x.OwnerPrincipalId).OriginalValue != record.OwnerPrincipalId
                || entry.Property(x => x.OwnerSubjectId).OriginalValue != record.OwnerSubjectId
                || entry.Property(x => x.OwnerWorkerId).OriginalValue != record.OwnerWorkerId
                || entry.Property(x => x.WorkLocationId).OriginalValue != record.WorkLocationId
                || entry.Property(x => x.CreatedBy).OriginalValue != record.CreatedBy
                || stored.OwnerPrincipalId != record.OwnerPrincipalId || stored.OwnerSubjectId != record.OwnerSubjectId
                || stored.OwnerWorkerId != record.OwnerWorkerId || stored.WorkLocationId != record.WorkLocationId
                || stored.CreatedBy != record.CreatedBy)
                throw new InvalidOperationException("RecurringPurchaseIdentityImmutable");
        }
    }
}
