using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>
/// Scoped storage factories, not authorization. The owning transaction must
/// use its retained M4 loader (including live demand holds,
/// original publication snapshot/Core outcome), current Core grants/branch scope,
/// source locks and original-key-first recovery. No employee link is required.
/// Nothing here mutates a supplied commercial record, sends mail or submits a bid.
/// </summary>
public static class PrincipalManualQuotationEvidence
{
    public sealed record LineInput(Guid SourcingLineId, decimal Quantity, decimal UnitPrice,
        decimal TaxAmount, decimal DeliveryAmount, string? LeadTimeText = null, string? Notes = null);
    public sealed record DraftProjection(SupplierBid Bid, IReadOnlyList<SupplierBidLine> Lines);

    /// <summary>Create the existing commercial records in the owned event scope;
    /// the invitation transition/contact receipt must already have been verified
    /// by the owning transaction. This pure factory does not add/save anything.</summary>
    public static DraftProjection Draft(SourcingEvent source, SourcingInvitation invitation,
        SourcingManualContact contact, IReadOnlyList<SourcingLine> sourceLines,
        IReadOnlyList<LineInput> input, string actor, DateTimeOffset? receivedAt = null,
        Guid? evidenceDocumentRefId = null)
    {
        Actor(actor, null); VerifyContact(contact);
        if (source is null || !SameScope(contact, source) || contact.SourcingEventId != source.Id
            || invitation is null || contact.InvitationId != invitation.Id
            || contact.SupplierId != invitation.SupplierId || contact.BranchId != source.BranchRef
            || input is null || input.Count is < 1 or > 200 || input.Any(x => x is null))
            throw new InvalidOperationException("ManualQuotationDraftInputInvalid");
        var bid = new SupplierBid
        {
            TenantId = source.TenantId, LegalEntityId = source.LegalEntityId, CreatedBy = actor,
            SourcingEventId = source.Id, SupplierId = invitation.SupplierId, InvitationId = invitation.Id,
            SourceChannel = "Manual", CurrencyCode = source.CurrencyCode, SurrogateEnteredBy = actor,
            ReceivedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(receivedAt),
            EvidenceDocumentRefId = evidenceDocumentRefId,
        };
        var lines = input.Select(line => new SupplierBidLine
        {
            TenantId = source.TenantId, LegalEntityId = source.LegalEntityId, CreatedBy = actor,
            SupplierBidId = bid.Id, SourcingLineId = line.SourcingLineId, Quantity = line.Quantity,
            UnitPrice = line.UnitPrice, TaxAmount = line.TaxAmount, DeliveryAmount = line.DeliveryAmount,
            LeadTimeText = line.LeadTimeText, Notes = line.Notes,
        }).ToArray();
        PrincipalManualQuotationDraft.Validate(source, invitation, bid, sourceLines, lines, actor);
        return new(bid, Array.AsReadOnly(lines));
    }

