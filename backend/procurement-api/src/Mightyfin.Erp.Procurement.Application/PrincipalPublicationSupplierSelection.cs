using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalSupplierPage(IReadOnlyList<Supplier> Items, int Total,
    int Page, int PageSize);

/// <summary>Current qualification and orderability are checked before the
/// buyer-visible page or total is formed.</summary>
public static class PrincipalPublicationSupplierSelection
{
    public static PrincipalSupplierPage Page(IReadOnlyList<Supplier> boundedCandidates,
        DateTimeOffset now, int page, int pageSize = 25)
    {
        if (boundedCandidates is null || boundedCandidates.Count > 500
            || now == default || page is < 1 or > 20 || pageSize is < 1 or > 25)
            throw new InvalidOperationException("SourcingSupplierSearchTooBroad");
        var eligible = boundedCandidates.Where(x => SupplierEligibility.CanReceiveNewOrders(x, now))
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id).ToArray();
        return new(Array.AsReadOnly(eligible.Skip((page - 1) * pageSize)
            .Take(pageSize).ToArray()), eligible.Length, page, pageSize);
    }
}
