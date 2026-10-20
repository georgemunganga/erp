using System.Globalization;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class InvoiceMatchingFoundationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Split_midpoint_credits_cannot_restore_full_quantity_without_cumulative_net_or_tax_credit(bool taxOnly)
    {
        var f = new Fixture();
        f.Input = f.Input with
        {
            Order = f.Input.Order with { Lines = [f.OrderLine with
                { Quantity = 2, UnitPrice = taxOnly ? 0m : .005m, TaxAmount = taxOnly ? .01m : 0m }] },
            Invoice = f.Input.Invoice with { TaxAmount = taxOnly ? .01m : 0m, Lines = [f.Line with
                { UnitPrice = taxOnly ? 0m : .005m, TaxAmount = taxOnly ? .01m : 0m }] },
        };
        f.Quantity(2);
        // Explicit commercial tolerance must not weaken the separate original-credit
        // quantity restoration invariant. All other quantity/unit-price limits stay exact.
        var oneCent = new InvoiceFieldTolerance(.01m, 0, InvoiceToleranceCombination.AnyLimit);
        f.Rule(f.RuleValue with { Total = oneCent, Tax = taxOnly ? oneCent : f.RuleValue.Tax });
        var debit = f.Debit(2) with { TaxAmount = taxOnly ? .01m : 0m };
        var first = f.Credit(debit, 1, 0) with { TaxAmount = 0 };
        var second = f.Credit(debit, 1, 0) with { TaxAmount = 0 };
        f.History(debit, first, second);
        var blocked = f.Evaluate();
        Assert.False(blocked.ComparisonPassed);
        Assert.Contains(blocked.Discrepancies, x => x.Code == "CumulativeCreditQuantityAmountMismatch");
        Assert.DoesNotContain(blocked.Discrepancies, x => x.Code == "CreditQuantityAmountMismatch");
        // A separately validated linked value/tax-only remainder can complete the
        // commercial credit, without itself restoring any additional quantity.
        var remainder = f.Credit(debit, 0, taxOnly ? 0m : .01m) with { TaxAmount = taxOnly ? .01m : 0m };
        f.History(debit, first, second, remainder);
        var completed = f.Evaluate();
        Assert.True(completed.ComparisonPassed, string.Join(", ", completed.Discrepancies.Select(x => x.Code)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Partial_two_or_three_way_comparison_is_not_AP_or_payment_authority(bool threeWay)
    {
        var f = new Fixture(threeWay); f.Quantity(2);
        var result = f.Evaluate();
        Assert.True(result.ComparisonPassed); Assert.False(result.CanPostToFinance);
        Assert.Equal("2", Assert.Single(result.Lines).Quantity);
        Assert.Equal("20", result.Lines[0].NetAmount);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("receipt")]
    public void Partial_prior_billing_cannot_overconsume_order_or_accepted_quantity(string ceiling)
    {
        var f = new Fixture(true); f.Quantity(3); f.History(f.Debit(8));
        if (ceiling == "receipt") f.Input = f.Input with { Acceptances = [f.Acceptance with { AcceptedQuantity = 9 }] };
        var result = f.Evaluate();
        Assert.Contains(result.Discrepancies, x => x.Code == "CumulativeOrderQuantityOutsideTolerance");
        Assert.Contains(result.Discrepancies, x => x.Code == "CumulativeAcceptedQuantityOutsideTolerance");
    }

    [Fact]
    public void Accepted_goods_only_and_net_returns_reduce_usable_capacity()
    {
        var f = new Fixture(true); f.Quantity(9);
        f.Input = f.Input with { Acceptances = [f.Acceptance with { ReturnedQuantity = 2 }] };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "CumulativeAcceptedQuantityOutsideTolerance");
        f.Input = f.Input with { Acceptances = [f.Acceptance with { Reversed = true }] };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "AuthoritativeAcceptedEvidenceInvalid");
    }

    [Fact]
    public void Service_needs_both_certified_quantity_and_value_even_if_commercial_tolerance_would_allow_more()
    {
        var f = new Fixture(true, service: true); f.Quantity(2);
        f.Input = f.Input with { Acceptances = [f.Acceptance with { AcceptedNetAmount = 19 }] };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "CertifiedServiceValueExceeded");
        f.Input = f.Input with { Acceptances = [f.Acceptance with { AcceptedNetAmount = 20 }] };
        Assert.True(f.Evaluate().ComparisonPassed);
    }

    [Fact]
    public void Exact_eight_decimal_certified_net_is_not_forced_to_invoice_money_scale()
    {
        var f = SmallCertifiedService();
        var result = f.Evaluate();
        Assert.True(result.ComparisonPassed, string.Join(", ", result.Discrepancies.Select(x => x.Code)));
        Assert.Equal("0.0001", Assert.Single(result.Lines).NetAmount);
        Assert.False(result.CanPostToFinance);
        Assert.Contains("0.00010001", result.EvidenceJson);
    }

    [Fact]
    public void Eight_decimal_return_reduces_hard_service_ceiling_even_with_commercial_tolerance()
    {
        var f = SmallCertifiedService();
        var permissive = new InvoiceFieldTolerance(1, 100, InvoiceToleranceCombination.AnyLimit);
        f.Rule(f.RuleValue with { Quantity = permissive, Total = permissive, UnitPrice = permissive });
        f.Input = f.Input with { Acceptances = [f.Acceptance with
            { ReturnedQuantity = .0002m, ReturnedNetAmount = .00000002m }] };
        var result = f.Evaluate();
        // .00010001 - .00000002 = .00009999, below the rounded invoice .0001.
        Assert.False(result.ComparisonPassed);
        Assert.Contains(result.Discrepancies, x => x.Code == "CertifiedServiceValueExceeded");
        Assert.DoesNotContain(result.Discrepancies, x => x.Code == "AuthoritativeAcceptedEvidenceInvalid"
            || x.Code == "CumulativeAcceptedQuantityOutsideTolerance");
    }

    [Fact]
    public void Accepted_net_scale_beyond_eight_is_rejected_without_truncation()
    {
        var f = SmallCertifiedService();
        f.Input = f.Input with { Acceptances = [f.Acceptance with { AcceptedNetAmount = .000100001m }] };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "AuthoritativeAcceptedEvidenceInvalid");
    }

    [Fact]
    public void Large_exact_certified_net_does_not_overflow_decimal_during_integer_conversion()
    {
        var f = new Fixture(true, service: true);
        const decimal quantity = 99_999_999_999_999.9999m;
        const decimal price = 100_000_000m;
        const decimal certified = 9_999_999_999_999_999_990_000m;
        f.Input = f.Input with
        {
            Order = f.Input.Order with { Lines = [f.OrderLine with { Quantity = quantity, UnitPrice = price }] },
            Acceptances = [f.Acceptance with { AcceptedQuantity = quantity, AcceptedNetAmount = certified }],
            Invoice = f.Input.Invoice with { NetAmount = price, GrossAmount = price,
                Lines = [f.Line with { Quantity = 1, UnitPrice = price }] },
        };
        var result = f.Evaluate();
        Assert.True(result.ComparisonPassed, string.Join(", ", result.Discrepancies.Select(x => x.Code)));
        Assert.Equal("100000000", Assert.Single(result.Lines).NetAmount);
    }

    private static Fixture SmallCertifiedService()
    {
        var f = new Fixture(true, service: true);
        f.Input = f.Input with
        {
            Policy = f.Input.Policy with { MoneyScale = 4 },
            Order = f.Input.Order with { Lines = [f.OrderLine with { UnitPrice = .0001m }] },
            Acceptances = [f.Acceptance with { AcceptedQuantity = 1.0001m, AcceptedNetAmount = .00010001m }],
            Invoice = f.Input.Invoice with { NetAmount = .0001m, GrossAmount = .0001m,
                Lines = [f.Line with { Quantity = 1, UnitPrice = .0001m }] },
        };
        return f;
    }

    [Fact]
    public void Validated_proportional_credit_restores_only_original_line_capacity()
    {
        var f = new Fixture(true); var debit = f.Debit(8); var credit = f.Credit(debit, 2, 20);
        f.Quantity(4); f.History(debit, credit);
        Assert.True(f.Evaluate().ComparisonPassed);
        f.History(debit, credit with { NetAmount = 0 });
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "CreditQuantityAmountMismatch");
    }

    [Theory]
    [InlineData("orphan")]
    [InlineData("over-credit")]
    [InlineData("other-source")]
    [InlineData("value-only")]
    public void Credits_cannot_invent_quantity_or_change_original_billing_source(string mutation)
    {
        var f = new Fixture(true); var debit = f.Debit(8); var credit = f.Credit(debit, 2, 20);
        credit = mutation switch
        {
            "orphan" => credit with { OriginalDebitId = Guid.NewGuid() },
            "over-credit" => credit with { Quantity = 9, NetAmount = 90 },
            "other-source" => credit with { AcceptanceId = null },
            _ => credit with { Quantity = 0 },
        };
        f.Quantity(3); f.History(debit, credit);
        Assert.False(f.Evaluate().ComparisonPassed);
    }

    [Fact]
    public void Quantity_tolerance_is_applied_cumulatively_not_once_per_partial_bill()
    {
        var f = new Fixture(true); f.Quantity(2.5m); f.History(f.Debit(8));
        f.Rule(f.RuleValue with { Quantity = new(.5m, 5, InvoiceToleranceCombination.AllLimits) });
        f.Input = f.Input with { Policy = f.Input.Policy with { MoneyScale = 4 } };
        Assert.True(f.Evaluate().ComparisonPassed);
        f.Quantity(2.5001m);
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "CumulativeOrderQuantityOutsideTolerance");
    }

    [Fact]
    public void Tax_and_total_tolerances_also_consider_prior_partial_billing()
    {
        var f = new Fixture(); f.Quantity(2);
        f.Input = f.Input with { Order = f.Input.Order with { Lines = [f.OrderLine with { TaxAmount = 20 }] } };
        f.Input = f.Input with { Invoice = f.Input.Invoice with { TaxAmount = 4, GrossAmount = 24,
            Lines = [f.Line with { TaxAmount = 4 }] } };
        Assert.True(f.Evaluate().ComparisonPassed);
        f.Input = f.Input with { Invoice = f.Input.Invoice with { TaxAmount = 4.01m, GrossAmount = 24.01m,
            Lines = [f.Line with { TaxAmount = 4.01m }] } };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "CumulativeTaxOutsideTolerance");
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "CumulativeTotalOutsideTolerance");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("currency")]
    [InlineData("expired")]
    [InlineData("company")]
    public void Policy_is_explicit_per_category_currency_and_effective_scope(string mutation)
    {
        var f = new Fixture();
        f.Input = f.Input with { Policy = mutation switch
        {
            "missing" => f.Input.Policy with { Rules = [] },
            "ambiguous" => f.Input.Policy with { Rules = [f.RuleValue, f.RuleValue] },
            "currency" => f.Input.Policy with { Rules = [f.RuleValue with { CurrencyCode = "USD" }] },
            "expired" => f.Input.Policy with { EffectiveUntil = f.Now },
            _ => f.Input.Policy with { LegalEntityId = Guid.NewGuid() },
        } };
        Assert.False(f.Evaluate().ComparisonPassed);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("branch")]
    [InlineData("supplier")]
    [InlineData("revision")]
    [InlineData("unit")]
    [InlineData("empty-acceptance")]
    [InlineData("missing-receipt")]
    [InlineData("draft-order")]
    [InlineData("draft-policy")]
    [InlineData("unaccepted")]
    public void Authoritative_source_and_scope_mismatches_fail_closed(string mutation)
    {
        var f = new Fixture(true);
        f.Input = mutation switch
        {
            "tenant" => f.Input with { Invoice = f.Input.Invoice with { TenantId = "foreign" } },
            "company" => f.Input with { Order = f.Input.Order with { LegalEntityId = Guid.NewGuid() } },
            "branch" => f.Input with { Acceptances = [f.Acceptance with { BranchRef = Guid.NewGuid() }] },
            "supplier" => f.Input with { Order = f.Input.Order with { SupplierId = Guid.NewGuid() } },
            "revision" => f.Input with { Acceptances = [f.Acceptance with { RevisionId = Guid.NewGuid() }] },
            "unit" => f.Input with { Invoice = f.Input.Invoice with { Lines = [f.Line with { UnitCode = "box" }] } },
            "empty-acceptance" => f.Input with { Acceptances = [f.Acceptance with { AcceptedQuantity = 0 }] },
            "draft-order" => f.Input with { Order = f.Input.Order with { RevisionState = "PendingApproval" } },
            "draft-policy" => f.Input with { Policy = f.Input.Policy with { State = "Draft" } },
            "unaccepted" => f.Input with { Acceptances = [f.Acceptance with { State = "PendingInspection" }] },
            _ => f.Input with { Acceptances = [] },
        };
        Assert.False(f.Evaluate().ComparisonPassed);
    }

    [Fact]
    public void Duplicate_identity_is_company_wide_and_does_not_reveal_other_invoice_ID()
    {
        var f = new Fixture(); var duplicate = f.Input.Duplicates.Identities[0] with { InvoiceId = Guid.NewGuid() };
        f.Input = f.Input with { Duplicates = f.Input.Duplicates with { Identities = [f.Input.Duplicates.Identities[0], duplicate] } };
        var result = f.Evaluate();
        Assert.Contains(result.Discrepancies, x => x.Code == "DuplicateInvoiceIdentity" && x.InvoiceLineId == null);
        Assert.DoesNotContain(result.Discrepancies, x => x.Code.Contains(duplicate.InvoiceId.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(duplicate.InvoiceId.ToString("D"), result.EvidenceJson);
    }

    [Theory]
    [InlineData("history")]
    [InlineData("duplicates")]
    [InlineData("own-identity")]
    [InlineData("already-allocated")]
    public void Incomplete_or_repeated_evidence_cannot_authorize_a_comparison(string mutation)
    {
        var f = new Fixture();
        f.Input = mutation switch
        {
            "history" => f.Input with { Consumption = f.Input.Consumption with { Complete = false } },
            "duplicates" => f.Input with { Duplicates = f.Input.Duplicates with { Complete = false } },
            "own-identity" => f.Input with { Duplicates = f.Input.Duplicates with { Identities = [] } },
            _ => f.Input with { Consumption = f.Input.Consumption with { Allocations = [f.Debit(1) with { InvoiceId = f.Input.Invoice.Id }] } },
        };
        Assert.False(f.Evaluate().ComparisonPassed);
    }

    [Fact]
    public void Exact_multiplication_uses_explicit_midpoint_rounding_without_binary_or_decimal_intermediate_loss()
    {
        var f = new Fixture(); f.Input = f.Input with
        {
            Policy = f.Input.Policy with { MoneyScale = 4, Rounding = InvoiceMoneyRounding.ToEven },
            Order = f.Input.Order with { Lines = [f.OrderLine with { UnitPrice = .0001m }] },
            Invoice = f.Input.Invoice with { NetAmount = 0, GrossAmount = 0,
                Lines = [f.Line with { Quantity = .5m, UnitPrice = .0001m }] },
        };
        Assert.True(f.Evaluate().ComparisonPassed);
        f.Input = f.Input with { Policy = f.Input.Policy with { Rounding = InvoiceMoneyRounding.AwayFromZero },
            Invoice = f.Input.Invoice with { NetAmount = .0001m, GrossAmount = .0001m } };
        Assert.True(f.Evaluate().ComparisonPassed);
    }

    [Fact]
    public void Maximum_inputs_never_silently_round_or_overflow_decimal_product()
    {
        var f = new Fixture(); const decimal maximum = 99_999_999_999_999.9999m;
        f.Input = f.Input with { Order = f.Input.Order with { Lines = [f.OrderLine with { Quantity = maximum, UnitPrice = maximum }] },
            Invoice = f.Input.Invoice with { NetAmount = maximum, GrossAmount = maximum,
                Lines = [f.Line with { Quantity = maximum, UnitPrice = maximum }] }, Policy = f.Input.Policy with { MoneyScale = 4 } };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "InvoiceHeaderLineTotalsMismatch");
        f.Input = f.Input with { Invoice = f.Input.Invoice with { Lines = [f.Line with { Quantity = .00001m }] } };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "InvoiceLineInvalid");
    }

    [Fact]
    public void Exact_command_replay_recovers_evidence_and_changed_source_command_or_response_conflicts()
    {
        var f = new Fixture(true); var original = f.Evaluate();
        Assert.Same(original, f.Evaluate(original));
        f.Command = f.Command with { Reason = "Different intent" };
        Assert.Throws<InvalidOperationException>(() => f.Evaluate(original));
        f.Command = f.Command with { Reason = null };
        Assert.Throws<InvalidOperationException>(() => f.Evaluate(original with { Outcome = "Blocked" }));
        f.Input = f.Input with { Consumption = f.Input.Consumption with { Watermark = "new-revision" } };
        Assert.Throws<InvalidOperationException>(() => f.Evaluate(original));
    }

    [Fact]
    public void Evidence_is_canonical_across_decimal_scale_culture_and_input_collection_order()
    {
        var f = new Fixture(); var first = f.Evaluate(); var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            f.Input = f.Input with { Invoice = f.Input.Invoice with { Lines = [f.Line with { Quantity = 1.0000m, UnitPrice = 10.0000m }] } };
            var second = f.Evaluate();
            Assert.Equal(first.InputFingerprint, second.InputFingerprint); Assert.Equal(first.EvidenceJson, second.EvidenceJson);
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    [Fact]
    public void Existing_intake_projection_needs_verified_line_units_and_does_not_mutate_source_records()
    {
        var f = new Fixture(); var source = f.Input.Invoice; var line = f.Line;
        var invoice = new SupplierInvoice { Id = source.Id, Version = source.Version, TenantId = source.TenantId,
            LegalEntityId = source.LegalEntityId, BranchRef = source.BranchRef, SupplierId = source.SupplierId,
            PurchaseOrderId = source.PurchaseOrderId, InvoiceNumber = source.InvoiceNumber,
            NormalizedInvoiceNumber = source.NormalizedInvoiceNumber, CurrencyCode = source.CurrencyCode,
            InvoiceDate = source.InvoiceDate, DueDate = source.DueDate, NetAmount = source.NetAmount, TaxAmount = source.TaxAmount, GrossAmount = source.GrossAmount };
        var invoiceLine = new SupplierInvoiceLine { Id = line.Id, Version = line.Version, TenantId = line.TenantId,
            LegalEntityId = line.LegalEntityId, SupplierInvoiceId = line.SupplierInvoiceId, PurchaseOrderLineId = line.PurchaseOrderLineId,
            Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice };
        f.Input = f.Input with { Invoice = InvoiceMatchDocument.From(invoice, [invoiceLine]) };
        Assert.Contains(f.Evaluate().Discrepancies, x => x.Code == "VerifiedInvoiceUnitMappingRequired");
        f.Input = f.Input with { Invoice = InvoiceMatchDocument.From(invoice, [invoiceLine], new Dictionary<Guid, string> { [line.Id] = "each" }) };
        Assert.True(f.Evaluate().ComparisonPassed); Assert.Equal("Received", invoice.Status);
        Assert.Null(invoice.FinancePostingRef);
    }

    [Theory]
    [InlineData("credit")]
    [InlineData("posted")]
    [InlineData("closed")]
    public void Intake_state_or_existing_Finance_credit_identity_cannot_be_treated_as_new_debit(string scenario)
    {
        var f = new Fixture();
        f.Input = f.Input with { Invoice = scenario switch
        {
            "credit" => f.Input.Invoice with { CreditForInvoiceRef = "ORIGINAL-INVOICE" },
            "posted" => f.Input.Invoice with { FinancePostingRef = "AP-POSTING" },
            _ => f.Input.Invoice with { Status = "Paid" },
        } };
        Assert.False(f.Evaluate().ComparisonPassed);
    }

    private sealed class Fixture : IProcurementScope
    {
        public string TenantId => "matching-test";
        public Guid LegalEntityId { get; } = Guid.NewGuid();
        public Guid? WorkLocationId { get; } = Guid.NewGuid();
        public Guid? OrgUnitId => null;
        public bool IsConfined => true;
        public string SubjectId => "matcher";
        public string? CorrelationId => "invoice-match-test";
        public DateTimeOffset Now { get; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public InvoiceMatchingInput Input { get; set; }
        public InvoiceMatchCommand Command { get; set; }
        public InvoiceMatchDocumentLine Line => Input.Invoice.Lines[0];
        public InvoiceIssuedOrderLine OrderLine => Input.Order.Lines[0];
        public InvoiceAcceptedFulfilment Acceptance => Input.Acceptances[0];
        public InvoiceCategoryMatchRule RuleValue => Input.Policy.Rules[0];
        public Fixture(bool threeWay = false, bool service = false)
        {
            var invoiceId = Guid.NewGuid(); var supplier = Guid.NewGuid(); var order = Guid.NewGuid(); var revision = Guid.NewGuid();
            var orderLine = new InvoiceIssuedOrderLine(Guid.NewGuid(), 1, "IT", "each", 10, 10, 0);
            var receipt = new InvoiceAcceptedFulfilment(Guid.NewGuid(), 1, TenantId, LegalEntityId, WorkLocationId,
                order, revision, orderLine.Id, "ZMW", "each", service ? InvoiceAcceptanceKind.Service : InvoiceAcceptanceKind.Goods,
                "Accepted", "SYNTHETIC-ACCEPTANCE", Now.AddHours(-1), false, 10, 0, service ? 100 : 0, 0);
            var line = new InvoiceMatchDocumentLine(Guid.NewGuid(), 1, invoiceId, TenantId, LegalEntityId, orderLine.Id,
                threeWay && !service ? receipt.Id : null, threeWay && service ? receipt.Id : null, 1, 10, 0, "Synthetic line", "each", null, null);
            var invoice = new InvoiceMatchDocument(invoiceId, 1, TenantId, LegalEntityId, WorkLocationId, supplier, order,
                "INV-1", "INV-1", "ZMW", "Received", new DateOnly(2026, 10, 20), null, 10, 0, 10, null, null, null, [line]);
            var exact = new InvoiceFieldTolerance(0, 0, InvoiceToleranceCombination.AllLimits);
            var rule = new InvoiceCategoryMatchRule("IT", "ZMW", threeWay ? InvoiceMatchingMethod.ThreeWay : InvoiceMatchingMethod.TwoWay,
                threeWay ? receipt.Kind : null, exact, exact, exact, exact);
            Input = new(invoice, new(order, revision, 1, 1, TenantId, LegalEntityId, WorkLocationId, supplier,
                "ZMW", "Issued", "SYNTHETIC-ISSUED", Now.AddDays(-1), [orderLine]), threeWay ? [receipt] : [],
                new(Guid.NewGuid(), 1, TenantId, LegalEntityId, null, "Published", "SYNTHETIC-APPROVED-POLICY", Now.AddDays(-1), null,
                    2, InvoiceMoneyRounding.ToEven, [rule]), new("consumption-1", true, []),
                new("duplicates-1", true, [new(invoiceId, TenantId, LegalEntityId, supplier, "INV-1", "ZMW")]));
            Command = new(Guid.NewGuid(), 1, Now, null);
        }
        public void Quantity(decimal quantity) => Input = Input with { Invoice = Input.Invoice with
            { NetAmount = quantity * Line.UnitPrice, GrossAmount = quantity * Line.UnitPrice + Line.TaxAmount,
                Lines = [Line with { Quantity = quantity }] } };
        public void Rule(InvoiceCategoryMatchRule rule) => Input = Input with { Policy = Input.Policy with { Rules = [rule] } };
        public void History(params InvoiceMatchConsumption[] rows) => Input = Input with { Consumption = Input.Consumption with { Allocations = rows } };
        public InvoiceMatchConsumption Debit(decimal quantity) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId,
            LegalEntityId, WorkLocationId, Input.Invoice.SupplierId, Input.Order.Id, Input.Order.RevisionId, OrderLine.Id,
            RuleValue.Method == InvoiceMatchingMethod.ThreeWay ? Acceptance.Id : null, "ZMW", InvoiceConsumptionKind.Debit,
            null, quantity, quantity * OrderLine.UnitPrice, 0, "SYNTHETIC-VALIDATED-MATCH");
        public InvoiceMatchConsumption Credit(InvoiceMatchConsumption debit, decimal quantity, decimal net) => debit with
            { Id = Guid.NewGuid(), InvoiceId = Guid.NewGuid(), InvoiceLineId = Guid.NewGuid(), Kind = InvoiceConsumptionKind.Credit,
                OriginalDebitId = debit.Id, Quantity = quantity, NetAmount = net };
        public InvoiceMatchingEvaluation Evaluate(InvoiceMatchingEvaluation? previous = null) => InvoiceMatchingFoundation.Evaluate(Command, this, Input, previous);
    }
}
