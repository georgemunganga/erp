using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Core.Approvals.Infrastructure;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Purpose-only, read-only schema-2 source bridge. The old issued and
/// receiving tables retain their own reader and keys. A projection is not a
/// mapped invoice, billing-capacity claim, AP approval or Finance posting.</summary>
internal static class ProcurementPrincipalInvoiceSourceBridge
{
    internal sealed record Verified(InvoiceIssuedOrder Order,
        IReadOnlyList<InvoiceAcceptedFulfilment> Acceptances,
        IReadOnlyDictionary<Guid, Guid> AcceptanceSourceStates);

    // Every caller composes search/count/page after this retained SQL + current
    // Core-branch fence. The full loader below is still required for each row.
    internal static IQueryable<PurchaseOrder> QueryBound(ProcurementDbContext db,
        ProcurementRequestScope scope, IReadOnlySet<Guid> currentBranches, SupplierInvoice invoice)
    {
        if (invoice.TenantId != scope.TenantId || invoice.LegalEntityId != scope.LegalEntityId
            || invoice.BranchRef is not { } branch || branch == Guid.Empty
            || !currentBranches.Contains(branch))
            throw new InvalidOperationException("PrincipalInvoiceCurrentScopeRequired");
        return ProcurementPrincipalOrderRetainedIssued.QueryBoundIssued(db, scope, currentBranches)
            .Where(x => x.BranchRef == branch && x.SupplierId == invoice.SupplierId
                && x.CurrencyCode == invoice.CurrencyCode
                && (invoice.PurchaseOrderId == null || x.Id == invoice.PurchaseOrderId));
    }

    internal static async Task<Verified> LoadAsync(ProcurementDbContext db, ApprovalDbContext cases,
        ProcurementRequestScope scope, IReadOnlySet<Guid> currentBranches, SupplierInvoice invoice,
        Guid orderId, Guid issuedId, CancellationToken ct)
    {
        if (orderId == Guid.Empty || issuedId == Guid.Empty
            || !await QueryBound(db, scope, currentBranches, invoice).AnyAsync(x => x.Id == orderId, ct))
            throw new InvalidOperationException("PrincipalInvoiceIssuedSourceUnavailable");
        var retained = await ProcurementPrincipalOrderRetainedIssued.LoadAsync(db, cases, scope,
            currentBranches, orderId, issuedId, null, false, ct);
        if (retained.Issued.Id != issuedId || retained.Order.SupplierId != invoice.SupplierId
            || retained.Order.CurrencyCode != invoice.CurrencyCode
            || retained.Order.BranchRef != invoice.BranchRef
            || retained.Lines.Count is < 1 or > 100)
            throw new InvalidOperationException("PrincipalInvoiceIssuedSourceChanged");

        var store = new PrincipalReceivingStore(db);
        // Full request/PO counters and unknown legacy histories must reconcile
        // even for a genuine two-way source with no acceptance state yet.
        foreach (var requestLineId in retained.Sources.Select(x => x.Binding.PurchaseRequestLineId).Distinct().Order())
            await store.ReconcileCountersAsync(requestLineId, ct);
        var accepted = new List<InvoiceAcceptedFulfilment>();
        var sourceStates = new Dictionary<Guid, Guid>();
        foreach (var line in retained.Lines)
        {
            var states = await db.PrincipalReceivingSourceStates.AsNoTracking()
                .Where(x => x.PurchaseOrderId == orderId && x.IssuedVersionId == issuedId
                    && x.PurchaseOrderLineId == line.Id).Take(2).ToArrayAsync(ct);
            if (states.Length > 1) throw new InvalidOperationException("PrincipalInvoiceReceivingSourceRepeated");
            if (states.Length == 0) continue;
            var state = states[0];
            var retainedLine = await ProcurementPrincipalOrderRetainedIssued.LoadAsync(db, cases, scope,
                currentBranches, orderId, issuedId, line.Id, false, ct);
            var source = PrincipalReceivingStorageCodec.Source(state);
            if (ProcurementPrincipalReceivingSource.ProjectRetained(retainedLine, source.Commercial.Kind) != source)
                throw new InvalidOperationException("PrincipalInvoiceReceivingOriginalChanged");
            var ledger = await store.LedgerAsync(state, ct);
            foreach (var policy in ledger.RetainedPolicies)
                _ = await ProcurementOperationalPolicyWorkerIdentity.VerifyPublicationAsync(db, scope,
                    policy, ct, OperationalPolicyPublicationMode.RetainedHistorical);
            foreach (var fact in PrincipalInvoiceSourceProjection.Accepted(state, ledger, line))
            {
                if (!sourceStates.TryAdd(fact.Id, state.Id))
                    throw new InvalidOperationException("PrincipalInvoiceAcceptanceRepeated");
                accepted.Add(fact);
            }
        }
        var byLine = retained.Sources.ToDictionary(x => x.OrderLine.Id);
        var issued = new InvoiceIssuedOrder(orderId, issuedId, retained.Issued.CommercialRevision,
            retained.Order.Version, retained.Order.TenantId, retained.Order.LegalEntityId,
            retained.Order.BranchRef, retained.Order.SupplierId, retained.Order.CurrencyCode,
            "Issued", retained.Issued.CommercialSnapshotSha256, retained.Acknowledgment.ObservedAt,
            retained.Lines.Select(line =>
            {
                if (!byLine.TryGetValue(line.Id, out var source)
                    || string.IsNullOrWhiteSpace(source.OriginalCategoryCode))
                    throw new InvalidOperationException("PrincipalInvoiceOriginalCategoryUnavailable");
                return new InvoiceIssuedOrderLine(line.Id, line.Version, source.OriginalCategoryCode,
                    line.UnitCode, line.Quantity, line.UnitPrice, line.TaxAmount);
            }).ToArray());
        return new(issued, accepted, sourceStates);
    }
}
