using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

/// <summary>Current typed target-PO claims and complete company inventory from one
/// serializable snapshot. This is evidence for the owner factory, not an owner lease.</summary>
public sealed record PrincipalInvoiceCurrentClaimRead(
    InvoiceProcurementInventorySummary Inventory,
    IReadOnlyList<PrincipalInvoiceAllocationSourceInput> ActiveClaims,
    IReadOnlyList<PrincipalInvoiceAcceptedHistory> RetainedSources);

public sealed partial class ProcurementDbContext
{
    public async Task<PrincipalInvoiceCurrentClaimRead> ReadPrincipalInvoiceCurrentClaimsAsync(
        PrincipalInvoiceIssuedWitness witness, CancellationToken ct)
    {
        if (!Database.IsNpgsql() || Database.CurrentTransaction is null
            || Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || witness is null || witness.TenantId != scope.TenantId
            || witness.LegalEntityId != scope.LegalEntityId || witness.BranchId == Guid.Empty
            || witness.PurchaseOrderId == Guid.Empty || witness.IssuedVersionId == Guid.Empty
            || witness.CoreApprovalCaseId == Guid.Empty || witness.SupplierId == Guid.Empty
            || string.IsNullOrWhiteSpace(witness.CurrencyCode) || witness.Sources is null)
            throw new InvalidOperationException("PrincipalInvoiceCurrentClaimTransactionRequired");
        var purchaseOrderId = witness.PurchaseOrderId;
        var issuedVersionId = witness.IssuedVersionId;
        var supplierId = witness.SupplierId;
        var currencyCode = witness.CurrencyCode;
        var order = await PurchaseOrders.AsNoTracking().SingleAsync(x => x.Id == purchaseOrderId, ct);
        var issued = await PrincipalOrderIssuedVersions.AsNoTracking()
            .SingleAsync(x => x.Id == issuedVersionId, ct);
        if (order.PrincipalDraftSchemaVersion != 2 || order.Status != "Issued"
            || order.BranchRef != witness.BranchId || order.SupplierId != supplierId
            || order.CurrencyCode != currencyCode || issued.PurchaseOrderId != order.Id
            || issued.CoreApprovalCaseId != witness.CoreApprovalCaseId
            || issued.SchemaVersion != 2 || issued.CommercialRevision != 1)
            throw new InvalidOperationException("PrincipalInvoiceIssuedWitnessChanged");

        // CaptureInventory sets row_security=off on this transaction. Any RLS policy
        // that could hide rows then raises an error instead of reducing this set.
        var capture = await CaptureInvoiceOwnerInventoryAsync(ct);
        var summary = InvoiceProcurementInventoryEvidence.Summarize(capture);
        var invoices = await SupplierInvoices.AsNoTracking().ToArrayAsync(ct);
        var lines = await SupplierInvoiceLines.AsNoTracking().ToArrayAsync(ct);
        var mappings = await InvoiceMatchMappingVersions.AsNoTracking().ToArrayAsync(ct);
        var results = await InvoiceMatchResults.AsNoTracking().ToArrayAsync(ct);
        var allocations = await InvoiceMatchAllocations.AsNoTracking().ToArrayAsync(ct);
        var exposures = await InvoiceMatchExposureStates.AsNoTracking().ToArrayAsync(ct);

        bool SameIds(string table, IEnumerable<ProcurementRecord> typed)
        {
            var rows = capture.Tables.Single(x => x.TableName == table).Rows;
            return typed.All(x => x.TenantId == scope.TenantId && x.LegalEntityId == scope.LegalEntityId)
                && rows.Select(x => x.Id).Order().SequenceEqual(typed.Select(x => x.Id).Order());
        }
        if (!SameIds("supplier_invoices", invoices)
            || !SameIds("supplier_invoice_lines", lines)
            || !SameIds("invoice_match_mapping_versions", mappings)
            || !SameIds("invoice_match_results", results)
            || !SameIds("invoice_match_allocations", allocations)
            || !SameIds("invoice_match_exposure_states", exposures))
            throw new InvalidOperationException("PrincipalInvoiceCurrentTypedInventoryIncomplete");

        var invoiceById = invoices.ToDictionary(x => x.Id);
        var lineById = lines.ToDictionary(x => x.Id);
        var resultById = results.ToDictionary(x => x.Id);
        var mappingById = mappings.ToDictionary(x => x.Id);
        var exposureById = exposures.ToDictionary(x => x.Id);
        var linkedInvoiceIds = invoices.Where(x => x.PurchaseOrderId == purchaseOrderId)
            .Select(x => x.Id)
            .Concat(mappings.Where(x => x.PurchaseOrderId == purchaseOrderId).Select(x => x.SupplierInvoiceId))
            .Concat(results.Where(x => x.PurchaseOrderId == purchaseOrderId).Select(x => x.SupplierInvoiceId))
            .Concat(allocations.Where(x => x.PurchaseOrderId == purchaseOrderId).Select(x => x.SupplierInvoiceId))
            .Distinct().ToArray();
        foreach (var invoiceId in linkedInvoiceIds)
        {
            if (!invoiceById.TryGetValue(invoiceId, out var linked))
                throw new InvalidOperationException("PrincipalInvoiceLinkedInvoiceMissing");
            if (linked.ActiveMatchClaimResultId is not { } activeResultId) continue;
            if (!resultById.TryGetValue(activeResultId, out var activeResult)
                || activeResult.PurchaseOrderId != purchaseOrderId
                || allocations.All(x => x.MatchResultId != activeResultId
                    || x.PurchaseOrderId != purchaseOrderId))
                throw new InvalidOperationException("PrincipalInvoiceTargetClaimRequiresReconciliation");
        }
        foreach (var exposure in exposures.Where(x => x.PurchaseOrderId == purchaseOrderId))
        {
            var retained = allocations.Where(x => x.ExposureStateId == exposure.Id).ToArray();
            var sequences = retained.Select(x => x.ExposureSequence).Distinct().Order().ToArray();
            if (exposure.IssuedVersionId != issuedVersionId
                || exposure.SupplierId != supplierId || exposure.CurrencyCode != currencyCode
                || retained.Any(x => x.PurchaseOrderId != purchaseOrderId
                    || x.IssuedVersionId != issuedVersionId)
                || sequences.LongLength != exposure.Sequence
                || sequences.Where((value, index) => value != index + 1L).Any())
                throw new InvalidOperationException("PrincipalInvoiceTargetExposureSequenceIncomplete");
        }
        var active = new List<PrincipalInvoiceAllocationSourceInput>();
        var orderLines = await PurchaseOrderLines.AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId).ToArrayAsync(ct);
        var orderLineById = orderLines.ToDictionary(x => x.Id);
        foreach (var allocation in allocations.Where(x => x.PurchaseOrderId == purchaseOrderId))
        {
            if (allocation.IssuedVersionId != issuedVersionId
                || !invoiceById.TryGetValue(allocation.SupplierInvoiceId, out var invoice)
                || !lineById.TryGetValue(allocation.SupplierInvoiceLineId, out var line)
                || !resultById.TryGetValue(allocation.MatchResultId, out var result)
                || result.MappingVersionId is not { } mappingId
                || !mappingById.TryGetValue(mappingId, out var mapping)
                || !exposureById.TryGetValue(allocation.ExposureStateId, out var exposure)
                || line.SupplierInvoiceId != invoice.Id
                || exposure.PurchaseOrderId != purchaseOrderId
                || exposure.IssuedVersionId != issuedVersionId
                || exposure.SupplierId != supplierId || exposure.CurrencyCode != currencyCode)
                throw new InvalidOperationException("PrincipalInvoiceCurrentClaimUnclassifiable");
            if (invoice.ActiveMatchClaimResultId != result.Id) continue;
            if (allocation.SupplierId != supplierId || allocation.CurrencyCode != currencyCode
                || invoice.SupplierId != supplierId || invoice.CurrencyCode != currencyCode
                || mapping.SourceSchemaVersion != 3 || mapping.IdentitySchemaVersion != 2
                || allocation.Kind != "Debit" || allocation.OriginalDebitId is not null)
                throw new InvalidOperationException("PrincipalInvoiceTargetClaimRequiresReconciliation");
            if (!orderLineById.TryGetValue(allocation.PurchaseOrderLineId, out var issuedLine)
                || issuedLine.TenantId != scope.TenantId || issuedLine.LegalEntityId != scope.LegalEntityId)
                throw new InvalidOperationException("PrincipalInvoiceIssuedLineUnavailable");
            var snapshot = PrincipalInvoiceMappingEvidence.VerifyStored(mapping, invoice,
                lines.Where(x => x.SupplierInvoiceId == invoice.Id).ToArray());
            var mapped = snapshot.Lines.SingleOrDefault(x => x.InvoiceLineId == allocation.SupplierInvoiceLineId
                && x.PurchaseOrderLineId == allocation.PurchaseOrderLineId);
            if (mapped is null) throw new InvalidOperationException("PrincipalInvoiceClaimMappingLineChanged");
            if (mapped.InvoiceUnitCode != issuedLine.UnitCode || allocation.UnitCode != issuedLine.UnitCode)
                throw new InvalidOperationException("PrincipalInvoiceUnitConversionReviewRequired");
            active.Add(new(allocation, result, mapping, invoice,
                lines.Where(x => x.SupplierInvoiceId == invoice.Id).ToArray()));
        }
        // The Host must reconstruct every target PO M5 source through the retained
        // issued/Core owner in this same transaction. A stored source JSON is not
        // an independent witness. The exact physical source ID set is checked here.
        var physicalIds = await PrincipalReceivingSourceStates.AsNoTracking()
            .Where(x => x.PurchaseOrderId == purchaseOrderId)
            .Select(x => x.Id).ToArrayAsync(ct);
        if (physicalIds.Length > 200 || physicalIds.Distinct().Count() != physicalIds.Length
            || !physicalIds.Order().SequenceEqual(witness.Sources.Keys.Order()))
            throw new InvalidOperationException("PrincipalInvoiceIssuedSourceSetIncomplete");
        var histories = await LoadPrincipalInvoiceAcceptedSourcesAsync(witness.Sources, ct);
        var selectedIds = active.Where(x => x.Allocation.PrincipalReceivingSourceStateId.HasValue)
            .Select(x => x.Allocation.PrincipalReceivingSourceStateId!.Value).ToHashSet();
        PrincipalInvoiceAllocationSourceEvidence.VerifySet(active,
            histories.Where(x => selectedIds.Contains(x.SourceStateId)).ToArray());
        return new(summary, active, histories);
    }
}
