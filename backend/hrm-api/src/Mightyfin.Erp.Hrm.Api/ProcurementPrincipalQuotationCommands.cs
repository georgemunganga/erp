using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static Task<IResult> QuotationContact(Guid id, Guid invitationId, QuotationContactInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        RunQuotation(http, scope, async (db, cases, core, auth, token) =>
        {
            var key = Key(http); await LockPrincipalSourcingCommandKey(db, key, token);
            _ = await PrincipalActor(core, scope, http, auth, "procurement-quotation-read", null, false, token);
            if (input is null || input.ExpectedEventVersion < 1 || input.ExpectedInvitationVersion is < 1 or long.MaxValue
                || input.Reference is null || input.Reference.Trim().Length is < 1 or > 200
                || input.ReportedAt == default || input.Channel is not ("EmailOutsideSystem" or "Physical" or "Telephone" or "Other"))
                throw new Problem("ManualContactInputInvalid", 422);
            var old = await db.SourcingManualContacts.AsNoTracking().SingleOrDefaultAsync(x =>
                x.ActorPrincipalId == scope.SubjectId && x.CommandKey == key, token);
            if (old is not null)
            {
                var retained = await QuotationSource(db, core, scope, http, auth, "procurement-quotation-read", old.SourcingEventId, token);
                _ = await QuotationRetainedSource(db, cases, scope, retained, token);
                if (old.SourcingEventId != id || old.InvitationId != invitationId
                    || !PrincipalManualQuotationEvidence.ContactRequestMatches(old, input.ExpectedEventVersion,
                        input.ExpectedInvitationVersion, input.Channel, input.Reference, input.ReportedAt))
                    throw new Problem("QuotationCommandKeyConflict", 409);
                return new(PrincipalManualQuotationEvidence.RecoverContact(old, scope.SubjectId, key, old.CommandSha256), 201);
            }
            await RequireQuotationKeyUnused(db, key, token);
            var source = await QuotationSource(db, core, scope, http, auth, "procurement-sourcing-contact", id, token);
            RequireQuotationBranch(http, scope, source);
            await RequireQuotationRecord(http, db, newCommercial: true, token);
            await LockQuotationSource(db, scope, id, invitationId, null, token);
            await db.Entry(source).ReloadAsync(token);
            var evidence = await PrincipalPublicationEvidence(db, cases, source, token);
            var invite = await db.SourcingInvitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.SourcingEventId == id, token)
                ?? throw new Problem("QuotationNotFound", 404);
            if (source.Version != input.ExpectedEventVersion || invite.Version != input.ExpectedInvitationVersion
                || invite.Status != "ReadyForManualDelivery") throw new Problem("QuotationSourceVersionChanged", 409);
            await RequireQuotationSupplier(db, invite.SupplierId, token);
            var approval = await db.SourcingPublicationDecisions.AsNoTracking().SingleAsync(x =>
                x.OpeningId == evidence.Opening.Id && x.CaseState == "Approved", token);
            var receipt = await db.SourcingPublicationReceipts.AsNoTracking().SingleAsync(x => x.DecisionId == approval.Id, token);
            var contact = PrincipalManualQuotationEvidence.Contact(source, invite, evidence.Opening, approval, receipt,
                scope.SubjectId, scope.CoreLinkedWorkerId, input.Channel, input.Reference, input.ReportedAt,
                DateTimeOffset.UtcNow, key);
            invite.Status = "ManualContactRecorded"; db.AddScoped(contact);
            QuotationRecord(db, scope, PrincipalQuotationEffects.Contact(contact));
            await db.SaveChangesAsync(token);
            return new(contact.ResponseJson, contact.ResponseStatusCode);
        }, ct);

    private static Task<IResult> QuotationCreate(Guid id, Guid invitationId, QuotationDraftInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        QuotationChange(id, invitationId, null, "Create", input, http, scope, ct);
    private static Task<IResult> QuotationRevise(Guid id, Guid bidId, QuotationDraftInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        QuotationChange(id, null, bidId, "Revise", input, http, scope, ct);
    private static Task<IResult> QuotationCancel(Guid id, Guid bidId, QuotationDraftInput? input,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        QuotationChange(id, null, bidId, "Cancel", input, http, scope, ct);

    private static Task<IResult> QuotationChange(Guid id, Guid? invitationId, Guid? bidId, string operation,
        QuotationDraftInput? input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct) =>
        RunQuotation(http, scope, async (db, cases, core, auth, token) =>
        {
            var key = Key(http); await LockPrincipalSourcingCommandKey(db, key, token);
            _ = await PrincipalActor(core, scope, http, auth, "procurement-quotation-read", null, false, token);
            PrincipalQuotationCommand command;
            try
            {
                command = QuotationInput(operation, input);
            }
            catch (InvalidOperationException) { throw new Problem("QuotationInputInvalid", 422); }
            var old = await db.SourcingManualQuoteVersions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.CommandKey == key && x.ActorPrincipalId == scope.SubjectId, token);
            if (old is not null)
            {
                var parent = await QuotationSource(db, core, scope, http, auth, "procurement-quotation-read", old.SourcingEventId, token);
                _ = await QuotationRetainedSource(db, cases, scope, parent, token);
                var request = PrincipalQuotationCommandEvidence.Build(scope.TenantId, scope.LegalEntityId, old.BranchId,
                    scope.SubjectId, key, id, invitationId ?? old.InvitationId, bidId, command);
                if (old.Operation != operation || old.SourcingEventId != id || old.RequestJson != request
                    || old.RequestSha256 != ProcurementEvidenceEncoding.Digest(request))
                    throw new Problem("QuotationCommandKeyConflict", 409);
                var retainedContact = await db.SourcingManualContacts.AsNoTracking().SingleAsync(x => x.Id == old.ManualContactId, token);
                var stored = PrincipalQuotationStorage.Restore(old.SnapshotJson);
                return new(PrincipalManualQuotationEvidence.RecoverQuotation(old, stored.Bid, stored.Lines,
                    retainedContact, scope.SubjectId, key, old.CommandSha256), old.ResponseStatusCode);
            }
            await RequireQuotationKeyUnused(db, key, token);
            var permission = operation == "Cancel" ? "procurement-quotation-cancel" : "procurement-quotation-capture";
            var source = await QuotationSource(db, core, scope, http, auth, permission, id, token);
            RequireQuotationBranch(http, scope, source);
            await RequireQuotationRecord(http, db, operation != "Cancel", token);
            var inviteId = invitationId ?? await db.SupplierBids.AsNoTracking().Where(x => x.Id == bidId
                && x.SourcingEventId == id && x.PrincipalCaptureVersion == 2).Select(x => x.InvitationId)
                .SingleOrDefaultAsync(token) ?? throw new Problem("QuotationNotFound", 404);
            await LockQuotationSource(db, scope, id, inviteId, bidId, token);
            await db.Entry(source).ReloadAsync(token);
            var published = operation == "Cancel"
                ? await QuotationRetainedSource(db, cases, scope, source, token)
                : await PrincipalPublicationEvidence(db, cases, source, token);
            var invitation = await db.SourcingInvitations.SingleOrDefaultAsync(x => x.Id == inviteId && x.SourcingEventId == id, token)
                ?? throw new Problem("QuotationNotFound", 404);
            var contact = await db.SourcingManualContacts.AsNoTracking().SingleOrDefaultAsync(x => x.InvitationId == inviteId, token)
                ?? throw new Problem("ManualContactRequired", 409);
            if (operation != "Cancel")
            {
                if (command.ExpectedEventVersion != source.Version || command.ExpectedInvitationVersion != invitation.Version)
                    throw new Problem("QuotationSourceVersionChanged", 409);
                await RequireQuotationSupplier(db, invitation.SupplierId, token);
                if (command.Offer!.Documents!.Count != 0) throw new Problem("QuotationDocumentUsesUnavailable", 503);
            }
            SupplierBid candidate; SupplierBidLine[] active;
            SourcingManualQuoteVersion? previous = null; PrincipalQuotationStorage.Restored? before = null;
            if (operation == "Create")
            {
                if (await db.SupplierBids.AnyAsync(x => x.SourcingEventId == id && x.SupplierId == invitation.SupplierId, token))
                    throw new Problem("QuotationAlreadyExists", 409); // Includes cancelled/legacy: no inferred recreation.
                var draft = PrincipalManualQuotationEvidence.Draft(source, invitation, contact,
                    published.Lines.Select(x => x.Line).ToArray(), command.Lines!.Select(x =>
                        new PrincipalManualQuotationEvidence.LineInput(x.SourcingLineId, x.Quantity, x.UnitPrice,
                            x.QuotedTaxAmount, x.DeliveryAmount, x.LeadTimeText, x.Notes)).ToArray(), scope.SubjectId, command.ReceivedAt);
                candidate = draft.Bid; active = draft.Lines.ToArray();
                PrincipalQuotationStorage.Apply(candidate, active, command.Offer!, command.Lines!);
                db.AddScoped(candidate); foreach (var line in active) db.AddScoped(line);
            }
            else
            {
                candidate = await db.SupplierBids.SingleOrDefaultAsync(x => x.Id == bidId && x.SourcingEventId == id
                    && x.PrincipalCaptureVersion == 2, token) ?? throw new Problem("QuotationNotFound", 404);
                var history = await LoadQuotationHistory(db, candidate, token);
                previous = history.Latest; before = PrincipalQuotationStorage.Restore(previous.SnapshotJson);
                if (candidate.Status != "Draft" || candidate.Version != command.ExpectedBidVersion)
                    throw new Problem("QuotationVersionChanged", 409);
                candidate.SurrogateEnteredBy = scope.SubjectId; candidate.CorrectionReason = command.Reason;
                active = history.Lines.Where(x => x.IsActive).ToArray();
                if (operation == "Cancel") candidate.Status = "Cancelled";
                else
                {
                    if (before.Pins.Count != 0) throw new Problem("QuotationDocumentUsesUnavailable", 503);
                    var all = history.Lines.ToDictionary(x => x.SourcingLineId);
                    foreach (var line in history.Lines) line.IsActive = false;
                    var selected = new List<SupplierBidLine>();
                    foreach (var value in command.Lines!)
                    {
                        if (!all.TryGetValue(value.SourcingLineId, out var line))
                        {
                            line = new SupplierBidLine { TenantId = source.TenantId, LegalEntityId = source.LegalEntityId,
                                SupplierBidId = candidate.Id, SourcingLineId = value.SourcingLineId, PrincipalCaptureVersion = 2 };
                            db.AddScoped(line);
                        }
                        line.Quantity = value.Quantity; line.UnitPrice = value.UnitPrice; line.TaxAmount = value.QuotedTaxAmount;
                        line.DeliveryAmount = value.DeliveryAmount; selected.Add(line);
                    }
                    active = selected.ToArray(); candidate.ReceivedAt = command.ReceivedAt;
                    PrincipalQuotationStorage.Apply(candidate, active, command.Offer!, command.Lines!);
                }
                // Explicit even for a line-only revision: existing CAS increments
                // the header once; no independent DraftRevision is introduced.
                db.Entry(candidate).State = EntityState.Modified;
            }
            var version = PrincipalManualQuotationEvidence.Quotation(source, invitation, contact, candidate, active,
                published.Lines.Select(x => x.Line).ToArray(), scope.SubjectId, scope.CoreLinkedWorkerId, key,
                DateTimeOffset.UtcNow, operation, previous, before?.Bid, before?.Lines, command,
                operation == "Cancel" ? before!.Pins : []);
            db.AddScoped(version);
            QuotationRecord(db, scope, PrincipalQuotationEffects.Quotation(version));
            await db.SaveChangesAsync(token);
            return new(version.ResponseJson, version.ResponseStatusCode);
        }, ct);

    private static void RequireQuotationBranch(HttpContext http, ProcurementRequestScope scope, SourcingEvent source)
    {
        if (!QuotationWriteContextAllowed(scope, source.BranchRef, http.Request.Headers.ContainsKey("X-Shell-Branch-Scope")))
            throw new Problem("QuotationBranchSelectionRequired", 422);
    }
    internal static bool QuotationWriteContextAllowed(ProcurementRequestScope scope, Guid? sourceBranch, bool rawAllHeader = false) =>
        !rawAllHeader && !scope.IsExplicitAllBranches && scope.OrgUnitId is null && sourceBranch is not null
        && scope.WorkLocationId is not null && scope.WorkLocationId == sourceBranch;

    private static async Task RequireQuotationSupplier(ProcurementDbContext db, Guid supplierId, CancellationToken ct)
    {
        var supplier = await db.Suppliers.Include(x => x.Qualifications).SingleOrDefaultAsync(x => x.Id == supplierId, ct);
        if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, DateTimeOffset.UtcNow))
            throw new Problem("QuotationSupplierUnavailable", 409);
    }
    private static void QuotationRecord(ProcurementDbContext db, ProcurementRequestScope scope,
        PrincipalQuotationEffects.Effects effects)
    {
        effects.Audit.CorrelationId = scope.CorrelationId;
        db.AddScoped(effects.Audit); db.AddScoped(effects.Outbox);
    }
}
