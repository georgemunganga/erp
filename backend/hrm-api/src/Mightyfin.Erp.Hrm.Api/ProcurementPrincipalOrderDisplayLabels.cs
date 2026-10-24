using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalOrderDraftRoutes
{
    private sealed record OrderDisplayLabels(string SupplierDisplayName,string BranchDisplayName);

    // Only the supplier and branch already authorized through this purpose read
    // are resolved. Historical names remain visible after supplier suspension;
    // they are labels, never a fresh eligibility or financial authority signal.
    private static async Task<OrderDisplayLabels> Labels(ProcurementDbContext db,HrmDbContext core,
        PurchaseOrder order,ProcurementRequestScope scope,CancellationToken ct)
    {
        var supplier=await db.Suppliers.AsNoTracking().Where(x=>x.Id==order.SupplierId)
            .Select(x=>x.DisplayName).SingleOrDefaultAsync(ct);
        var branch=order.BranchRef is { } branchId
            ?await core.WorkLocations.AsNoTracking().Where(x=>x.Id==branchId
                &&x.LegalEntityId==scope.LegalEntityId&&!x.IsArchived)
                .Select(x=>x.Name).SingleOrDefaultAsync(ct):null;
        return new(supplier??"Supplier record unavailable",branch??"Branch record unavailable");
    }
}
