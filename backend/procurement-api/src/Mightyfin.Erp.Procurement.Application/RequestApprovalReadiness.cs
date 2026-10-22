using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ApprovalReadiness(bool PrerequisitesMet, long RequestVersion,
    decimal EstimatedAmount, string CurrencyCode, IReadOnlyList<string> Blockers);

/// <summary>Read-only prerequisites. Passing does not open a task or authorize spending.</summary>
public static class RequestApprovalReadiness
{
    public static ApprovalReadiness Check(PurchaseRequest request, PolicyEvaluation? evaluation,
        PolicyVersion? policy, BudgetCheckSnapshot? finance)
    {
        var blockers = new List<string>();
        if (request.RequesterPrincipalId is not null && !PurchaseRequestOwnership.HasLegacyWorkerEvidenceIdentity(request))
            blockers.Add("PrincipalApprovalEvidenceRequired");
        if (request.RequesterWorkerId is null || request.RequesterWorkerId == Guid.Empty)
            blockers.Add("LegacyWorkerApprovalIdentityUnavailable");
        var version = request.SubmittedVersion ?? request.Version;
        if (request.Status != "Submitted" || request.Version != version)
            blockers.Add("RequestNotCurrent");
        if (request.ApprovalInstanceRef is not null)
            blockers.Add("ApprovalAlreadyOpened");
        if (request.Lines.All(x => x.ApprovalStatus == "Cancelled"))
            blockers.Add("NoActiveLines");
        decimal amount = 0;
        try { amount = request.Lines.Where(x => x.ApprovalStatus != "Cancelled")
            .Sum(x => x.RequestedQuantity * x.EstimatedUnitPrice); }
        catch (OverflowException) { blockers.Add("AmountOverflow"); }
        if (evaluation is null || evaluation.PurchaseRequestId != request.Id ||
            evaluation.RequestVersion != version || evaluation.Outcome != "Matched" ||
            evaluation.AmountBasis != "EstimatedLineBase")
            blockers.Add("BuyingRulesNotMatched");
        else if (policy is null || policy.Id != evaluation.PolicyVersionId ||
            policy.PolicyVersionNumber != evaluation.PolicyVersionNumber ||
            policy.State != "Published" || policy.PolicyKey != "PURCHASE_REQUEST")
            blockers.Add("PinnedPolicyUnavailable");
        if (finance is null || finance.PurchaseRequestId != request.Id ||
            finance.RequestVersion != version || finance.Status != "Available" ||
            finance.ReservationStatus != "Confirmed" ||
            string.IsNullOrWhiteSpace(finance.ReservationRef) ||
            string.IsNullOrWhiteSpace(finance.FinanceCheckRef) ||
            string.IsNullOrWhiteSpace(finance.EvidenceRef) ||
            !string.Equals(finance.CurrencyCode, request.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
            finance.CheckedAmount < amount)
            blockers.Add("FinanceReservationNotConfirmed");
        return new(blockers.Count == 0, version, amount, request.CurrencyCode, blockers);
    }
}
