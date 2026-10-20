using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>
/// Deterministic comparison, deliberately not a live posting/authorization service.
/// A future adapter must reload authoritative issued/accepted evidence and complete consumption
/// under locks, enforce server permissions, and atomically persist evaluation plus allocations.
/// </summary>
public static class InvoiceMatchingFoundation
{
    private static readonly JsonSerializerOptions Json = JsonOptions();
    private sealed record LineWork(InvoiceMatchDocumentLine Invoice, InvoiceIssuedOrderLine Order,
        InvoiceCategoryMatchRule Rule, Guid? AcceptanceId, BigInteger Quantity, BigInteger Net, BigInteger Tax);

    public static InvoiceMatchingEvaluation Evaluate(InvoiceMatchCommand command, IProcurementScope scope,
        InvoiceMatchingInput input, InvoiceMatchingEvaluation? previous = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        // Snapshot all caller-owned collections before validation/comparison/fingerprinting.
        input = input with
        {
            Invoice = input.Invoice with { Lines = input.Invoice.Lines.OrderBy(x => x.Id).ToArray() },
            Order = input.Order with { Lines = input.Order.Lines.OrderBy(x => x.Id).ToArray() },
            Acceptances = input.Acceptances.OrderBy(x => x.Id).ToArray(),
            Policy = input.Policy with { Rules = input.Policy.Rules.OrderBy(x => x.CategoryCode, StringComparer.Ordinal).ThenBy(x => x.CurrencyCode, StringComparer.Ordinal).ToArray() },
            Consumption = input.Consumption with { Allocations = input.Consumption.Allocations.OrderBy(x => x.Id).ToArray() },
            Duplicates = input.Duplicates with { Identities = input.Duplicates.Identities.OrderBy(x => x.InvoiceId).ToArray() },
        };
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, command, scope.TenantId, scope.LegalEntityId, scope.SubjectId,
            scope.WorkLocationId, scope.OrgUnitId, scope.IsConfined, input,
        }, Json));
        // Replay recovers an original immutable comparison; caller still rechecks current access before invoking this helper.
        if (previous is not null)
        {
            if (command.CommandKey == Guid.Empty || previous.CommandKey != command.CommandKey
                || previous.InvoiceId != input.Invoice.Id || previous.InvoiceVersion != input.Invoice.Version || previous.ActorSubjectId != scope.SubjectId
                || previous.InputFingerprint != fingerprint || previous.EvidenceSha256 != Hash(previous.EvidenceJson)
                || previous.Outcome is not ("Compared" or "Blocked")
                || previous.EvidenceJson != Evidence(previous.Outcome, previous.Discrepancies, previous.Lines))
                throw new InvalidOperationException("InvoiceMatchCommandConflict");
            return previous;
        }
        var problems = new List<InvoiceMatchDiscrepancy>();
        void Block(string code, Guid? line = null) => problems.Add(new(line, code));
        var invoice = input.Invoice; var order = input.Order; var policy = input.Policy;
        bool Scope(string tenant, Guid company, Guid? branch) => tenant == scope.TenantId && company == scope.LegalEntityId
            && (!scope.WorkLocationId.HasValue || branch == scope.WorkLocationId)
            && (!scope.IsConfined || scope.WorkLocationId.HasValue && branch == scope.WorkLocationId);
        if (string.IsNullOrWhiteSpace(scope.TenantId) || scope.LegalEntityId == Guid.Empty
            || command.CommandKey == Guid.Empty || command.ExpectedInvoiceVersion != invoice.Version || invoice.Version <= 0
            || command.EvaluatedAt == default || string.IsNullOrWhiteSpace(scope.SubjectId) || command.Reason?.Length > 2000)
            Block("MatchCommandInvalid");
        if (scope.OrgUnitId.HasValue) Block("InvoiceDepartmentScopeAdapterRequired");
        if (invoice.Id == Guid.Empty || invoice.SupplierId == Guid.Empty || !Scope(invoice.TenantId, invoice.LegalEntityId, invoice.BranchRef)
            || string.IsNullOrWhiteSpace(invoice.InvoiceNumber) || invoice.InvoiceNumber.Trim().Length > 120 || invoice.NormalizedInvoiceNumber != invoice.InvoiceNumber.Trim().ToUpperInvariant()
            || !Currency(invoice.CurrencyCode) || invoice.InvoiceDate == default
            || invoice.DueDate is { } due && due < invoice.InvoiceDate) Block("InvoiceIdentityOrScopeInvalid");
        if (invoice.Status is not ("Received" or "MatchPending" or "Exception")) Block("InvoiceStateNotMatchable");
        if (!string.IsNullOrWhiteSpace(invoice.CreditForInvoiceRef)) Block("CurrentCreditInvoiceRouteUnsupported");
        if (!string.IsNullOrWhiteSpace(invoice.FinancePostingRef)) Block("InvoiceAlreadyHandedToFinance");
        if (order.Id == Guid.Empty || order.RevisionId == Guid.Empty || order.RevisionNumber <= 0 || order.Version <= 0
            || invoice.PurchaseOrderId != order.Id || invoice.SupplierId != order.SupplierId || invoice.CurrencyCode != order.CurrencyCode
            || !Scope(order.TenantId, order.LegalEntityId, order.BranchRef) || order.BranchRef != invoice.BranchRef
            || order.RevisionState != "Issued" || string.IsNullOrWhiteSpace(order.IssuedReference) || order.IssuedAt == default || order.IssuedAt > command.EvaluatedAt)
            Block("AuthoritativeIssuedRevisionRequired");
        if (policy.Id == Guid.Empty || policy.Version <= 0 || policy.TenantId != scope.TenantId || policy.LegalEntityId != scope.LegalEntityId
            || policy.BranchRef.HasValue && policy.BranchRef != invoice.BranchRef || policy.State != "Published" || string.IsNullOrWhiteSpace(policy.ApprovedReference)
            || policy.EffectiveFrom == default || policy.EffectiveFrom > command.EvaluatedAt
            || policy.EffectiveUntil is { } until && (until <= policy.EffectiveFrom || command.EvaluatedAt >= until)
            || policy.MoneyScale is < 0 or > 4 || !Enum.IsDefined(policy.Rounding) || policy.Rules.Count == 0)
            Block("MatchPolicyUnavailable");
        if (!input.Duplicates.Complete || string.IsNullOrWhiteSpace(input.Duplicates.Watermark)) Block("CompleteDuplicateEvidenceRequired");
        if (input.Duplicates.Identities.Select(x => x.InvoiceId).Distinct().Count() != input.Duplicates.Identities.Count
            || input.Duplicates.Identities.Count(x => x.InvoiceId == invoice.Id && x.SupplierId == invoice.SupplierId
                && x.NormalizedInvoiceNumber == invoice.NormalizedInvoiceNumber && x.CurrencyCode == invoice.CurrencyCode) != 1)
            Block("CurrentInvoiceIdentityEvidenceRequired");
        if (input.Duplicates.Identities.Any(x => x.TenantId != scope.TenantId || x.LegalEntityId != scope.LegalEntityId))
            Block("DuplicateEvidenceScopeInvalid");
        if (input.Duplicates.Identities.Any(x => x.InvoiceId != invoice.Id && x.TenantId == invoice.TenantId
            && x.LegalEntityId == invoice.LegalEntityId && x.SupplierId == invoice.SupplierId
            && x.NormalizedInvoiceNumber == invoice.NormalizedInvoiceNumber && x.CurrencyCode == invoice.CurrencyCode))
            Block("DuplicateInvoiceIdentity"); // Never expose the hidden duplicate's ID/branch.
        if (!input.Consumption.Complete || string.IsNullOrWhiteSpace(input.Consumption.Watermark)) Block("CompleteConsumptionEvidenceRequired");
        if (invoice.Lines.Count is < 1 or > 200 || invoice.Lines.Select(x => x.Id).Distinct().Count() != invoice.Lines.Count
            || order.Lines.Count == 0 || order.Lines.Select(x => x.Id).Distinct().Count() != order.Lines.Count
            || input.Acceptances.Select(x => x.Id).Distinct().Count() != input.Acceptances.Count
            || input.Consumption.Allocations.Select(x => x.Id).Distinct().Count() != input.Consumption.Allocations.Count
            || input.Consumption.Allocations.Select(x => x.InvoiceLineId).Distinct().Count() != input.Consumption.Allocations.Count)
            Block("MatchEvidenceSetInvalid");
        if (problems.Count != 0) return Finish([]);
        if (!InvoiceMatchExact.MoneyInput(invoice.NetAmount, policy) || !InvoiceMatchExact.MoneyInput(invoice.TaxAmount, policy)
            || !InvoiceMatchExact.MoneyInput(invoice.GrossAmount, policy)) Block("InvoiceHeaderAmountInvalid");
        foreach (var rule in policy.Rules)
            if (string.IsNullOrWhiteSpace(rule.CategoryCode) || !Currency(rule.CurrencyCode) || !Enum.IsDefined(rule.Method)
                || rule.Method == InvoiceMatchingMethod.ThreeWay && (rule.AcceptanceKind is null || !Enum.IsDefined(rule.AcceptanceKind.Value))
                || rule.Method == InvoiceMatchingMethod.TwoWay && rule.AcceptanceKind is not null
                || new[] { rule.Quantity, rule.UnitPrice, rule.Tax, rule.Total }.Any(x => !Tolerance(x)))
                Block("CategoryToleranceInvalid");
        foreach (var line in order.Lines)
            if (line.Id == Guid.Empty || line.Version <= 0 || string.IsNullOrWhiteSpace(line.CategoryCode) || string.IsNullOrWhiteSpace(line.UnitCode)
                || !InvoiceMatchExact.Input(line.Quantity, true) || !InvoiceMatchExact.Input(line.UnitPrice)
                || !InvoiceMatchExact.MoneyInput(line.TaxAmount, policy)) Block("IssuedLineInvalid");
        if (problems.Count != 0) return Finish([]);
        var issued = order.Lines.ToDictionary(x => x.Id); var accepted = input.Acceptances.ToDictionary(x => x.Id);
        foreach (var acceptance in input.Acceptances)
            if (acceptance.Id == Guid.Empty || acceptance.Version <= 0 || !Scope(acceptance.TenantId, acceptance.LegalEntityId, acceptance.BranchRef)
                || acceptance.BranchRef != invoice.BranchRef || acceptance.PurchaseOrderId != order.Id || acceptance.RevisionId != order.RevisionId
                || !issued.TryGetValue(acceptance.PurchaseOrderLineId, out var source) || acceptance.CurrencyCode != invoice.CurrencyCode
                || source.UnitCode != acceptance.UnitCode || !Enum.IsDefined(acceptance.Kind) || acceptance.Reversed
                || acceptance.State is not ("Accepted" or "PartiallyAccepted")
                || string.IsNullOrWhiteSpace(acceptance.AcceptanceReference) || acceptance.AcceptedAt == default || acceptance.AcceptedAt > command.EvaluatedAt
                || !InvoiceMatchExact.Input(acceptance.AcceptedQuantity) || !InvoiceMatchExact.Input(acceptance.ReturnedQuantity)
                || acceptance.ReturnedQuantity > acceptance.AcceptedQuantity || !InvoiceMatchExact.MoneyInput(acceptance.AcceptedNetAmount, policy)
                || !InvoiceMatchExact.MoneyInput(acceptance.ReturnedNetAmount, policy) || acceptance.ReturnedNetAmount > acceptance.AcceptedNetAmount)
                Block("AuthoritativeAcceptedEvidenceInvalid");
        var work = new List<LineWork>();
        foreach (var line in invoice.Lines)
        {
            if (line.Id == Guid.Empty || line.Version <= 0 || line.SupplierInvoiceId != invoice.Id || line.TenantId != invoice.TenantId
                || line.LegalEntityId != invoice.LegalEntityId || line.PurchaseOrderLineId is null
                || !issued.TryGetValue(line.PurchaseOrderLineId.Value, out var source)
                || !InvoiceMatchExact.Input(line.Quantity, true) || !InvoiceMatchExact.Input(line.UnitPrice)
                || !InvoiceMatchExact.MoneyInput(line.TaxAmount, policy)) { Block("InvoiceLineInvalid", line.Id); continue; }
            if (string.IsNullOrWhiteSpace(line.UnitCode) || line.UnitCode != source.UnitCode)
            { Block("VerifiedInvoiceUnitMappingRequired", line.Id); continue; }
            var rules = policy.Rules.Where(x => x.CategoryCode == source.CategoryCode && x.CurrencyCode == invoice.CurrencyCode).ToArray();
            if (rules.Length != 1) { Block("CategoryMatchPolicyMissingOrAmbiguous", line.Id); continue; }
            var rule = rules[0]; var acceptanceId = line.GoodsReceiptLineId ?? line.ServiceAcceptanceId;
            if (line.GoodsReceiptLineId.HasValue && line.ServiceAcceptanceId.HasValue
                || rule.Method == InvoiceMatchingMethod.TwoWay && acceptanceId.HasValue
                || rule.Method == InvoiceMatchingMethod.ThreeWay && (acceptanceId is null || !accepted.TryGetValue(acceptanceId.Value, out var receipt)
                    || receipt.PurchaseOrderLineId != source.Id || receipt.Kind != rule.AcceptanceKind
                    || receipt.Kind == InvoiceAcceptanceKind.Goods && line.GoodsReceiptLineId is null
                    || receipt.Kind == InvoiceAcceptanceKind.Service && line.ServiceAcceptanceId is null
                    || receipt.AcceptedQuantity <= receipt.ReturnedQuantity))
            { Block("InvoiceAcceptanceMappingInvalid", line.Id); continue; }
            var quantity = InvoiceMatchExact.Units(line.Quantity); var unitPrice = InvoiceMatchExact.Units(line.UnitPrice);
            var net = InvoiceMatchExact.ProductMoney(quantity, unitPrice, policy); var tax = InvoiceMatchExact.Units(line.TaxAmount);
            if (!InvoiceMatchExact.Within(unitPrice - InvoiceMatchExact.Units(source.UnitPrice), InvoiceMatchExact.Units(source.UnitPrice), rule.UnitPrice))
                Block("UnitPriceOutsideTolerance", line.Id);
            work.Add(new(line, source, rule, acceptanceId, quantity, net, tax));
        }
        ValidateHistory(input.Consumption.Allocations, invoice, order, issued, accepted, policy, Block);
        if (problems.Count != 0) return Finish(work);
        var prior = input.Consumption.Allocations;
        BigInteger Sum(IEnumerable<InvoiceMatchConsumption> rows, Func<InvoiceMatchConsumption, decimal> select) => rows.Aggregate(BigInteger.Zero,
            (sum, x) => sum + InvoiceMatchExact.Units(select(x)) * (x.Kind == InvoiceConsumptionKind.Debit ? 1 : -1));
        foreach (var group in work.GroupBy(x => x.Order.Id))
        {
            var first = group.First(); var source = first.Order; var rule = first.Rule;
            var history = prior.Where(x => x.PurchaseOrderLineId == source.Id).ToArray();
            var quantity = Sum(history, x => x.Quantity) + group.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Quantity);
            var net = Sum(history, x => x.NetAmount) + group.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Net);
            var tax = Sum(history, x => x.TaxAmount) + group.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Tax);
            var orderedQuantity = InvoiceMatchExact.Units(source.Quantity);
            if (quantity > orderedQuantity && !InvoiceMatchExact.Within(quantity - orderedQuantity, orderedQuantity, rule.Quantity))
                Block("CumulativeOrderQuantityOutsideTolerance", first.Invoice.Id);
            var expectedTax = InvoiceMatchExact.RatioMoney(InvoiceMatchExact.Units(source.TaxAmount), quantity, orderedQuantity, policy);
            if (!InvoiceMatchExact.Within(tax - expectedTax, expectedTax, rule.Tax)) Block("CumulativeTaxOutsideTolerance", first.Invoice.Id);
            var expectedGross = InvoiceMatchExact.ProductMoney(quantity, InvoiceMatchExact.Units(source.UnitPrice), policy) + expectedTax;
            if (!InvoiceMatchExact.Within(net + tax - expectedGross, expectedGross, rule.Total)) Block("CumulativeTotalOutsideTolerance", first.Invoice.Id);
            foreach (var receiptGroup in group.Where(x => x.AcceptanceId.HasValue).GroupBy(x => x.AcceptanceId!.Value))
            {
                var receipt = accepted[receiptGroup.Key]; var used = prior.Where(x => x.AcceptanceId == receipt.Id).ToArray();
                var receiptQuantity = Sum(used, x => x.Quantity) + receiptGroup.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Quantity);
                var available = InvoiceMatchExact.Units(receipt.AcceptedQuantity - receipt.ReturnedQuantity);
                if (receiptQuantity > available && !InvoiceMatchExact.Within(receiptQuantity - available, available, rule.Quantity))
                    Block("CumulativeAcceptedQuantityOutsideTolerance", receiptGroup.First().Invoice.Id);
                if (receipt.Kind == InvoiceAcceptanceKind.Service)
                {
                    var value = Sum(used, x => x.NetAmount) + receiptGroup.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Net);
                    // Certified service value is a hard ceiling; commercial variance does not invent acceptance.
                    if (value > InvoiceMatchExact.Units(receipt.AcceptedNetAmount - receipt.ReturnedNetAmount))
                        Block("CertifiedServiceValueExceeded", receiptGroup.First().Invoice.Id);
                }
            }
        }
        if (work.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Net) != InvoiceMatchExact.Units(invoice.NetAmount)
            || work.Aggregate(BigInteger.Zero, (sum, x) => sum + x.Tax) != InvoiceMatchExact.Units(invoice.TaxAmount)
            || InvoiceMatchExact.Units(invoice.NetAmount) + InvoiceMatchExact.Units(invoice.TaxAmount) != InvoiceMatchExact.Units(invoice.GrossAmount))
            Block("InvoiceHeaderLineTotalsMismatch");
        return Finish(work);

        string Evidence(string outcome, IReadOnlyList<InvoiceMatchDiscrepancy> discrepancies, IReadOnlyList<InvoiceMatchLineComparison> lines)
            => JsonSerializer.Serialize(new { schemaVersion = 1, command.CommandKey, inputFingerprint = fingerprint,
                actor = scope.SubjectId, invoiceId = input.Invoice.Id, invoiceVersion = input.Invoice.Version,
                outcome, discrepancies, lines, source = new { input.Invoice, input.Order, input.Acceptances, input.Policy,
                    input.Consumption, duplicates = new { input.Duplicates.Watermark, input.Duplicates.Complete,
                        setSha256 = Hash(JsonSerializer.Serialize(input.Duplicates.Identities, Json)) } } }, Json);

        InvoiceMatchingEvaluation Finish(IEnumerable<LineWork> compared)
        {
            var lines = compared.Select(x => new InvoiceMatchLineComparison(x.Invoice.Id, x.Order.Id, x.AcceptanceId,
                x.Rule.Method, InvoiceMatchExact.Text(x.Quantity), InvoiceMatchExact.Text(x.Net), InvoiceMatchExact.Text(x.Tax),
                InvoiceMatchExact.Text(InvoiceMatchExact.Units(x.Order.UnitPrice)))).ToArray();
            var errors = problems.Distinct().OrderBy(x => x.InvoiceLineId).ThenBy(x => x.Code, StringComparer.Ordinal).ToArray();
            var outcome = errors.Length == 0 ? "Compared" : "Blocked";
            var evidence = Evidence(outcome, errors, lines);
            return new(command.CommandKey, invoice.Id, invoice.Version, scope.SubjectId, fingerprint, outcome, Array.AsReadOnly(errors), Array.AsReadOnly(lines), evidence, Hash(evidence));
        }
    }

    private static void ValidateHistory(IReadOnlyList<InvoiceMatchConsumption> history, InvoiceMatchDocument invoice,
        InvoiceIssuedOrder order, IReadOnlyDictionary<Guid, InvoiceIssuedOrderLine> issued,
        IReadOnlyDictionary<Guid, InvoiceAcceptedFulfilment> accepted, InvoiceMatchingPolicy policy,
        Action<string, Guid?> block)
    {
        var invalid = false;
        void Bad(string code) { invalid = true; block(code, null); }
        foreach (var allocation in history)
        {
            if (allocation.Id == Guid.Empty || allocation.InvoiceId == Guid.Empty || allocation.InvoiceLineId == Guid.Empty
                || allocation.InvoiceId == invoice.Id || allocation.TenantId != invoice.TenantId || allocation.LegalEntityId != invoice.LegalEntityId
                || allocation.BranchRef != invoice.BranchRef || allocation.SupplierId != invoice.SupplierId
                || allocation.PurchaseOrderId != order.Id || allocation.RevisionId != order.RevisionId
                || !issued.ContainsKey(allocation.PurchaseOrderLineId) || allocation.CurrencyCode != invoice.CurrencyCode
                || !Enum.IsDefined(allocation.Kind) || string.IsNullOrWhiteSpace(allocation.ValidationReference)
                || !InvoiceMatchExact.Input(allocation.Quantity) || !InvoiceMatchExact.MoneyInput(allocation.NetAmount, policy)
                || !InvoiceMatchExact.MoneyInput(allocation.TaxAmount, policy)
                || allocation.Kind == InvoiceConsumptionKind.Debit && (allocation.Quantity <= 0 || allocation.OriginalDebitId is not null)
                || allocation.Kind == InvoiceConsumptionKind.Credit && (allocation.OriginalDebitId is null
                    || allocation.Quantity == 0 && allocation.NetAmount == 0 && allocation.TaxAmount == 0))
            { Bad("ConsumptionAllocationInvalid"); continue; }
            var source = issued[allocation.PurchaseOrderLineId];
            var rules = policy.Rules.Where(x => x.CategoryCode == source.CategoryCode && x.CurrencyCode == invoice.CurrencyCode).ToArray();
            if (rules.Length != 1) { Bad("HistoricalCategoryPolicyMissingOrAmbiguous"); continue; }
            var rule = rules[0];
            if (rule.Method == InvoiceMatchingMethod.ThreeWay && (allocation.AcceptanceId is null
                    || !accepted.TryGetValue(allocation.AcceptanceId.Value, out var receipt) || receipt.Kind != rule.AcceptanceKind
                    || receipt.PurchaseOrderLineId != source.Id)
                || rule.Method == InvoiceMatchingMethod.TwoWay && allocation.AcceptanceId is not null)
                Bad("HistoricalAcceptanceMappingRequired");
        }
        if (invalid) return;
        var debits = history.Where(x => x.Kind == InvoiceConsumptionKind.Debit).ToDictionary(x => x.Id);
        foreach (var credits in history.Where(x => x.Kind == InvoiceConsumptionKind.Credit).GroupBy(x => x.OriginalDebitId!.Value))
        {
            if (!debits.TryGetValue(credits.Key, out var debit)) { Bad("CreditOriginalDebitMissing"); continue; }
            foreach (var credit in credits)
            {
                if (credit.PurchaseOrderLineId != debit.PurchaseOrderLineId || credit.AcceptanceId != debit.AcceptanceId
                    || credit.InvoiceId == debit.InvoiceId || credit.InvoiceLineId == debit.InvoiceLineId)
                { Bad("CreditOriginalDebitMismatch"); continue; }
                // Quantity restoration requires a proportional validated commercial credit. A header/value-only credit never releases quantity.
                if (credit.Quantity > 0 && (InvoiceMatchExact.Units(credit.NetAmount) != InvoiceMatchExact.RatioMoney(
                        InvoiceMatchExact.Units(debit.NetAmount), InvoiceMatchExact.Units(credit.Quantity), InvoiceMatchExact.Units(debit.Quantity), policy)
                    || InvoiceMatchExact.Units(credit.TaxAmount) != InvoiceMatchExact.RatioMoney(
                        InvoiceMatchExact.Units(debit.TaxAmount), InvoiceMatchExact.Units(credit.Quantity), InvoiceMatchExact.Units(debit.Quantity), policy)))
                    Bad("CreditQuantityAmountMismatch");
            }
            var creditedQuantity = credits.Aggregate(BigInteger.Zero, (sum, x) => sum + InvoiceMatchExact.Units(x.Quantity));
            var creditedNet = credits.Aggregate(BigInteger.Zero, (sum, x) => sum + InvoiceMatchExact.Units(x.NetAmount));
            var creditedTax = credits.Aggregate(BigInteger.Zero, (sum, x) => sum + InvoiceMatchExact.Units(x.TaxAmount));
            if (creditedQuantity > InvoiceMatchExact.Units(debit.Quantity)
                || creditedNet > InvoiceMatchExact.Units(debit.NetAmount)
                || creditedTax > InvoiceMatchExact.Units(debit.TaxAmount))
                Bad("CumulativeCreditExceedsOriginalDebit");
            // Midpoint rounding must not restart on each fragment. For example two
            // qty1/net0 credits against qty2/net0.01 each round to zero under ToEven,
            // but together cannot restore all quantity while the original money remains.
            // Value-only adjustments may supply a verified rounding remainder; they
            // never increase creditedQuantity and all components remain capped above.
            if (creditedQuantity > 0 && (creditedNet < InvoiceMatchExact.RatioMoney(
                    InvoiceMatchExact.Units(debit.NetAmount), creditedQuantity, InvoiceMatchExact.Units(debit.Quantity), policy)
                || creditedTax < InvoiceMatchExact.RatioMoney(
                    InvoiceMatchExact.Units(debit.TaxAmount), creditedQuantity, InvoiceMatchExact.Units(debit.Quantity), policy)))
                Bad("CumulativeCreditQuantityAmountMismatch");
        }
    }

    private static bool Currency(string? code) => code is { Length: 3 } && code.All(x => x is >= 'A' and <= 'Z');
    private static bool Tolerance(InvoiceFieldTolerance tolerance) => tolerance is not null && InvoiceMatchExact.Input(tolerance.Absolute)
        && InvoiceMatchExact.Input(tolerance.Percent) && tolerance.Percent <= 100 && Enum.IsDefined(tolerance.Combination);
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static JsonSerializerOptions JsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new CanonicalDecimal()); options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
    private sealed class CanonicalDecimal : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture));
    }
}