    public static SourcingManualContact Contact(SourcingEvent source, SourcingInvitation invitation,
        SourcingPublicationOpening opening, SourcingPublicationDecision decision,
        SourcingPublicationReceipt publicationReceipt, string actor, Guid? linkedWorker,
        string channel, string reference, DateTimeOffset reportedAt, DateTimeOffset recordedAt, Guid key)
    {
        Publication(source, opening, decision, publicationReceipt);
        Actor(actor, linkedWorker);
        if (invitation is null || !SameScope(invitation, source) || invitation.Id == Guid.Empty
            || invitation.SourcingEventId != source.Id || invitation.SupplierId == Guid.Empty
            || invitation.Status != "ReadyForManualDelivery" || invitation.SentAt is not null
            || invitation.ViewedAt is not null || invitation.DeliveryRef is not null
            || channel is not ("EmailOutsideSystem" or "Physical" or "Telephone" or "Other")
            || reference is null || reference.Trim().Length is < 1 or > 200
            || reportedAt == default || recordedAt == default || key == Guid.Empty)
            throw new InvalidOperationException("ManualContactEvidenceInvalid");
        var observed = At(reportedAt); var at = At(recordedAt);
        if (observed < At(decision.DecidedAt) || observed > at)
            throw new InvalidOperationException("ManualContactTimeInvalid");
        var id = Guid.CreateVersion7(); var text = reference.Trim();
        var command = ContactCommand(source.TenantId, source.LegalEntityId, source.Id, invitation.Id,
            invitation.SupplierId, source.BranchRef!.Value, opening.Id, decision.Id, source.Version,
            actor, linkedWorker, channel, text, observed, checked(invitation.Version + 1), key);
        var response = ContactResponse(id, source.Id, invitation.Id, invitation.SupplierId, checked(invitation.Version + 1), key);
        return new SourcingManualContact
        {
            Id = id, TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
            CreatedBy = actor, CreatedAt = at, SourcingEventId = source.Id,
            InvitationId = invitation.Id, SupplierId = invitation.SupplierId, BranchId = source.BranchRef.Value,
            PublicationOpeningId = opening.Id, PublicationDecisionId = decision.Id,
            EventVersionAtCapture = source.Version, InvitationVersionAtCapture = checked(invitation.Version + 1), ActorPrincipalId = actor,
            ActorCoreLinkedWorkerId = linkedWorker, Channel = channel, Reference = text,
            ReportedContactAt = observed, RecordedAt = at, CommandKey = key,
            CommandSha256 = ProcurementEvidenceEncoding.Digest(command), ResponseJson = response,
            ResponseSha256 = ProcurementEvidenceEncoding.Digest(response), ResponseStatusCode = 201,
        };
    }

