using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class CompetitiveSourcingSourceTests
{
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(20)]
    public void Explicit_competitive_quote_requirement_is_retained_without_authorizing_a_direct_order(int count)
    {
        var f = Fixture(count); f.Line.Route = "PendingSourcing";
        var actual = Check(f);
        Assert.Equal(count, actual.MinimumQuoteCount);
        Assert.Equal(10m, actual.AvailableQuantity); Assert.Equal(100m, actual.ApprovedLineBase);
        Assert.Equal(f.Method.Id, actual.MethodId); Assert.Equal(f.Source.Submission.Id, actual.SourceSubmissionId);
        Assert.Throws<InvalidOperationException>(() => f.Available());
        f.Line.Route = "Unassigned";
        Assert.Equal("OrderSourceMethodNotDirect", Assert.Throws<InvalidOperationException>(() => f.Reserve(1)).Message);
        Assert.Empty(f.Ledger); Assert.Equal(0m, f.Line.AllocatedQuantity);
    }

    [Theory]
    [InlineData(null)][InlineData(-1)][InlineData(21)]
    public void Unknown_or_out_of_contract_quote_requirements_are_not_defaulted(int? count)
    {
        var f = Fixture(count);
        Assert.Equal("SourcingSourcePolicyMismatch", Assert.Throws<InvalidOperationException>(() => Check(f)).Message);
        Assert.Empty(f.Ledger);
    }

    [Fact]
    public void Originally_pinned_missing_quote_requirement_is_unknown_not_zero()
    {
        var f = new PurchaseOrderSourceAllocationTests.Fixture(executionRoute: "CompetitiveSourcing",
            minimumQuoteCount: null, omitMinimumQuoteCount: true);
        ApprovedPurchaseDemand.Verify(f.Source, f.Request.TenantId, f.Company, f.Now.AddMinutes(2));
        Assert.Equal("SourcingSourcePolicyMismatch", Assert.Throws<InvalidOperationException>(() => Check(f)).Message);
        Assert.Empty(f.Ledger);
    }

    [Theory]
    [InlineData("direct")][InlineData("contract")][InlineData("inactive")]
    [InlineData("foreign")][InlineData("wrong-code")][InlineData("direct-line")]
    [InlineData("pin-tamper")][InlineData("missing-quotes")]
    public void Current_method_scope_route_and_original_policy_evidence_cannot_be_bypassed(string scenario)
    {
        var f = Fixture(2);
        switch (scenario)
        {
            case "direct": f.Method.ExecutionRoute = "DirectPurchase"; break;
            case "contract": f.Method.ExecutionRoute = "ContractRelease"; break;
            case "inactive": f.Method.Status = "Inactive"; break;
            case "foreign": f.Method.LegalEntityId = Guid.NewGuid(); break;
            case "wrong-code": f.Method.Code = "OTHER"; break;
            case "direct-line": f.Line.Route = "DirectPurchase"; break;
            case "missing-quotes": f.Source.Evaluation.DecisionJson = f.Source.Evaluation.DecisionJson.Replace("\"minimumQuoteCount\":2,", ""); break;
            default: f.Source.Evaluation.DecisionJson = f.Source.Evaluation.DecisionJson.Replace("\"minimumQuoteCount\":2", "\"minimumQuoteCount\":0"); break;
        }
        var before = (f.Request.UpdatedAt, f.Line.AllocatedQuantity, f.Line.OrderedQuantity);
        Assert.Throws<InvalidOperationException>(() => Check(f));
        Assert.Equal(before, (f.Request.UpdatedAt, f.Line.AllocatedQuantity, f.Line.OrderedQuantity));
        Assert.Empty(f.Ledger);
    }

    [Fact]
    public void Competitive_availability_uses_the_same_retained_PO_and_RFQ_ledger_without_mutation()
    {
        var f = Fixture(0);
        var sourcing = new SourcingEvent { TenantId = f.Request.TenantId, LegalEntityId = f.Company };
        var line = new SourcingLine { TenantId = f.Request.TenantId, LegalEntityId = f.Company,
            SourcingEventId = sourcing.Id, PurchaseRequestLineId = f.Line.Id, Status = "Cancelled" };
        f.Owners.Add(PurchaseDemandAllocationOwner.FromSourcing(sourcing, line));
        f.Ledger.Add(new PurchaseRequestAllocation { TenantId = f.Request.TenantId, LegalEntityId = f.Company,
            PurchaseRequestId = f.Request.Id, PurchaseRequestLineId = f.Line.Id, CurrencyCode = "ZMW",
            TargetType = "SourcingEvent", TargetId = sourcing.Id, Quantity = 4, EstimatedValue = 40, Status = "Reserved" });
        f.Ledger.Add(new PurchaseRequestAllocation { TenantId = f.Request.TenantId, LegalEntityId = f.Company,
            PurchaseRequestId = f.Request.Id, PurchaseRequestLineId = f.Line.Id, CurrencyCode = "ZMW",
            TargetType = "PurchaseOrder", TargetId = f.Order.Id, Quantity = 3, EstimatedValue = 30, Status = "Ordered" });
        f.Line.AllocatedQuantity = 4; f.Line.OrderedQuantity = 3;
        Assert.Equal(3m, Check(f).AvailableQuantity);
        f.Owners.RemoveAll(x => x.TargetType == "SourcingEvent");
        Assert.Throws<InvalidOperationException>(() => Check(f));
        Assert.Equal(4m, f.Line.AllocatedQuantity); Assert.Equal(3m, f.Line.OrderedQuantity);
    }

    [Fact]
    public void Nonrepresentable_approved_base_fails_before_any_sourcing_effect()
    {
        var f = new PurchaseOrderSourceAllocationTests.Fixture(99999999999999.9999m, 99999999999999.9999m,
            executionRoute: "CompetitiveSourcing", minimumQuoteCount: 0);
        var before = (f.Request.UpdatedAt, f.Line.AllocatedQuantity, f.Line.OrderedQuantity);
        Assert.Equal("ExactDecimalNotRepresentable", Assert.Throws<InvalidOperationException>(() => Check(f)).Message);
        Assert.Equal(before, (f.Request.UpdatedAt, f.Line.AllocatedQuantity, f.Line.OrderedQuantity));
        Assert.Empty(f.Ledger);
    }
    private static PurchaseOrderSourceAllocationTests.Fixture Fixture(int? count) =>
        new(executionRoute: "CompetitiveSourcing", minimumQuoteCount: count);
    private static CompetitiveSourceAvailability Check(PurchaseOrderSourceAllocationTests.Fixture f) =>
        CompetitiveSourcingSource.Check(f.Source, f.Request.TenantId, f.Company, f.Now.AddMinutes(2));
}
