using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private void GuardPurchaseRequestIdentity()
    {
        foreach (var entry in ChangeTracker.Entries<PurchaseRequest>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity.RequesterPrincipalId == Guid.Empty || entry.Entity.RequesterWorkerId == Guid.Empty)
                throw new InvalidOperationException("PurchaseRequestIdentityInvalid");
            if (entry.State == EntityState.Modified
                && (entry.Property(x => x.RequesterPrincipalId).OriginalValue != entry.Entity.RequesterPrincipalId
                    || entry.Property(x => x.RequesterWorkerId).OriginalValue != entry.Entity.RequesterWorkerId
                    || entry.Property(x => x.CreatedBy).OriginalValue != entry.Entity.CreatedBy))
                throw new InvalidOperationException("PurchaseRequestIdentityImmutable");
            if (entry.State == EntityState.Added && entry.Entity.RequesterPrincipalId is { } principal
                && (!Guid.TryParseExact(scope.SubjectId, "D", out var actor) || principal != actor))
                throw new InvalidOperationException("PurchaseRequestPrincipalScopeMismatch");
        }
        // Existing operational evidence schemas bind employees, not Core request owners.
        // Enforce the boundary for owning-context callers as well as HTTP commands.
        var operationalSources = ChangeTracker.Entries<PurchaseRequestApprovalSubmission>()
            .Where(x => x.State == EntityState.Added).Select(x => x.Entity.PurchaseRequestId)
            .Concat(ChangeTracker.Entries<BudgetCheckSnapshot>().Where(x => x.State == EntityState.Added)
                .Select(x => x.Entity.PurchaseRequestId)).Distinct().ToArray();
        foreach (var sourceId in operationalSources)
        {
            var source = PurchaseRequests.Local.SingleOrDefault(x => x.Id == sourceId)
                ?? PurchaseRequests.AsNoTracking().SingleOrDefault(x => x.Id == sourceId);
            if (source?.RequesterPrincipalId is not null)
                throw new InvalidOperationException("PrincipalOperationalEvidenceRequired");
        }
    }
}