    /// <summary>Candidate Version is still the locked original EF version for
    /// Revise/Cancel; the owning SaveChanges increments it once. No DraftRevision
    /// is introduced. Before/lines must be the retained pre-edit owning projection.</summary>
    public static SourcingManualQuoteVersion Quotation(SourcingEvent source, SourcingInvitation invitation,
        SourcingManualContact contact, SupplierBid candidate, IReadOnlyList<SupplierBidLine> lines,
        IReadOnlyList<SourcingLine> sourceLines, string actor, Guid? linkedWorker, Guid key,
        DateTimeOffset recordedAt, string operation, SourcingManualQuoteVersion? previous = null,
        SupplierBid? before = null, IReadOnlyList<SupplierBidLine>? beforeLines = null,
        PrincipalQuotationCommand? command = null, IReadOnlyList<PrincipalQuotationStorage.Pin>? documentPins = null)
    {
        Actor(actor, linkedWorker);
        VerifyContact(contact);
        if (source is null || source.ExecutionVersion != 2 || (operation != "Cancel" && source.Status != "Published")
            || source.BranchRef is null || source.BranchRef == Guid.Empty
            || !SameScope(contact, source) || contact.SourcingEventId != source.Id
            || source.Version < contact.EventVersionAtCapture
            || invitation is null || !SameScope(invitation, source) || invitation.SourcingEventId != source.Id
            || (operation != "Cancel" && invitation.Status != "ManualContactRecorded")
            || contact.InvitationId != invitation.Id || contact.SupplierId != invitation.SupplierId
            || contact.BranchId != source.BranchRef || recordedAt == default || At(recordedAt) < contact.RecordedAt
            || key == Guid.Empty || operation is not ("Create" or "Revise" or "Cancel"))
            throw new InvalidOperationException("ManualQuotationEvidenceInvalid");
        long resultVersion;
        if (operation == "Create")
        {
            if (previous is not null || before is not null || beforeLines is not null || candidate?.Version != 1)
                throw new InvalidOperationException("ManualQuotationCreateEvidenceInvalid");
            PrincipalManualQuotationDraft.Validate(source, invitation, candidate, sourceLines, lines, actor);
            resultVersion = 1;
        }
        else
        {
            if (previous is null || before is null || beforeLines is null)
                throw new InvalidOperationException("ManualQuotationPriorEvidenceRequired");
            VerifyQuotation(previous, before, beforeLines, contact);
            if (At(recordedAt) < previous.RecordedAt)
                throw new InvalidOperationException("ManualQuotationRevisionBackdated");
            if (before.Status != "Draft" || candidate is null || candidate.Id != before.Id
                || candidate.Version != before.Version || previous.BidVersion != before.Version
                || candidate.SurrogateEnteredBy != actor
                || candidate.SourcingEventId != source.Id || candidate.SupplierId != contact.SupplierId
                || candidate.InvitationId != contact.InvitationId || !SameScope(candidate, source))
                throw new InvalidOperationException("ManualQuotationPriorEvidenceChanged");
            resultVersion = checked(before.Version + 1);
            if (operation == "Revise")
                PrincipalManualQuotationDraft.Validate(source, invitation, candidate, sourceLines, lines, actor);
            else
            {
                if (candidate.Status != "Cancelled" || string.IsNullOrWhiteSpace(candidate.CorrectionReason)
                    || candidate.CorrectionReason.Length > 1000)
                    throw new InvalidOperationException("ManualQuotationCancellationInvalid");
                // Cancellation may change only state/reason, never disguise a
                // commercial revision as a cancellation of the recorded draft.
                var cancelled = CopyForCancellation(before, candidate.CorrectionReason, actor);
                if (PrincipalManualQuotationSnapshot.Build(candidate, lines)
                    != PrincipalManualQuotationSnapshot.Build(cancelled, beforeLines))
                    throw new InvalidOperationException("ManualQuotationCancellationChangedTerms");
            }
        }
        string snapshot; string request = ""; var pins = documentPins ?? [];
        var snapshotVersion = candidate!.PrincipalCaptureVersion == 2 ? 3 : 2;
        if (snapshotVersion == 3)
        {
            if (command is null) throw new InvalidOperationException("QuotationTypedCommandRequired");
            command = PrincipalQuotationCommandEvidence.Normalize(command);
            if (command.Operation != operation || command.ExpectedBidVersion != (operation == "Create" ? (long?)null : before!.Version)
                || operation != "Cancel" && (command.ExpectedEventVersion != source.Version
                    || command.ExpectedInvitationVersion != invitation.Version))
                throw new InvalidOperationException("QuotationCommandVersionChanged");
            var selections = command.Offer?.Documents;
            if (operation == "Cancel")
            {
                if (previous!.SnapshotSchemaVersion != 3) throw new InvalidOperationException("QuotationLegacyVersionCannotBeAdopted");
                var prior = PrincipalQuotationStorage.Restore(previous.SnapshotJson);
                selections = prior.Offer.Documents;
                if (PrincipalQuotationStorage.PinDigest(pins) != previous.DocumentPinSetSha256
                    || PrincipalQuotationStorage.Build(candidate, lines, resultVersion, selections!, pins)
                    != PrincipalQuotationStorage.Build(CopyForCancellation(before!, candidate.CorrectionReason!, actor),
                        beforeLines!, resultVersion, selections!, pins))
                    throw new InvalidOperationException("ManualQuotationCancellationChangedTerms");
            }
            snapshot = PrincipalQuotationStorage.Build(candidate, lines, resultVersion, selections!, pins);
            request = PrincipalQuotationCommandEvidence.Build(source.TenantId, source.LegalEntityId,
                contact.BranchId, actor, key, source.Id, invitation.Id,
                operation == "Create" ? null : candidate.Id, command);
        }
        else snapshot = PrincipalManualQuotationSnapshot.Build(candidate, lines);
        var snapshotSha = ProcurementEvidenceEncoding.Digest(snapshot); var id = Guid.CreateVersion7();
        var sealedCommand = QuoteCommand(source.TenantId, source.LegalEntityId, source.Id, invitation.Id,
            contact.SupplierId, candidate!.Id, contact.BranchId, contact.Id, previous?.Id,
            resultVersion, operation, actor, linkedWorker, key, snapshotSha);
        var response = QuoteResponse(id, source.Id, candidate.Id, resultVersion, operation,
            candidate.Status, contact.Id, key, snapshotSha);
        return new SourcingManualQuoteVersion
        {
            Id = id, TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
            CreatedBy = actor, CreatedAt = At(recordedAt), SourcingEventId = source.Id,
            InvitationId = invitation.Id, SupplierId = contact.SupplierId, SupplierBidId = candidate.Id,
            BranchId = contact.BranchId, ManualContactId = contact.Id, PreviousVersionId = previous?.Id,
            BidVersion = resultVersion, SnapshotSchemaVersion = snapshotVersion,
            EventVersionAtCapture = source.Version, InvitationVersionAtCapture = invitation.Version,
            RequestJson = request, RequestSha256 = ProcurementEvidenceEncoding.Digest(request),
            DocumentUseKey = pins.Count == 0 ? null : pins[0].Source.Key,
            DocumentPinCount = pins.Count, DocumentPinSetSha256 = PrincipalQuotationStorage.PinDigest(pins),
            Operation = operation, ActorPrincipalId = actor,
            ActorCoreLinkedWorkerId = linkedWorker, SnapshotJson = snapshot, SnapshotSha256 = snapshotSha,
            CommandKey = key, CommandSha256 = ProcurementEvidenceEncoding.Digest(sealedCommand),
            ResponseJson = response, ResponseSha256 = ProcurementEvidenceEncoding.Digest(response),
            ResponseStatusCode = operation == "Create" ? 201 : 200, RecordedAt = At(recordedAt),
        };
    }

