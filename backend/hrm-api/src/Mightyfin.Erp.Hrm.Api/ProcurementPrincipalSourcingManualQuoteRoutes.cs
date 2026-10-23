using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Buyer-entered quotation Draft only. It cannot submit, evaluate, award,
/// notify a supplier, create a PO or change a purchase commitment.</summary>
public static partial class ProcurementSourcingExecutionRoutes
{
    public sealed record PrincipalManualQuoteLineInput(Guid SourcingLineId,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal Quantity,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal UnitPrice,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal TaxAmount,
        [property: JsonConverter(typeof(CommercialLineDecimalJsonConverter))] decimal DeliveryAmount,
        string? LeadTimeText, string? Notes);
    public sealed record PrincipalManualQuoteCreateInput(Guid InvitationId,
        PrincipalManualQuoteLineInput[] Lines);
    public sealed record PrincipalManualQuoteReviseInput(long ExpectedBidVersion,
        PrincipalManualQuoteLineInput[] Lines);
    public sealed record PrincipalManualQuoteCancelInput(long ExpectedBidVersion);

    private static void MapPrincipalManualQuoteRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/principal-manual-quotes", PrincipalManualQuoteCreate)
            .RequireAuthorization("procurement-sourcing-submit");
        group.MapGet("/{id:guid}/principal-manual-quotes", PrincipalManualQuoteList)
            .RequireAuthorization("procurement-sourcing-submit");
        group.MapGet("/{id:guid}/principal-manual-quotes/{bidId:guid}", PrincipalManualQuoteRead)
            .RequireAuthorization("procurement-sourcing-submit");
        group.MapPut("/{id:guid}/principal-manual-quotes/{bidId:guid}", PrincipalManualQuoteRevise)
            .RequireAuthorization("procurement-sourcing-submit");
        group.MapPost("/{id:guid}/principal-manual-quotes/{bidId:guid}/cancel", PrincipalManualQuoteCancel)
            .RequireAuthorization("procurement-sourcing-submit");
        group.MapGet("/principal-manual-quote-commands/{key:guid}", PrincipalManualQuoteRecover)
            .RequireAuthorization("procurement-access");
    }

    private static async Task<(RfqPrincipalActor Actor, SourcingEvent Event)> PrincipalQuoteEvent(
        ProcurementDbContext db, HrmDbContext core, ProcurementRequestScope scope, HttpContext http,
        Microsoft.AspNetCore.Authorization.IAuthorizationService auth, Guid id, CancellationToken ct)
    {
        var actor = await PrincipalActor(core, scope, http, auth,
            "procurement-sourcing-submit", null, false, ct);
        var ev = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
            "procurement-sourcing-submit", id, ct);
        if (ev.Status != "Published") throw new Problem("SourcingNotInternallyPublished", 409);
        return (actor, ev);
    }

    private static void QuoteLinesValid(PrincipalManualQuoteLineInput[]? lines)
    {
        if (lines is not { Length: > 0 and <= 200 }
            || lines.Any(x => x.SourcingLineId == Guid.Empty
                || x.LeadTimeText is { Length: > 200 } || x.Notes is { Length: > 2000 })
            || lines.Select(x => x.SourcingLineId).Distinct().Count() != lines.Length)
            throw new Problem("SourcingManualQuoteLinesInvalid", 422);
    }

    private static string QuoteResponse(SupplierBid bid, SourcingManualQuoteVersion version,
        IReadOnlyList<SupplierBidLine> lines, Guid commandKey)
    {
        static string Exact(decimal value) => value.ToString(CultureInfo.InvariantCulture);
        return JsonSerializer.Serialize(new
        {
            eventId = bid.SourcingEventId, bidId = bid.Id, supplierId = bid.SupplierId,
            invitationId = bid.InvitationId, status = bid.Status,
            bidVersion = version.BidVersion, draftRevision = version.Revision,
            sourceChannel = "Manual", supplierSubmitted = false,
            lines = lines.OrderBy(x => x.SourcingLineId).Select(x => new
            {
                x.SourcingLineId, quantityExact = Exact(x.Quantity),
                unitPriceExact = Exact(x.UnitPrice), taxAmountExact = Exact(x.TaxAmount),
                deliveryAmountExact = Exact(x.DeliveryAmount), x.LeadTimeText, x.Notes,
            }), commandKey,
        }, Json);
    }

    private static async Task<Reply?> QuoteReplay(ProcurementDbContext db, HrmDbContext core,
        ProcurementRequestScope scope, HttpContext http,
        Microsoft.AspNetCore.Authorization.IAuthorizationService auth,
        RfqPrincipalActor actor, Guid key, string operation, string fingerprint,
        Guid eventId, Guid? bidId, CancellationToken ct)
    {
        var previous = await db.SourcingManualQuoteVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CommandKey == key, ct);
        if (previous is null) return null;
        if (previous.ActorPrincipalId != actor.PrincipalId
            || previous.ActorCoreLinkedWorkerId != actor.LinkedWorkerId)
            throw new Problem("SourcingQuoteNotFound", 404);
        _ = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
            "procurement-sourcing-submit", previous.SourcingEventId, ct);
        if (previous.Operation != operation || previous.CommandSha256 != fingerprint
            || previous.SourcingEventId != eventId || bidId is { } wanted && previous.SupplierBidId != wanted)
            throw new Problem("SourcingCommandKeyConflict", 409);
        if (previous.ResponseSha256 != ProcurementEvidenceEncoding.Digest(previous.ResponseJson))
            throw new Problem("SourcingQuoteNeedsReview", 409);
        return new Reply(previous.ResponseJson, operation == "Create" ? 201 : 200);
    }

    private static Task<IResult> PrincipalManualQuoteCreate(Guid id,
        PrincipalManualQuoteCreateInput input, HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, _, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var key = Key(http);
            await LockPrincipalSourcingCommandKey(db, key, token);
            if (id == Guid.Empty || input.InvitationId == Guid.Empty)
                throw new Problem("SourcingManualQuoteInputInvalid", 422);
            QuoteLinesValid(input.Lines);
            var actor = await PrincipalActor(core, scope, http, auth,
                "procurement-sourcing-submit", null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 2, operation = "CreateManualQuote",
                eventId = id, input.InvitationId, input.Lines });
            if (await QuoteReplay(db, core, scope, http, auth, actor, key,
                "Create", fingerprint, id, null, token) is { } replay) return replay;
            await RequirePrincipalSourcingKeyUnused(db, key, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.sourcing_invitations WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={input.InvitationId} FOR UPDATE", token);
            var ev = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                "procurement-sourcing-submit", id, token);
            if (ev.Status != "Published") throw new Problem("SourcingNotInternallyPublished", 409);
            var invitation = await db.SourcingInvitations.SingleOrDefaultAsync(x => x.Id == input.InvitationId
                && x.SourcingEventId == id && x.Status == "ManualContactRecorded", token)
                ?? throw new Problem("SourcingManualContactRequired", 409);
            var contact = await db.SourcingManualContacts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.InvitationId == invitation.Id, token)
                ?? throw new Problem("SourcingManualContactRequired", 409);
            if (contact.SupplierId != invitation.SupplierId)
                throw new Problem("SourcingManualContactChanged", 409);
            var supplier = await db.Suppliers.Include(x => x.Qualifications)
                .SingleOrDefaultAsync(x => x.Id == invitation.SupplierId, token);
            if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, DateTimeOffset.UtcNow))
                throw new Problem("SourcingSupplierUnavailable", 409);
            if (await db.SupplierBids.AsNoTracking().AnyAsync(x => x.SourcingEventId == id
                    && x.SupplierId == invitation.SupplierId, token))
                throw new Problem("SourcingManualQuoteAlreadyExists", 409);
            var bid = new SupplierBid
            {
                SourcingEventId = id, SupplierId = invitation.SupplierId, InvitationId = invitation.Id,
                Status = "Draft", SourceChannel = "Manual", SubmissionVersion = 1, DraftRevision = 1,
                CurrencyCode = ev.CurrencyCode, SurrogateEnteredBy = actor.PrincipalId,
                ReceivedAt = null, SubmittedAt = null, EvidenceDocumentRefId = null,
            };
            db.AddScoped(bid);
            var lines = input.Lines.Select(x => new SupplierBidLine
            {
                SupplierBidId = bid.Id, SourcingLineId = x.SourcingLineId,
                Quantity = x.Quantity, UnitPrice = x.UnitPrice, TaxAmount = x.TaxAmount,
                DeliveryAmount = x.DeliveryAmount, LeadTimeText = Normalize(x.LeadTimeText),
                Notes = Normalize(x.Notes),
            }).ToArray();
            foreach (var line in lines) db.AddScoped(line);
            var sourceLines = await db.SourcingLines.Where(x => x.SourcingEventId == id).ToArrayAsync(token);
            PrincipalManualQuotationDraft.Validate(ev, invitation, bid, sourceLines, lines, actor.PrincipalId);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var version = new SourcingManualQuoteVersion
            {
                SourcingEventId = id, SupplierBidId = bid.Id, Revision = 1, BidVersion = 1,
                Operation = "Create", ActorPrincipalId = actor.PrincipalId,
                ActorCoreLinkedWorkerId = actor.LinkedWorkerId,
                SnapshotJson = PrincipalManualQuotationSnapshot.Build(bid, lines),
                CommandKey = key, CommandSha256 = fingerprint, RecordedAt = now,
            };
            version.SnapshotSha256 = ProcurementEvidenceEncoding.Digest(version.SnapshotJson);
            version.ResponseJson = QuoteResponse(bid, version, lines, key);
            version.ResponseSha256 = ProcurementEvidenceEncoding.Digest(version.ResponseJson);
            db.AddScoped(version);
            db.AddScoped(new AuditEvent { RecordType = "SupplierBid", RecordId = bid.Id,
                Action = "ManualQuotationDraftCreated", ActorSubjectId = actor.PrincipalId,
                ChangedFieldsJson = JsonSerializer.Serialize(new { revision = 1,
                    invitationId = invitation.Id, snapshotSha256 = version.SnapshotSha256,
                    supplierSubmitted = false }, Json), CorrelationId = scope.CorrelationId });
            await db.SaveChangesAsync(token);
            return new Reply(version.ResponseJson, 201);
        }, ct);
    }

    private static Task<IResult> PrincipalManualQuoteRead(Guid id, Guid bidId,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, _, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var (actor, _) = await PrincipalQuoteEvent(db, core, scope, http, auth, id, token);
            var bid = await db.SupplierBids.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bidId
                && x.SourcingEventId == id && x.SourceChannel == "Manual"
                && x.SurrogateEnteredBy == actor.PrincipalId, token)
                ?? throw new Problem("SourcingQuoteNotFound", 404);
            var version = await db.SourcingManualQuoteVersions.AsNoTracking()
                .Where(x => x.SupplierBidId == bid.Id).OrderByDescending(x => x.Revision)
                .FirstOrDefaultAsync(token) ?? throw new Problem("SourcingQuoteEvidenceMissing", 409);
            var lines = await db.SupplierBidLines.AsNoTracking()
                .Where(x => x.SupplierBidId == bid.Id).ToArrayAsync(token);
            if (version.Revision != bid.DraftRevision || version.BidVersion != bid.Version
                || version.SnapshotJson != PrincipalManualQuotationSnapshot.Build(bid, lines)
                || version.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(version.SnapshotJson)
                || version.ResponseSha256 != ProcurementEvidenceEncoding.Digest(version.ResponseJson))
                throw new Problem("SourcingQuoteEvidenceChanged", 409);
            return new Reply(version.ResponseJson);
        }, ct);
    }

    private static Task<IResult> PrincipalManualQuoteList(Guid id, int? cursor, int? limit,
        HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, _, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            if (id == Guid.Empty || cursor is < 0 or > 1000000 || limit is < 1 or > 50)
                throw new Problem("SourcingQuoteListInputInvalid", 422);
            var (actor, _) = await PrincipalQuoteEvent(db, core, scope, http, auth, id, token);
            var take = limit ?? 25;
            var skip = cursor ?? 0;
            var query = db.SupplierBids.AsNoTracking().Where(x => x.SourcingEventId == id
                && x.SourceChannel == "Manual" && x.SurrogateEnteredBy == actor.PrincipalId);
            var rows = await query.OrderBy(x => x.Id).Skip(skip).Take(take + 1).ToArrayAsync(token);
            var page = rows.Take(take).ToArray();
            var body = JsonSerializer.Serialize(new
            {
                items = page.Select(x => new { bidId = x.Id, x.SupplierId, x.InvitationId,
                    x.Status, x.CurrencyCode, bidVersion = x.Version, x.DraftRevision,
                    supplierSubmitted = false }),
                nextCursor = rows.Length > take ? skip + take : (int?)null,
                hasMore = rows.Length > take,
            }, Json);
            return new Reply(body);
        }, ct);
    }

    private static Task<IResult> PrincipalManualQuoteRevise(Guid id, Guid bidId,
        PrincipalManualQuoteReviseInput input, HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct) => PrincipalManualQuoteWrite(id, bidId, input.ExpectedBidVersion,
            "Revise", input.Lines, http, scope, ct);

    private static Task<IResult> PrincipalManualQuoteCancel(Guid id, Guid bidId,
        PrincipalManualQuoteCancelInput input, HttpContext http, ProcurementRequestScope scope,
        CancellationToken ct) => PrincipalManualQuoteWrite(id, bidId, input.ExpectedBidVersion,
            "Cancel", null, http, scope, ct);

    private static Task<IResult> PrincipalManualQuoteWrite(Guid id, Guid bidId, long expectedBidVersion,
        string operation, PrincipalManualQuoteLineInput[]? inputLines, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, _, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            var key = Key(http);
            await LockPrincipalSourcingCommandKey(db, key, token);
            if (id == Guid.Empty || bidId == Guid.Empty || expectedBidVersion < 1
                || operation is not ("Revise" or "Cancel"))
                throw new Problem("SourcingManualQuoteInputInvalid", 422);
            if (operation == "Revise") QuoteLinesValid(inputLines);
            var actor = await PrincipalActor(core, scope, http, auth,
                "procurement-sourcing-submit", null, false, token);
            var fingerprint = Fingerprint(new { schemaVersion = 2, operation,
                eventId = id, bidId, expectedBidVersion, lines = inputLines });
            if (await QuoteReplay(db, core, scope, http, auth, actor, key, operation,
                fingerprint, id, bidId, token) is { } replay) return replay;
            await RequirePrincipalSourcingKeyUnused(db, key, token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM procurement.supplier_bids WHERE tenant_id={scope.TenantId} AND legal_entity_id={scope.LegalEntityId} AND id={bidId} FOR UPDATE", token);
            var ev = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                "procurement-sourcing-submit", id, token);
            if (ev.Status != "Published") throw new Problem("SourcingNotInternallyPublished", 409);
            var bid = await db.SupplierBids.SingleOrDefaultAsync(x => x.Id == bidId
                && x.SourcingEventId == id && x.SourceChannel == "Manual"
                && x.SurrogateEnteredBy == actor.PrincipalId, token)
                ?? throw new Problem("SourcingQuoteNotFound", 404);
            if (bid.Status != "Draft" || bid.Version != expectedBidVersion)
                throw new Problem("SourcingQuoteChanged", 409);
            var invitation = await db.SourcingInvitations.SingleOrDefaultAsync(x => x.Id == bid.InvitationId
                && x.SourcingEventId == id && x.SupplierId == bid.SupplierId
                && x.Status == "ManualContactRecorded", token)
                ?? throw new Problem("SourcingManualContactRequired", 409);
            var previous = await db.SourcingManualQuoteVersions.AsNoTracking()
                .Where(x => x.SupplierBidId == bid.Id).OrderByDescending(x => x.Revision)
                .FirstOrDefaultAsync(token) ?? throw new Problem("SourcingQuoteEvidenceMissing", 409);
            var lines = await db.SupplierBidLines.Where(x => x.SupplierBidId == bid.Id)
                .OrderBy(x => x.SourcingLineId).ToArrayAsync(token);
            if (previous.Revision != bid.DraftRevision || previous.BidVersion != bid.Version
                || previous.SnapshotJson != PrincipalManualQuotationSnapshot.Build(bid, lines)
                || previous.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(previous.SnapshotJson))
                throw new Problem("SourcingQuoteEvidenceChanged", 409);
            if (operation == "Revise")
            {
                var supplier = await db.Suppliers.Include(x => x.Qualifications)
                    .SingleOrDefaultAsync(x => x.Id == bid.SupplierId, token);
                if (supplier is null || !SupplierEligibility.CanReceiveNewOrders(supplier, DateTimeOffset.UtcNow))
                    throw new Problem("SourcingSupplierUnavailable", 409);
                if (inputLines!.Length != lines.Length
                    || !inputLines.Select(x => x.SourcingLineId).ToHashSet()
                        .SetEquals(lines.Select(x => x.SourcingLineId)))
                    throw new Problem("SourcingQuoteLineSetChanged", 422);
                var values = inputLines.ToDictionary(x => x.SourcingLineId);
                foreach (var line in lines)
                {
                    var value = values[line.SourcingLineId];
                    line.Quantity = value.Quantity; line.UnitPrice = value.UnitPrice;
                    line.TaxAmount = value.TaxAmount; line.DeliveryAmount = value.DeliveryAmount;
                    line.LeadTimeText = Normalize(value.LeadTimeText);
                    line.Notes = Normalize(value.Notes);
                }
                var sources = await db.SourcingLines.Where(x => x.SourcingEventId == id).ToArrayAsync(token);
                PrincipalManualQuotationDraft.Validate(ev, invitation, bid, sources, lines, actor.PrincipalId);
            }
            else bid.Status = "Cancelled";
            bid.DraftRevision = checked(bid.DraftRevision + 1);
            var now = ProcurementEvidenceEncoding.DatabaseTimestamp(DateTimeOffset.UtcNow)!.Value;
            var version = new SourcingManualQuoteVersion
            {
                SourcingEventId = id, SupplierBidId = bid.Id, Revision = bid.DraftRevision,
                BidVersion = checked(bid.Version + 1), Operation = operation,
                ActorPrincipalId = actor.PrincipalId, ActorCoreLinkedWorkerId = actor.LinkedWorkerId,
                SnapshotJson = PrincipalManualQuotationSnapshot.Build(bid, lines),
                CommandKey = key, CommandSha256 = fingerprint, RecordedAt = now,
            };
            version.SnapshotSha256 = ProcurementEvidenceEncoding.Digest(version.SnapshotJson);
            version.ResponseJson = QuoteResponse(bid, version, lines, key);
            version.ResponseSha256 = ProcurementEvidenceEncoding.Digest(version.ResponseJson);
            db.AddScoped(version);
            db.AddScoped(new AuditEvent { RecordType = "SupplierBid", RecordId = bid.Id,
                Action = operation == "Revise" ? "ManualQuotationDraftRevised" : "ManualQuotationDraftCancelled",
                ActorSubjectId = actor.PrincipalId,
                ChangedFieldsJson = JsonSerializer.Serialize(new { revision = version.Revision,
                    snapshotSha256 = version.SnapshotSha256, supplierSubmitted = false }, Json),
                CorrelationId = scope.CorrelationId });
            await db.SaveChangesAsync(token);
            return new Reply(version.ResponseJson);
        }, ct);
    }

    private static Task<IResult> PrincipalManualQuoteRecover(Guid key, HttpContext http,
        ProcurementRequestScope scope, CancellationToken ct)
    {
        if (!http.RequestServices.GetRequiredService<ProcurementPrincipalSourcingPublicationOptions>().Enabled)
            return Task.FromResult(Error("PrincipalSourcingPublicationUnavailable", 503));
        return Run(http, scope, async (db, _, core, auth, token) =>
        {
            await RequirePrincipalPublication(http, db, token);
            if (key == Guid.Empty) throw new Problem("SourcingCommandKeyRequired", 400);
            var previous = await db.SourcingManualQuoteVersions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CommandKey == key, token)
                ?? throw new Problem("SourcingQuoteNotFound", 404);
            if (previous.ActorPrincipalId != scope.SubjectId
                || previous.ActorCoreLinkedWorkerId != scope.CoreLinkedWorkerId)
                throw new Problem("SourcingQuoteNotFound", 404);
            var actor = await PrincipalActor(core, scope, http, auth,
                "procurement-sourcing-submit", null, false, token);
            _ = await PrincipalOwnedEvent(db, core, scope, http, auth, actor,
                "procurement-sourcing-submit", previous.SourcingEventId, token);
            if (previous.ResponseSha256 != ProcurementEvidenceEncoding.Digest(previous.ResponseJson))
                throw new Problem("SourcingQuoteNeedsReview", 409);
            return new Reply(previous.ResponseJson, previous.Operation == "Create" ? 201 : 200);
        }, ct);
    }
}
