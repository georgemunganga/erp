using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementReceivingRoutes
{
    private static Task<IResult> RecordGoods(Guid id, RecordReceipt input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, true, async (db, cases, core, auth, token) =>
        {
            var (header, lines) = await LoadReceipt(db, id, token); ReceivingPersistenceProjection.VerifyHeader(header);
            var resolved = new List<(GoodsReceiptLine Line, FinalSource Source, ReceivingLedger Ledger, GoodsPhysicalObservation Fact)>();
            var key = Key(http); var fingerprint = Fingerprint(new { id, input }); var now = Now(); string? replayResponse = null;
            foreach (var line in lines.OrderBy(x => x.PurchaseOrderLineId))
            {
                var source = await Source(db, cases, scope, header.PurchaseOrderId, header.IssuedVersionId!.Value, line.PurchaseOrderLineId, token);
                var final = await Resolve(db, source, line.PhysicalPolicyVersionId, token);
                var ledger = await Ledger(db, final, token);
                var actor = await Actor(core, scope, http, auth, ControlledReceiving.RecordPermission, header.BranchRef, header.SiteRef, token);
                ReceivingPolicyRules.VerifyReference(final.Rule, input.EvidenceReference);
                ControlledReceiving.VerifyCurrentActor(final.Source, final.Rules, actor, ledger, ControlledReceiving.RecordPermission, now, header.SiteRef);
                var prior = await Replay(db, scope, "ReceivingRecord", key, fingerprint, header.PurchaseOrderId, id, token);
                if (prior is not null) { replayResponse = prior.ResponseJson; continue; }
                if (header.RecordedAt != null || header.Status != "Draft" || header.SiteRef is null || header.ReceivedAt < source.Issued.AuthorizedAt)
                    throw new ReceivingProblem("ReceivingPhysicalAlreadyRecordedOrDateInvalid", 409);
                // Each line owns a fact UUID; the whole command retains one durable command key.
                var fact = ControlledReceiving.RecordGoods(final.Source, final.Rules, actor, ledger, id, line.Id, header.SiteRef.Value,
                    line.ReceivedQuantity, line.InitiallyRejectedQuantity, input.EvidenceReference, Guid.NewGuid(), now);
                ControlledReceiving.ValidateLedger(ledger with { Sequence = fact.LedgerSequence, Observations = ledger.Observations.Append(fact).ToArray() });
                resolved.Add((line, final, ledger, fact));
            }
            if (replayResponse is not null) return replayResponse;
            if (resolved.Count == 0) throw new ReceivingProblem("ReceivingLinesRequired", 422);
            await LockReceipt(db, scope, id, token); Version(header.Version, input.Version);
            foreach (var item in resolved)
            {
                var row = item.Line; var fact = item.Fact;
                row.ReceivingSourceStateId = item.Source.State.Id; row.SourceStateBindingId = item.Source.State.Id;
                row.PhysicalObservationId = fact.Id; row.PhysicalBindingId = fact.Id; row.PhysicalPolicyVersionId = fact.ReceivingPolicyVersionId;
                row.PhysicalEvidenceRef = fact.EvidenceReference; row.PhysicalSequence = fact.LedgerSequence;
                row.PhysicalEffectKey = fact.EffectKey; row.PhysicalRecordedAt = fact.RecordedAt;
                row.Status = fact.ReceivedQuantity == fact.InitiallyRejectedQuantity ? "Rejected" : "Held";
                item.Source.State.Sequence = fact.LedgerSequence;
                Event(db, id, "procurement.goods.recorded.v1", fact.EffectKey, new { receiptLineId = row.Id, sourceStateId = item.Source.State.Id,
                    fact.Id, fact.Source.IssuedVersionId, fact.Source.IssuedSnapshotSha256, quantity = Exact(fact.ReceivedQuantity),
                    initiallyRejectedQuantity = Exact(fact.InitiallyRejectedQuantity), fact.LedgerSequence });
            }
            header.RecordedAt = now; header.ReceiverSubjectId = scope.SubjectId; header.ReceiverWorkerId = resolved[0].Fact.ReceiverWorkerId;
            header.Status = "Recorded";
            return await Save(db, scope, header.PurchaseOrderId, id, "ReceivingRecord", key, fingerprint, () => View(header, lines, resolved.Select(x => x.Source.State).ToDictionary(x => x.Id)), token);
        }, ct);
    private static Task<IResult> InspectGoods(Guid id, Guid lineId, ReceivingAction input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => GoodsAction(id, lineId, input, true, http, scope, ct);
    private static Task<IResult> AcceptGoods(Guid id, Guid lineId, ReceivingAction input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => GoodsAction(id, lineId, input, false, http, scope, ct);
    private static Task<IResult> GoodsAction(Guid id, Guid lineId, ReceivingAction input, bool inspect, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, true, async (db, cases, core, auth, token) =>
        {
            var (header, lines) = await LoadReceipt(db, id, token); ReceivingPersistenceProjection.VerifyHeader(header);
            var line = lines.SingleOrDefault(x => x.Id == lineId) ?? throw new ReceivingProblem("ReceivingLineNotFound", 404);
            if (line.PhysicalObservationId is null || line.ReceivingSourceStateId is null) throw new ReceivingProblem("ReceivingPhysicalObservationRequired", 409);
            var source = await Source(db, cases, scope, header.PurchaseOrderId, header.IssuedVersionId!.Value, line.PurchaseOrderLineId, token);
            var final = await Resolve(db, source, line.PhysicalPolicyVersionId, token); var ledger = await Ledger(db, final, token);
            var permission = inspect ? ControlledReceiving.InspectPermission : ControlledReceiving.AcceptPermission;
            var actor = await Actor(core, scope, http, auth, permission, header.BranchRef, header.SiteRef, token);
            var now = Now(); ReceivingPolicyRules.VerifyReference(final.Rule, input.EvidenceReference);
            ControlledReceiving.VerifyCurrentActor(final.Source, final.Rules, actor, ledger, permission, now, header.SiteRef, line.PhysicalObservationId);
            var operation = inspect ? "ReceivingInspect" : "ReceivingAccept"; var key = Key(http); var fingerprint = Fingerprint(new { id, lineId, input });
            var prior = await Replay(db, scope, operation, key, fingerprint, header.PurchaseOrderId, id, token);
            if (prior is not null) return prior.ResponseJson;
            await LockReceipt(db, scope, id, token); Version(line.Version, input.Version); Version(final.State.Version, input.SourceVersion);
            var quantity = Quantity(input.Quantity, !inspect);
            if (inspect)
            {
                var fact = ControlledReceiving.InspectGoods(final.Source, final.Rules, actor, ledger, line.PhysicalObservationId.Value,
                    quantity, input.EvidenceReference, Guid.NewGuid(), now);
                ControlledReceiving.ValidateLedger(ledger with { Sequence = fact.LedgerSequence, Inspections = ledger.Inspections.Append(fact).ToArray() });
                db.AddScoped(new ReceiptInspection { Id = fact.Id, ExecutionVersion = 1, ReceivingSourceStateId = final.State.Id,
                    SourceStateBindingId = final.State.Id, GoodsReceiptLineId = line.Id, PhysicalObservationId = fact.PhysicalObservationId, PhysicalBindingId = fact.PhysicalObservationId,
                    Status = fact.Outcome.ToString(), InspectedQuantity = fact.InspectedQuantity, QualifiedQuantity = fact.QualifiedQuantity,
                    DefectQuantity = ExactDecimalArithmetic.Subtract(fact.InspectedQuantity, fact.QualifiedQuantity), InspectorSubjectId = actor.PrincipalId,
                    InspectorWorkerId = actor.WorkerId, ChecklistRef = fact.ChecklistReference, PolicyVersionId = fact.ReceivingPolicyVersionId,
                    EvidenceRef = fact.EvidenceReference, LedgerSequence = fact.LedgerSequence, EffectKey = fact.EffectKey, InspectedAt = fact.InspectedAt });
                final.State.Sequence = fact.LedgerSequence; line.Status = fact.QualifiedQuantity == 0 ? "Rejected" : "Inspected";
                Event(db, id, "procurement.goods.inspected.v1", fact.EffectKey, new { fact.Id, receiptLineId = line.Id,
                    sourceStateId = final.State.Id, qualifiedQuantity = Exact(fact.QualifiedQuantity), fact.LedgerSequence });
            }
            else
            {
                var fact = ControlledReceiving.AcceptGoods(final.Source, final.Rules, actor, ledger, line.PhysicalObservationId.Value,
                    quantity, input.EvidenceReference, Guid.NewGuid(), now);
                await ApplyAcceptance(db, final, ledger, fact, line, null, token);
                line.Status = line.AcceptedQuantity == ExactDecimalArithmetic.Subtract(line.ReceivedQuantity, line.InitiallyRejectedQuantity) ? "Accepted" : "Partially Accepted";
                header.Status = lines.All(x => x.Status is "Accepted" or "Rejected") ? "Accepted" : "Partially Accepted";
            }
            return await Save(db, scope, header.PurchaseOrderId, id, operation, key, fingerprint, () => View(header, lines, new Dictionary<Guid, ReceivingSourceState> { [final.State.Id] = final.State }), token);
        }, ct);
    private static Task<IResult> CertifyService(Guid id, ReceivingAction input, HttpContext http, ProcurementRequestScope scope, CancellationToken ct)
        => Run(http, scope, true, async (db, cases, core, auth, token) =>
        {
            var row = await db.ServiceAcceptances.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new ReceivingProblem("ReceivingServiceNotFound", 404);
            if (row.ExecutionVersion != 1 || row.SourcePurchaseOrderId is null || row.IssuedBindingId == Guid.Empty)
                throw new ReceivingProblem("ReceivingLegacyOrUnsupportedEvidence", 409);
            var source = await Source(db, cases, scope, row.SourcePurchaseOrderId.Value, row.IssuedBindingId, row.PurchaseOrderLineId, token);
            var final = await Resolve(db, source, row.PolicyVersionId, token); var ledger = await Ledger(db, final, token);
            var actor = await Actor(core, scope, http, auth, ControlledReceiving.CertifyPermission, source.Order.BranchRef, source.Order.BranchRef, token);
            var now = Now(); ReceivingPolicyRules.VerifyReference(final.Rule, input.EvidenceReference);
            ControlledReceiving.VerifyCurrentActor(final.Source, final.Rules, actor, ledger, ControlledReceiving.CertifyPermission, now);
            var key = Key(http); var fingerprint = Fingerprint(new { id, input });
            var prior = await Replay(db, scope, "ServiceCertify", key, fingerprint, source.Order.Id, id, token);
            if (prior is not null) return prior.ResponseJson;
            Version(row.Version, input.Version); Version(db.Entry(final.State).State == EntityState.Added ? 0 : final.State.Version, input.SourceVersion);
            if (row.CertifiedAt != null || row.Status != "PendingCertification") throw new ReceivingProblem("ReceivingServiceAlreadyCertified", 409);
            var quantity = Quantity(input.Quantity, true);
            if (quantity != row.AcceptedQuantity) throw new ReceivingProblem("ReceivingServiceDraftQuantityChanged", 409);
            var amount = ExactDecimalArithmetic.Multiply(quantity, final.Source.Pin.NetUnitPrice);
            var fact = ControlledReceiving.CertifyService(final.Source, final.Rules, actor, ledger, id, quantity, amount,
                row.DeliverableRef!, row.PeriodFrom, row.PeriodTo, input.EvidenceReference, Guid.NewGuid(), now);
            await ApplyAcceptance(db, final, ledger, fact, null, row, token);
            row.OwnerSubjectId = actor.PrincipalId; row.OwnerWorkerId = actor.WorkerId; row.PolicyVersionId = final.Policy.Id;
            row.EvidenceRef = input.EvidenceReference; row.CertificationEffectId = fact.Id; row.CertifiedAt = fact.OccurredAt;
            row.AcceptedAmount = fact.NetAmountDelta; row.AcceptedQuantity = fact.QuantityDelta; row.Status = "Accepted";
            row.ReceivingSourceStateId = final.State.Id; row.SourceStateBindingId = final.State.Id;
            return await Save(db, scope, source.Order.Id, id, "ServiceCertify", key, fingerprint, () => View(row, final.State), token);
        }, ct);
    private static async Task ApplyAcceptance(ProcurementDbContext db, FinalSource source, ReceivingLedger ledger,
        ReceivingAcceptanceEffect effect, GoodsReceiptLine? line, ServiceAcceptance? service, CancellationToken ct)
    {
        if (!ledger.AcceptanceHistoryVerified) throw new ReceivingProblem("ReceivingLegacyAcceptanceReconciliationRequired", 409);
        // Compute every exact next value before changing tracked aggregates.
        var sourceQuantity = ExactDecimalArithmetic.Add(source.State.CurrentAcceptedQuantity, effect.QuantityDelta);
        var sourceAmount = ExactDecimalArithmetic.Add(source.State.CurrentAcceptedNetAmount, effect.NetAmountDelta);
        var poQuantity = ExactDecimalArithmetic.Add(source.Load.Line.AcceptedQuantity, effect.QuantityDelta);
        var requestLine = await db.PurchaseRequestLines.SingleAsync(x => x.Id == source.Load.Line.PurchaseRequestLineId, ct);
        var prQuantity = ExactDecimalArithmetic.Add(requestLine.AcceptedQuantity, effect.QuantityDelta);
        var recordQuantity = ExactDecimalArithmetic.Add(line?.AcceptedQuantity ?? service!.CurrentAcceptedQuantity, effect.QuantityDelta);
        var recordAmount = service is null ? 0 : ExactDecimalArithmetic.Add(service.CurrentAcceptedNetAmount, effect.NetAmountDelta);
        var persisted = ReceivingPersistenceProjection.PersistedEffect(effect, source.State.Id);
        ControlledReceiving.ValidateLedger(ledger with { Sequence = effect.LedgerSequence, Effects = ledger.Effects.Append(persisted).ToArray() });
        db.AddScoped(persisted); source.State.Sequence = effect.LedgerSequence;
        source.State.CurrentAcceptedQuantity = sourceQuantity; source.State.CurrentAcceptedNetAmount = sourceAmount;
        source.Load.Line.AcceptedQuantity = poQuantity; requestLine.AcceptedQuantity = prQuantity;
        if (line is not null) line.AcceptedQuantity = recordQuantity;
        else { service!.CurrentAcceptedQuantity = recordQuantity; service.CurrentAcceptedNetAmount = recordAmount; }
        Event(db, effect.AcceptanceRecordId, "procurement.fulfilment.accepted.v1", effect.EffectKey, new { effect.Id, sourceStateId = source.State.Id,
            effect.AcceptanceRecordId, effect.Source.IssuedVersionId, effect.Source.IssuedSnapshotSha256, effect.Source.PurchaseOrderLineId,
            quantityDelta = Exact(effect.QuantityDelta), netAmountDelta = Exact(effect.NetAmountDelta), effect.Source.CurrencyCode,
            effect.LedgerSequence, handoff = "NotConfigured" });
    }
}
