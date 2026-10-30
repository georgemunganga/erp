namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Separate M6E physical and finite-role admission. No implementation is
/// registered until genuine EF/native schema and privilege captures have been
/// reviewed. Missing registration fails closed for reads and writes alike.</summary>
public interface IProcurementInvoiceReplacementSchemaReadiness
{
    Task<bool> IsReadyAsync(CancellationToken ct);
}
