using System.Linq.Expressions;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Core-account ownership. HRMS employee/job metadata grants no access.</summary>
public static class PurchaseRequestOwnership
{
    public static Expression<Func<PurchaseRequest, bool>> OwnedBy(Guid? principalId)
    {
        if (principalId is not { } principal || principal == Guid.Empty) return request => false;
        var canonicalSubject = principal.ToString("D");
        // Exact historical attribution is a runtime compatibility projection, never a
        // persisted principal/employee backfill. Unknown/noncanonical legacy IDs stay hidden.
        return request => request.RequesterPrincipalId == principal
            || request.RequesterPrincipalId == null && request.CreatedBy == canonicalSubject;
    }

    public static bool IsOwner(PurchaseRequest request, Guid? principalId) => principalId is { } principal
        && principal != Guid.Empty && (request.RequesterPrincipalId == principal
            || request.RequesterPrincipalId is null && request.CreatedBy == principal.ToString("D"));
}
