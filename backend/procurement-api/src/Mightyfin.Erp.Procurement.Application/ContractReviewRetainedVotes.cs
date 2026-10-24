using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Every retained Core vote must have its original Procurement link and
/// actor-owned acknowledgement. Core terminal state alone cannot reopen terms.</summary>
public static class ContractReviewRetainedVotes
{
    public static void VerifyPending(ContractReviewSubmission source, ApprovalCase approval,
        IReadOnlyList<ContractReviewAssignmentPins.Pin> pins,
        IReadOnlyCollection<ApprovalDecision> decisions,
        IReadOnlyCollection<ContractReviewVote> votes,
        IReadOnlyCollection<ContractReviewCommandReceipt> receipts)
    {
        ContractReviewCoreHistory.VerifyPending(source, approval, pins, decisions);
        Verify(source, decisions, votes, receipts);
    }

    public static void Verify(ContractReviewSubmission source,
        IReadOnlyCollection<ApprovalDecision> decisions,
        IReadOnlyCollection<ContractReviewVote> votes,
        IReadOnlyCollection<ContractReviewCommandReceipt> receipts)
    {
        var submit = receipts.Where(x => x.Operation == "Submit" && x.SubmissionId == source.Id).ToArray();
        if (submit.Length != 1 || submit[0].ContractId != source.ContractId
            || submit[0].ReviewBranchId != source.ReviewBranchId
            || submit[0].ActorPrincipalId != source.MakerPrincipalId
            || submit[0].ResultContractVersion != checked(source.ContractBusinessVersion + 1))
            throw new InvalidOperationException("ContractReviewOriginalSubmissionUnconfirmed");
        ContractReviewReceiptProof.Verify(submit[0]);
        if (votes.Count != decisions.Count || receipts.Count != decisions.Count + 1
            || votes.Select(x => x.CoreDecisionId).Distinct().Count() != votes.Count)
            throw new InvalidOperationException("ContractReviewRetainedVotesChanged");
        foreach (var decision in decisions)
        {
            var vote = votes.SingleOrDefault(x => x.CoreDecisionId == decision.Id);
            if (vote is null || vote.SubmissionId != source.Id || vote.ContractId != source.ContractId
                || vote.ReviewBranchId != source.ReviewBranchId || vote.CoreCaseId != source.CoreCaseId
                || vote.CoreStageId != decision.StageId || vote.CoreAssignmentId != decision.AssignmentId
                || vote.ActorPrincipalId != decision.ActorSubjectId || vote.Action != decision.Action
                || vote.DecidedAt != decision.DecidedAt)
                throw new InvalidOperationException("ContractReviewRetainedVotesChanged");
            var receipt = receipts.SingleOrDefault(x => x.Operation == "Decide" && x.VoteId == vote.Id);
            if (receipt is null || receipt.SubmissionId != source.Id || receipt.ContractId != source.ContractId
                || receipt.ReviewBranchId != source.ReviewBranchId || receipt.ActorPrincipalId != vote.ActorPrincipalId
                || receipt.CommandKey.ToString("D") != decision.IdempotencyKey
                || receipt.CompletedAt != vote.DecidedAt)
                throw new InvalidOperationException("ContractReviewRetainedVotesChanged");
            ContractReviewReceiptProof.Verify(receipt);
        }
    }
}
