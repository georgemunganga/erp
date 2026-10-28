using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Validates the old active debit graph against the complete captured
/// row set. This is baseline evidence, not a current qualified owner lease.</summary>
public static class InvoiceProcurementHistoricalClaims
{
    public static IReadOnlyList<InvoiceMatchConsumption> Verify(
        InvoiceProcurementInventoryCapture capture,
        IReadOnlyList<SupplierInvoice> invoices,
        IReadOnlyList<SupplierInvoiceLine> lines,
        IReadOnlyList<InvoiceMatchMappingVersion> mappings,
        IReadOnlyList<InvoiceMatchResult> results,
        IReadOnlyList<InvoiceMatchAllocation> allocations,
        IReadOnlyList<InvoiceMatchExposureState> exposures)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(exposures);
        InvoiceProcurementInventoryEvidence.Summarize(capture);
        static bool SameIds(InvoiceProcurementInventoryCapture source,string table,IEnumerable<Guid> ids)
            => source.Tables.Single(x=>x.TableName==table).Rows.Select(x=>x.Id).Order()
                .SequenceEqual(ids.Order());
        bool Scoped(IEnumerable<ProcurementRecord> rows) => rows.All(x=>x.TenantId==capture.TenantId
            && x.LegalEntityId==capture.LegalEntityId);
        if(!Scoped(invoices) || !Scoped(lines) || !Scoped(mappings) || !Scoped(results)
            || !Scoped(allocations) || !Scoped(exposures)
            || !SameIds(capture,"supplier_invoices",invoices.Select(x=>x.Id))
            || !SameIds(capture,"supplier_invoice_lines",lines.Select(x=>x.Id))
            || !SameIds(capture,"invoice_match_mapping_versions",mappings.Select(x=>x.Id))
            || !SameIds(capture,"invoice_match_results",results.Select(x=>x.Id))
            || !SameIds(capture,"invoice_match_allocations",allocations.Select(x=>x.Id))
            || !SameIds(capture,"invoice_match_exposure_states",exposures.Select(x=>x.Id)))
            throw new InvalidOperationException("InvoiceOwnerTypedInventoryChanged");
        var invoiceById=invoices.ToDictionary(x=>x.Id);
        var lineById=lines.ToDictionary(x=>x.Id);
        var mappingById=mappings.ToDictionary(x=>x.Id);
        var resultById=results.ToDictionary(x=>x.Id);
        var exposureById=exposures.ToDictionary(x=>x.Id);
        var active=new List<InvoiceMatchConsumption>(allocations.Count);
        foreach(var allocation in allocations)
        {
            if(!invoiceById.TryGetValue(allocation.SupplierInvoiceId,out var invoice)
                || !lineById.TryGetValue(allocation.SupplierInvoiceLineId,out var line)
                || !resultById.TryGetValue(allocation.MatchResultId,out var result)
                || result.MappingVersionId is not { } mappingId
                || !mappingById.TryGetValue(mappingId,out var mapping)
                || !exposureById.TryGetValue(allocation.ExposureStateId,out var exposure)
                || allocation.Kind!="Debit" || allocation.OriginalDebitId is not null
                || allocation.Quantity<=0 || allocation.NetAmount<0 || allocation.TaxAmount<0
                || allocation.ExposureSequence<1 || allocation.ExposureSequence>exposure.Sequence
                || invoice.ActiveMatchClaimResultId!=result.Id
                || !line.IsActive || line.SupplierInvoiceId!=invoice.Id
                || line.PurchaseOrderLineId!=allocation.PurchaseOrderLineId
                || result.Outcome!="Compared" || result.ExecutionVersion is not (1 or 2)
                || result.SupplierInvoiceId!=invoice.Id || mapping.SupplierInvoiceId!=invoice.Id
                || invoice.CurrentMappingVersionId!=mapping.Id
                || result.PurchaseOrderId!=allocation.PurchaseOrderId
                || result.IssuedVersionId!=allocation.IssuedVersionId
                || mapping.PurchaseOrderId!=allocation.PurchaseOrderId
                || mapping.IssuedVersionId!=allocation.IssuedVersionId
                || exposure.PurchaseOrderId!=allocation.PurchaseOrderId
                || exposure.IssuedVersionId!=allocation.IssuedVersionId
                || exposure.SupplierId!=allocation.SupplierId
                || exposure.CurrencyCode!=allocation.CurrencyCode
                || invoice.SupplierId!=allocation.SupplierId
                || invoice.CurrencyCode!=allocation.CurrencyCode
                || string.IsNullOrWhiteSpace(allocation.OwnerCoverageReference))
                throw new InvalidOperationException("InvoiceOwnerHistoricalClaimUnclassifiable");
            active.Add(InvoiceMatchExecutionEvidence.Consumption(allocation,invoice.BranchRef));
        }
        if(active.Select(x=>x.Id).Distinct().Count()!=active.Count
            || active.Select(x=>(x.InvoiceId,x.InvoiceLineId)).Distinct().Count()!=active.Count)
            throw new InvalidOperationException("InvoiceOwnerHistoricalClaimOverlap");
        foreach(var exposure in exposures)
        {
            var group=allocations.Where(x=>x.ExposureStateId==exposure.Id).ToArray();
            var groupIds=group.Select(x=>x.Id).ToHashSet();
            if(group.Length==0 && exposure.Sequence!=0)
                throw new InvalidOperationException("InvoiceOwnerHistoricalSequenceGap");
            InvoiceProcurementHistoricalOwnerContract.VerifyHistoricalSequence(exposure.Sequence,
                group.Select(x=>new InvoiceProcurementClaimCursor(x.Id,x.ExposureSequence,true)).ToArray(),
                active.Where(x=>groupIds.Contains(x.Id)).ToArray());
        }
        return active;
    }
}