    /// <summary>Retained M4 invitations remain readable after an outside-contact
    /// report only when that exact transition has owned immutable evidence.
    /// Status strings alone never prove a delivery or supplier submission.</summary>
    public static void VerifyPublishedInvitation(SourcingEvent source, SourcingInvitation invitation,
        SourcingPublicationOpening opening, SourcingPublicationDecision decision,
        SourcingPublicationReceipt receipt, SourcingManualContact? contact)
    {
        Publication(source, opening, decision, receipt);
        if (invitation is null || !SameScope(invitation, source)
            || invitation.SourcingEventId != source.Id || invitation.SupplierId == Guid.Empty
            || invitation.SentAt is not null || invitation.ViewedAt is not null || invitation.DeliveryRef is not null)
            throw new InvalidOperationException("ManualContactInvitationChanged");
        if (invitation.Status == "ReadyForManualDelivery")
        {
            if (contact is not null) throw new InvalidOperationException("ManualContactTransitionMissing");
            return;
        }
        if (invitation.Status != "ManualContactRecorded" || contact is null)
            throw new InvalidOperationException("ManualContactEvidenceRequired");
        VerifyContact(contact);
        if (!SameScope(contact, source) || contact.SourcingEventId != source.Id
            || contact.InvitationId != invitation.Id || contact.SupplierId != invitation.SupplierId
            || contact.BranchId != source.BranchRef || contact.PublicationOpeningId != opening.Id
            || contact.PublicationDecisionId != decision.Id || contact.EventVersionAtCapture != source.Version
            || contact.InvitationVersionAtCapture != invitation.Version
            || contact.ReportedContactAt < At(decision.DecidedAt))
            throw new InvalidOperationException("ManualContactSourceChanged");
    }

