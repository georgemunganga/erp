using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Version dispatch for original-document unit review only. The normalized
/// links do not turn a principal receiving fact into a legacy acceptance or
/// establish invoice capacity. Comparison and claims need their own source path.</summary>
public static class InvoiceMappingReviewSourceDispatch
{
    public static InvoiceMappingSnapshot Verify(InvoiceMatchMappingVersion mapping,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (mapping.SourceSchemaVersion is null)
            return InvoiceMappingEvidence.Verify(mapping, invoice, originalLines);
        if (mapping.SourceSchemaVersion != 3)
            throw new InvalidOperationException("InvoiceMappingSourceSchemaUnavailable");
        var principal = PrincipalInvoiceMappingEvidence.VerifyStored(mapping, invoice, originalLines);
        return new InvoiceMappingSnapshot(3, principal.InvoiceId, principal.PurchaseOrderId,
            principal.IssuedVersionId, principal.Revision, principal.InvoiceOriginalSha256,
            principal.Lines.Select(x => new InvoiceMappingLine(x.InvoiceLineId,
                x.PurchaseOrderLineId, null, null, x.InvoiceUnitCode,
                x.DocumentVersionId, x.DocumentLineReference)).ToArray())
        {
            ActorPrincipalId = principal.ActorPrincipalId,
            ActorWorkerId = principal.ActorWorkerId
        };
    }
}
