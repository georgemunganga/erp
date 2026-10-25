using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ContractReviewReceiptProofTests
{
    [Fact]
    public void Original_submit_ack_is_exact_and_another_target_cannot_borrow_it()
    {
        var actor = Guid.CreateVersion7().ToString("D");
        var receipt = new ContractReviewCommandReceipt { ContractId = Guid.CreateVersion7(),
            SubmissionId = Guid.CreateVersion7(), ReviewBranchId = Guid.CreateVersion7(),
            Operation = "Submit", CommandKey = Guid.CreateVersion7(), ActorPrincipalId = actor,
            CommandSha256 = new string('a', 64), ResultContractVersion = 2,
            ResponseStatus = 201, CompletedAt = DateTimeOffset.UtcNow };
        receipt.ResponseJson = ContractReviewReceiptProof.Response(receipt, "PendingReview");
        receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(receipt.ResponseJson);
        ContractReviewReceiptProof.Verify(receipt);
        var other = new ContractReviewCommandReceipt { ContractId = Guid.CreateVersion7(),
            SubmissionId = receipt.SubmissionId, ReviewBranchId = receipt.ReviewBranchId,
            Operation = receipt.Operation, CommandKey = receipt.CommandKey, ActorPrincipalId = receipt.ActorPrincipalId,
            CommandSha256 = receipt.CommandSha256, ResultContractVersion = receipt.ResultContractVersion,
            ResponseStatus = receipt.ResponseStatus, ResponseJson = receipt.ResponseJson,
            ResponseSha256 = receipt.ResponseSha256 };
        Assert.Throws<InvalidOperationException>(() => ContractReviewReceiptProof.Verify(other));
    }

    [Fact]
    public void Rehashed_but_changed_status_does_not_become_a_new_original_result()
    {
        var receipt = new ContractReviewCommandReceipt { ContractId = Guid.CreateVersion7(),
            SubmissionId = Guid.CreateVersion7(), ReviewBranchId = Guid.CreateVersion7(),
            Operation = "Submit", CommandKey = Guid.CreateVersion7(),
            ActorPrincipalId = Guid.CreateVersion7().ToString("D"), CommandSha256 = new string('a', 64),
            ResultContractVersion = 2, ResponseStatus = 201 };
        receipt.ResponseJson = ContractReviewReceiptProof.Response(receipt, "PendingReview")
            .Replace("PendingReview", "Approved", StringComparison.Ordinal);
        receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(receipt.ResponseJson);
        Assert.Throws<InvalidOperationException>(() => ContractReviewReceiptProof.Verify(receipt));
    }

    [Fact]
    public void Correction_ack_requires_its_own_target_and_Draft_result()
    {
        var receipt = new ContractReviewCommandReceipt { ContractId = Guid.CreateVersion7(),
            SubmissionId = Guid.CreateVersion7(), ReviewBranchId = Guid.CreateVersion7(),
            CorrectionId = Guid.CreateVersion7(), Operation = "Correction", CommandKey = Guid.CreateVersion7(),
            ActorPrincipalId = Guid.CreateVersion7().ToString("D"), CommandSha256 = new string('a', 64),
            ResultContractVersion = 4, ResponseStatus = 200 };
        receipt.ResponseJson = ContractReviewReceiptProof.Response(receipt, "Draft");
        receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(receipt.ResponseJson);
        ContractReviewReceiptProof.Verify(receipt);
        var other = new ContractReviewCommandReceipt { ContractId = receipt.ContractId,
            SubmissionId = receipt.SubmissionId, ReviewBranchId = receipt.ReviewBranchId,
            CorrectionId = Guid.CreateVersion7(), Operation = receipt.Operation, CommandKey = receipt.CommandKey,
            ActorPrincipalId = receipt.ActorPrincipalId, CommandSha256 = receipt.CommandSha256,
            ResultContractVersion = receipt.ResultContractVersion, ResponseStatus = receipt.ResponseStatus,
            ResponseJson = receipt.ResponseJson, ResponseSha256 = receipt.ResponseSha256 };
        Assert.Throws<InvalidOperationException>(() => ContractReviewReceiptProof.Verify(other));
    }
}
