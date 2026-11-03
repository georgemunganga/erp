namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Procurement's result of a shared Core Approval case transition.</summary>
public static class PlanReviewLifecycle
{
    public sealed record Result(string PlanState, string RoundState);

    public static Result Submit(string current, bool hasActiveLine, bool branchClaimed)
    {
        if (current is not ("Draft" or "Returned") || !hasActiveLine || !branchClaimed)
            throw new InvalidOperationException("PlanReviewSubmitUnavailable");
        return new("Submitted", "Pending");
    }

    public static Result Review(string current, string decision, string coreCaseState)
    {
        if (current is not ("Submitted" or "Reviewed"))
            throw new InvalidOperationException("PlanReviewNotPending");
        return (current, decision, coreCaseState) switch
        {
            ("Submitted", "Approve", "Pending") => new("Reviewed", "Pending"),
            ("Reviewed", "Approve", "Approved") => new("Active", "Approved"),
            (_, "Return", "Returned") => new("Returned", "Returned"),
            (_, "Reject", "Rejected") => new("Rejected", "Rejected"),
            _ => throw new InvalidOperationException("PlanReviewCaseTransitionMismatch"),
        };
    }

    public static Result Withdraw(string current, string coreCaseState)
    {
        if (current is not ("Submitted" or "Reviewed") || coreCaseState != "Cancelled")
            throw new InvalidOperationException("PlanReviewWithdrawalUnavailable");
        return new("Draft", "Withdrawn");
    }
}
