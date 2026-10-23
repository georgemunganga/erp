using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    public sealed record PrincipalManualContactInput(string Channel, string Reference,
        DateTimeOffset ReportedContactAt);

    private static Task<IResult> PrincipalManualContact(Guid id, Guid invitationId,
        PrincipalManualContactInput input, HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, _, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var key = Key(http);
            await LockPrincipalSourcingCommandKey(db, key, token);
            var reference = Normalize(input.Reference);
            if (id == Guid.Empty || invitationId == Guid.Empty || reference.Length is < 1 or > 200
                || input.Channel is not ("EmailOutsideSystem" or "Physical" or "Telephone" or "Other")
                || input.ReportedContactAt == default)
                throw new Problem("SourcingManualContactInvalid", 422);
            var actor = await PrincipalActor(core, scope, http, auth,
                "procurement-sourcing-submit", null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 2, operation = "RecordManualContact",
                eventId = id, invitationId, input.Channel, reference, input.ReportedContactAt });
            var previous = await db.SourcingManualContacts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CommandKey == key, token);
            if (previous is not null)
            {
                if (previous.ActorPrincipalId != actor.PrincipalId
                    || previous.ActorCoreLinkedWorkerId != actor.LinkedWorkerId)
                    throw new Problem("SourcingContactNotFound", 404);
                _ = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                    "procurement-sourcing-submit", previous.SourcingEventId, token);
                if (previous.SourcingEventId != id || previous.InvitationId != invitationId
                    || previous.CommandSha256 != fingerprint)
                    throw new Problem("SourcingCommandKeyConflict", 409);
                if (previous.ResponseSha256 != ProcurementEvidenceEncoding.Digest(previous.ResponseJson))
                    throw new Problem("SourcingContactNeedsReview", 409);
                return new Reply(previous.ResponseJson, 201);
            }
            await RequirePrincipalSourcingKeyUnused(db, key, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_invitations WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={invitationId} FOR UPDATE", token);
            var ev = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                "procurement-sourcing-submit", id, token);
            if (ev.Status != "Published") throw new Problem("SourcingNotInternallyPublished", 409);
            var invitation = await db.SourcingInvitations.SingleOrDefaultAsync(x => x.Id == invitationId
                && x.SourcingEventId == id, token) ?? throw new Problem("SourcingInvitationNotFound", 404);
            if (invitation.Status != "ReadyForManualDelivery" || invitation.SentAt is not null
                || invitation.DeliveryRef is not null || invitation.ViewedAt is not null)
                throw new Problem("SourcingInvitationContactChanged", 409);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var reportedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(input.ReportedContactAt)!.Value;
            var approved = await db.SourcingPublicationDecisions.AsNoTracking()
                .Where(x => x.SourcingEventId == id && x.CaseState == "Approved")
                .OrderByDescending(x => x.DecidedAt).FirstOrDefaultAsync(token)
                ?? throw new Problem("SourcingPublicationApprovalMissing", 409);
            if (reportedAt < approved.DecidedAt || reportedAt > now)
                throw new Problem("SourcingManualContactTimeInvalid", 422);
            var supplier = await db.Suppliers.Include(x => x.Qualifications)
                .SingleOrDefaultAsync(x => x.Id == invitation.SupplierId, token);
            if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, now))
                throw new Problem("SourcingSupplierUnavailable", 409);
            invitation.Status = "ManualContactRecorded";
            var contact = new SourcingManualContact
            {
                SourcingEventId = id, InvitationId = invitationId, SupplierId = invitation.SupplierId,
                ActorPrincipalId = actor.PrincipalId, ActorCoreLinkedWorkerId = actor.LinkedWorkerId,
                Channel = input.Channel, Reference = reference, ReportedContactAt = reportedAt,
                RecordedAt = now, CommandKey = key, CommandSha256 = fingerprint,
            };
            var body = JsonSerializer.Serialize(new { eventId = id, invitationId,
                status = invitation.Status, manualContactId = contact.Id,
                supplierDeliveryReported = true, emailSentBySystem = false, commandKey = key }, Json);
            contact.ResponseJson = body;
            contact.ResponseSha256 = ProcurementEvidenceEncoding.Digest(body);
            db.AddScoped(contact);
            db.AddScoped(new AuditEvent { RecordType = "SourcingInvitation", RecordId = invitationId,
                Action = "ManualSupplierContactRecorded", ActorSubjectId = actor.PrincipalId,
                ChangedFieldsJson = JsonSerializer.Serialize(new { contactId = contact.Id,
                    input.Channel, reportedAt, emailSentBySystem = false }, Json),
                CorrelationId = scope.CorrelationId });
            await db.SaveChangesAsync(token);
            return new Reply(body, 201);
        }, ct);
    }
}
