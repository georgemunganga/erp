using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ManualPaymentOrderAdvanceCapacityTests
{
    private static readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid(), Supplier = Guid.NewGuid(), Order = Guid.NewGuid(), Invoice = Guid.NewGuid();
    private static ManualPaymentOrderAdvanceSource Source() => new(Order, "tenant", Company, Branch, Supplier, "ZMW", 2, 1000m, "Issued");
    private static ManualPaymentAdvanceTerms Terms() => new("ZMW", 5000m, 30m);
    private static ManualPaymentAdvanceCommitment Claim(string state = "PendingApproval", decimal amount = 100m) =>
        new(Guid.NewGuid(), "tenant", Company, Branch, Supplier, Order, "ZMW", 2, 1000m, amount, state,
            state is "Approved" or "PaymentRecorded" or "ConfirmedManually" or "Disputed" ? amount : null,
            state is "PaymentRecorded" or "ConfirmedManually" or "Disputed" ? amount : null);
    private static ManualPaymentInvoiceSource Bill(decimal gross = 500m, decimal remaining = 400m) =>
        new(Invoice, "tenant", Company, Branch, Supplier, "ZMW", new DateOnly(2026, 11, 4), gross, remaining, true) { InvoiceVersion = 3 };
    private static ManualPaymentAdvanceAllocationClaim Allocation(Guid advance, Guid? invoice = null, decimal amount = 30m) =>
        new(Guid.NewGuid(), "tenant", Company, Branch, Supplier, Order, advance, invoice ?? Invoice, "ZMW", amount, "Applied");
    private static ManualPaymentAdvanceCapacitySnapshot Capacity(ManualPaymentAdvanceCommitment[] claims,
        Guid? exclude = null) => ManualPaymentOrderAdvanceCapacity.Calculate(Source(), Terms(), claims,
            ManualPaymentOrderSettlementReview.NoUnreconciledRegisteredSettlements, exclude);

    [Fact]
    public void Whole_order_ceiling_prevents_split_payment_limit_evasion()
    {
        Assert.Equal("ManualPaymentAdvanceWholeOrderLimitExceeded", Assert.Throws<InvalidOperationException>(() =>
            ManualPaymentOrderAdvanceCapacity.Calculate(Source(), Terms() with { MaximumOrderGross = 999m }, [],
                ManualPaymentOrderSettlementReview.NoUnreconciledRegisteredSettlements)).Message);
    }
    [Theory]
    [InlineData("Draft", 0)] [InlineData("PendingApproval", 100)] [InlineData("Approved", 100)]
    [InlineData("PaymentRecorded", 100)] [InlineData("ConfirmedManually", 100)] [InlineData("Disputed", 100)]
    [InlineData("Rejected", 0)] [InlineData("Cancelled", 0)]
    public void Disputes_and_recorded_money_remain_reserved_while_drafts_and_terminal_releases_do_not(string state, int reserved)
    {
        var result = Capacity([Claim(state)]);
        Assert.Equal(300m, result.AdvanceCeiling); Assert.Equal(reserved, result.OtherReservedAdvances);
        Assert.Equal(300m - reserved, result.RemainingExcludingThisRequest);
    }
    [Fact]
    public void Percentage_capacity_is_exact_and_does_not_round_small_fractional_limits_up()
    {
        var result = ManualPaymentOrderAdvanceCapacity.Calculate(Source() with { ApprovedGrossAmount = 0.0001m },
            Terms() with { MaximumAdvancePercent = 0.0001m }, [], ManualPaymentOrderSettlementReview.NoUnreconciledRegisteredSettlements);
        Assert.Equal(0.0000000001m, result.AdvanceCeiling);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentOrderAdvanceCapacity.RequireAmount(0.0001m, result));
    }
    [Fact]
    public void Approved_order_eight_decimal_precision_is_preserved_in_percentage_capacity()
    {
        var source = Source() with { ApprovedGrossAmount = 1000.00000001m };
        var claim = Claim() with { ApprovedOrderGross = source.ApprovedGrossAmount };
        var result = ManualPaymentOrderAdvanceCapacity.Calculate(source, Terms(), [claim], ManualPaymentOrderSettlementReview.NoUnreconciledRegisteredSettlements);
        Assert.Equal(300.000000003m, result.AdvanceCeiling);
        Assert.Equal(200.000000003m, result.RemainingExcludingThisRequest);
        ManualPaymentOrderAdvanceCapacity.RequireAmount(200m, result);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentOrderAdvanceCapacity.RequireAmount(200.0001m, result));
    }
    [Fact]
    public void Exact_four_decimal_amount_is_allowed_at_remaining_boundary_only()
    {
        var current = Claim(amount: 200.1234m); var result = Capacity([current]);
        ManualPaymentOrderAdvanceCapacity.RequireAmount(99.8766m, result);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentOrderAdvanceCapacity.RequireAmount(99.8767m, result));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentOrderAdvanceCapacity.RequireAmount(0.00001m, result));
    }
    [Fact]
    public void Current_request_exclusion_requires_exact_retained_claim_and_duplicates_are_rejected()
    {
        var current = Claim(); Assert.Equal(300m, Capacity([current], current.RequestId).RemainingExcludingThisRequest);
        Assert.Throws<InvalidOperationException>(() => Capacity([current], Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => Capacity([current, current]));
        Assert.Throws<InvalidOperationException>(() => Capacity([current], Guid.Empty));
    }
    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(99)]
    public void Unknown_or_unreconciled_settlements_never_mean_zero_paid(int state) => Assert.Throws<InvalidOperationException>(() =>
        ManualPaymentOrderAdvanceCapacity.Calculate(Source(), Terms(), [], (ManualPaymentOrderSettlementReview)state));

    [Fact]
    public void Out_of_scope_and_stale_claims_are_not_silently_excluded_from_capacity()
    {
        var current = Claim();
        foreach (var invalid in new[] { current with { TenantId = "other" }, current with { LegalEntityId = Guid.NewGuid() },
            current with { BranchId = Guid.NewGuid() }, current with { SupplierId = Guid.NewGuid() },
            current with { OrderId = Guid.NewGuid() }, current with { CurrencyCode = "USD" },
            current with { CommercialVersion = 1 }, current with { ApprovedOrderGross = 999m } })
            Assert.Throws<InvalidOperationException>(() => Capacity([invalid]));
    }
    [Theory]
    [InlineData("Draft")] [InlineData("Cancelled")] [InlineData("Closed")] [InlineData("OnHold")] [InlineData("Unknown")]
    public void New_advance_cannot_use_unapproved_or_closed_source(string status) => Assert.Throws<InvalidOperationException>(() =>
        ManualPaymentOrderAdvanceCapacity.Calculate(Source() with { Status = status }, Terms(), [], ManualPaymentOrderSettlementReview.NoUnreconciledRegisteredSettlements));
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(100.0001)]
    public void Zero_negative_and_above_full_order_percentage_are_never_defaults(double percent) => Assert.Throws<InvalidOperationException>(() =>
        ManualPaymentOrderAdvanceCapacity.Calculate(Source(), Terms() with { MaximumAdvancePercent = (decimal)percent }, [], ManualPaymentOrderSettlementReview.NoUnreconciledRegisteredSettlements));
    [Fact]
    public void Forged_capacity_snapshots_cannot_expand_amount_limit()
    {
        var capacity = Capacity([]);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentOrderAdvanceCapacity.RequireAmount(400m,
            capacity with { RemainingExcludingThisRequest = 400m }));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentOrderAdvanceCapacity.RequireAmount(1m,
            capacity with { OtherReservedAdvances = -1m, RemainingExcludingThisRequest = 301m }));
    }
    [Fact]
    public void Corrupt_approved_or_recorded_history_fails_instead_of_freeing_money()
    {
        foreach (var invalid in new[] { Claim("Approved") with { ApprovedAmount = null }, Claim("ConfirmedManually") with { RecordedAmount = 90m },
            Claim("PendingApproval") with { RecordedAmount = 100m }, Claim("Draft") with { ApprovedAmount = 100m } })
            Assert.Throws<InvalidOperationException>(() => Capacity([invalid]));
        Assert.Throws<InvalidOperationException>(() => Capacity([Claim(amount: 301m)]));
    }
    [Fact]
    public void Allocation_is_capped_by_both_confirmed_advance_and_actual_invoice_capacity()
    {
        var advance = Claim("ConfirmedManually", 200m); var old = Allocation(advance.RequestId, Guid.NewGuid(), 150m);
        var other = Allocation(Guid.NewGuid(), amount: 380m);
        var result = ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [old], [other]);
        Assert.Equal(50m, result.RemainingAdvance); Assert.Equal(20m, result.RemainingInvoice); Assert.Equal(20m, result.MaximumNewAllocation);
        ManualPaymentAdvanceAllocationCapacity.RequireAmount(20m, result);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.RequireAmount(20.0001m, result));
    }
    [Theory]
    [InlineData("Draft")] [InlineData("Approved")] [InlineData("PaymentRecorded")] [InlineData("Disputed")]
    public void Unconfirmed_or_disputed_payment_is_never_an_invoice_credit(string status) => Assert.Throws<InvalidOperationException>(() =>
        ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), Claim(status), Bill(), Order, [], []));
    [Fact]
    public void Closing_order_does_not_destroy_allocation_of_already_confirmed_money()
    {
        var result = ManualPaymentAdvanceAllocationCapacity.Calculate(Source() with { Status = "Closed" },
            Claim("ConfirmedManually"), Bill(), Order, [], []);
        Assert.Equal(100m, result.MaximumNewAllocation);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(
            Source() with { Status = "Draft" }, Claim("ConfirmedManually"), Bill(), Order, [], []));
    }
    [Fact]
    public void Allocation_history_must_agree_across_invoice_and_advance_queries()
    {
        var advance = Claim("ConfirmedManually"); var row = Allocation(advance.RequestId);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [row], []));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [row], [row with { Amount = 31m }]));
        var result = ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [row], [row]);
        Assert.Equal(70m, result.RemainingAdvance); Assert.Equal(370m, result.RemainingInvoice);
    }
    [Fact]
    public void One_allocation_identity_cannot_describe_two_different_sources()
    {
        var advance = Claim("ConfirmedManually");
        var a = Allocation(advance.RequestId, Guid.NewGuid());
        var b = a with { AdvanceRequestId = Guid.NewGuid(), InvoiceId = Invoice };
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [a], [b]));
    }
    [Fact]
    public void Explicit_reversal_projection_preserves_record_without_consuming_capacity()
    {
        var advance = Claim("ConfirmedManually"); var row = Allocation(advance.RequestId) with { State = "Reversed" };
        var result = ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [row], [row]);
        Assert.Equal(100m, result.RemainingAdvance); Assert.Equal(400m, result.RemainingInvoice);
    }
    [Fact]
    public void Wrong_source_duplicate_rows_and_overallocated_history_block()
    {
        var advance = Claim("ConfirmedManually"); var row = Allocation(advance.RequestId);
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Guid.NewGuid(), [], []));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill() with { BranchId = Guid.NewGuid() }, Order, [], []));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [row, row], [row]));
        var excess = row with { Amount = 100.0001m };
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance, Bill(), Order, [excess], [excess]));
        Assert.Throws<InvalidOperationException>(() => ManualPaymentAdvanceAllocationCapacity.Calculate(Source(), advance,
            Bill() with { ExcludingManualPaymentRequestId = Guid.NewGuid() }, Order, [], []));
    }
}
