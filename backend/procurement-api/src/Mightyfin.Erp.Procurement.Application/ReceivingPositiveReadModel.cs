using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record ReceivingCapacityView(bool Verified, decimal? OrderRemainingQuantity,
    decimal? PhysicalRemainingQuantity, decimal? AcceptableQuantity, string? Blocker);
public sealed record ReceivingActionCandidate(string Action, bool Candidate, string? Blocker);

/// <summary>Read hints computed with the same pure command guards. No facts, keys or state
/// are persisted. Hints never reserve capacity or authorize a later command/replay.</summary>
public static class ReceivingPositiveReadModel
{
    public static ReceivingCapacityView Capacity(ReceivingLedger ledger, ReceivingPolicyProjection policy, Guid? physicalId)
    {
        ControlledReceiving.ValidateLedger(ledger);
        if (!ledger.AcceptanceHistoryVerified)
            return new(false, null, null, null, "ReceivingLegacyAcceptanceReconciliationRequired");
        var remaining = ExactDecimalArithmetic.Subtract(ledger.Source.AuthorizedQuantity,
            ExactDecimalArithmetic.Sum(ledger.Effects.Select(x => x.QuantityDelta)));
        if (remaining < 0) throw new InvalidOperationException("ReceivingAcceptanceCapacityCorrupt");
        if (physicalId is null) return ledger.Source.Kind == ReceivingKind.Service
            ? new(true, remaining, null, remaining, null) : new(true, remaining, null, null, "PhysicalObservationRequired");
        var observation = ledger.Observations.Single(x => x.Id == physicalId);
        var inspection = ledger.Inspections.SingleOrDefault(x => x.PhysicalObservationId == physicalId);
        if (policy.InspectionRequired && inspection is null)
            return new(true, remaining, null, null, "QualifiedInspectionRequired");
        var qualified = policy.InspectionRequired ? inspection!.QualifiedQuantity
            : ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity, observation.InitiallyRejectedQuantity);
        var physical = ExactDecimalArithmetic.Subtract(qualified, ExactDecimalArithmetic.Sum(ledger.Effects
            .Where(x => x.PhysicalObservationId == physicalId && x.Kind == AcceptanceEffectKind.Acceptance).Select(x => x.QuantityDelta)));
        if (physical < 0) throw new InvalidOperationException("ReceivingPhysicalCapacityCorrupt");
        return new(true, remaining, physical, Math.Min(remaining, physical), null);
    }
    public static ReceivingActionCandidate Record(ReceivingSourceProjection source, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor, ReceivingLedger ledger, GoodsReceipt header, GoodsReceiptLine line, DateTimeOffset now)
        => Hint("Record", () =>
        {
            if (header.RecordedAt != null || header.Status != "Draft" || header.SiteRef is null || header.ReceivedAt < source.Issued.AuthorizedAt)
                throw new InvalidOperationException("ReceivingPhysicalAlreadyRecordedOrDateInvalid");
            return ControlledReceiving.RecordGoods(source, policy, actor, ledger, header.Id, line.Id, header.SiteRef.Value,
                line.ReceivedQuantity, line.InitiallyRejectedQuantity, "Read hint only", Guid.NewGuid(), now);
        });
    public static ReceivingActionCandidate Inspect(ReceivingSourceProjection source, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor, ReceivingLedger ledger, Guid physicalId, DateTimeOffset now)
        => Hint("Inspect", () => ControlledReceiving.InspectGoods(source, policy, actor, ledger, physicalId, 0, "Read hint only", Guid.NewGuid(), now));
    public static ReceivingActionCandidate Accept(ReceivingSourceProjection source, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor, ReceivingLedger ledger, Guid physicalId, DateTimeOffset now)
        => Hint("Accept", () =>
        {
            var capacity = Capacity(ledger, policy, physicalId);
            if (capacity.AcceptableQuantity is not > 0) throw new InvalidOperationException(capacity.Blocker ?? "ReceivingNoRemainingAcceptance");
            return ControlledReceiving.AcceptGoods(source, policy, actor, ledger, physicalId, capacity.AcceptableQuantity.Value,
                "Read hint only", Guid.NewGuid(), now);
        });
    public static ReceivingActionCandidate Certify(ReceivingSourceProjection source, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor, ReceivingLedger ledger, ServiceAcceptance service, DateTimeOffset now)
        => Hint("Certify", () =>
        {
            if (service.CertifiedAt != null || service.Status != "PendingCertification") throw new InvalidOperationException("ReceivingServiceAlreadyCertified");
            return ControlledReceiving.CertifyService(source, policy, actor, ledger, service.Id, service.AcceptedQuantity,
                ExactDecimalArithmetic.Multiply(service.AcceptedQuantity, source.Pin.NetUnitPrice), service.DeliverableRef!,
                service.PeriodFrom, service.PeriodTo, "Read hint only", Guid.NewGuid(), now);
        });
    private static ReceivingActionCandidate Hint(string action, Func<object> evaluate)
    {
        try { _ = evaluate(); return new(action, true, null); }
        catch (InvalidOperationException ex) { return new(action, false, ex.Message); }
    }
}
