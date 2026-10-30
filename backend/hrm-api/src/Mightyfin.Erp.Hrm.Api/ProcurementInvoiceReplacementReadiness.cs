using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Separate M6E physical and finite-role admission on the caller's
/// actual Procurement connection/transaction. The registered implementation
/// requires the verified native schema and finite-role privileges on every call.
/// Historical reads never depend on INSERT privileges or the new-work flag.</summary>
public interface IProcurementInvoiceReplacementSchemaReadiness
{
    Task<bool> CanReadAsync(ProcurementDbContext db, CancellationToken ct);
    Task<bool> CanRecordAsync(ProcurementDbContext db, CancellationToken ct);
}
