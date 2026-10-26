using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqPublicationReceiptResultTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Opening_reply_is_bound_to_its_event_case_version_and_original_command()
    {
        var maker = Guid.NewGuid().ToString("D");
        var opening = new SourcingPublicationOpening
        {
            SourcingEventId = Guid.NewGuid(), CoreApprovalCaseId = Guid.NewGuid(),
            EventVersionAtOpening = 4, MakerPrincipalId = maker,
        };
        var receipt = new SourcingPublicationReceipt
        {
            SourcingEventId = opening.SourcingEventId, OpeningId = opening.Id,
            CommandKey = Guid.NewGuid(), ActorPrincipalId = maker, Operation = "Open",
            ResponseStatusCode = 201, ResultVersion = 4,
        };
        void Save(string body) { receipt.ResponseJson = body; receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(body); }
        Save(JsonSerializer.Serialize(new { eventId = opening.SourcingEventId, status = "PublicationReview",
            version = 4, publicationReviewId = opening.Id, coreCaseId = opening.CoreApprovalCaseId,
            coreCaseVersion = 1, supplierDeliveryState = "NotStarted", financeReservationChanged = false,
            commandKey = receipt.CommandKey }, Json));
        Assert.True(PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, null));
        Save(receipt.ResponseJson.Replace(opening.SourcingEventId.ToString("D"), Guid.NewGuid().ToString("D")));
        Assert.False(PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, null));
        Save("{}");
        Assert.False(PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, null));
        Save("{\"eventId\":\"" + opening.SourcingEventId + "\",\"eventId\":\"" + opening.SourcingEventId + "\"}");
        Assert.False(PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, null));
    }

    [Fact]
    public void Decision_reply_is_bound_to_the_immutable_decision_and_status()
    {
        var reviewer = Guid.NewGuid().ToString("D");
        var opening = new SourcingPublicationOpening
        {
            SourcingEventId = Guid.NewGuid(), CoreApprovalCaseId = Guid.NewGuid(),
            EventVersionAtOpening = 4, MakerPrincipalId = Guid.NewGuid().ToString("D"),
        };
        var decision = new SourcingPublicationDecision
        {
            SourcingEventId = opening.SourcingEventId, OpeningId = opening.Id,
            CoreApprovalCaseId = opening.CoreApprovalCaseId,
            CoreApprovalDecisionId = Guid.NewGuid(), ActorPrincipalId = reviewer,
            CaseState = "Approved", EventVersionAtDecision = 5,
        };
        var receipt = new SourcingPublicationReceipt
        {
            SourcingEventId = opening.SourcingEventId, OpeningId = opening.Id,
            DecisionId = decision.Id, CommandKey = Guid.NewGuid(), ActorPrincipalId = reviewer,
            Operation = "Decide", ResponseStatusCode = 200, ResultVersion = 5,
        };
        void Save(string body) { receipt.ResponseJson = body; receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(body); }
        Save(JsonSerializer.Serialize(new { eventId = opening.SourcingEventId, status = "Published",
            version = 5, publicationReviewId = opening.Id, coreCaseId = opening.CoreApprovalCaseId,
            coreCaseVersion = 2, coreDecisionId = decision.CoreApprovalDecisionId, caseState = "Approved",
            supplierDeliveryState = "ReadyForManualDelivery", financeReservationChanged = false,
            commandKey = receipt.CommandKey }, Json));
        Assert.True(PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, decision));
        Save(receipt.ResponseJson.Replace("ReadyForManualDelivery", "Sent"));
        Assert.False(PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, decision));
        decision.CaseState = "Returned";
        Assert.False(PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, decision));
    }
}
