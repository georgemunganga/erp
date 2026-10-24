using System.Globalization;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Commercially published round proof. The owning adapter must load and
/// verify its independent Core approval graph and immutable plan before use;
/// constructing this projection never grants publication or opening authority.</summary>
public sealed record PrincipalRfqPublishedRoundProof(Guid RoundId, Guid EventId,
    Guid PreparedPlanId, string PreparedPlanSha256, Guid CommercialCaseId,
    Guid ApprovedDecisionId, string ApprovalEvidenceSha256, string TenantId,
    Guid CompanyId, Guid BranchId, string CurrencyCode, long EventVersion,
    DateTimeOffset OpensAt, DateTimeOffset ClosesAt,
    string ClosingOfficerPrincipalId, Guid? ClosingOfficerWorkerAliasId,
    string State, IReadOnlyList<Guid> ApprovedSupplierIds);

/// <summary>Exact immutable offer content prepared for an owned append-only
/// submission. Supplier authentication is never inferred from recorder identity.</summary>
public sealed record PrincipalRfqManualOfferSubmission(Guid RoundId, Guid EventId,
    Guid BidId, Guid InvitationId, Guid SupplierId, int DraftRevision,
    long BidVersion, string DraftSnapshotSha256, Guid OriginalDocumentVersionId,
    string OriginalDocumentSha256, string RecorderPrincipalId,
    Guid? RecorderWorkerAliasId, DateTimeOffset ReportedReceivedAt,
    DateTimeOffset SubmittedAt, string SnapshotJson, string SnapshotSha256);

public sealed record PrincipalRfqRoundSealProof(Guid RoundId, Guid EventId,
    string ActorPrincipalId, Guid? ActorWorkerAliasId, DateTimeOffset ClosedAt,
    int SubmissionCount, string SubmissionSetSha256);

/// <summary>Pure transition checks. An adapter must still enforce current Core
/// actions/scope, supplier eligibility, scanned document use, real published
/// round/approval, locks, CAS, same-save receipt, and finite physical readiness.</summary>
public static class PrincipalRfqSealedManualOffers
{
    public const string SubmitAction = "procurement-sourcing-offer-submit";
    public const string CloseAction = "procurement-sourcing-round-close";

