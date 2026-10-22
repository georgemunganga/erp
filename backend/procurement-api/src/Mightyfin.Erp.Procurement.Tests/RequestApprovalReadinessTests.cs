using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class RequestApprovalReadinessTests
{
    private static (PurchaseRequest Request, PolicyEvaluation Evaluation, PolicyVersion Policy,
        BudgetCheckSnapshot Finance) Ready()
    {
        var request = new PurchaseRequest
        {
            Status = "Submitted", Version = 4, SubmittedVersion = 4,
            RequesterWorkerId = Guid.NewGuid(),
            CurrencyCode = "ZMW", ApprovalState = "PendingPolicyAndBudget",
            Lines = [new PurchaseRequestLine
            {
                RequestedQuantity = 2, EstimatedUnitPrice = 125,
                ApprovalStatus = "Pending",
            }],
        };
        var policy = new PolicyVersion
        {
            PolicyKey = "PURCHASE_REQUEST", PolicyVersionNumber = 2, State = "Published",
        };
        var evaluation = new PolicyEvaluation
        {
            PurchaseRequestId = request.Id, RequestVersion = 4,
            PolicyVersionId = policy.Id, PolicyVersionNumber = 2,
            AmountBasis = "EstimatedLineBase", Outcome = "Matched",
        };
        var finance = new BudgetCheckSnapshot
        {
            PurchaseRequestId = request.Id, RequestVersion = 4,
            Status = "Available", CheckedAmount = 250, CurrencyCode = "ZMW",
            ReservationStatus = "Confirmed", ReservationRef = "FIN-1",
            FinanceCheckRef = "CHECK-1", EvidenceRef = "FIN-DOC-1",
        };
        return (request, evaluation, policy, finance);
    }

    [Fact]
    public void Matching_evidence_for_current_version_meets_prerequisites_only()
    {
        var (request, evaluation, policy, finance) = Ready();
        var result = RequestApprovalReadiness.Check(request, evaluation, policy, finance);
        Assert.True(result.PrerequisitesMet);
        Assert.Equal(250m, result.EstimatedAmount);
        Assert.Empty(result.Blockers);
        Assert.Equal("PendingPolicyAndBudget", request.ApprovalState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_legacy_worker_identity_cannot_satisfy_worker_pinned_approval(bool emptyWorker)
    {
        var (request, evaluation, policy, finance) = Ready();
        request.RequesterWorkerId = emptyWorker ? Guid.Empty : null;
        var result = RequestApprovalReadiness.Check(request, evaluation, policy, finance);
        Assert.False(result.PrerequisitesMet);
        Assert.Contains("LegacyWorkerApprovalIdentityUnavailable", result.Blockers);
        Assert.Equal("PendingPolicyAndBudget", request.ApprovalState);
    }

    [Fact]
    public void System_user_request_requires_principal_based_approval_evidence()
    {
        var (request, evaluation, policy, finance) = Ready();
        request.RequesterPrincipalId = Guid.NewGuid();
        request.CreatedBy = request.RequesterPrincipalId.Value.ToString("D");
        request.RequesterWorkerId = null;
        var result = RequestApprovalReadiness.Check(request, evaluation, policy, finance);
        Assert.False(result.PrerequisitesMet);
        Assert.Contains("PrincipalApprovalEvidenceRequired", result.Blockers);
        Assert.Equal("PendingPolicyAndBudget", request.ApprovalState);
    }

    [Fact]
    public void Old_finance_result_and_wrong_currency_block_current_request()
    {
        var (request, evaluation, policy, finance) = Ready();
        finance.RequestVersion = 3;
        finance.CurrencyCode = "USD";
        var result = RequestApprovalReadiness.Check(request, evaluation, policy, finance);
        Assert.False(result.PrerequisitesMet);
        Assert.Contains("FinanceReservationNotConfirmed", result.Blockers);
    }

    [Fact]
    public void Unpublished_policy_or_prior_approval_blocks_case_opening()
    {
        var (request, evaluation, policy, finance) = Ready();
        policy.State = "Draft";
        request.ApprovalInstanceRef = Guid.NewGuid().ToString();
        var result = RequestApprovalReadiness.Check(request, evaluation, policy, finance);
        Assert.Contains("PinnedPolicyUnavailable", result.Blockers);
        Assert.Contains("ApprovalAlreadyOpened", result.Blockers);
    }
}
