using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardPurchaseRequestIdentity()
    {
        foreach (var entry in ChangeTracker.Entries<PurchaseRequest>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity.RequesterPrincipalId == Guid.Empty || entry.Entity.RequesterWorkerId == Guid.Empty
                || entry.Entity.RequesterPrincipalId is null && entry.Entity.RequesterWorkerId is null)
                throw new InvalidOperationException("PurchaseRequestIdentityInvalid");
            if (entry.State == EntityState.Modified)
            {
                // EF OriginalValues are caller-controlled for detached graphs. Compare the
                // real persisted scoped owner before trusting those values or recording audit.
                var persisted = PurchaseRequests.AsNoTracking().Where(x => x.Id == entry.Entity.Id)
                    .Select(x => new { x.RequesterPrincipalId, x.RequesterWorkerId, x.CreatedBy, x.Version })
                    .SingleOrDefault();
                if (persisted is null || persisted.Version != entry.Property(x => x.Version).OriginalValue)
                    throw new DbUpdateConcurrencyException("The purchase request changed or is outside this scope.");
                if (entry.Property(x => x.RequesterPrincipalId).OriginalValue != entry.Entity.RequesterPrincipalId
                    || entry.Property(x => x.RequesterWorkerId).OriginalValue != entry.Entity.RequesterWorkerId
                    || entry.Property(x => x.CreatedBy).OriginalValue != entry.Entity.CreatedBy
                    || persisted.RequesterPrincipalId != entry.Entity.RequesterPrincipalId
                    || persisted.RequesterWorkerId != entry.Entity.RequesterWorkerId
                    || persisted.CreatedBy != entry.Entity.CreatedBy)
                    throw new InvalidOperationException("PurchaseRequestIdentityImmutable");
            }
            if (entry.State == EntityState.Added && entry.Entity.RequesterPrincipalId is { } principal
                && (!Guid.TryParseExact(scope.SubjectId, "D", out var actor) || principal != actor))
                throw new InvalidOperationException("PurchaseRequestPrincipalScopeMismatch");
        }
        // Existing employee-specific evidence remains usable only with a real worker
        // and principal metadata exactly equivalent to the already-pinned creator.
        // Enforce the boundary for owning-context callers as well as HTTP commands.
        var operationalSources = ChangeTracker.Entries<PurchaseRequestApprovalSubmission>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity.PurchaseRequestId)
            .Concat(ChangeTracker.Entries<BudgetCheckSnapshot>().Where(x => x.State == EntityState.Added)
                .Select(x => x.Entity.PurchaseRequestId)).Distinct().ToArray();
        foreach (var sourceId in operationalSources)
        {
            var source = PurchaseRequests.Local.SingleOrDefault(x => x.Id == sourceId)
                ?? PurchaseRequests.AsNoTracking().SingleOrDefault(x => x.Id == sourceId);
            if (source?.RequesterPrincipalId is not null && !PurchaseRequestOwnership.HasLegacyWorkerEvidenceIdentity(source))
                throw new InvalidOperationException("PrincipalOperationalEvidenceRequired");
        }
    }
}
