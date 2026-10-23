using System.Numerics;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalInvoiceComparisonLine(Guid InvoiceLineId, Guid PurchaseOrderLineId,
    Guid? PrincipalAcceptanceFactId, InvoiceMatchingMethod Method,
    string QuantityExact, string NetExact, string TaxExact);
public sealed record PrincipalInvoiceComparisonPreview(bool CanClaim,
    IReadOnlyList<InvoiceMatchDiscrepancy> Blockers,
    IReadOnlyList<PrincipalInvoiceComparisonLine> Lines,
    string? OwnerWatermark);

/// <summary>Typed PO2 arithmetic. The caller must load actual Core-reviewed
/// original units and authenticated, complete owner history. This pure result
/// never posts a claim or treats an unavailable owner as zero consumption.</summary>
public static class PrincipalInvoiceComparisonRules
{
    private sealed record Work(SupplierInvoiceLine Original, PrincipalInvoiceMappingInputLine Link,
        InvoiceIssuedOrderLine Order, InvoiceCategoryMatchRule Rule,
        BigInteger Quantity, BigInteger Net, BigInteger Tax);

    public static PrincipalInvoiceComparisonPreview Evaluate(SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originals,
        PrincipalInvoiceState state, PrincipalInvoiceMappingVersion mappingRow,
        PrincipalInvoiceMappingSnapshot mapping, InvoiceIssuedOrder issued,
        IReadOnlyList<InvoiceAcceptedFulfilment> accepted,
        IReadOnlyDictionary<Guid, Guid> acceptanceSourceStates,
        IReadOnlyDictionary<Guid, string> verifiedUnits,
        InvoiceMatchingPolicy policy, InvoiceHistoricalExposure owner,
        InvoiceExposureReadContext ownerContext, InvoiceConsumptionEvidence history,
        InvoiceDuplicateEvidence duplicates, DateTimeOffset now)
    {
        var errors = new List<InvoiceMatchDiscrepancy>();
        void Block(string code, Guid? id = null) => errors.Add(new(id, code));
        if (invoice.BranchRef is null || invoice.Status is not ("Received" or "MatchPending" or "Exception")
            || invoice.FinancePostingRef is not null || invoice.CreditForInvoiceRef is not null
            || state.SupplierInvoiceId != invoice.Id || state.TenantId != invoice.TenantId
            || state.LegalEntityId != invoice.LegalEntityId || state.BranchId != invoice.BranchRef
            || state.SupplierId != invoice.SupplierId || state.CurrencyCode != invoice.CurrencyCode
            || state.InvoiceVersion != invoice.Version || state.MappingRevision != mapping.Revision
            || state.CurrentMappingId != mappingRow.Id || state.ActiveClaimComparisonId is not null
            || state.State is not ("Reviewed" or "ComparisonBlocked")
            || mappingRow.SupplierInvoiceId != invoice.Id || mappingRow.MappingRevision != mapping.Revision
            || mappingRow.PurchaseOrderId != mapping.PurchaseOrderId
            || mappingRow.PrincipalIssuedVersionId != mapping.PrincipalIssuedVersionId
            || mappingRow.MappingSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(mappingRow.MappingJson)
            || invoice.Id != mapping.InvoiceId || mapping.SchemaVersion != 2
            || mapping.OriginalInvoiceSha256 != InvoiceMappingEvidence.OriginalDigest(invoice, originals)
            || invoice.PurchaseOrderId is { } invoiceOrder && invoiceOrder != mapping.PurchaseOrderId
            || issued.Id != mapping.PurchaseOrderId || issued.RevisionId != mapping.PrincipalIssuedVersionId
            || issued.RevisionState != "Issued" || issued.RevisionNumber != 1
            || issued.TenantId != invoice.TenantId || issued.LegalEntityId != invoice.LegalEntityId
            || issued.BranchRef != invoice.BranchRef || issued.SupplierId != invoice.SupplierId
            || issued.CurrencyCode != invoice.CurrencyCode || issued.IssuedReference != mapping.IssuedSnapshotSha256
            || issued.IssuedAt == default || issued.IssuedAt > now
            || issued.Lines.Count is < 1 or > 100
            || issued.Lines.Select(x => x.Id).Distinct().Count() != issued.Lines.Count
            || accepted.Select(x => x.Id).Distinct().Count() != accepted.Count)
            Block("PrincipalInvoiceSourceChanged");
        if (originals.Count is < 1 or > 200 || mapping.Lines.Count != originals.Count
            || originals.Select(x => x.Id).Distinct().Count() != originals.Count
            || mapping.Lines.Select(x => x.InvoiceLineId).Distinct().Count() != mapping.Lines.Count
            || !mapping.Lines.Select(x => x.InvoiceLineId).Order().SequenceEqual(originals.Select(x => x.Id).Order()))
            Block("PrincipalInvoiceOriginalLinesChanged");
        if (verifiedUnits is null || verifiedUnits.Count != mapping.Lines.Count
            || mapping.Lines.Any(x => !verifiedUnits.TryGetValue(x.InvoiceLineId, out var unit)
                || unit != x.InvoiceUnitCode))
            Block("OriginalInvoiceUnitsNotVerified");
        if (policy is null || policy.Id == Guid.Empty || policy.State != "Published"
            || string.IsNullOrWhiteSpace(policy.ApprovedReference)
            || policy.TenantId != invoice.TenantId || policy.LegalEntityId != invoice.LegalEntityId
            || policy.BranchRef is { } policyBranch && policyBranch != invoice.BranchRef
            || policy.EffectiveFrom > now || policy.EffectiveUntil is { } until && now >= until
            || policy.MoneyScale is < 0 or > 4 || !Enum.IsDefined(policy.Rounding)
            || policy.Rules.Count == 0
            || policy.Rules.Any(x => string.IsNullOrWhiteSpace(x.CategoryCode)
                || x.CurrencyCode != invoice.CurrencyCode || !Enum.IsDefined(x.Method)
                || x.Method == InvoiceMatchingMethod.ThreeWay && x.AcceptanceKind is null
                || x.Method == InvoiceMatchingMethod.TwoWay && x.AcceptanceKind is not null
                || new[] { x.Quantity, x.UnitPrice, x.Tax, x.Total }.Any(t => t is null
                    || !InvoiceMatchExact.Input(t.Absolute) || !InvoiceMatchExact.Input(t.Percent)
                    || t.Percent > 100 || !Enum.IsDefined(t.Combination))))
            Block("PublishedInvoiceMatchingPolicyUnavailable");
        if (ownerContext is null || ownerContext.TenantId != invoice.TenantId
            || ownerContext.LegalEntityId != invoice.LegalEntityId
            || ownerContext.BranchId != invoice.BranchRef
            || ownerContext.PurchaseOrderId != issued.Id
            || ownerContext.IssuedVersionId != issued.RevisionId
            || ownerContext.SupplierId != invoice.SupplierId
            || ownerContext.CurrencyCode != invoice.CurrencyCode
            || ownerContext.At != now
            || !InvoiceMatchExecutionEvidence.OwnerCovers(owner, ownerContext)
            || history is null || !history.Complete || history.Watermark is not { Length: 64 }
            || history.Allocations is null)
            Block("CompleteHistoricalExposureUnavailable");
        if (duplicates is null || !duplicates.Complete || string.IsNullOrWhiteSpace(duplicates.Watermark)
            || duplicates.Identities is null
            || duplicates.Identities.Select(x => x.InvoiceId).Distinct().Count() != duplicates.Identities.Count
            || duplicates.Identities.Any(x => x.TenantId != invoice.TenantId
                || x.LegalEntityId != invoice.LegalEntityId)
            || duplicates.Identities.Count(x => x.InvoiceId == invoice.Id
                && x.SupplierId == invoice.SupplierId && x.NormalizedInvoiceNumber == invoice.NormalizedInvoiceNumber
                && x.CurrencyCode == invoice.CurrencyCode) != 1
            || duplicates.Identities.Any(x => x.InvoiceId != invoice.Id && x.SupplierId == invoice.SupplierId
                && x.NormalizedInvoiceNumber == invoice.NormalizedInvoiceNumber && x.CurrencyCode == invoice.CurrencyCode))
            Block("InvoiceDuplicateEvidenceUnavailable");
        if (errors.Count != 0) return Preview([], errors, null);

        if (!InvoiceMatchExact.MoneyInput(invoice.NetAmount, policy)
            || !InvoiceMatchExact.MoneyInput(invoice.TaxAmount, policy)
            || !InvoiceMatchExact.MoneyInput(invoice.GrossAmount, policy))
            Block("InvoiceHeaderAmountInvalid");
        var orders = issued.Lines.ToDictionary(x => x.Id);
        var facts = accepted.ToDictionary(x => x.Id);
        var work = new List<Work>();
        foreach (var link in mapping.Lines)
        {
            var original = originals.Single(x => x.Id == link.InvoiceLineId);
            if (!orders.TryGetValue(link.PurchaseOrderLineId, out var order)
                || original.TenantId != invoice.TenantId || original.LegalEntityId != invoice.LegalEntityId
                || original.SupplierInvoiceId != invoice.Id || !original.IsActive
                || original.PurchaseOrderLineId is { } oldOrderLine && oldOrderLine != link.PurchaseOrderLineId
                || original.GoodsReceiptLineId is not null || original.ServiceAcceptanceId is not null
                || !InvoiceMatchExact.Input(original.Quantity, true)
                || !InvoiceMatchExact.Input(original.UnitPrice)
                || !InvoiceMatchExact.MoneyInput(original.TaxAmount, policy)
                || !InvoiceMatchExact.Input(order.Quantity, true)
                || !InvoiceMatchExact.Input(order.UnitPrice)
                || !InvoiceMatchExact.MoneyInput(order.TaxAmount, policy)
                || order.UnitCode != link.InvoiceUnitCode)
            { Block("PrincipalInvoiceLineBindingInvalid", link.InvoiceLineId); continue; }
            var rules = policy.Rules.Where(x => x.CategoryCode == order.CategoryCode
                && x.CurrencyCode == invoice.CurrencyCode).ToArray();
            if (rules.Length != 1) { Block("CategoryMatchPolicyMissingOrAmbiguous", link.InvoiceLineId); continue; }
            var rule = rules[0];
            if (rule.Method == InvoiceMatchingMethod.TwoWay
                    && (link.PrincipalAcceptanceFactId is not null || link.PrincipalReceivingSourceStateId is not null)
                || rule.Method == InvoiceMatchingMethod.ThreeWay
                    && (link.PrincipalAcceptanceFactId is not { } factId || !facts.TryGetValue(factId, out var fact)
                        || fact.PurchaseOrderId != issued.Id || fact.RevisionId != issued.RevisionId
                        || fact.PurchaseOrderLineId != order.Id || fact.UnitCode != order.UnitCode
                        || acceptanceSourceStates is null
                        || !acceptanceSourceStates.TryGetValue(factId, out var sourceStateId)
                        || sourceStateId != link.PrincipalReceivingSourceStateId
                        || fact.Version != link.AcceptanceSequence
                        || fact.Kind != rule.AcceptanceKind || fact.State != "Accepted"
                        || fact.Reversed || fact.ReturnedQuantity != 0 || fact.ReturnedNetAmount != 0
                        || fact.AcceptedQuantity <= 0 || fact.AcceptedAt > now))
            { Block("PrincipalInvoiceAcceptanceRequired", link.InvoiceLineId); continue; }
            var quantity = InvoiceMatchExact.Units(original.Quantity);
            var price = InvoiceMatchExact.Units(original.UnitPrice);
            var net = InvoiceMatchExact.ProductMoney(quantity, price, policy);
            var tax = InvoiceMatchExact.Units(original.TaxAmount);
            if (!InvoiceMatchExact.Within(price - InvoiceMatchExact.Units(order.UnitPrice),
                InvoiceMatchExact.Units(order.UnitPrice), rule.UnitPrice))
                Block("UnitPriceOutsideTolerance", link.InvoiceLineId);
            work.Add(new(original, link, order, rule, quantity, net, tax));
        }
        if (errors.Count != 0) return Preview(work, errors, history.Watermark);

        var prior = history.Allocations;
        if (prior.Select(x => x.Id).Distinct().Count() != prior.Count
            || prior.Any(x => x.Id == Guid.Empty || x.InvoiceId == invoice.Id
                || x.TenantId != invoice.TenantId || x.LegalEntityId != invoice.LegalEntityId
                || x.BranchRef != invoice.BranchRef || x.SupplierId != invoice.SupplierId
                || x.PurchaseOrderId != issued.Id || x.RevisionId != issued.RevisionId
                || x.CurrencyCode != invoice.CurrencyCode || !orders.ContainsKey(x.PurchaseOrderLineId)
                || x.AcceptanceId is { } priorFact && !facts.ContainsKey(priorFact)
                || !Enum.IsDefined(x.Kind) || string.IsNullOrWhiteSpace(x.ValidationReference)
                || !InvoiceMatchExact.Input(x.Quantity)
                // Retained claims were authorized under their frozen policy.
                // Reconstruct has already bound each debit to that policy and
                // original invoice. Only the invoice being compared must meet
                // the policy in force today.
                || !InvoiceMatchExact.Input(x.NetAmount)
                || !InvoiceMatchExact.Input(x.TaxAmount)
                || x.Kind == InvoiceConsumptionKind.Debit && (x.Quantity <= 0 || x.OriginalDebitId is not null)
                || x.Kind == InvoiceConsumptionKind.Credit && (x.OriginalDebitId is null
                    || x.Quantity == 0 && x.NetAmount == 0 && x.TaxAmount == 0)))
            Block("HistoricalConsumptionInvalid");
        if (errors.Count != 0) return Preview(work, errors, history.Watermark);
        // P31's authenticated owner currently reconstructs debit claims only.
        // A future typed credit owner must attest its original policy and
        // proportional amounts before credits may restore capacity. Applying
        // today's rounding policy to an older credit would invent that proof.
        if (prior.Any(x => x.Kind == InvoiceConsumptionKind.Credit))
            Block("HistoricalCreditOwnershipUnavailable");
        if (errors.Count != 0) return Preview(work, errors, history.Watermark);
        BigInteger Sum(IEnumerable<InvoiceMatchConsumption> rows, Func<InvoiceMatchConsumption, decimal> amount)
            => rows.Aggregate(BigInteger.Zero, (sum, x) => sum
                + InvoiceMatchExact.Units(amount(x)) * (x.Kind == InvoiceConsumptionKind.Debit ? 1 : -1));
        foreach (var group in work.GroupBy(x => x.Order.Id))
        {
            var order = group.First().Order;
            var rule = group.First().Rule;
            var historyLine = prior.Where(x => x.PurchaseOrderLineId == order.Id).ToArray();
            var currentQuantity = group.Aggregate(BigInteger.Zero, (v, x) => v + x.Quantity);
            var currentNet = group.Aggregate(BigInteger.Zero, (v, x) => v + x.Net);
            var currentTax = group.Aggregate(BigInteger.Zero, (v, x) => v + x.Tax);
            var quantity = Sum(historyLine, x => x.Quantity) + currentQuantity;
            var net = Sum(historyLine, x => x.NetAmount) + currentNet;
            var tax = Sum(historyLine, x => x.TaxAmount) + currentTax;
            var ordered = InvoiceMatchExact.Units(order.Quantity);
            if (quantity > ordered && !InvoiceMatchExact.Within(quantity - ordered, ordered, rule.Quantity))
                Block("CumulativeOrderQuantityOutsideTolerance", group.First().Original.Id);
            // The current policy checks this invoice's rounded amounts only.
            // The owner has already verified each retained debit against its
            // own frozen published policy; re-rounding all historic quantity
            // under today's scale changes valid historical commercial facts.
            var orderTax = InvoiceMatchExact.Units(order.TaxAmount);
            var expectedTax = group.Aggregate(BigInteger.Zero, (sum, line) => sum
                + InvoiceMatchExact.RatioMoney(orderTax, line.Quantity, ordered, policy));
            if (!InvoiceMatchExact.Within(currentTax - expectedTax, expectedTax, rule.Tax))
                Block("CurrentInvoiceTaxOutsideTolerance", group.First().Original.Id);
            // Each original invoice line has its own rounded amount. Summing
            // their exact saved results avoids re-rounding a combined quantity.
            var expectedGross = group.Aggregate(BigInteger.Zero, (sum, line) => sum + line.Net) + expectedTax;
            if (!InvoiceMatchExact.Within(currentNet + currentTax - expectedGross, expectedGross, rule.Total))
                Block("CurrentInvoiceTotalOutsideTolerance", group.First().Original.Id);
            // A separate source ceiling still prevents today's claim from
            // consuming more value than the issued order. Its reference uses
            // the unrounded eight-decimal product of issued 4dp quantity and
            // rate, independent of any later invoice rounding mode.
            var orderNet = BigInteger.DivRem(ordered * InvoiceMatchExact.Units(order.UnitPrice),
                new BigInteger(100_000_000), out var fractional);
            if (fractional != BigInteger.Zero)
                Block("PrincipalInvoiceLineBindingInvalid", group.First().Original.Id);
            var orderGross = orderNet + orderTax;
            if (tax > orderTax && !InvoiceMatchExact.Within(tax - orderTax, orderTax, rule.Tax))
                Block("CumulativeTaxOutsideTolerance", group.First().Original.Id);
            if (net + tax > orderGross && !InvoiceMatchExact.Within(net + tax - orderGross, orderGross, rule.Total))
                Block("CumulativeTotalOutsideTolerance", group.First().Original.Id);
            foreach (var factGroup in group.Where(x => x.Link.PrincipalAcceptanceFactId is not null)
                .GroupBy(x => x.Link.PrincipalAcceptanceFactId!.Value))
            {
                var fact = facts[factGroup.Key];
                var consumed = prior.Where(x => x.AcceptanceId == fact.Id).ToArray();
                var used = Sum(consumed, x => x.Quantity)
                    + factGroup.Aggregate(BigInteger.Zero, (v, x) => v + x.Quantity);
                if (used > InvoiceMatchExact.Units(fact.AcceptedQuantity))
                    Block("CumulativeAcceptedQuantityExceeded", factGroup.First().Original.Id);
                if (fact.Kind == InvoiceAcceptanceKind.Service
                    && Sum(consumed, x => x.NetAmount)
                        + factGroup.Aggregate(BigInteger.Zero, (v, x) => v + x.Net)
                        > InvoiceMatchExact.Units(fact.AcceptedNetAmount))
                    Block("CertifiedServiceValueExceeded", factGroup.First().Original.Id);
            }
        }
        if (work.Aggregate(BigInteger.Zero, (v, x) => v + x.Net) != InvoiceMatchExact.Units(invoice.NetAmount)
            || work.Aggregate(BigInteger.Zero, (v, x) => v + x.Tax) != InvoiceMatchExact.Units(invoice.TaxAmount)
            || InvoiceMatchExact.Units(invoice.NetAmount) + InvoiceMatchExact.Units(invoice.TaxAmount)
                != InvoiceMatchExact.Units(invoice.GrossAmount))
            Block("InvoiceHeaderLineTotalsMismatch");
        return Preview(work, errors, history.Watermark);

        static PrincipalInvoiceComparisonPreview Preview(IReadOnlyList<Work> work,
            IReadOnlyList<InvoiceMatchDiscrepancy> errors, string? ownerWatermark)
        {
            var lines = work.Select(x => new PrincipalInvoiceComparisonLine(x.Original.Id,
                x.Order.Id, x.Link.PrincipalAcceptanceFactId, x.Rule.Method,
                InvoiceMatchExact.Text(x.Quantity), InvoiceMatchExact.Text(x.Net),
                InvoiceMatchExact.Text(x.Tax))).ToArray();
            var blockers = errors.Distinct().OrderBy(x => x.InvoiceLineId)
                .ThenBy(x => x.Code, StringComparer.Ordinal).ToArray();
            return new(blockers.Length == 0, blockers, lines, ownerWatermark);
        }
    }
}
