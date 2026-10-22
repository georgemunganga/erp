using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Hrm.Infrastructure.Data;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementSourcingExecutionRoutes
{
    private static async Task<(RfqCurrentActor Actor, string Permission)> ReadActor(HrmDbContext core,
        ProcurementRequestScope scope, HttpContext http, IAuthorizationService auth, CancellationToken ct)
    {
        foreach (var action in new[] { "procurement-sourcing-convert", "procurement-sourcing-cancel" })
            if (await ProcurementAuthorityIdentity.ActorAsync(core, scope, http.User, auth, action, ct) is not null)
                return (await Actor(core, scope, http, auth, action, null, false, ct), action);
        throw new Problem("SourcingActionDenied", 403);
    }
    private static Task<IResult> Read(Guid id, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            var actor = await ReadActor(core, scope, http, auth, token);
            var ev = await OwnedEvent(db, core, scope, http, auth, actor.Actor, actor.Permission, id, token);
            return await ReadOwned(db, ev, token);
        }, ct);

    private static async Task<Reply> ReadOwned(ProcurementDbContext db, Mightyfin.Erp.Procurement.Domain.SourcingEvent ev, CancellationToken token)
    {
        var id = ev.Id;
        var evidence = await db.SourcingDraftEvidenceRecords.AsNoTracking().SingleAsync(x => x.Id == ev.SourceEvidenceId, token);
        var bindings = await db.SourcingDemandBindings.AsNoTracking().Where(x => x.SourcingEventId == id)
            .OrderBy(x => x.PurchaseRequestLineId).Take(201).ToArrayAsync(token);
        var lines = await db.SourcingLines.AsNoTracking().Where(x => x.SourcingEventId == id).Take(201).ToArrayAsync(token);
        if (bindings.Length is < 1 or > 200 || lines.Length != bindings.Length) throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
        GovernedRfqRestoration.VerifyEnvelopeAndPins(SourcingDraftPersistenceProjection.Evidence(evidence), ev,
            bindings.Select(SourcingDraftPersistenceProjection.Pin).ToArray());
        var allocationIds = bindings.Select(x => x.AllocationId).ToArray();
        var allocations = await db.PurchaseRequestAllocations.AsNoTracking()
            .Where(x => allocationIds.Contains(x.Id)).ToArrayAsync(token);
        if (allocations.Length != bindings.Length) throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
        foreach (var binding in bindings)
        {
            var line = lines.SingleOrDefault(x => x.Id == binding.SourcingLineId);
            var allocation = allocations.SingleOrDefault(x => x.Id == binding.AllocationId);
            if (line is null || line.PurchaseRequestLineId != binding.PurchaseRequestLineId || line.Quantity != binding.Quantity
                || line.UnitCode != binding.UnitCode || line.CategoryCode != binding.CategoryCode || allocation is null
                || allocation.TargetType != "SourcingEvent" || allocation.TargetId != ev.Id || allocation.Quantity != binding.Quantity
                || allocation.EstimatedValue != binding.EstimatedValue || allocation.CurrencyCode != ev.CurrencyCode
                || (ev.Status == "Draft" ? line.Status != "Active" || allocation.Status != "Reserved"
                    : ev.Status != "Cancelled" || line.Status != "Cancelled" || allocation.Status != "Released"))
                throw new Problem("SourcingOriginalEvidenceUnavailable", 409);
        }
        return new Reply(JsonSerializer.Serialize(new { ev.Id, ev.Number, ev.Title, ev.Method, ev.CurrencyCode,
            ev.Status, ev.Version, originalDraftFingerprint = ev.OriginalDraftFingerprint, ev.CancellationReason,
            canPublish = false, supplierDeliveryState = "NotStarted", financeReservationChanged = false,
            lines = bindings.Select(binding => new { id = binding.SourcingLineId, binding.PurchaseRequestId, binding.PurchaseRequestLineId,
                description = lines.Single(x => x.Id == binding.SourcingLineId).Description,
                binding.UnitCode, binding.CategoryCode, quantity = ProcurementEvidenceEncoding.Decimal(binding.Quantity),
                approvedUnitEstimate = ProcurementEvidenceEncoding.Decimal(binding.UnitEstimate),
                estimatedValue = ProcurementEvidenceEncoding.Decimal(binding.EstimatedValue),
                allocationState = allocations.Single(x => x.Id == binding.AllocationId).Status }).ToArray() }, Json));
    }

    private static Task<IResult> Recover(string operation, Guid key, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, async (db, cases, core, auth, token) =>
        {
            if (operation is not ("Create" or "Cancel") || key == Guid.Empty) throw new Problem("SourcingReceiptNotFound", 404);
            var permission = operation == "Create" ? "procurement-sourcing-convert" : "procurement-sourcing-cancel";
            var actor = await Actor(core, scope, http, auth, permission, null, false, token);
            var original = await Replay(db, core, scope, http, auth, actor, permission, operation, key, null, null, token)
                ?? throw new Problem("SourcingReceiptNotFound", 404);
            // The JSON string preserves the original recorded response bytes. This is history,
            // not a new mutation or a claim that the event still has its original state/version.
            return new Reply(JsonSerializer.Serialize(new { operation, commandKey = key, originalStatusCode = original.Status,
                responseJson = original.Body, recordedHistoricalResult = true, canPublish = false }, Json));
        }, ct);
}
