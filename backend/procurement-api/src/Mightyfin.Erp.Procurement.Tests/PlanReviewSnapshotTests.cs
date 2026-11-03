using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PlanReviewSnapshotTests
{
    [Fact]
    public void Pins_exact_decimal_and_line_order_then_detects_business_change()
    {
        var plan = Plan();
        var original = PlanReviewSnapshot.Create(plan, 2);
        Assert.Contains("\"estimatedAmount\":\"36.5000\"", original.Json);
        Assert.Equal(36.50m, original.EstimatedAmount);
        var submission = new PlanReviewSubmission
        {
            ProcurementPlanId = plan.Id, BranchRef = plan.BranchRef!.Value,
            CurrencyCode = plan.CurrencyCode, SubmittedPlanVersion = 2,
            EstimatedAmount = original.EstimatedAmount,
            SnapshotJson = original.Json, SnapshotSha256 = original.Sha256,
        };
        Assert.True(PlanReviewSnapshot.Matches(plan, submission));
        plan.Version = 3; // A later status transition need not alter the frozen business evidence.
        Assert.True(PlanReviewSnapshot.Matches(plan, submission));
        plan.Lines.First().EstimatedAmount += 0.0001m;
        Assert.False(PlanReviewSnapshot.Matches(plan, submission));
    }

    [Fact]
    public void Branchless_legacy_plan_cannot_produce_approval_evidence()
    {
        var plan = Plan();
        plan.BranchRef = null;
        Assert.Throws<InvalidOperationException>(() => PlanReviewSnapshot.Create(plan, 2));
    }

    private static ProcurementPlan Plan()
    {
        var plan = new ProcurementPlan
        {
            BranchRef = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Number = "PLAN-1", OwnerSubjectId = "maker", CurrencyCode = "ZMW",
            FinancialPeriodRef = "FY-2027", Status = "Draft",
        };
        plan.Lines.Add(new ProcurementPlanLine { ProcurementPlanId = plan.Id, LineNumber = 2,
            Description = "Service", UnitCode = "EA", EstimatedQuantity = 1m, EstimatedAmount = 30m });
        plan.Lines.Add(new ProcurementPlanLine { ProcurementPlanId = plan.Id, LineNumber = 1,
            Description = "Goods", UnitCode = "EA", EstimatedQuantity = 2m, EstimatedAmount = 6.5m });
        return plan;
    }
}
