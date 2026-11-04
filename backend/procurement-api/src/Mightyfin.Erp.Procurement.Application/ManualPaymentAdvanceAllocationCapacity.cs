namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Current projection of append-only allocation/reversal evidence, not editable
/// accounting state. Applied claims must be backed by independently authorised persisted
/// records; Reversed is derived from the original plus its explicit retained reversal.</summary>
public sealed record ManualPaymentAdvanceAllocationClaim(Guid Id, string TenantId, Guid LegalEntityId,
    Guid BranchId, Guid SupplierId, Guid OrderId, Guid AdvanceRequestId, Guid InvoiceId,
    string CurrencyCode, decimal Amount, string State);
public sealed record ManualPaymentAdvanceAllocationSnapshot(decimal ConfirmedAdvance,
    decimal AllocatedFromAdvance, decimal RemainingAdvance, decimal AllocatedToInvoice,
    decimal RemainingInvoice, decimal MaximumNewAllocation);

/// <summary>Pure allocation limits. The future store must lock order → invoices → payments
/// in a stable order, supply complete same-advance and same-invoice evidence and use the
/// existing current Core authority/idempotency/audit transaction. No Finance posting occurs.</summary>
public static class ManualPaymentAdvanceAllocationCapacity
{
    public static ManualPaymentAdvanceAllocationSnapshot Calculate(ManualPaymentOrderAdvanceSource order,
        ManualPaymentAdvanceCommitment advance, ManualPaymentInvoiceSource invoice, Guid invoiceOrderId,
        IReadOnlyCollection<ManualPaymentAdvanceAllocationClaim> forAdvance,
        IReadOnlyCollection<ManualPaymentAdvanceAllocationClaim> forInvoice)
    {
        ManualPaymentOrderAdvanceCapacity.ValidateSource(order);
        Need(order.Status is "Approved" or "Issued" or "PartiallyReceived" or "FullyReceived" or "Closed",
            "ManualPaymentAdvanceAllocationOrderNotEligible");
        ManualPaymentOrderAdvanceCapacity.ValidateClaim(order, advance);
        Need(advance.Status == "ConfirmedManually", "ManualPaymentAdvanceConfirmationRequired");
        Need(invoiceOrderId == order.OrderId && invoice.InvoiceId != Guid.Empty && invoice.IsEligible
            && invoice.InvoiceVersion > 0 && invoice.TenantId == order.TenantId && invoice.LegalEntityId == order.LegalEntityId
            && invoice.BranchId == order.BranchId && invoice.SupplierId == order.SupplierId && invoice.CurrencyCode == order.CurrencyCode
            && invoice.InvoiceDate != default && ManualPaymentOrderAdvanceCapacity.Money(invoice.InvoiceGrossAmount, 18, 4)
            && invoice.RemainingExcludingThisRequest >= 0 && invoice.RemainingExcludingThisRequest <= invoice.InvoiceGrossAmount
            && ExactDecimalJsonCodec.IsRepresentable(invoice.RemainingExcludingThisRequest, 38, 8)
            && invoice.ExcludingManualPaymentRequestId is null, "ManualPaymentAdvanceInvoiceNotEligible");
        foreach (var rows in new[] { forAdvance, forInvoice })
        {
            Need(rows.Select(x => x.Id).Distinct().Count() == rows.Count, "ManualPaymentAdvanceAllocationDuplicated");
            foreach (var row in rows)
                Need(row.Id != Guid.Empty && row.TenantId == order.TenantId && row.LegalEntityId == order.LegalEntityId
                    && row.BranchId == order.BranchId && row.SupplierId == order.SupplierId && row.OrderId == order.OrderId
                    && row.CurrencyCode == order.CurrencyCode && row.AdvanceRequestId != Guid.Empty && row.InvoiceId != Guid.Empty
                    && ManualPaymentOrderAdvanceCapacity.Money(row.Amount, 18, 4) && row.State is "Applied" or "Reversed",
                    "ManualPaymentAdvanceAllocationInvalid");
        }
        Need(forAdvance.All(x => x.AdvanceRequestId == advance.RequestId) && forInvoice.All(x => x.InvoiceId == invoice.InvoiceId),
            "ManualPaymentAdvanceAllocationWrongSource");
        var byId = forAdvance.ToDictionary(x => x.Id);
        Need(forInvoice.All(x => !byId.TryGetValue(x.Id, out var retained) || retained == x),
            "ManualPaymentAdvanceAllocationHistoryIncomplete");
        var intersectionA = forAdvance.Where(x => x.InvoiceId == invoice.InvoiceId).OrderBy(x => x.Id).ToArray();
        var intersectionB = forInvoice.Where(x => x.AdvanceRequestId == advance.RequestId).OrderBy(x => x.Id).ToArray();
        Need(intersectionA.SequenceEqual(intersectionB), "ManualPaymentAdvanceAllocationHistoryIncomplete");
        try
        {
            var usedAdvance = ExactDecimalArithmetic.Sum(forAdvance.Where(x => x.State == "Applied").Select(x => x.Amount));
            var usedInvoice = ExactDecimalArithmetic.Sum(forInvoice.Where(x => x.State == "Applied").Select(x => x.Amount));
            var remainingAdvance = ExactDecimalArithmetic.Subtract(advance.Amount, usedAdvance);
            var remainingInvoice = ExactDecimalArithmetic.Subtract(invoice.RemainingExcludingThisRequest, usedInvoice);
            Need(remainingAdvance >= 0 && remainingInvoice >= 0, "ManualPaymentAdvanceAllocationExceedsSource");
            return new(advance.Amount, usedAdvance, remainingAdvance, usedInvoice, remainingInvoice,
                Math.Min(remainingAdvance, remainingInvoice));
        }
        catch (InvalidOperationException exception) when (exception.Message == "ExactDecimalNotRepresentable")
        { throw new InvalidOperationException("ManualPaymentAdvanceAllocationNotRepresentable"); }
    }
    public static void RequireAmount(decimal amount, ManualPaymentAdvanceAllocationSnapshot capacity)
    {
        Need(ManualPaymentOrderAdvanceCapacity.Money(amount, 18, 4), "ManualPaymentAdvanceAllocationAmountInvalid");
        Need(capacity.ConfirmedAdvance > 0 && capacity.AllocatedFromAdvance >= 0 && capacity.AllocatedToInvoice >= 0
            && capacity.RemainingAdvance >= 0 && capacity.RemainingInvoice >= 0
            && ExactDecimalArithmetic.Add(capacity.AllocatedFromAdvance, capacity.RemainingAdvance) == capacity.ConfirmedAdvance
            && capacity.MaximumNewAllocation == Math.Min(capacity.RemainingAdvance, capacity.RemainingInvoice),
            "ManualPaymentAdvanceAllocationCapacityInvalid");
        Need(amount <= capacity.MaximumNewAllocation, "ManualPaymentAdvanceAllocationAmountExceedsRemaining");
    }
    private static void Need(bool condition, string code) => ManualPaymentOrderAdvanceCapacity.Need(condition, code);
}