    public static void VerifyRetainedInvitation(SourcingEvent source, SourcingInvitation invitation,
        SourcingPublicationOpening opening, SourcingPublicationDecision decision,
        SourcingPublicationReceipt receipt, SourcingManualContact? contact)
    {
        Publication(source, opening, decision, receipt, retained: true);
        if (invitation is null || !SameScope(invitation, source)
            || invitation.SourcingEventId != source.Id || invitation.SupplierId == Guid.Empty
            || invitation.SentAt is not null || invitation.ViewedAt is not null || invitation.DeliveryRef is not null)
            throw new InvalidOperationException("ManualContactInvitationChanged");
        if (contact is null)
        {
            if (invitation.Status != "ReadyForManualDelivery")
                throw new InvalidOperationException("ManualContactEvidenceRequired");
            return;
        }
        VerifyContact(contact);
        if (invitation.Status is not ("ManualContactRecorded" or "Cancelled")
            || !SameScope(contact, source) || contact.SourcingEventId != source.Id
            || contact.InvitationId != invitation.Id || contact.SupplierId != invitation.SupplierId
            || contact.BranchId != source.BranchRef || contact.PublicationOpeningId != opening.Id
            || contact.PublicationDecisionId != decision.Id || contact.EventVersionAtCapture > source.Version
            || contact.EventVersionAtCapture < decision.EventVersionAtDecision
            || contact.InvitationVersionAtCapture > invitation.Version
            || contact.ReportedContactAt < At(decision.DecidedAt))
            throw new InvalidOperationException("ManualContactRetainedSourceChanged");
    }

    public static void VerifyContact(SourcingManualContact row)
    {
        if (row is null || row.SchemaVersion != 2 || !Scope(row) || row.Id == Guid.Empty
            || row.SourcingEventId == Guid.Empty || row.InvitationId == Guid.Empty || row.SupplierId == Guid.Empty
            || row.BranchId == Guid.Empty || row.PublicationOpeningId == Guid.Empty
            || row.PublicationDecisionId == Guid.Empty || row.EventVersionAtCapture < 1 || row.InvitationVersionAtCapture < 2
            || row.Channel is not ("EmailOutsideSystem" or "Physical" or "Telephone" or "Other")
            || string.IsNullOrWhiteSpace(row.Reference) || row.Reference != row.Reference.Trim()
            || row.Reference.Length > 200 || row.ReportedContactAt == default || row.RecordedAt == default
            || row.ReportedContactAt > row.RecordedAt || At(row.ReportedContactAt) != row.ReportedContactAt
            || At(row.RecordedAt) != row.RecordedAt || row.CommandKey == Guid.Empty || row.ResponseStatusCode != 201)
            throw new InvalidOperationException("ManualContactEvidenceInvalid");
        Actor(row.ActorPrincipalId, row.ActorCoreLinkedWorkerId);
        var command = ContactCommand(row.TenantId, row.LegalEntityId, row.SourcingEventId,
            row.InvitationId, row.SupplierId, row.BranchId, row.PublicationOpeningId,
            row.PublicationDecisionId, row.EventVersionAtCapture, row.ActorPrincipalId,
            row.ActorCoreLinkedWorkerId, row.Channel, row.Reference, row.ReportedContactAt, row.InvitationVersionAtCapture, row.CommandKey);
        var response = ContactResponse(row.Id, row.SourcingEventId, row.InvitationId, row.SupplierId, row.InvitationVersionAtCapture, row.CommandKey);
        if (row.CommandSha256 != ProcurementEvidenceEncoding.Digest(command) || row.ResponseJson != response
            || row.ResponseSha256 != ProcurementEvidenceEncoding.Digest(response))
            throw new InvalidOperationException("ManualContactReceiptChanged");
    }

