using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseOrderSourceAllocationTests
{
    [Fact]
    public void Reserved_quantity_moves_to_ordered_once_and_is_not_available_to_another_buyer()
    {
        var f = new Fixture();
        var allocation = f.Reserve(3);
        Assert.Equal(7, f.Available());
        f.Order.Status = "Approved";
        PurchaseOrderSourceAllocation.ConfirmOrdered(f.Source, f.Order, f.PoLine, allocation, f.Now.AddMinutes(2));
        Assert.Equal(0, f.Line.AllocatedQuantity);
        Assert.Equal(3, f.Line.OrderedQuantity);
        Assert.Equal(7, f.Available());
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderSourceAllocation.ConfirmOrdered(
            f.Source, f.Order, f.PoLine, allocation, f.Now.AddMinutes(2)));
        Assert.Equal(3, f.Line.OrderedQuantity);
    }

    [Fact]
    public void Another_order_cannot_reserve_more_than_remaining_approved_quantity()
    {
        var f = new Fixture();
        _ = f.Reserve(8);
        var other = f.NewOrder(); var line = f.NewPoLine(other, 3);
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderSourceAllocation.Reserve(f.Source,
            other, line, 3, Guid.NewGuid(), "other-buyer", f.Now.AddMinutes(2)));
        Assert.Equal(8, f.Line.AllocatedQuantity);
    }

    [Fact]
    public void Unreconciled_counter_does_not_make_quantity_available()
    {
        var f = new Fixture();
        _ = f.Reserve(8);
        f.Line.AllocatedQuantity = 0;
        Assert.Throws<InvalidOperationException>(() => f.Available());
    }

    [Theory]
    [InlineData("unapproved")]
    [InlineData("wrongCoreSource")]
    [InlineData("policyUnitChange")]
    [InlineData("wrongCurrency")]
    [InlineData("foreignSource")]
    [InlineData("methodUnconfigured")]
    [InlineData("quotesRequired")]
    public void Source_authority_cannot_be_replaced_by_a_label_or_unknown_buying_method(string change)
    {
        var f = new Fixture();
        switch (change)
        {
            case "unapproved": f.Case.State = "Pending"; break;
            case "wrongCoreSource": f.Case.SourceId = Guid.NewGuid(); break;
            case "policyUnitChange": f.Line.UnitCode = "BOX"; break;
            case "wrongCurrency": f.Request.CurrencyCode = "USD"; break;
            case "foreignSource": f.Request.LegalEntityId = Guid.NewGuid(); break;
            case "methodUnconfigured": f.Method.ExecutionRoute = "Unconfigured"; break;
            case "quotesRequired": f.Method.ExecutionRoute = "CompetitiveSourcing"; break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Available());
    }

    [Fact]
    public void Unsupported_unit_conversion_and_currency_cannot_reserve_source_authority()
    {
        var f = new Fixture();
        f.PoLine.UnitCode = "BOX";
        Assert.Throws<InvalidOperationException>(() => f.Reserve(1));
        f.PoLine.UnitCode = "EA"; f.Order.CurrencyCode = "USD";
        Assert.Throws<InvalidOperationException>(() => f.Reserve(1));
        Assert.Equal(0, f.Line.AllocatedQuantity);
    }

    [Fact]
    public void Draft_release_requires_no_active_Finance_commitment_and_keeps_allocation_history()
    {
        var f = new Fixture(); var allocation = f.Reserve(4);
        f.Order.FinanceCommitmentRef = "FINANCE-COMMITMENT";
        Assert.Throws<InvalidOperationException>(() => PurchaseOrderSourceAllocation.ReleaseDraft(
            f.Source, f.Order, allocation, "Cancelled", f.Now.AddMinutes(2)));
        Assert.Equal(4, f.Line.AllocatedQuantity);
        f.Order.FinanceCommitmentRef = null;
        PurchaseOrderSourceAllocation.ReleaseDraft(f.Source, f.Order, allocation, "Cancelled", f.Now.AddMinutes(2));
        Assert.Equal("Released", allocation.Status);
        Assert.Equal(10, f.Available());
        Assert.Single(f.Ledger);
    }

    [Fact]
    public void Source_unit_value_is_retained_separately_from_proposed_PO_value()
    {
        var f = new Fixture(); f.PoLine.UnitPrice = 20;
        var allocation = f.Reserve(2);
        var source = PurchaseOrderSourceAllocation.Check(f.Source, Fixture.Tenant, f.Company, f.Now.AddMinutes(2));
        Assert.Equal(10, source.ApprovedUnitPrice);
        Assert.Equal(40, allocation.EstimatedValue);
        Assert.Equal(100, source.ApprovedLineBase);
        Assert.Equal("Draft", f.Order.Status); // a larger proposed value still needs independent PO approval/Finance.
    }

    private sealed class Fixture
    {
        public const string Tenant = "source-test";
        public Guid Company { get; } = Guid.NewGuid();
        public DateTimeOffset Now { get; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public PurchaseRequest Request { get; }
        public PurchaseRequestLine Line => Request.Lines.Single();
        public ProcurementMethod Method { get; }
        public ApprovalCase Case { get; }
        public ApprovedPurchaseSource Source { get; }
        public List<PurchaseRequestAllocation> Ledger { get; } = [];
        public PurchaseOrder Order { get; }
        public PurchaseOrderLine PoLine { get; }

        public Fixture()
        {
            Request = Scoped(new PurchaseRequest { Number = "PR-TEST", RequesterWorkerId = Guid.NewGuid(),
                CurrencyCode = "ZMW", Purpose = "Source authority test", Status = "Submitted", Version = 1,
                SubmittedVersion = 1, SubmittedAt = Now, CreatedBy = "requester" });
            Request.Lines.Add(Scoped(new PurchaseRequestLine { PurchaseRequestId = Request.Id, LineNumber = 1,
                Description = "Item", UnitCode = "EA", RequestedQuantity = 10, EstimatedUnitPrice = 10,
                EstimatedCurrencyCode = "ZMW", CategoryCode = "OFFICE" }));
            Method = Scoped(new ProcurementMethod { Code = "BUSINESS_METHOD", Name = "Configured direct route",
                Status = "Active", ExecutionRoute = "DirectPurchase" });
            var policy = Scoped(new PolicyVersion { PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 1,
                State = "Published", EffectiveFrom = Now.AddDays(-1), RulesJson = "{}" });
            var evaluation = Scoped(new PolicyEvaluation { PurchaseRequestId = Request.Id, RequestVersion = 1,
                PolicyVersionId = policy.Id, PolicyVersionNumber = 1, Outcome = "Matched", AmountBasis = "EstimatedLineBase",
                DecisionJson = JsonSerializer.Serialize(new { requestId = Request.Id, requestVersion = 1, policyId = policy.Id,
                    lines = new[] { new { lineId = Line.Id, decision = new { status = "Match",
                        sourcingMethodCode = Method.Code, minimumQuoteCount = 0,
                        approvalStages = new[] { new { code = "NEED", assignee = "LineManager", mode = "One" } } } } } }) });
            var finance = Scoped(new BudgetCheckSnapshot { PurchaseRequestId = Request.Id, RequestVersion = 1,
                Status = "Available", CheckedAmount = 100, CurrencyCode = "ZMW", FinanceCheckRef = "CHECK",
                EvidenceRef = "SYNTHETIC", ReservationRef = "RESERVE", ReservationStatus = "Confirmed", CheckedAt = Now });
            Case = new ApprovalCase { TenantId = Tenant, LegalEntityId = Company, SourceModule = "procurement",
                SourceType = "purchase_request", SourceId = Request.Id, SourceVersion = 1,
                PolicyVersionId = policy.Id, PolicyVersionNumber = 1, MakerSubjectId = "requester" };
            var stage = new ApprovalStage { TenantId = Tenant, LegalEntityId = Company, CaseId = Case.Id,
                Sequence = 1, Code = "NEED", State = "Active", Mode = "Any", RequiredApprovals = 1 };
            var assignment = new ApprovalAssignment { TenantId = Tenant, LegalEntityId = Company, CaseId = Case.Id,
                StageId = stage.Id, AssigneeSubjectId = "independent-approver", PermissionKey = "procurement-approval-decide" };
            stage.Assignments.Add(assignment); Case.Stages.Add(stage);
            var pin = PurchaseRequestApprovalPinning.Pin(Request, evaluation, policy, finance, Tenant, Company,
                Case.Id, Guid.NewGuid(), "ApprovedManualEvidence", "SYNTHETIC", Now.AddSeconds(1));
            Request.ApprovalInstanceRef = Case.Id.ToString("D"); Request.Version++;
            var decisions = new List<ApprovalDecision>();
            ApprovalEngine.Decide(Case, assignment, new("independent-approver", Tenant, Company,
                new HashSet<string> { "procurement-approval-decide" }), "Approve", Guid.NewGuid().ToString("D"),
                null, decisions, Now.AddSeconds(2));
            var decision = decisions.Single();
            var effects = PurchaseRequestApprovalOutcomes.Apply(Request, pin, Case, decision, evaluation, policy,
                finance, Tenant, Company, Now.AddSeconds(2));
            Source = new(Request, Line, pin, effects.Outcome, Case, decision, evaluation, policy, finance, Method, Ledger);
            Order = NewOrder(); PoLine = NewPoLine(Order, 1);
        }
        public PurchaseOrder NewOrder() => Scoped(new PurchaseOrder { Number = Guid.NewGuid().ToString(),
            CurrencyCode = "ZMW", BuyerSubjectId = "buyer", SupplierId = Guid.NewGuid(), Status = "Draft" });
        public PurchaseOrderLine NewPoLine(PurchaseOrder order, decimal quantity) => Scoped(new PurchaseOrderLine
        { PurchaseOrderId = order.Id, PurchaseRequestLineId = Line.Id, LineNumber = 1, Description = "Item",
            UnitCode = "EA", Quantity = quantity, UnitPrice = 10 });
        public PurchaseRequestAllocation Reserve(decimal quantity)
        {
            PoLine.Quantity = quantity;
            var allocation = PurchaseOrderSourceAllocation.Reserve(Source, Order, PoLine, quantity,
                Guid.NewGuid(), "buyer", Now.AddMinutes(1));
            Ledger.Add(allocation); return allocation;
        }
        public decimal Available() => PurchaseOrderSourceAllocation.Check(Source, Tenant, Company, Now.AddMinutes(2)).AvailableQuantity;
        private T Scoped<T>(T record) where T : ProcurementRecord
        { record.TenantId = Tenant; record.LegalEntityId = Company; return record; }
    }
}
