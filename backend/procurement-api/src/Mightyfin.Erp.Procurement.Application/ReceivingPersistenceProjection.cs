using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Deterministic codec over existing receipt/certificate identities. Callers must load
/// complete, locked source fact sets; it never infers completeness from an empty query or a counter.</summary>
public static class ReceivingPersistenceProjection
{
    public static string PinJson(ReceivingSourcePin source) => JsonSerializer.Serialize(source);
    public static string PinDigest(ReceivingSourcePin source) => ProcurementEvidenceEncoding.PolicyJsonDigest(PinJson(source));
    public static void VerifyState(ReceivingSourceState state)
    {
        Need(state.ExecutionVersion == 1 && state.Source is { } && state.Id != Guid.Empty
            && state.Source.TenantId == state.TenantId && state.Source.LegalEntityId == state.LegalEntityId
            && state.Source.PurchaseOrderId == state.PurchaseOrderId && state.Source.PurchaseOrderLineId == state.PurchaseOrderLineId
            && state.Source.IssuedVersionId == state.IssuedVersionId && state.SubmissionId != Guid.Empty
            && state.DeliveryEvidenceId != Guid.Empty && state.ClassificationPolicyVersionId != Guid.Empty
            && state.SourceSha256 == PinDigest(state.Source)
            && state.Source.ClassificationSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(state.ClassificationJson), "ReceivingSourceStateEvidenceInvalid");
    }
    public static void VerifyHeader(GoodsReceipt header)
        => Need(header.ExecutionVersion == 1 && header.IssuedVersionId is { } issued && issued != Guid.Empty
            && header.IssuedBindingId == issued && header.SubmissionId is { } sub && sub != Guid.Empty,
            "ReceivingLegacyOrUnsupportedEvidence");
    public static GoodsPhysicalObservation Observation(ReceivingSourceState state, GoodsReceipt header, GoodsReceiptLine line)
    {
        VerifyState(state); VerifyHeader(header);
        Need(header.TenantId == state.TenantId && header.LegalEntityId == state.LegalEntityId
            && header.PurchaseOrderId == state.PurchaseOrderId && header.IssuedBindingId == state.IssuedVersionId
            && header.SubmissionId == state.SubmissionId && header.ReceiverWorkerId is { } worker && worker != Guid.Empty
            && header.RecordedAt is not null && header.SiteRef is { } site && site != Guid.Empty
            && line.ExecutionVersion == 1 && line.TenantId == state.TenantId && line.LegalEntityId == state.LegalEntityId
            && line.ReceivingSourceStateId == state.Id && line.SourceStateBindingId == state.Id && line.GoodsReceiptId == header.Id
            && line.SourcePurchaseOrderId == state.PurchaseOrderId && line.IssuedBindingId == state.IssuedVersionId
            && line.PurchaseOrderLineId == state.PurchaseOrderLineId && line.PhysicalObservationId is { } id && id != Guid.Empty
            && line.PhysicalBindingId == line.PhysicalObservationId && line.PhysicalPolicyVersionId is { } policy && policy != Guid.Empty && line.PhysicalSequence > 0
            && line.PhysicalEffectKey is { } key && key != Guid.Empty && line.PhysicalRecordedAt is not null
            && !string.IsNullOrWhiteSpace(line.PhysicalEvidenceRef), "ReceivingPhysicalPersistenceInvalid");
        return new() { Id = line.PhysicalObservationId!.Value, TenantId = state.TenantId, LegalEntityId = state.LegalEntityId,
            CreatedAt = line.PhysicalRecordedAt!.Value, CreatedBy = header.ReceiverSubjectId, GoodsReceiptId = header.Id,
            GoodsReceiptLineId = line.Id, Source = state.Source, SiteId = header.SiteRef!.Value,
            ReceivedQuantity = line.ReceivedQuantity, InitiallyRejectedQuantity = line.InitiallyRejectedQuantity,
            ReceiverWorkerId = header.ReceiverWorkerId!.Value, ReceivingPolicyVersionId = line.PhysicalPolicyVersionId!.Value,
            EvidenceReference = line.PhysicalEvidenceRef!, LedgerSequence = line.PhysicalSequence!.Value,
            EffectKey = line.PhysicalEffectKey!.Value, RecordedAt = line.PhysicalRecordedAt.Value };
    }
    public static GoodsInspectionEvidence Inspection(ReceivingSourceState state, ReceiptInspection row)
    {
        VerifyState(state);
        Need(row.ExecutionVersion == 1 && row.ReceivingSourceStateId == state.Id && row.SourceStateBindingId == state.Id && row.PhysicalBindingId == row.PhysicalObservationId
            && row.TenantId == state.TenantId && row.LegalEntityId == state.LegalEntityId
            && row.PhysicalObservationId is { } observation && observation != Guid.Empty
            && row.InspectorWorkerId is { } worker && worker != Guid.Empty && row.PolicyVersionId is { } policy && policy != Guid.Empty
            && row.LedgerSequence > 0 && row.EffectKey is { } key && key != Guid.Empty && row.InspectedAt is not null
            && !string.IsNullOrWhiteSpace(row.ChecklistRef) && !string.IsNullOrWhiteSpace(row.EvidenceRef)
            && Enum.TryParse<ReceivingInspectionOutcome>(row.Status, out var outcome) && Enum.IsDefined(outcome), "ReceivingInspectionPersistenceInvalid");
        return new() { Id = row.Id, TenantId = row.TenantId, LegalEntityId = row.LegalEntityId, CreatedAt = row.InspectedAt!.Value,
            CreatedBy = row.InspectorSubjectId, PhysicalObservationId = row.PhysicalObservationId!.Value,
            GoodsReceiptLineId = row.GoodsReceiptLineId, Source = state.Source, InspectorWorkerId = row.InspectorWorkerId!.Value,
            ReceivingPolicyVersionId = row.PolicyVersionId!.Value, ChecklistReference = row.ChecklistRef!,
            Outcome = Enum.Parse<ReceivingInspectionOutcome>(row.Status), InspectedQuantity = row.InspectedQuantity,
            QualifiedQuantity = row.QualifiedQuantity, EvidenceReference = row.EvidenceRef!, LedgerSequence = row.LedgerSequence!.Value,
            EffectKey = row.EffectKey!.Value, InspectedAt = row.InspectedAt.Value };
    }
    public static ReceivingLedger Ledger(ReceivingSourceState state, IReadOnlyList<(GoodsReceipt Header, GoodsReceiptLine Line)> lines,
        IReadOnlyList<ReceiptInspection> inspections, IReadOnlyList<ServiceAcceptance> services,
        IReadOnlyList<ReceivingAcceptanceEffect> effects, bool complete, bool acceptanceHistoryVerified)
    {
        VerifyState(state); Need(complete, "CompleteReceivingLedgerRequired");
        Need(lines.All(x => x.Line.ReceivingSourceStateId == state.Id)
            && services.All(x => x.ReceivingSourceStateId == state.Id), "ReceivingPersistenceSourceMismatch");
        var ledger = new ReceivingLedger(state.Source, state.Sequence, true, acceptanceHistoryVerified,
            lines.Where(x => x.Line.PhysicalObservationId is not null).Select(x => Observation(state, x.Header, x.Line)).ToArray(),
            inspections.Select(x => Inspection(state, x)).ToArray(), effects);
        ControlledReceiving.ValidateLedger(ledger);
        Need(effects.All(x => x.ReceivingSourceStateId == state.Id && x.Kind == AcceptanceEffectKind.Acceptance
            && x.Source == state.Source && (x.Source.Kind == ReceivingKind.Goods
                ? x.GoodsReceiptLineId == x.AcceptanceRecordId && x.ServiceAcceptanceId is null
                    && lines.Any(row => row.Line.Id == x.AcceptanceRecordId && row.Line.PhysicalObservationId == x.PhysicalObservationId)
                : x.ServiceAcceptanceId == x.AcceptanceRecordId && x.GoodsReceiptLineId is null
                    && services.Any(row => row.Id == x.AcceptanceRecordId))), "ReceivingPersistenceEffectBindingInvalid");
        foreach (var line in lines)
        {
            var accepted = ExactDecimalArithmetic.Sum(effects.Where(x => x.AcceptanceRecordId == line.Line.Id).Select(x => x.QuantityDelta));
            Need(line.Line.AcceptedQuantity == accepted, "ReceivingCounterReconciliationRequired");
        }
        foreach (var row in services)
        {
            var acceptedQuantity = ExactDecimalArithmetic.Sum(effects.Where(x => x.AcceptanceRecordId == row.Id).Select(x => x.QuantityDelta));
            var acceptedNet = ExactDecimalArithmetic.Sum(effects.Where(x => x.AcceptanceRecordId == row.Id).Select(x => x.NetAmountDelta));
            Need(row.ExecutionVersion == 1 && row.SourceStateBindingId == state.Id && row.SourcePurchaseOrderId == state.PurchaseOrderId && row.IssuedBindingId == state.IssuedVersionId
                && row.PurchaseOrderLineId == state.PurchaseOrderLineId
                && row.CurrentAcceptedQuantity == acceptedQuantity && row.CurrentAcceptedNetAmount == acceptedNet,
                "ReceivingServiceCounterReconciliationRequired");
            if (row.CertifiedAt is not null)
            {
                var effect = effects.SingleOrDefault(x => x.Id == row.CertificationEffectId && x.AcceptanceRecordId == row.Id);
                Need(effect is not null && effect.Kind == AcceptanceEffectKind.Acceptance && effect.ActorWorkerId == row.OwnerWorkerId
                    && effect.QuantityDelta == row.AcceptedQuantity && effect.NetAmountDelta == row.AcceptedAmount
                    && effect.DeliverableReference == row.DeliverableRef && effect.PeriodFrom == row.PeriodFrom && effect.PeriodTo == row.PeriodTo
                    && effect.ReceivingPolicyVersionId == row.PolicyVersionId && effect.EvidenceReference == row.EvidenceRef
                    && effect.OccurredAt == row.CertifiedAt && effect.CreatedBy == row.OwnerSubjectId, "ReceivingServiceCertificationEvidenceInvalid");
            }
            else Need(acceptedQuantity == 0 && row.CertificationEffectId is null, "ReceivingUncertifiedServiceEffectInvalid");
        }
        Need(state.CurrentAcceptedQuantity == ExactDecimalArithmetic.Sum(effects.Select(x => x.QuantityDelta))
            && state.CurrentAcceptedNetAmount == ExactDecimalArithmetic.Sum(effects.Select(x => x.NetAmountDelta)), "ReceivingSourceCounterReconciliationRequired");
        return ledger;
    }
    public static ReceivingAcceptanceEffect PersistedEffect(ReceivingAcceptanceEffect effect, Guid stateId)
        => new() { Id = effect.Id, TenantId = effect.TenantId, LegalEntityId = effect.LegalEntityId,
            CreatedAt = effect.CreatedAt, CreatedBy = effect.CreatedBy, Source = effect.Source, ReceivingSourceStateId = stateId,
            GoodsReceiptLineId = effect.Source.Kind == ReceivingKind.Goods ? effect.AcceptanceRecordId : null,
            ServiceAcceptanceId = effect.Source.Kind == ReceivingKind.Service ? effect.AcceptanceRecordId : null,
            AcceptanceRecordId = effect.AcceptanceRecordId, PhysicalObservationId = effect.PhysicalObservationId,
            InspectionEvidenceId = effect.InspectionEvidenceId, OriginalAcceptanceEffectId = effect.OriginalAcceptanceEffectId,
            CorrectionEvidence = effect.CorrectionEvidence, Kind = effect.Kind, QuantityDelta = effect.QuantityDelta,
            NetAmountDelta = effect.NetAmountDelta, ActorWorkerId = effect.ActorWorkerId, ReceivingPolicyVersionId = effect.ReceivingPolicyVersionId,
            EvidenceReference = effect.EvidenceReference, Reason = effect.Reason, DeliverableReference = effect.DeliverableReference,
            PeriodFrom = effect.PeriodFrom, PeriodTo = effect.PeriodTo, LedgerSequence = effect.LedgerSequence,
            EffectKey = effect.EffectKey, OccurredAt = effect.OccurredAt };
    private static void Need(bool valid, string code) { if (!valid) throw new InvalidOperationException(code); }
}
