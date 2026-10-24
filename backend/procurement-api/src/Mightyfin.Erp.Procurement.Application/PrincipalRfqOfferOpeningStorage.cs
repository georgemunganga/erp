using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public static class PrincipalRfqOfferOpeningStorage
{
    public static void Verify(PrincipalRfqOfferOpeningRecord row,
        PrincipalRfqCommercialRoundOpeningRecord round,
        PrincipalRfqCommercialRoundSealRecord seal)
    {
        if (row.SchemaVersion != 1 || row.TenantId != round.TenantId
            || row.LegalEntityId != round.LegalEntityId || row.OpeningId != round.Id
            || row.SealId != seal.Id || row.SourcingEventId != round.SourcingEventId
            || row.BranchId != round.BranchId || seal.OpeningId != round.Id
            || seal.TenantId != row.TenantId || seal.LegalEntityId != row.LegalEntityId
            || seal.SourcingEventId != row.SourcingEventId || seal.BranchId != row.BranchId
            || row.PolicyVersionId != round.CommercialPolicyVersionId
            || row.PublishedPolicyIdentityId != round.PublishedPolicyIdentityId
            || row.PolicyRulesSha256 != round.PolicyRulesSha256
            || row.ApprovedRoundSha256 != round.SnapshotSha256
            || row.OfferCount != seal.SubmissionCount || row.OfferSetSha256 != seal.SubmissionSetSha256
            || row.CommandKey == Guid.Empty || !Hash(row.RequestSha256)
            || !ProcurementPrincipalEvidence.Canonical(row.OfficerPrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(row.OfficerWorkerAliasId)
            || row.OpenedAt < seal.ClosedAt || row.OpenedAt.Offset != TimeSpan.Zero
            || row.OpenedAt.UtcTicks % 10 != 0 || row.Reason is null || row.Reference is null
            || row.Reason.Length > 1000 || row.Reference.Length > 200
            || row.SnapshotJson is not { Length: > 0 and <= 262144 }
            || row.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(row.SnapshotJson))
            throw new InvalidOperationException("RfqOfferOpeningStoredProofChanged");
        _ = ProcurementEvidenceEncoding.PolicyJsonDigest(row.SnapshotJson);
        using var document = JsonDocument.Parse(row.SnapshotJson);
        var pin = document.RootElement;
        if (pin.GetProperty("schemaVersion").GetInt32() != 1
            || pin.GetProperty("roundId").GetGuid() != round.Id
            || pin.GetProperty("sealId").GetGuid() != seal.Id
            || pin.GetProperty("eventId").GetGuid() != row.SourcingEventId
            || pin.GetProperty("tenantId").GetString() != row.TenantId
            || pin.GetProperty("companyId").GetGuid() != row.LegalEntityId
            || pin.GetProperty("branchId").GetGuid() != row.BranchId
            || pin.GetProperty("preparedPlanId").GetGuid() != round.PreparedPlanId
            || pin.GetProperty("preparedPlanSha256").GetString() != round.PreparedPlanSha256
            || pin.GetProperty("approvalEvidenceSha256").GetString() != row.ApprovedRoundSha256
            || pin.GetProperty("policyVersionId").GetGuid() != row.PolicyVersionId
            || pin.GetProperty("policyRulesSha256").GetString() != row.PolicyRulesSha256
            || pin.GetProperty("officerPrincipalId").GetString() != row.OfficerPrincipalId
            || (pin.GetProperty("officerWorkerAliasId").ValueKind == JsonValueKind.Null
                ? row.OfficerWorkerAliasId is not null
                : pin.GetProperty("officerWorkerAliasId").GetGuid() != row.OfficerWorkerAliasId)
            || pin.GetProperty("openedAt").GetDateTimeOffset() != row.OpenedAt
            || pin.GetProperty("offerCount").GetInt32() != row.OfferCount
            || pin.GetProperty("offerSetSha256").GetString() != row.OfferSetSha256
            || pin.GetProperty("reason").GetString() != row.Reason
            || pin.GetProperty("reference").GetString() != row.Reference)
            throw new InvalidOperationException("RfqOfferOpeningStoredSnapshotChanged");
        using var approved = JsonDocument.Parse(round.SnapshotJson);
        if (!approved.RootElement.TryGetProperty("offerOpening", out var controls)
            || pin.GetProperty("openingControls").GetRawText() != controls.GetRawText()
            || controls.GetProperty("OfficerPrincipalId").GetString() != row.OfficerPrincipalId
            || row.OpenedAt - seal.ClosedAt < TimeSpan.FromSeconds(controls.GetProperty("DelayAfterCloseSeconds").GetInt32())
            || controls.GetProperty("ReasonRequired").GetBoolean() && row.Reason.Length == 0
            || controls.GetProperty("ReferenceRequired").GetBoolean() && row.Reference.Length == 0)
            throw new InvalidOperationException("RfqOfferOpeningStoredControlsChanged");
    }

    public static string Response(PrincipalRfqOfferOpeningRecord row) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, operation = "OpenOffers", commandKey = row.CommandKey,
        unsealingId = row.Id, roundId = row.OpeningId, eventId = row.SourcingEventId,
        state = "OffersOpened", offerCount = row.OfferCount, offerSetSha256 = row.OfferSetSha256,
        snapshotSha256 = row.SnapshotSha256, openedAt = row.OpenedAt,
        awardMade = false, evaluationPerformed = false, purchaseOrderCreated = false,
    });

    public static string AuditPayload(PrincipalRfqOfferOpeningRecord row) => JsonSerializer.Serialize(new
    { row.OpeningId, row.SealId, key = row.CommandKey, row.OfferSetSha256, row.SnapshotSha256 });

    public static string EventPayload(PrincipalRfqOfferOpeningRecord row) => JsonSerializer.Serialize(new
    { roundId = row.OpeningId, openingId = row.Id, row.OfferCount,
        evaluationPerformed = false, awardMade = false, purchaseOrderCreated = false });

    public static void VerifyReceipt(PrincipalRfqOfferOpeningRecord row, PrincipalRfqOfferOpeningReceipt receipt)
    {
        if (receipt.SchemaVersion != 1 || receipt.UnsealingId != row.Id
            || receipt.TenantId != row.TenantId || receipt.LegalEntityId != row.LegalEntityId
            || receipt.OpeningId != row.OpeningId || receipt.SourcingEventId != row.SourcingEventId
            || receipt.BranchId != row.BranchId || receipt.ActorPrincipalId != row.OfficerPrincipalId
            || receipt.CommandKey != row.CommandKey || receipt.RequestSha256 != row.RequestSha256
            || receipt.ResponseStatus != 200 || receipt.ResponseJson != Response(row)
            || receipt.ResponseSha256 != ProcurementEvidenceEncoding.Digest(receipt.ResponseJson))
            throw new InvalidOperationException("RfqOfferOpeningStoredReceiptChanged");
    }

    private static bool Hash(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
