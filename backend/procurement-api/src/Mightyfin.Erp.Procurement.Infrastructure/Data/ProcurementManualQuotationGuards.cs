using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Infrastructure.Data;

public sealed partial class ProcurementDbContext
{
    // Runs after shared metadata/version preparation. A legacy controller may
    // neither adopt nor mutate an M7 quotation without its owning V3 receipt.
    private void GuardManualQuotations()
    {
        var contacts = ChangeTracker.Entries<SourcingManualContact>().ToArray();
        var versions = ChangeTracker.Entries<SourcingManualQuoteVersion>().ToArray();
        if (contacts.Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || versions.Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("ManualQuotationEvidenceAppendOnly");
        foreach (var entry in contacts.Where(x => x.State == EntityState.Added))
        {
            var row = entry.Entity;
            PrincipalManualQuotationEvidence.VerifyContact(row);
            RequireManualActor(row, row.ActorPrincipalId, row.ActorCoreLinkedWorkerId);
            var source = SourcingEvents.Local.SingleOrDefault(x => x.Id == row.SourcingEventId)
                ?? throw new InvalidOperationException("ManualContactSourceNotTracked");
            var invitation = SourcingInvitations.Local.SingleOrDefault(x => x.Id == row.InvitationId)
                ?? throw new InvalidOperationException("ManualContactInvitationNotTracked");
            var inviteEntry = Entry(invitation);
            if (inviteEntry.State != EntityState.Modified
                || inviteEntry.OriginalValues.GetValue<string>(nameof(SourcingInvitation.Status)) != "ReadyForManualDelivery"
                || invitation.Version != row.InvitationVersionAtCapture)
                throw new InvalidOperationException("ManualContactTransitionMissing");
            VerifyTrackedManualContact(source, invitation, row);
            VerifyManualEffects(PrincipalQuotationEffects.Contact(row));
        }
        var added = versions.Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        foreach (var row in added)
        {
            RequireManualActor(row, row.ActorPrincipalId, row.ActorCoreLinkedWorkerId);
            if (row.SnapshotSchemaVersion != 3)
                throw new InvalidOperationException("ManualQuotationNewCaptureRequiresV3");
            var bid = SupplierBids.Local.SingleOrDefault(x => x.Id == row.SupplierBidId)
                ?? throw new InvalidOperationException("ManualQuotationHeaderNotTracked");
            var bidEntry = Entry(bid);
            if (bid.PrincipalCaptureVersion != 2 || row.BidVersion != bid.Version
                || (row.Operation == "Create" ? bidEntry.State != EntityState.Added
                    : bidEntry.State != EntityState.Modified)
                || row.Operation != "Create" && bidEntry.OriginalValues.GetValue<int>(nameof(SupplierBid.PrincipalCaptureVersion)) != 2
                || added.Count(x => x.SupplierBidId == bid.Id) != 1)
                throw new InvalidOperationException("ManualQuotationHeaderReceiptMismatch");
            var contact = SourcingManualContacts.Local.SingleOrDefault(x => x.Id == row.ManualContactId)
                ?? SourcingManualContacts.SingleOrDefault(x => x.Id == row.ManualContactId)
                ?? throw new InvalidOperationException("ManualQuotationContactMissing");
            VerifyManualEffects(PrincipalQuotationEffects.Quotation(row));
            var lines = TrackedManualLines(bid.Id);
            PrincipalManualQuotationEvidence.VerifyQuotation(row, bid, lines.Where(x => x.IsActive).ToArray(), contact);
            if (row.Operation != "Create")
            {
                var previous = SourcingManualQuoteVersions.Local.SingleOrDefault(x => x.Id == row.PreviousVersionId)
                    ?? SourcingManualQuoteVersions.SingleOrDefault(x => x.Id == row.PreviousVersionId)
                    ?? throw new InvalidOperationException("ManualQuotationPriorVersionMissing");
                if (Entry(previous).State != EntityState.Unchanged || previous.SnapshotSchemaVersion != 3
                    || previous.SupplierBidId != bid.Id || previous.BidVersion != row.BidVersion - 1
                    || previous.RecordedAt > row.RecordedAt)
                    throw new InvalidOperationException("ManualQuotationPriorVersionChanged");
                var prior = PrincipalQuotationStorage.Restore(previous.SnapshotJson);
                PrincipalManualQuotationEvidence.VerifyQuotation(previous, prior.Bid, prior.Lines, contact);
                var original = (SupplierBid)bidEntry.OriginalValues.ToObject();
                var originalLines = lines.Where(x => Entry(x).State != EntityState.Added)
                    .Select(x => Entry(x).State == EntityState.Modified
                        ? (SupplierBidLine)Entry(x).OriginalValues.ToObject() : x).Where(x => x.IsActive).ToArray();
                if (original.Status != "Draft" || PrincipalQuotationStorage.Build(original, originalLines,
                    previous.BidVersion, prior.Offer.Documents!, prior.Pins) != previous.SnapshotJson)
                    throw new InvalidOperationException("ManualQuotationCurrentVersionChanged");
                if (row.Operation == "Cancel")
                {
                    // Cancellation retains all original terms and actual pins;
                    // current supplier eligibility and RFQ status are irrelevant.
                    original.Status = "Cancelled"; original.CorrectionReason = bid.CorrectionReason;
                    original.SurrogateEnteredBy = row.ActorPrincipalId;
                    var current = PrincipalQuotationStorage.Restore(row.SnapshotJson);
                    if (PrincipalQuotationStorage.Build(original, originalLines, row.BidVersion,
                        prior.Offer.Documents!, prior.Pins) != row.SnapshotJson
                        || PrincipalQuotationStorage.PinDigest(current.Pins) != previous.DocumentPinSetSha256)
                        throw new InvalidOperationException("ManualQuotationCancellationChangedTerms");
                }
            }
            if (row.Operation != "Cancel")
            {
                var source = SourcingEvents.Local.SingleOrDefault(x => x.Id == row.SourcingEventId)
                    ?? throw new InvalidOperationException("ManualQuotationSourceNotTracked");
                var invite = SourcingInvitations.Local.SingleOrDefault(x => x.Id == row.InvitationId)
                    ?? throw new InvalidOperationException("ManualQuotationInvitationNotTracked");
                VerifyTrackedManualContact(source, invite, contact);
                if (source.Version != row.EventVersionAtCapture || invite.Version != row.InvitationVersionAtCapture)
                    throw new InvalidOperationException("ManualQuotationSourceVersionChanged");
                _ = SourcingLines.Where(x => x.SourcingEventId == source.Id).ToArray();
                var sourceLines = SourcingLines.Local.Where(x => x.SourcingEventId == source.Id).ToArray();
                PrincipalManualQuotationDraft.Validate(source, invite, bid, sourceLines,
                    lines.Where(x => x.IsActive).ToArray(), row.ActorPrincipalId);
            }
        }
        var effects = contacts.Where(x => x.State == EntityState.Added).Select(x => PrincipalQuotationEffects.Contact(x.Entity))
            .Concat(added.Select(PrincipalQuotationEffects.Quotation)).ToArray();
        foreach (var audit in ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added
            && x.Entity.RecordType == "SupplierQuotation" && x.Entity.Action is
                "OutsideContactRecorded" or "QuotationDraftCreate" or "QuotationDraftRevise" or "QuotationDraftCancel").Select(x => x.Entity))
            if (!effects.Any(x => x.Audit.RecordId == audit.RecordId && x.Audit.Action == audit.Action
                && x.Audit.ChangedFieldsJson == audit.ChangedFieldsJson))
                throw new InvalidOperationException("QuotationOwningEffectsUnbacked");
        foreach (var item in ChangeTracker.Entries<OutboxEvent>().Where(x => x.State == EntityState.Added
            && x.Entity.EventType is "sourcing.OutsideContactRecorded.v3" or "sourcing.QuotationDraftCreate.v3"
                or "sourcing.QuotationDraftRevise.v3" or "sourcing.QuotationDraftCancel.v3").Select(x => x.Entity))
            if (!effects.Any(x => x.Outbox.IdempotencyKey == item.IdempotencyKey && x.Outbox.PayloadJson == item.PayloadJson))
                throw new InvalidOperationException("QuotationOwningEffectsUnbacked");
        foreach (var entry in ChangeTracker.Entries<SupplierBid>().Where(x =>
            x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity.PrincipalCaptureVersion != 2 && (entry.State == EntityState.Added
                || entry.OriginalValues.GetValue<int>(nameof(SupplierBid.PrincipalCaptureVersion)) != 2))
            {
                if (LegacyQuotationTermsChanged(entry, ManualHeaderTerms))
                    throw new InvalidOperationException("ManualQuotationOwnedCommandRequired");
                continue;
            }
            if (entry.State == EntityState.Deleted || !added.Any(x => x.SupplierBidId == entry.Entity.Id))
                throw new InvalidOperationException("ManualQuotationOwnedCommandRequired");
        }
        foreach (var entry in ChangeTracker.Entries<SupplierBidLine>().Where(x =>
            x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var owned = entry.Entity.PrincipalCaptureVersion == 2
                || entry.State != EntityState.Added && entry.OriginalValues.GetValue<int>(nameof(SupplierBidLine.PrincipalCaptureVersion)) == 2
                || SupplierBids.Local.Any(x => x.Id == entry.Entity.SupplierBidId && x.PrincipalCaptureVersion == 2);
            if (!owned && LegacyQuotationTermsChanged(entry, ManualLineTerms))
                throw new InvalidOperationException("ManualQuotationOwnedCommandRequired");
            if (owned && (entry.State == EntityState.Deleted || !added.Any(x => x.SupplierBidId == entry.Entity.SupplierBidId)))
                throw new InvalidOperationException("ManualQuotationOwnedCommandRequired");
        }
    }

    private static readonly string[] ManualHeaderTerms =
        [nameof(SupplierBid.SupplierQuoteReference), nameof(SupplierBid.QuoteDate), nameof(SupplierBid.OfferValidityKind),
         nameof(SupplierBid.ValidUntil), nameof(SupplierBid.SupplierOfferedPaymentCode), nameof(SupplierBid.SupplierOfferedPaymentText),
         nameof(SupplierBid.WarrantyText), nameof(SupplierBid.DeliveryTermsText), nameof(SupplierBid.DeliveryDate)];
    private static readonly string[] ManualLineTerms =
        [nameof(SupplierBidLine.DiscountKind), nameof(SupplierBidLine.DiscountValue), nameof(SupplierBidLine.TaxTreatment),
         nameof(SupplierBidLine.SupplierOfferedTaxCode), nameof(SupplierBidLine.IsActive)];
    private static bool LegacyQuotationTermsChanged(EntityEntry entry, string[] properties)
    {
        // Existing legacy values are not retroactively rejected during unrelated
        // edits. New records/defaults or a change to a new offered field cannot
        // be smuggled through an older overposting/generic persistence path.
        if (entry.State != EntityState.Added)
            return entry.Properties.Any(x => x.IsModified && properties.Contains(x.Metadata.Name));
        return properties.Any(name => name == nameof(SupplierBidLine.IsActive)
            ? entry.Property(name).CurrentValue is not true
            : name == nameof(SupplierBidLine.DiscountValue)
                ? entry.Property(name).CurrentValue is not decimal value || value != 0m
                : entry.Property(name).CurrentValue is not null);
    }

    private void VerifyManualEffects(PrincipalQuotationEffects.Effects expected)
    {
        PrincipalQuotationEffects.Verify(expected,
            ChangeTracker.Entries<AuditEvent>().Where(x => x.State == EntityState.Added
                && x.Entity.RecordType == expected.Audit.RecordType && x.Entity.RecordId == expected.Audit.RecordId
                && x.Entity.Action == expected.Audit.Action).Select(x => x.Entity).ToArray(),
            ChangeTracker.Entries<OutboxEvent>().Where(x => x.State == EntityState.Added
                && x.Entity.IdempotencyKey == expected.Outbox.IdempotencyKey).Select(x => x.Entity).ToArray(),
            scope.CorrelationId);
    }

    private SupplierBidLine[] TrackedManualLines(Guid bidId)
    {
        _ = SupplierBidLines.Where(x => x.SupplierBidId == bidId).ToArray();
        return SupplierBidLines.Local.Where(x => x.SupplierBidId == bidId).ToArray();
    }

    private void RequireManualActor(ProcurementRecord row, string actor, Guid? historicalWorker)
    {
        if (row.TenantId != scope.TenantId || row.LegalEntityId != scope.LegalEntityId
            || actor != scope.SubjectId || historicalWorker != scope.CoreLinkedWorkerId)
            throw new InvalidOperationException("ManualQuotationActorScopeMismatch");
        // Alias comparison is only for a NEW capture's historical attribution;
        // retained receipt reads never invoke this write guard.
    }

    private void VerifyTrackedManualContact(SourcingEvent source, SourcingInvitation invitation,
        SourcingManualContact contact)
    {
        var opening = SourcingPublicationOpenings.Local.SingleOrDefault(x => x.Id == contact.PublicationOpeningId)
            ?? SourcingPublicationOpenings.SingleOrDefault(x => x.Id == contact.PublicationOpeningId)
            ?? throw new InvalidOperationException("ManualContactOpeningMissing");
        var decision = SourcingPublicationDecisions.Local.SingleOrDefault(x => x.Id == contact.PublicationDecisionId)
            ?? SourcingPublicationDecisions.SingleOrDefault(x => x.Id == contact.PublicationDecisionId)
            ?? throw new InvalidOperationException("ManualContactDecisionMissing");
        var receipt = SourcingPublicationReceipts.Local.SingleOrDefault(x => x.DecisionId == decision.Id)
            ?? SourcingPublicationReceipts.SingleOrDefault(x => x.DecisionId == decision.Id)
            ?? throw new InvalidOperationException("ManualContactPublicationReceiptMissing");
        PrincipalManualQuotationEvidence.VerifyPublishedInvitation(source, invitation, opening, decision, receipt, contact);
    }
}
