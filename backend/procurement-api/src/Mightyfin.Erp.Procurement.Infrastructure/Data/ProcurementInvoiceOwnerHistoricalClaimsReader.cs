using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    /// <summary>Read retained claims in the same serializable snapshot as the
    /// complete inventory. No row is treated as active merely because it exists.</summary>
    private async Task<IReadOnlyList<InvoiceMatchConsumption>> CaptureInitialInvoiceClaimsAsync(
        InvoiceProcurementInventoryCapture capture, CancellationToken ct)
    {
        var invoices=await SupplierInvoices.AsNoTracking().ToListAsync(ct);
        var lines=await SupplierInvoiceLines.AsNoTracking().ToListAsync(ct);
        var mappings=await InvoiceMatchMappingVersions.AsNoTracking().ToListAsync(ct);
        var results=await InvoiceMatchResults.AsNoTracking().ToListAsync(ct);
        var allocations=await InvoiceMatchAllocations.AsNoTracking().ToListAsync(ct);
        var exposures=await InvoiceMatchExposureStates.AsNoTracking().ToListAsync(ct);
        return InvoiceProcurementHistoricalClaims.Verify(capture,invoices,lines,mappings,results,allocations,exposures);
    }
}