    /// <summary>Compare to a fully restored owning version projection, not the
    /// latest mutable bid or a legacy row merely labelled Manual. Future history
    /// loading must restore the exact typed snapshot and enforce current Core READ.</summary>
    public static void VerifyQuotation(SourcingManualQuoteVersion row, SupplierBid ownedVersion,
        IReadOnlyList<SupplierBidLine> ownedLines, SourcingManualContact contact)
    {
        VerifyContact(contact);
        if (row is null || row.SchemaVersion != 2 || !Scope(row) || row.Id == Guid.Empty
            || !SameScope(row, contact) || row.SourcingEventId != contact.SourcingEventId
            || row.InvitationId != contact.InvitationId || row.SupplierId != contact.SupplierId
            || row.BranchId != contact.BranchId || row.ManualContactId != contact.Id
            || ownedVersion is null || row.SupplierBidId != ownedVersion.Id || !SameScope(ownedVersion, row)
            || ownedVersion.SourcingEventId != row.SourcingEventId
            || ownedVersion.SupplierId != row.SupplierId || ownedVersion.InvitationId != row.InvitationId
            || ownedVersion.Version != row.BidVersion || row.BidVersion < 1
            || ownedVersion.SurrogateEnteredBy != row.ActorPrincipalId || row.CommandKey == Guid.Empty
            || row.RecordedAt == default || row.RecordedAt < contact.RecordedAt
            || (row.Operation == "Create" && row.BidVersion != 1)
            || (row.Operation != "Create" && row.BidVersion < 2)
            || row.Operation is not ("Create" or "Revise" or "Cancel")
            || (row.Operation == "Create") != (row.PreviousVersionId is null)
            || row.PreviousVersionId == Guid.Empty || row.PreviousVersionId == row.Id
            || row.ResponseStatusCode != (row.Operation == "Create" ? 201 : 200)
            || ownedVersion.Status != (row.Operation == "Cancel" ? "Cancelled" : "Draft")
            || (row.Operation == "Cancel" && (string.IsNullOrWhiteSpace(ownedVersion.CorrectionReason)
                || ownedVersion.CorrectionReason.Length > 1000)))
            throw new InvalidOperationException("ManualQuotationEvidenceInvalid");
        Actor(row.ActorPrincipalId, row.ActorCoreLinkedWorkerId);
        string snapshot;
        if (row.SnapshotSchemaVersion == 3)
        {
            var restored = PrincipalQuotationStorage.Restore(row.SnapshotJson);
            snapshot = PrincipalQuotationStorage.Build(ownedVersion, ownedLines, row.BidVersion,
                restored.Offer.Documents!, restored.Pins);
            var request = PrincipalQuotationCommandEvidence.FromStored(row, restored);
            if (row.RequestJson != request || row.RequestSha256 != ProcurementEvidenceEncoding.Digest(request)
                || row.DocumentPinCount != restored.Pins.Count
                || row.DocumentPinSetSha256 != PrincipalQuotationStorage.PinDigest(restored.Pins)
                || row.DocumentUseKey != (restored.Pins.Count == 0 ? null : restored.Pins[0].Source.Key)
                || row.EventVersionAtCapture < 1 || row.InvitationVersionAtCapture < 1)
                throw new InvalidOperationException("QuotationTypedCommandChanged");
        }
        else if (row.SnapshotSchemaVersion == 2)
            snapshot = PrincipalManualQuotationSnapshot.Build(ownedVersion, ownedLines);
        else throw new InvalidOperationException("QuotationSnapshotSchemaUnsupported");
        var snapshotSha = ProcurementEvidenceEncoding.Digest(snapshot);
        var command = QuoteCommand(row.TenantId, row.LegalEntityId, row.SourcingEventId,
            row.InvitationId, row.SupplierId, row.SupplierBidId, row.BranchId, row.ManualContactId,
            row.PreviousVersionId, row.BidVersion, row.Operation, row.ActorPrincipalId,
            row.ActorCoreLinkedWorkerId, row.CommandKey, snapshotSha);
        var response = QuoteResponse(row.Id, row.SourcingEventId, row.SupplierBidId, row.BidVersion,
            row.Operation, ownedVersion.Status, row.ManualContactId, row.CommandKey, snapshotSha);
        if (row.SnapshotJson != snapshot || row.SnapshotSha256 != snapshotSha
            || row.CommandSha256 != ProcurementEvidenceEncoding.Digest(command)
            || row.ResponseJson != response || row.ResponseSha256 != ProcurementEvidenceEncoding.Digest(response))
            throw new InvalidOperationException("ManualQuotationReceiptChanged");
    }

