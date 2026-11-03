using Mightyfin.Erp.Procurement.Application;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PlanReviewLifecycleTests
{
    [Fact]
    public void Independent_review_then_final_Core_approval_activates_only_at_the_end()
    {
        Assert.Equal("Submitted", PlanReviewLifecycle.Submit("Draft", true, true).PlanState);
        var reviewed = PlanReviewLifecycle.Review("Submitted", "Approve", "Pending", "Approved");
        Assert.Equal("Reviewed", reviewed.PlanState);
        Assert.Equal("Pending", reviewed.RoundState);
        var final = PlanReviewLifecycle.Review(reviewed.PlanState, "Approve", "Approved", "Approved");
        Assert.Equal("Active", final.PlanState);
        Assert.Equal("Approved", final.RoundState);
    }

    [Fact]
    public void Withdrawal_restores_editable_draft_and_retains_cancelled_round()
    {
        var withdrawn = PlanReviewLifecycle.Withdraw("Reviewed", "Cancelled");
        Assert.Equal("Draft", withdrawn.PlanState);
        Assert.Equal("Withdrawn", withdrawn.RoundState);
        Assert.Equal("Submitted", PlanReviewLifecycle.Submit(withdrawn.PlanState, true, true).PlanState);
    }

    [Fact]
    public void Return_allows_new_round_but_rejection_does_not()
    {
        var returned = PlanReviewLifecycle.Review("Submitted", "Return", "Returned", "Returned");
        Assert.Equal("Submitted", PlanReviewLifecycle.Submit(returned.PlanState, true, true).PlanState);
        var rejected = PlanReviewLifecycle.Review("Reviewed", "Reject", "Rejected", "Rejected");
        Assert.Throws<InvalidOperationException>(() => PlanReviewLifecycle.Submit(rejected.PlanState, true, true));
    }

    [Fact]
    public void Unclaimed_legacy_or_empty_plan_cannot_submit()
    {
        Assert.Throws<InvalidOperationException>(() => PlanReviewLifecycle.Submit("Draft", true, false));
        Assert.Throws<InvalidOperationException>(() => PlanReviewLifecycle.Submit("Draft", false, true));
    }

    [Theory]
    [InlineData("Submitted", "Approve", "Approved", "Approved")]
    [InlineData("Draft", "Approve", "Approved", "Approved")]
    public void Mismatched_Core_outcomes_do_not_change_plan(string current, string decision, string core, string stage) =>
        Assert.Throws<InvalidOperationException>(() => PlanReviewLifecycle.Review(current, decision, core, stage));

    [Fact]
    public void Parallel_quorum_keeps_plan_at_its_current_stage_until_quorum_finishes()
    {
        Assert.Equal("Submitted", PlanReviewLifecycle.Review("Submitted", "Approve", "Pending", "Active").PlanState);
        Assert.Equal("Reviewed", PlanReviewLifecycle.Review("Reviewed", "Approve", "Pending", "Active").PlanState);
    }
}
