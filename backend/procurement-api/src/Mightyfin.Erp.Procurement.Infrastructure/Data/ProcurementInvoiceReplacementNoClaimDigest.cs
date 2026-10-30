using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    private string ReplacementNoClaimDigest(SupplierInvoice source,
        InvoiceMatchMappingVersion mapping, InvoiceMatchResult blocked,
        InvoiceUnitReviewSource reviewSource, InvoiceUnitReviewOutcome reviewOutcome,
        IReadOnlyList<Guid> exceptionIds)
    {
        if (blocked.CommandKey is not { } key || key == Guid.Empty
            || blocked.PurchaseOrderId is not { } orderId || orderId == Guid.Empty)
            throw new InvalidOperationException("InvoiceReplacementCompareKeyMissing");
        var keyText = key.ToString("D");
        var compareReceipt = CommercialCommandReceipts.AsNoTracking().Single(x =>
            x.Operation == "InvoiceCompare" && x.CommandKey == key && x.PurchaseOrderId == orderId);
        var comparedAudit = AuditEvents.AsNoTracking().Single(x =>
            x.RecordType == nameof(SupplierInvoice) && x.RecordId == source.Id
            && x.Action == "PrincipalInvoiceCompared" && x.CorrelationId == keyText);
        var comparedOutbox = OutboxEvents.AsNoTracking().Single(x =>
            x.AggregateType == nameof(SupplierInvoice) && x.AggregateId == source.Id
            && x.EventType == "supplier_invoice.compared.v3" && x.IdempotencyKey == keyText);
        return InvoiceReplacementEvidence.NoClaimProofDigest(source, mapping, blocked,
            reviewSource, reviewOutcome, exceptionIds, compareReceipt,
            comparedAudit, comparedOutbox);
    }
}