    public static PrincipalRfqManualOfferSubmission Submit(
        PrincipalRfqPublishedRoundProof round, PrincipalRfqPreparedPlan prepared,
        SourcingEvent source, SourcingInvitation invitation, SupplierBid bid,
        SourcingManualQuoteVersion draftVersion,
        IReadOnlyList<SourcingLine> sourceLines, IReadOnlyList<SupplierBidLine> bidLines,
        RfqPrincipalActor recorder, Guid originalDocumentVersionId,
        string originalDocumentSha256, DateTimeOffset reportedReceivedAt,
        DateTimeOffset serverObservedAt)
    {
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(invitation);
        ArgumentNullException.ThrowIfNull(bid);
        ArgumentNullException.ThrowIfNull(draftVersion);
        ArgumentNullException.ThrowIfNull(sourceLines);
        ArgumentNullException.ThrowIfNull(bidLines);
        ArgumentNullException.ThrowIfNull(recorder);
        Need(round.State == "Accepting" && round.RoundId != Guid.Empty
            && round.PreparedPlanId == prepared.Id
            && round.PreparedPlanSha256 == prepared.SnapshotSha256
            && round.EventId == source.Id && round.EventVersion == source.Version
            && round.TenantId == source.TenantId && round.CompanyId == source.LegalEntityId
            && round.BranchId == source.BranchRef && round.CurrencyCode == source.CurrencyCode
            && round.CommercialCaseId != Guid.Empty && round.ApprovedDecisionId != Guid.Empty
            && round.ApprovedSupplierIds is { Count: > 0 }
            && round.ApprovedSupplierIds.Distinct().Count() == round.ApprovedSupplierIds.Count
            && round.ApprovedSupplierIds.Contains(bid.SupplierId)
            && Hash(round.ApprovalEvidenceSha256) && source.Status == "Published"
            && prepared.SourcingEventId == source.Id && prepared.EventVersion > 0
            && prepared.TenantId == source.TenantId && prepared.LegalEntityId == source.LegalEntityId
            && prepared.BranchId == round.BranchId && prepared.SnapshotSha256 == round.PreparedPlanSha256,
            "RfqSealedRoundPublicationRequired");
        Need(Instant(serverObservedAt) && Instant(reportedReceivedAt)
            && Instant(round.OpensAt) && Instant(round.ClosesAt)
            && round.OpensAt < round.ClosesAt
            && serverObservedAt >= round.OpensAt && serverObservedAt < round.ClosesAt
            && reportedReceivedAt <= serverObservedAt,
            "RfqSealedOfferWindowClosed");
        Need(recorder.PrincipalId == bid.SurrogateEnteredBy
            && ProcurementPrincipalEvidence.Canonical(recorder.PrincipalId)
            && ProcurementPrincipalEvidence.OptionalWorker(recorder.LinkedWorkerId)
            && recorder.Permissions is not null && recorder.Permissions.Contains(SubmitAction)
            && recorder.Branches is not null
            && recorder.TenantId == round.TenantId && recorder.CompanyId == round.CompanyId
            && (recorder.SelectedBranchId is null || recorder.SelectedBranchId == round.BranchId)
            && (recorder.OrganisationWide || recorder.Branches.Contains(round.BranchId)),
            "RfqSealedOfferRecorderNotAuthorized");
        Need(invitation.Id == bid.InvitationId && invitation.SourcingEventId == round.EventId
            && invitation.SupplierId == bid.SupplierId && invitation.Status == "ManualContactRecorded"
            && invitation.TenantId == round.TenantId && invitation.LegalEntityId == round.CompanyId
            && bid.TenantId == round.TenantId && bid.LegalEntityId == round.CompanyId
            && bid.SourceChannel == "Manual" && bid.Status == "Draft"
            && bid.SubmissionVersion == 1 && bid.SubmittedAt is null
            && draftVersion.SupplierBidId == bid.Id && draftVersion.SourcingEventId == round.EventId
            && draftVersion.TenantId == round.TenantId && draftVersion.LegalEntityId == round.CompanyId
            && draftVersion.Revision == bid.DraftRevision && draftVersion.BidVersion == bid.Version
            && draftVersion.Operation is "Create" or "Revise"
            && draftVersion.ActorPrincipalId == recorder.PrincipalId
            && draftVersion.ActorCoreLinkedWorkerId == recorder.LinkedWorkerId
            && draftVersion.CommandKey != Guid.Empty && Hash(draftVersion.CommandSha256)
            && Instant(draftVersion.RecordedAt) && draftVersion.RecordedAt <= serverObservedAt
            && draftVersion.SnapshotSha256 == ProcurementEvidenceEncoding.Digest(draftVersion.SnapshotJson)
            && draftVersion.SnapshotJson == PrincipalManualQuotationSnapshot.Build(bid, bidLines)
            && draftVersion.ResponseSha256 == ProcurementEvidenceEncoding.Digest(draftVersion.ResponseJson),
            "RfqSealedOfferDraftChanged");
        PrincipalManualQuotationDraft.Validate(source, invitation, bid, sourceLines, bidLines,
            recorder.PrincipalId);
        Need(originalDocumentVersionId != Guid.Empty && Hash(originalDocumentSha256),
            "RfqSealedOfferOriginalDocumentRequired");
        static string Exact(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, roundId = round.RoundId, eventId = round.EventId,
            bidId = bid.Id, invitationId = invitation.Id, supplierId = bid.SupplierId,
            draftRevision = bid.DraftRevision, bidVersion = bid.Version,
            draftSnapshotSha256 = draftVersion.SnapshotSha256,
            preparedPlanId = prepared.Id, preparedPlanSha256 = prepared.SnapshotSha256,
            originalDocumentVersionId, originalDocumentSha256,
            recorderPrincipalId = recorder.PrincipalId, recorderWorkerAliasId = recorder.LinkedWorkerId,
            reportedReceivedAt, submittedAt = serverObservedAt,
            currencyCode = bid.CurrencyCode,
            lines = bidLines.OrderBy(x => x.SourcingLineId).Select(x => new
            {
                x.SourcingLineId, quantityExact = Exact(x.Quantity),
                unitPriceExact = Exact(x.UnitPrice), taxAmountExact = Exact(x.TaxAmount),
                deliveryAmountExact = Exact(x.DeliveryAmount), x.LeadTimeText, x.Notes,
            }),
        });
        Need(snapshot.Length <= 262144, "RfqSealedOfferSnapshotTooLarge");
        return new(round.RoundId, round.EventId, bid.Id, invitation.Id, bid.SupplierId,
            bid.DraftRevision, bid.Version, draftVersion.SnapshotSha256,
            originalDocumentVersionId, originalDocumentSha256, recorder.PrincipalId,
            recorder.LinkedWorkerId, reportedReceivedAt, serverObservedAt,
            snapshot, ProcurementEvidenceEncoding.Digest(snapshot));
    }

    public static PrincipalRfqRoundSealProof Close(
        PrincipalRfqPublishedRoundProof round,
        IReadOnlyList<PrincipalRfqManualOfferSubmission> submissions,
        RfqPrincipalActor officer, DateTimeOffset serverObservedAt)
    {
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(submissions);
        ArgumentNullException.ThrowIfNull(officer);
        Need(round.State == "Accepting" && round.RoundId != Guid.Empty
            && Instant(serverObservedAt) && Instant(round.ClosesAt)
            && serverObservedAt >= round.ClosesAt && submissions.Count <= 500
            && round.ApprovedSupplierIds is { Count: > 0 }
            && round.ApprovedSupplierIds.All(x => x != Guid.Empty)
            && round.ApprovedSupplierIds.Distinct().Count() == round.ApprovedSupplierIds.Count,
            "RfqSealedRoundCannotClose");
        Need(ProcurementPrincipalEvidence.Canonical(officer.PrincipalId)
            && ProcurementPrincipalEvidence.OptionalWorker(officer.LinkedWorkerId)
            && officer.TenantId == round.TenantId && officer.CompanyId == round.CompanyId
            && officer.Permissions is not null && officer.Permissions.Contains(CloseAction)
            && officer.Branches is not null
            && officer.PrincipalId == round.ClosingOfficerPrincipalId
            && officer.LinkedWorkerId == round.ClosingOfficerWorkerAliasId
            && (officer.SelectedBranchId is null || officer.SelectedBranchId == round.BranchId)
            && (officer.OrganisationWide || officer.Branches.Contains(round.BranchId)),
            "RfqSealedRoundOfficerNotAuthorized");
        Need(submissions.All(x => x is not null && x.RoundId == round.RoundId
            && x.EventId == round.EventId && Hash(x.SnapshotSha256)
            && x.SnapshotSha256 == ProcurementEvidenceEncoding.Digest(x.SnapshotJson)
            && round.ApprovedSupplierIds.Contains(x.SupplierId)
            && SubmissionPinsMatch(x, round)
            && x.SubmittedAt >= round.OpensAt && x.SubmittedAt < round.ClosesAt)
            && submissions.Select(x => x.BidId).Distinct().Count() == submissions.Count
            && submissions.Select(x => x.SupplierId).Distinct().Count() == submissions.Count,
            "RfqSealedSubmissionSetChanged");
        // This digest seals IDs and exact immutable snapshots, not just a count.
        var set = JsonSerializer.Serialize(submissions.OrderBy(x => x.BidId)
            .Select(x => new { x.BidId, x.SupplierId, x.SnapshotSha256 }));
        return new(round.RoundId, round.EventId, officer.PrincipalId,
            officer.LinkedWorkerId, serverObservedAt, submissions.Count,
            ProcurementEvidenceEncoding.Digest(set));
    }

    private static bool Instant(DateTimeOffset value) => value != default
        && value.Offset == TimeSpan.Zero && value.UtcTicks % 10 == 0;
    private static bool SubmissionPinsMatch(PrincipalRfqManualOfferSubmission offer,
        PrincipalRfqPublishedRoundProof round)
    {
        try
        {
            using var document = JsonDocument.Parse(offer.SnapshotJson);
            var pin = document.RootElement;
            return pin.GetProperty("schemaVersion").GetInt32() == 1
                && pin.GetProperty("roundId").GetGuid() == offer.RoundId
                && pin.GetProperty("eventId").GetGuid() == offer.EventId
                && pin.GetProperty("bidId").GetGuid() == offer.BidId
                && pin.GetProperty("invitationId").GetGuid() == offer.InvitationId
                && pin.GetProperty("supplierId").GetGuid() == offer.SupplierId
                && pin.GetProperty("draftRevision").GetInt32() == offer.DraftRevision
                && pin.GetProperty("bidVersion").GetInt64() == offer.BidVersion
                && pin.GetProperty("draftSnapshotSha256").GetString() == offer.DraftSnapshotSha256
                && pin.GetProperty("preparedPlanId").GetGuid() == round.PreparedPlanId
                && pin.GetProperty("preparedPlanSha256").GetString() == round.PreparedPlanSha256
                && pin.GetProperty("currencyCode").GetString() == round.CurrencyCode
                && pin.GetProperty("originalDocumentVersionId").GetGuid() == offer.OriginalDocumentVersionId
                && pin.GetProperty("originalDocumentSha256").GetString() == offer.OriginalDocumentSha256
                && pin.GetProperty("recorderPrincipalId").GetString() == offer.RecorderPrincipalId
                && (pin.GetProperty("recorderWorkerAliasId").ValueKind == JsonValueKind.Null
                    ? offer.RecorderWorkerAliasId is null
                    : pin.GetProperty("recorderWorkerAliasId").GetGuid() == offer.RecorderWorkerAliasId)
                && pin.GetProperty("reportedReceivedAt").GetDateTimeOffset() == offer.ReportedReceivedAt
                && pin.GetProperty("submittedAt").GetDateTimeOffset() == offer.SubmittedAt
                && pin.GetProperty("lines").ValueKind == JsonValueKind.Array
                && pin.GetProperty("lines").GetArrayLength() > 0;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException) { return false; }
    }
    private static bool Hash(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Need(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }
}
