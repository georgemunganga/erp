using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ManualPaymentCapacitySnapshot(decimal CapturedInvoiceGross,
    decimal OtherManualCommitments, decimal RegisteredCredits, decimal ImportedSettlements,
    decimal RemainingExcludingThisRequest);

/// <summary>
/// Exact capacity within registered Procurement evidence, never a supplier ledger or bank balance.
/// The owning adapter must lock the invoice and read ALL its same-company evidence in one
/// serializable transaction. Drafts do not reserve capacity; submitted requests do. Recorded
/// disputed payments remain reserved. Missing Finance data does not mean no external payment.
/// </summary>
public static class ManualPaymentCapacity
{
    public static ManualPaymentCapacitySnapshot Calculate(SupplierInvoice invoice,
        IReadOnlyCollection<ManualPaymentRequest> requests, IReadOnlyCollection<SupplierCreditNoteRef> credits,
        IReadOnlyCollection<FinancePaymentStatusRef> settlements, Guid? excludingRequestId = null)
    {
        if (invoice.Id == Guid.Empty || string.IsNullOrWhiteSpace(invoice.TenantId) || invoice.LegalEntityId == Guid.Empty
            || invoice.BranchRef is null || invoice.BranchRef == Guid.Empty || invoice.SupplierId == Guid.Empty
            || invoice.CurrencyCode is not { Length: 3 } || invoice.CurrencyCode.Any(c => c is < 'A' or > 'Z')
            || !Money(invoice.GrossAmount, 18, 4) || excludingRequestId == Guid.Empty)
            Fail("ManualPaymentInvoiceCapacityInvalid");
        if (requests.Select(x => x.Id).Distinct().Count() != requests.Count
            || credits.Select(x => x.Id).Distinct().Count() != credits.Count
            || settlements.Select(x => x.FinancePaymentRef).Distinct(StringComparer.Ordinal).Count() != settlements.Count)
            Fail("ManualPaymentCapacityEvidenceDuplicated");
        foreach (var request in requests)
        {
            if (request.Id == Guid.Empty || !SameInvoice(invoice, request, request.SupplierInvoiceId)
                || request.BranchId != invoice.BranchRef || request.SupplierId != invoice.SupplierId
                || request.CurrencyCode != invoice.CurrencyCode || !Money(request.Amount, 18, 4)
                || request.Status is not ("Draft" or "PendingApproval" or "Approved" or "PaymentRecorded"
                    or "ConfirmedManually" or "Disputed" or "Rejected" or "Cancelled")
                || (request.Status is "Approved" or "PaymentRecorded" or "ConfirmedManually" or "Disputed")
                    && request.ApprovedAmount != request.Amount
                || IsRecorded(request) && request.RecordedAmount != request.Amount
                || !IsRecorded(request) && request.RecordedAmount is not null)
                Fail("ManualPaymentCapacityHistoryNeedsReview");
        }
        foreach (var credit in credits)
            if (credit.Id == Guid.Empty || !SameInvoice(invoice, credit, credit.SupplierInvoiceId)
                || credit.SupplierId != invoice.SupplierId || credit.CurrencyCode != invoice.CurrencyCode
                || !Money(credit.Amount, 18, 4) || credit.Status is not ("Draft" or "Received" or "Cancelled")
                || credit.Status == "Cancelled" && credit.FinanceCreditRef is not null)
                Fail("ManualPaymentCreditHistoryNeedsReview");
        foreach (var settlement in settlements)
            if (settlement.Id == Guid.Empty || !SameInvoice(invoice, settlement, settlement.SupplierInvoiceId)
                || string.IsNullOrWhiteSpace(settlement.FinancePaymentRef) || settlement.CurrencyCode != invoice.CurrencyCode
                || !Money(settlement.PaidAmount, 38, 8)
                || settlement.Status is not ("Paid" or "PartiallyPaid"))
                Fail("ManualPaymentFinanceHistoryNeedsReview");
        // There is currently no Finance-to-manual reconciliation link. Summing both would
        // silently double-count, while choosing either could omit a real settlement.
        if (settlements.Count > 0 && requests.Any(IsRecorded))
            Fail("ManualPaymentFinanceReconciliationRequired");
        decimal manual, creditTotal, paid, remaining;
        try
        {
            manual = ExactDecimalArithmetic.Sum(requests.Where(x => x.Id != excludingRequestId
                && (x.Status is "PendingApproval" or "Approved" or "PaymentRecorded" or "ConfirmedManually" or "Disputed"))
                .Select(x => x.Amount));
            // Captured credit claims reserve capacity conservatively; this is NOT credit posting/application.
            creditTotal = ExactDecimalArithmetic.Sum(credits.Where(x => x.Status != "Cancelled").Select(x => x.Amount));
            paid = ExactDecimalArithmetic.Sum(settlements.Select(x => x.PaidAmount));
            remaining = ExactDecimalArithmetic.Subtract(invoice.GrossAmount,
                ExactDecimalArithmetic.Sum([manual, creditTotal, paid]));
        }
        catch (InvalidOperationException) { throw new InvalidOperationException("ManualPaymentCapacityNotRepresentable"); }
        if (remaining < 0 || !ExactDecimalJsonCodec.IsRepresentable(remaining, 38, 8))
            Fail("ManualPaymentRegisteredAmountExceedsInvoice");
        return new(invoice.GrossAmount, manual, creditTotal, paid, remaining);
    }

    private static bool IsRecorded(ManualPaymentRequest request) => request.Status is "PaymentRecorded" or "ConfirmedManually" or "Disputed";
    private static bool SameInvoice(SupplierInvoice invoice, ProcurementRecord evidence, Guid? invoiceId) =>
        evidence.TenantId == invoice.TenantId && evidence.LegalEntityId == invoice.LegalEntityId && invoiceId == invoice.Id;
    private static bool Money(decimal value, int precision, int scale) => value > 0 && ExactDecimalJsonCodec.IsRepresentable(value, precision, scale);
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
