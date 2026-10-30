using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Separate M6E physical and finite-role admission on the caller's
/// actual Procurement connection/transaction. No implementation is registered
/// until genuine EF/native schema and privilege captures have been reviewed.
/// Historical reads never depend on INSERT privileges or the new-work flag.</summary>
public interface IProcurementInvoiceReplacementSchemaReadiness
{
    Task<bool> CanReadAsync(ProcurementDbContext db, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext db, CancellationToken ct);
}
