using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ManualPaymentCapacityTests
{
    [Fact]
    public void Empty_registered_history_allows_manual_review_without_Finance_or_employee_data()
    {
        var f = new Fixture(); var capacity = f.Calculate();
        Assert.Equal(100m, capacity.RemainingExcludingThisRequest);
        Assert.Equal(0m, capacity.ImportedSettlements);
    }

    [Theory]
    [InlineData("Draft", 100)][InlineData("Rejected", 100)][InlineData("Cancelled", 100)]
    [InlineData("PendingApproval", 75)][InlineData("Approved", 75)][InlineData("PaymentRecorded", 75)]
    [InlineData("ConfirmedManually", 75)][InlineData("Disputed", 75)]
    public void Pending_and_executed_requests_reserve_capacity_but_drafts_and_unexecuted_cancellations_do_not(string state, int remaining)
    {
        var f = new Fixture(); f.Requests.Add(f.Request(state));
        Assert.Equal((decimal)remaining, f.Calculate().RemainingExcludingThisRequest);
    }

    [Fact]
    public void Rechecking_a_request_excludes_itself_but_not_another_pending_installment()
    {
        var f = new Fixture(); var own = f.Request("Approved", 70m);
        f.Requests.Add(own); f.Requests.Add(f.Request("PendingApproval", 20m));
        Assert.Equal(80m, f.Calculate(own.Id).RemainingExcludingThisRequest);
        Assert.Equal(10m, f.Calculate().RemainingExcludingThisRequest);
    }

    [Fact]
    public void Registered_credit_claims_reserve_capacity_without_becoming_posted_credit()
    {
        var f = new Fixture(); f.Credits.Add(f.Credit("Draft", 5.0001m));
        f.Credits.Add(f.Credit("Received", 4.9999m)); f.Credits.Add(f.Credit("Cancelled", 90m));
        var result = f.Calculate(); Assert.Equal(10m, result.RegisteredCredits);
        Assert.Equal(90m, result.RemainingExcludingThisRequest);
    }

    [Fact]
    public void Imported_settlement_and_manual_commitment_preserve_sub_minor_unit_precision()
    {
        var f = new Fixture(); f.Requests.Add(f.Request("Approved", 20.0001m));
        f.Settlements.Add(f.Settlement(10.00000001m));
        var result = f.Calculate();
        Assert.Equal(69.99989999m, result.RemainingExcludingThisRequest);
        Assert.Equal(10.00000001m, result.ImportedSettlements);
    }

    [Theory]
    [InlineData("PaymentRecorded")][InlineData("ConfirmedManually")][InlineData("Disputed")]
    public void Mixed_manual_execution_and_Finance_outcomes_require_reconciliation_instead_of_guessing(string state)
    {
        var f = new Fixture(); var own = f.Request(state); f.Requests.Add(own); f.Settlements.Add(f.Settlement(25m));
        Assert.Equal("ManualPaymentFinanceReconciliationRequired", Assert.Throws<InvalidOperationException>(() => f.Calculate(own.Id)).Message);
    }

    [Fact]
    public void Already_overcommitted_history_is_rejected_instead_of_clamping_remaining_to_zero()
    {
        var f = new Fixture(); f.Requests.Add(f.Request("Approved", 80m)); f.Credits.Add(f.Credit("Received", 30m));
        Assert.Equal("ManualPaymentRegisteredAmountExceedsInvoice", Assert.Throws<InvalidOperationException>(() => f.Calculate()).Message);
    }

    [Theory]
    [InlineData("tenant")][InlineData("company")][InlineData("invoice")][InlineData("supplier")]
    [InlineData("branch")][InlineData("currency")][InlineData("state")][InlineData("amount")]
    [InlineData("approvedAmount")][InlineData("cancelledRecorded")]
    public void Foreign_or_inconsistent_manual_history_cannot_be_ignored(string field)
    {
        var f = new Fixture(); var request = f.Request("Approved");
        switch (field)
        {
            case "tenant": request.TenantId = "other"; break;
            case "company": request.LegalEntityId = Guid.NewGuid(); break;
            case "invoice": request.SupplierInvoiceId = Guid.NewGuid(); break;
            case "supplier": request.SupplierId = Guid.NewGuid(); break;
            case "branch": request.BranchId = Guid.NewGuid(); break;
            case "currency": request.CurrencyCode = "USD"; break;
            case "state": request.Status = "Paid"; break;
            case "amount": request.Amount = 25.00001m; break;
            case "approvedAmount": request.ApprovedAmount = 20m; break;
            case "cancelledRecorded": request.Status = "Cancelled"; request.RecordedAmount = 25m; break;
        }
        f.Requests.Add(request); Assert.Throws<InvalidOperationException>(() => f.Calculate());
    }

    [Theory]
    [InlineData("credit")][InlineData("request")][InlineData("financeRef")]
    public void Duplicated_evidence_is_rejected_instead_of_double_counting(string kind)
    {
        var f = new Fixture();
        if (kind == "credit") { var row = f.Credit("Draft", 2m); f.Credits.Add(row); f.Credits.Add(row); }
        else if (kind == "request") { var row = f.Request("Approved"); f.Requests.Add(row); f.Requests.Add(row); }
        else { var row = f.Settlement(2m); f.Settlements.Add(row); f.Settlements.Add(row); }
        Assert.Equal("ManualPaymentCapacityEvidenceDuplicated", Assert.Throws<InvalidOperationException>(() => f.Calculate()).Message);
    }

    [Theory]
    [InlineData("Cancelled")][InlineData("Scheduled")][InlineData("Failed")][InlineData("Unknown")]
    public void Unsupported_Finance_outcomes_do_not_silently_free_capacity(string state)
    {
        var f = new Fixture(); var row = f.Settlement(5m); row.Status = state; f.Settlements.Add(row);
        Assert.Equal("ManualPaymentFinanceHistoryNeedsReview", Assert.Throws<InvalidOperationException>(() => f.Calculate()).Message);
    }

    [Fact]
    public void Cancelled_credit_with_a_Finance_reference_needs_explicit_reconciliation()
    {
        var f = new Fixture(); var row = f.Credit("Cancelled", 5m); row.FinanceCreditRef = "SYN-CREDIT"; f.Credits.Add(row);
        Assert.Equal("ManualPaymentCreditHistoryNeedsReview", Assert.Throws<InvalidOperationException>(() => f.Calculate()).Message);
    }

    private sealed class Fixture
    {
        internal readonly SupplierInvoice Invoice = new() { TenantId = "synthetic-capacity", LegalEntityId = Guid.NewGuid(),
            BranchRef = Guid.NewGuid(), SupplierId = Guid.NewGuid(), CurrencyCode = "ZMW", GrossAmount = 100m };
        internal readonly List<ManualPaymentRequest> Requests = [];
        internal readonly List<SupplierCreditNoteRef> Credits = [];
        internal readonly List<FinancePaymentStatusRef> Settlements = [];
        internal ManualPaymentCapacitySnapshot Calculate(Guid? excluding = null) => ManualPaymentCapacity.Calculate(Invoice, Requests, Credits, Settlements, excluding);
        internal ManualPaymentRequest Request(string status, decimal amount = 25m) => new()
        {
            TenantId = Invoice.TenantId, LegalEntityId = Invoice.LegalEntityId, SupplierInvoiceId = Invoice.Id,
            BranchId = Invoice.BranchRef!.Value, SupplierId = Invoice.SupplierId, CurrencyCode = Invoice.CurrencyCode,
            Status = status, Amount = amount,
            ApprovedAmount = status is "Approved" or "PaymentRecorded" or "ConfirmedManually" or "Disputed" ? amount : null,
            RecordedAmount = status is "PaymentRecorded" or "ConfirmedManually" or "Disputed" ? amount : null,
        };
        internal SupplierCreditNoteRef Credit(string state, decimal amount) => new()
        { TenantId = Invoice.TenantId, LegalEntityId = Invoice.LegalEntityId, SupplierInvoiceId = Invoice.Id,
            SupplierId = Invoice.SupplierId, CurrencyCode = Invoice.CurrencyCode, Amount = amount, Status = state };
        internal FinancePaymentStatusRef Settlement(decimal amount) => new()
        { TenantId = Invoice.TenantId, LegalEntityId = Invoice.LegalEntityId, SupplierInvoiceId = Invoice.Id,
            CurrencyCode = Invoice.CurrencyCode, PaidAmount = amount, Status = "PartiallyPaid", FinancePaymentRef = Guid.NewGuid().ToString("D") };
    }
}
