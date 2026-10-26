using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Verifies the saved reply against its immutable owning graph before
/// insertion or replay. The original text remains the response authority.</summary>
public static class PrincipalRfqPublicationReceiptResult
{
    private static readonly HashSet<string> OpenFields = ["eventId", "status", "version",
        "publicationReviewId", "coreCaseId", "coreCaseVersion", "supplierDeliveryState",
        "financeReservationChanged", "commandKey"];
    private static readonly HashSet<string> DecideFields = [.. OpenFields, "coreDecisionId", "caseState"];

    public static bool Matches(SourcingPublicationReceipt receipt, SourcingPublicationOpening opening,
        SourcingPublicationDecision? decision)
    {
        if (receipt is null || opening is null || receipt.OpeningId != opening.Id
            || receipt.SourcingEventId != opening.SourcingEventId
            || receipt.CommandKey == Guid.Empty || receipt.ResponseJson is null
            || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
            || receipt.Operation is not ("Open" or "Decide")
            || (receipt.Operation == "Open") != (decision is null)
            || receipt.ResponseStatusCode != (decision is null ? 201 : 200)) return false;
        if (decision is not null && (receipt.DecisionId != decision.Id
            || decision.OpeningId != opening.Id || decision.SourcingEventId != opening.SourcingEventId
            || decision.CoreApprovalCaseId != opening.CoreApprovalCaseId
            || decision.ActorPrincipalId != receipt.ActorPrincipalId
            || decision.ActorCoreLinkedWorkerId != receipt.ActorCoreLinkedWorkerId)) return false;
        if (decision is null && (receipt.DecisionId is not null
            || opening.MakerPrincipalId != receipt.ActorPrincipalId
            || opening.MakerCoreLinkedWorkerId != receipt.ActorCoreLinkedWorkerId)) return false;
        var expectedVersion = decision?.EventVersionAtDecision ?? opening.EventVersionAtOpening;
        if (receipt.ResultVersion != expectedVersion || expectedVersion < 1) return false;
        try
        {
            using var document = JsonDocument.Parse(receipt.ResponseJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var names = root.EnumerateObject().Select(x => x.Name).ToArray();
            if (names.Length != (decision is null ? OpenFields.Count : DecideFields.Count)
                || names.Distinct(StringComparer.Ordinal).Count() != names.Length
                || !(decision is null ? OpenFields : DecideFields).SetEquals(names)) return false;
            if (!GuidIs(root, "eventId", opening.SourcingEventId)
                || !GuidIs(root, "publicationReviewId", opening.Id)
                || !GuidIs(root, "coreCaseId", opening.CoreApprovalCaseId)
                || !GuidIs(root, "commandKey", receipt.CommandKey)
                || !LongIs(root, "version", expectedVersion)
                || !PositiveLong(root, "coreCaseVersion")
                || !BoolIs(root, "financeReservationChanged", false)) return false;
            if (decision is null)
                return StringIs(root, "status", "PublicationReview")
                    && StringIs(root, "supplierDeliveryState", "NotStarted");
            var status = decision.CaseState switch
            {
                "Pending" => "PublicationReview", "Approved" => "Published",
                "Returned" or "Rejected" => "Draft", _ => null,
            };
            return status is not null && StringIs(root, "status", status)
                && StringIs(root, "caseState", decision.CaseState)
                && GuidIs(root, "coreDecisionId", decision.CoreApprovalDecisionId)
                && StringIs(root, "supplierDeliveryState",
                    decision.CaseState == "Approved" ? "ReadyForManualDelivery" : "NotStarted");
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool GuidIs(JsonElement root, string name, Guid value) =>
        root.GetProperty(name).ValueKind == JsonValueKind.String
        && root.GetProperty(name).TryGetGuid(out var actual) && actual == value;
    private static bool LongIs(JsonElement root, string name, long value) =>
        root.GetProperty(name).ValueKind == JsonValueKind.Number
        && root.GetProperty(name).TryGetInt64(out var actual) && actual == value;
    private static bool PositiveLong(JsonElement root, string name) =>
        root.GetProperty(name).ValueKind == JsonValueKind.Number
        && root.GetProperty(name).TryGetInt64(out var actual) && actual > 0;
    private static bool BoolIs(JsonElement root, string name, bool value) =>
        root.GetProperty(name).ValueKind is JsonValueKind.True or JsonValueKind.False
        && root.GetProperty(name).GetBoolean() == value;
    private static bool StringIs(JsonElement root, string name, string value) =>
        root.GetProperty(name).ValueKind == JsonValueKind.String
        && root.GetProperty(name).GetString() == value;
}