    public static bool ContactRequestMatches(SourcingManualContact row, long expectedEventVersion,
        long expectedInvitationVersion, string channel, string reference, DateTimeOffset reportedAt)
    {
        VerifyContact(row);
        if (expectedEventVersion < 1 || expectedInvitationVersion < 1 || expectedInvitationVersion == long.MaxValue
            || reference is null || reference.Trim().Length is < 1 or > 200 || reportedAt == default
            || channel is not ("EmailOutsideSystem" or "Physical" or "Telephone" or "Other")) return false;
        // Reuse the stored historical alias in the old command seal. A current
        // employee link changing is not a changed original private request.
        var request = ContactCommand(row.TenantId, row.LegalEntityId, row.SourcingEventId,
            row.InvitationId, row.SupplierId, row.BranchId, row.PublicationOpeningId, row.PublicationDecisionId,
            expectedEventVersion, row.ActorPrincipalId, row.ActorCoreLinkedWorkerId, channel,
            reference.Trim(), At(reportedAt), checked(expectedInvitationVersion + 1), row.CommandKey);
        return row.CommandSha256 == ProcurementEvidenceEncoding.Digest(request);
    }

    public static string RecoverContact(SourcingManualContact row, string actor,
        Guid commandKey, string requestSha256)
    {
        VerifyContact(row);
        if (row.ActorPrincipalId != actor || row.CommandKey != commandKey || row.CommandSha256 != requestSha256)
            throw new InvalidOperationException("ManualContactCommandNotOwned");
        return row.ResponseJson;
    }

    public static string RecoverQuotation(SourcingManualQuoteVersion row, SupplierBid ownedVersion,
        IReadOnlyList<SupplierBidLine> ownedLines, SourcingManualContact contact,
        string actor, Guid commandKey, string requestSha256)
    {
        VerifyQuotation(row, ownedVersion, ownedLines, contact);
        if (row.ActorPrincipalId != actor || row.CommandKey != commandKey || row.CommandSha256 != requestSha256)
            throw new InvalidOperationException("ManualQuotationCommandNotOwned");
        return row.ResponseJson;
    }

