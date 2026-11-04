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
            TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            ProcurementPlanId = plan.Id, BranchRef = plan.BranchRef!.Value,
            MakerSubjectId = plan.OwnerSubjectId,
            CurrencyCode = plan.CurrencyCode, SubmittedPlanVersion = 2,
            EstimatedAmount = original.EstimatedAmount,
            SnapshotJson = original.Json, SnapshotSha256 = original.Sha256,
        };
        Assert.True(PlanReviewSnapshot.Matches(plan, submission));
        Assert.True(PlanReviewSnapshot.TryReadRetained(submission, out var submitted));
        Assert.Equal("Goods", submitted.GetProperty("lines")[0].GetProperty("description").GetString());
        plan.Version = 3; // A later status transition need not alter the frozen business evidence.
        Assert.True(PlanReviewSnapshot.Matches(plan, submission));
        plan.Lines.First().EstimatedAmount += 0.0001m;
        Assert.False(PlanReviewSnapshot.Matches(plan, submission));
        Assert.True(PlanReviewSnapshot.TryReadRetained(submission, out _));
    }

    [Fact]
    public void Line_version_change_invalidates_the_frozen_round_even_when_values_are_unchanged()
    {
        var plan = Plan();
        var snapshot = PlanReviewSnapshot.Create(plan, 2);
        var round = new PlanReviewSubmission
        {
            TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            ProcurementPlanId = plan.Id, BranchRef = plan.BranchRef!.Value,
            MakerSubjectId = plan.OwnerSubjectId, CurrencyCode = plan.CurrencyCode,
            SubmittedPlanVersion = 2, EstimatedAmount = snapshot.EstimatedAmount,
            SnapshotJson = snapshot.Json, SnapshotSha256 = snapshot.Sha256,
        };
        Assert.True(PlanReviewSnapshot.Matches(plan, round));
        plan.Lines.First().Version++;
        Assert.False(PlanReviewSnapshot.Matches(plan, round));
        Assert.True(PlanReviewSnapshot.TryReadRetained(round, out _));
    }

    [Fact]
    public void Branchless_legacy_plan_cannot_produce_approval_evidence()
    {
        var plan = Plan();
        plan.BranchRef = null;
        Assert.Throws<InvalidOperationException>(() => PlanReviewSnapshot.Create(plan, 2));
    }

    [Fact]
    public void Historical_detail_rejects_tampered_payload_even_with_recomputed_digest()
    {
        var plan = Plan();
        var sealedSnapshot = PlanReviewSnapshot.Create(plan, 2);
        var round = new PlanReviewSubmission
        {
            TenantId = plan.TenantId, LegalEntityId = plan.LegalEntityId,
            ProcurementPlanId = plan.Id, BranchRef = plan.BranchRef!.Value,
            MakerSubjectId = plan.OwnerSubjectId, CurrencyCode = plan.CurrencyCode,
            SubmittedPlanVersion = 2, EstimatedAmount = sealedSnapshot.EstimatedAmount,
            SnapshotJson = sealedSnapshot.Json, SnapshotSha256 = sealedSnapshot.Sha256,
        };
        Assert.True(PlanReviewSnapshot.TryReadRetained(round, out _));
        round.SnapshotJson = round.SnapshotJson.Replace("\"currencyCode\":\"ZMW\"",
            "\"currencyCode\":\"USD\"");
        Assert.False(PlanReviewSnapshot.TryReadRetained(round, out _));
        round.SnapshotSha256 = ProcurementEvidenceEncoding.Digest(round.SnapshotJson);
        Assert.False(PlanReviewSnapshot.TryReadRetained(round, out _));
    }

    private static ProcurementPlan Plan()
    {
        var plan = new ProcurementPlan
        {
            TenantId = "tenant", LegalEntityId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
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
