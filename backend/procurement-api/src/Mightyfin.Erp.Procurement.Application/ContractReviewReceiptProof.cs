using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Exact original acknowledgement; GET recovery never infers a command
/// result from a later contract status or a matching list row.</summary>
public static class ContractReviewReceiptProof
{
    public static string Response(ContractReviewCommandReceipt receipt, string contractStatus)
    {
        if (receipt.Operation is not ("Submit" or "Decide" or "Correction")
            || contractStatus is not ("PendingReview" or "Approved" or "Returned" or "Rejected" or "Draft")
            || receipt.Operation == "Submit" && contractStatus != "PendingReview"
            || receipt.Operation == "Submit" && (receipt.VoteId is not null || receipt.CorrectionId is not null)
            || receipt.Operation == "Decide" && (receipt.VoteId is null || receipt.CorrectionId is not null
                || contractStatus == "Draft")
            || receipt.Operation == "Correction" && (contractStatus != "Draft"
                || receipt.CorrectionId is null || receipt.VoteId is not null))
            throw new InvalidOperationException("ContractReviewReceiptResponseInvalid");
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1, receipt.ContractId, receipt.SubmissionId, receipt.ReviewBranchId,
            receipt.VoteId, receipt.CorrectionId, receipt.Operation, receipt.CommandKey, receipt.ActorPrincipalId,
            contractVersion = receipt.ResultContractVersion, contractStatus,
        });
    }

    public static void Verify(ContractReviewCommandReceipt receipt)
    {
        if (receipt.CommandKey == Guid.Empty || receipt.ContractId == Guid.Empty
            || receipt.SubmissionId == Guid.Empty || receipt.ReviewBranchId == Guid.Empty
            || !ProcurementPrincipalEvidence.Canonical(receipt.ActorPrincipalId)
            || receipt.ResponseStatus != (receipt.Operation == "Submit" ? 201 : 200)
            || receipt.ResultContractVersion < 1
            || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new InvalidOperationException("ContractReviewReceiptInvalid");
        try
        {
            using var document = JsonDocument.Parse(receipt.ResponseJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 11
                || root.GetProperty("schemaVersion").GetInt32() != 1
                || root.GetProperty("ContractId").GetGuid() != receipt.ContractId
                || root.GetProperty("SubmissionId").GetGuid() != receipt.SubmissionId
                || root.GetProperty("ReviewBranchId").GetGuid() != receipt.ReviewBranchId
                || root.GetProperty("Operation").GetString() != receipt.Operation
                || root.GetProperty("CommandKey").GetGuid() != receipt.CommandKey
                || root.GetProperty("ActorPrincipalId").GetString() != receipt.ActorPrincipalId
                || root.GetProperty("contractVersion").GetInt64() != receipt.ResultContractVersion
                || (receipt.VoteId is null ? root.GetProperty("VoteId").ValueKind != JsonValueKind.Null
                    : root.GetProperty("VoteId").GetGuid() != receipt.VoteId)
                || (receipt.CorrectionId is null ? root.GetProperty("CorrectionId").ValueKind != JsonValueKind.Null
                    : root.GetProperty("CorrectionId").GetGuid() != receipt.CorrectionId)
                || Response(receipt, root.GetProperty("contractStatus").GetString() ?? "") != receipt.ResponseJson)
                throw new InvalidOperationException("ContractReviewReceiptInvalid");
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException
            or InvalidOperationException or OverflowException)
        { throw new InvalidOperationException("ContractReviewReceiptInvalid", error); }
    }
}