    private static void Publication(SourcingEvent source, SourcingPublicationOpening opening,
        SourcingPublicationDecision decision, SourcingPublicationReceipt receipt, bool retained = false)
    {
        if (source is null || !Scope(source) || source.Id == Guid.Empty || source.ExecutionVersion != 2
            || (!retained && source.Status != "Published" || retained && source.Status is not ("Published" or "Cancelled"))
            || source.Version < 1 || source.BranchRef is null
            || source.BranchRef == Guid.Empty || opening is null || decision is null || receipt is null
            || !SameScope(opening, source) || !SameScope(decision, source) || !SameScope(receipt, source)
            || opening.SchemaVersion != 2 || decision.SchemaVersion != 2 || receipt.SchemaVersion != 2
            || opening.Id == Guid.Empty || decision.Id == Guid.Empty || opening.SourcingEventId != source.Id
            || decision.SourcingEventId != source.Id || decision.CaseState != "Approved"
            || decision.Action != "Approve" || opening.CoreApprovalCaseId == Guid.Empty
            || decision.CoreApprovalDecisionId == Guid.Empty || decision.CoreAssignmentId == Guid.Empty
            || opening.EventVersionAtOpening < 1 || decision.EventVersionAtDecision < opening.EventVersionAtOpening
            || !ProcurementPrincipalEvidence.Canonical(opening.MakerPrincipalId)
            || !ProcurementPrincipalEvidence.Canonical(decision.ActorPrincipalId)
            || decision.DecidedAt == default || decision.EventVersionAtDecision > source.Version
            || !PrincipalRfqPublicationReceiptResult.Matches(receipt, opening, decision))
            throw new InvalidOperationException("ManualContactPublicationEvidenceRequired");
    }
    private static SupplierBid CopyForCancellation(SupplierBid before, string reason, string actor) => new()
    {
        Id = before.Id, TenantId = before.TenantId, LegalEntityId = before.LegalEntityId,
        SourcingEventId = before.SourcingEventId, SupplierId = before.SupplierId, InvitationId = before.InvitationId,
        SubmissionVersion = before.SubmissionVersion, Version = before.Version, Status = "Cancelled",
        SourceChannel = before.SourceChannel, CurrencyCode = before.CurrencyCode, ReceivedAt = before.ReceivedAt,
        SubmittedAt = before.SubmittedAt, SurrogateEnteredBy = actor,
        EvidenceDocumentRefId = before.EvidenceDocumentRefId, CorrectionReason = reason,
        PrincipalCaptureVersion = before.PrincipalCaptureVersion, SupplierQuoteReference = before.SupplierQuoteReference,
        QuoteDate = before.QuoteDate, OfferValidityKind = before.OfferValidityKind, ValidUntil = before.ValidUntil,
        SupplierOfferedPaymentCode = before.SupplierOfferedPaymentCode, SupplierOfferedPaymentText = before.SupplierOfferedPaymentText,
        WarrantyText = before.WarrantyText, DeliveryTermsText = before.DeliveryTermsText, DeliveryDate = before.DeliveryDate,
    };
    private static void Actor(string actor, Guid? linkedWorker)
    {
        if (!ProcurementPrincipalEvidence.Canonical(actor) || !ProcurementPrincipalEvidence.OptionalWorker(linkedWorker))
            throw new InvalidOperationException("ManualQuotationPrincipalInvalid");
    }
    private static bool Scope(ProcurementRecord row) => !string.IsNullOrWhiteSpace(row.TenantId)
        && row.TenantId.Length <= 128 && row.LegalEntityId != Guid.Empty;
    private static bool SameScope(ProcurementRecord? row, ProcurementRecord owner) => row is not null
        && row.TenantId == owner.TenantId && row.LegalEntityId == owner.LegalEntityId;
    private static DateTimeOffset At(DateTimeOffset value) => ProcurementEvidenceEncoding.DatabaseTimestamp(value)!.Value;
    private static string ContactCommand(string tenant, Guid company, Guid source, Guid invitation, Guid supplier,
        Guid branch, Guid opening, Guid decision, long version, string actor, Guid? worker,
        string channel, string reference, DateTimeOffset reportedAt, long invitationVersion, Guid key) => JsonSerializer.Serialize(new
        { schemaVersion = 2, operation = "RecordManualContact", tenant, company, source, invitation,
            supplier, branch, opening, decision, version, invitationVersion, actor, worker, channel, reference, reportedAt, key });
    private static string ContactResponse(Guid id, Guid source, Guid invitation, Guid supplier, long invitationVersion, Guid key) =>
        JsonSerializer.Serialize(new { schemaVersion = 2, manualContactId = id, eventId = source,
            invitationId = invitation, supplierId = supplier, invitationVersion, status = "ManualContactRecorded", commandKey = key,
            supplierDeliveryReported = true, emailSentBySystem = false });
    private static string QuoteCommand(string tenant, Guid company, Guid source, Guid invitation, Guid supplier,
        Guid bid, Guid branch, Guid contact, Guid? previous, long version, string operation,
        string actor, Guid? worker, Guid key, string snapshotSha) => JsonSerializer.Serialize(new
        { schemaVersion = 2, operation, tenant, company, source, invitation, supplier, bid, branch,
            contact, previous, version, actor, worker, key, snapshotSha });
    private static string QuoteResponse(Guid id, Guid source, Guid bid, long version, string operation,
        string status, Guid contact, Guid key, string snapshotSha) => JsonSerializer.Serialize(new
        { schemaVersion = 2, quotationVersionId = id, eventId = source, quotationId = bid,
            version, operation, status, manualContactId = contact, commandKey = key, snapshotSha256 = snapshotSha,
            supplierSubmitted = false, emailSentBySystem = false });
}
