using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Infrastructure.Approvals;
using Mightyfin.Erp.Procurement.Infrastructure.Documents;
namespace Mightyfin.Erp.Hrm.Api;
internal static class ProcurementInvoiceUnitReviewCapability
{
    internal static async Task<bool> ReadyAsync(IServiceProvider services, CancellationToken ct)
    {
        var options = services.GetService<ProcurementInvoiceUnitReviewOptions>();
        var schema = services.GetService<IProcurementInvoiceUnitReviewSchemaReadiness>();
        if (options is not { Enabled: true, ExecutionDatabaseConfigured: true } || schema is null
            || services.GetService<CoreDocumentFeature>() is not { Enabled: true }) return false;
        var configuration = services.GetRequiredService<IConfiguration>();
        var execution = configuration.GetConnectionString("ProcurementApprovalExecution");
        var documents = configuration.GetConnectionString("CoreDocuments");
        if (string.IsNullOrWhiteSpace(execution) || !InvoiceDocumentStoreBinding.SamePhysicalDatabase(documents, execution)
            || !await services.GetRequiredService<IDocumentSchemaReadiness>().IsReadyAsync(ct)) return false;
        // Commands use this explicit finite connection/transaction. A default read-store
        // context (possibly another role/database) must never advertise its authority.
        // This callback attaches no records; the coordinator commits no business changes.
        return await services.GetRequiredService<ProcurementApprovalTransaction>().ExecuteAsync(async (owner, _, token) =>
            InvoiceDocumentStoreBinding.SamePhysicalDatabase(documents, owner.Database.GetDbConnection().ConnectionString)
                && await schema.CanRecordAsync(owner, token)
                && await ProcurementInvoiceReviewDocumentReadiness.ReadyAsync(owner,
                    services.GetRequiredService<ProcurementRequestScope>(), true, false, token), ct);
    }
}
