using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Pure receiving decisions. No database mutation, stock posting, Finance release or
/// approval is performed. Complete trusted inputs and atomic persistence are adapter obligations.</summary>
public static class ControlledReceiving
{
    public const string NetPriceBasis = "SealedNetUnitPriceBeforeTax.NoAdditionalDiscount.v1";
    public const string RecordPermission = "procurement-receipt-record";
    public const string InspectPermission = "procurement-receipt-inspect";
    public const string AcceptPermission = "procurement-receipt-accept";
    public const string CertifyPermission = "procurement-service-certify";
    public const string ReversePermission = "procurement-receipt-reverse";
    public const string ServiceReversePermission = "procurement-service-reverse";
    public const string ReturnPermission = "procurement-return-dispatch";

    /// <summary>Current authorization for exact replay; no new capacity is allocated. The
    /// saved command receipt never substitutes for current source, worker or policy controls.</summary>
    public static void VerifyCurrentActor(ReceivingSourceProjection source, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor, ReceivingLedger ledger, string permission, DateTimeOffset at,
        Guid? siteId = null, Guid? observationId = null)
    {
        Require(permission is RecordPermission or InspectPermission or AcceptPermission or CertifyPermission, "ReceivingOperationUnsupported");
        Guard(source, policy, actor, ledger, permission, at, siteId);
        var observation = observationId is { } id ? Observation(ledger, id) : null;
        if (permission == InspectPermission)
            Require(observation is not null && policy.InspectionRequired && policy.InspectorWorkerIds.Contains(actor.WorkerId)
                && (!policy.RequireIndependentInspector || observation.ReceiverWorkerId != actor.WorkerId)
                && observation.ReceivingPolicyVersionId == policy.VersionId, "InspectionAuthorityRequired");
        if (permission is AcceptPermission or CertifyPermission)
        {
            Require(ledger.AcceptanceHistoryVerified, "ReceivingLegacyAcceptanceReconciliationRequired");
            CertificationActor(source, policy, actor);
            if (permission == AcceptPermission) Require(observation is not null && observation.ReceivingPolicyVersionId == policy.VersionId
                && (policy.AllowReceiverCertification || observation.ReceiverWorkerId != actor.WorkerId), "ReceiverCannotCertify");
            else Require(source.Pin.Kind == ReceivingKind.Service && policy.ServiceOwnerWorkerIds.Contains(actor.WorkerId), "ServiceCertificationAuthorityRequired");
        }
    }

    public static GoodsPhysicalObservation RecordGoods(ReceivingSourceProjection source,
        ReceivingPolicyProjection policy, ReceivingActorProjection actor, ReceivingLedger ledger,
        Guid receiptId, Guid receiptLineId, Guid siteId, decimal received, decimal rejected,
        string evidence, Guid effectKey, DateTimeOffset at)
    {
        Guard(source, policy, actor, ledger, RecordPermission, at, siteId);
        Require(source.Pin.Kind == ReceivingKind.Goods && receiptId != Guid.Empty && receiptLineId != Guid.Empty
            && received > 0 && rejected >= 0 && rejected <= received && QuantityPrecision(received) && QuantityPrecision(rejected), "InvalidPhysicalDisposition");
        NewKey(ledger, effectKey, evidence);
        Require(!ledger.Observations.Any(x => x.GoodsReceiptLineId == receiptLineId), "PhysicalObservationAlreadyRecorded");
        return new GoodsPhysicalObservation
        {
            TenantId = source.Pin.TenantId, LegalEntityId = source.Pin.LegalEntityId,
            CreatedAt = Time(at), CreatedBy = actor.PrincipalId, GoodsReceiptId = receiptId,
            GoodsReceiptLineId = receiptLineId, Source = source.Pin, SiteId = siteId,
            ReceivedQuantity = received, InitiallyRejectedQuantity = rejected,
            ReceiverWorkerId = actor.WorkerId, ReceivingPolicyVersionId = policy.VersionId,
            EvidenceReference = evidence, LedgerSequence = checked(ledger.Sequence + 1), EffectKey = effectKey, RecordedAt = Time(at),
        };
    }

    public static GoodsInspectionEvidence InspectGoods(ReceivingSourceProjection source,
        ReceivingPolicyProjection policy, ReceivingActorProjection actor, ReceivingLedger ledger,
        Guid observationId, decimal qualified, string evidence, Guid effectKey, DateTimeOffset at)
    {
        var observation = Observation(ledger, observationId);
        Guard(source, policy, actor, ledger, InspectPermission, at, observation.SiteId);
        NewKey(ledger, effectKey, evidence);
        Require(policy.InspectionRequired && !string.IsNullOrWhiteSpace(policy.ChecklistReference)
            && policy.InspectorWorkerIds.Contains(actor.WorkerId), "InspectionAuthorityRequired");
        Require(!policy.RequireIndependentInspector || observation.ReceiverWorkerId != actor.WorkerId,
            "InspectorMustBeIndependent");
        Require(observation.ReceivingPolicyVersionId == policy.VersionId
            && !ledger.Inspections.Any(x => x.PhysicalObservationId == observationId), "InspectionAlreadyFinalOrPolicyChanged");
        var inspected = ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity, observation.InitiallyRejectedQuantity);
        Require(inspected > 0 && qualified >= 0 && qualified <= inspected && QuantityPrecision(qualified), "InvalidInspectionDisposition");
        return new GoodsInspectionEvidence
        {
            TenantId = source.Pin.TenantId, LegalEntityId = source.Pin.LegalEntityId,
            CreatedAt = Time(at), CreatedBy = actor.PrincipalId, PhysicalObservationId = observationId,
            GoodsReceiptLineId = observation.GoodsReceiptLineId, Source = source.Pin,
            InspectorWorkerId = actor.WorkerId, ReceivingPolicyVersionId = policy.VersionId,
            ChecklistReference = policy.ChecklistReference!, InspectedQuantity = inspected,
            QualifiedQuantity = qualified, Outcome = qualified == 0 ? ReceivingInspectionOutcome.Rejected
                : qualified == inspected ? ReceivingInspectionOutcome.Passed : ReceivingInspectionOutcome.PartiallyPassed,
            EvidenceReference = evidence, LedgerSequence = checked(ledger.Sequence + 1), EffectKey = effectKey, InspectedAt = Time(at),
        };
    }

    public static ReceivingAcceptanceEffect AcceptGoods(ReceivingSourceProjection source,
        ReceivingPolicyProjection policy, ReceivingActorProjection actor, ReceivingLedger ledger,
        Guid observationId, decimal quantity, string evidence, Guid effectKey, DateTimeOffset at)
    {
        var observation = Observation(ledger, observationId);
        Guard(source, policy, actor, ledger, AcceptPermission, at, observation.SiteId);
        CertificationActor(source, policy, actor);
        NewKey(ledger, effectKey, evidence);
        Require(source.Pin.Kind == ReceivingKind.Goods && observation.ReceivingPolicyVersionId == policy.VersionId,
            "GoodsSourceOrPolicyMismatch");
        Require(policy.AllowReceiverCertification || observation.ReceiverWorkerId != actor.WorkerId,
            "ReceiverCannotCertify");
        var inspection = ledger.Inspections.SingleOrDefault(x => x.PhysicalObservationId == observationId);
        var available = ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity, observation.InitiallyRejectedQuantity);
        if (policy.InspectionRequired)
        {
            Require(inspection is not null && inspection.ReceivingPolicyVersionId == policy.VersionId
                && inspection.ChecklistReference == policy.ChecklistReference, "QualifiedInspectionRequired");
            available = inspection!.QualifiedQuantity;
        }
        // Returned/reversed goods cannot be accepted a second time from the same physical observation.
        var alreadyCertified = ExactDecimalArithmetic.Sum(ledger.Effects.Where(x => x.PhysicalObservationId == observationId
            && x.Kind == AcceptanceEffectKind.Acceptance).Select(x => x.QuantityDelta));
        Require(quantity > 0 && QuantityPrecision(quantity) && quantity <= ExactDecimalArithmetic.Subtract(available, alreadyCertified), "PhysicalAcceptanceCapacityExceeded");
        Capacity(source.Pin, ledger, quantity);
        return Effect(source.Pin, ledger, policy, actor, observation.GoodsReceiptLineId, observationId,
            policy.InspectionRequired ? inspection!.Id : null, null, AcceptanceEffectKind.Acceptance,
            quantity, evidence, null, null, null, null, effectKey, at, checked(ledger.Sequence + 1));
    }

    public static ReceivingAcceptanceEffect CertifyService(ReceivingSourceProjection source,
        ReceivingPolicyProjection policy, ReceivingActorProjection actor, ReceivingLedger ledger,
        Guid serviceAcceptanceId, decimal quantity, decimal netAmount, string deliverable,
        DateOnly periodFrom, DateOnly periodTo, string evidence, Guid effectKey, DateTimeOffset at)
    {
        Guard(source, policy, actor, ledger, CertifyPermission, at);
        CertificationActor(source, policy, actor);
        NewKey(ledger, effectKey, evidence);
        Require(source.Pin.Kind == ReceivingKind.Service && serviceAcceptanceId != Guid.Empty
            && policy.ServiceOwnerWorkerIds.Contains(actor.WorkerId), "ServiceCertificationAuthorityRequired");
        Require(quantity > 0 && QuantityPrecision(quantity) && netAmount == ExactDecimalArithmetic.Multiply(quantity, source.Pin.NetUnitPrice)
            && periodFrom <= periodTo && periodTo <= DateOnly.FromDateTime(at.UtcDateTime)
            && !string.IsNullOrWhiteSpace(deliverable), "InvalidServiceCertification");
        Require(!ledger.Effects.Any(x => x.Kind == AcceptanceEffectKind.Acceptance
            && (x.AcceptanceRecordId == serviceAcceptanceId || x.DeliverableReference == deliverable)),
            "ServiceDeliverableAlreadyCertified");
        Capacity(source.Pin, ledger, quantity);
        return Effect(source.Pin, ledger, policy, actor, serviceAcceptanceId, null, null, null,
            AcceptanceEffectKind.Acceptance, quantity, evidence, null, deliverable,
            periodFrom, periodTo, effectKey, at, checked(ledger.Sequence + 1));
    }

    public static ReceivingAcceptanceEffect CorrectAcceptance(ReceivingSourceProjection source,
        ReceivingPolicyProjection policy, ReceivingActorProjection actor, ReceivingLedger ledger,
        ReceivingConsumptionProjection consumption, ReceivingCorrectionAuthorityProjection authority,
        Guid originalEffectId, decimal quantity,
        AcceptanceEffectKind kind, string reason, string evidence, Guid effectKey, DateTimeOffset at)
    {
        Require(kind is AcceptanceEffectKind.AdministrativeReversal or AcceptanceEffectKind.ReturnDispatched,
            "InvalidCorrectionKind");
        var original = ledger.Effects.SingleOrDefault(x => x.Id == originalEffectId
            && x.Kind == AcceptanceEffectKind.Acceptance);
        Require(original is not null, "OriginalAcceptanceRequired");
        var observation = original!.PhysicalObservationId is { } id ? Observation(ledger, id) : null;
        var permission = kind == AcceptanceEffectKind.ReturnDispatched ? ReturnPermission
            : source.Pin.Kind == ReceivingKind.Service ? ServiceReversePermission : ReversePermission;
        Guard(source, policy, actor, ledger, permission, at, observation?.SiteId);
        NewKey(ledger, effectKey, evidence);
        Require(policy.CorrectionWorkerIds.Contains(actor.WorkerId) && actor.WorkerId != original.ActorWorkerId,
            "IndependentCorrectionAuthorityRequired");
        Require(kind != AcceptanceEffectKind.ReturnDispatched || source.Pin.Kind == ReceivingKind.Goods,
            "ServiceCannotHavePhysicalReturn");
        Require(quantity > 0 && QuantityPrecision(quantity) && !string.IsNullOrWhiteSpace(reason), "CorrectionReasonAndQuantityRequired");
        Require(authority.Source == source.Pin && authority.OriginalAcceptanceEffectId == originalEffectId
            && authority.EffectKey == effectKey && authority.Kind == kind && authority.CurrentAndGranted
            && authority.AuthorizedQuantity >= quantity && authority.RequesterWorkerId != Guid.Empty
            && authority.AuthorizerWorkerId != authority.RequesterWorkerId && authority.AuthorizerWorkerId != Guid.Empty
            && authority.AuthorizerWorkerId != original.ActorWorkerId
            && !string.IsNullOrWhiteSpace(authority.DecisionReference), "CorrectionDecisionRequired");
        Require(kind != AcceptanceEffectKind.ReturnDispatched || authority.DispatchVerified
            && !string.IsNullOrWhiteSpace(authority.DispatchEvidenceReference), "PhysicalReturnDispatchEvidenceRequired");
        var alreadyRemoved = -ExactDecimalArithmetic.Sum(ledger.Effects.Where(x => x.OriginalAcceptanceEffectId == originalEffectId)
            .Select(x => x.QuantityDelta));
        Require(quantity <= ExactDecimalArithmetic.Subtract(original.QuantityDelta, alreadyRemoved), "OriginalAcceptanceCapacityExceeded");
        var balance = Balance(ledger, original.AcceptanceRecordId);
        Require(consumption.Source == source.Pin && consumption.AcceptanceRecordId == original.AcceptanceRecordId
            && consumption.AcceptanceSequence == ledger.Sequence && consumption.AcceptanceEffectsSha256 == balance.EffectsSha256
            && consumption.Complete && !string.IsNullOrWhiteSpace(consumption.EvidenceReference), "CompleteCurrentConsumptionRequired");
        Require(consumption.ConsumedQuantity >= 0 && consumption.ConsumedNetAmount >= 0
            && !consumption.PostingBound && consumption.ConsumedQuantity <= balance.AvailableQuantity
            && consumption.ConsumedNetAmount <= balance.AvailableNetAmount, "AcceptanceConsumptionBlocked");
        Require(consumption.OwnerState is ReceivingOwnerCorrectionState.Unprocessed or ReceivingOwnerCorrectionState.CompensationConfirmed
            && consumption.CorrectionEffectKey == effectKey && !string.IsNullOrWhiteSpace(consumption.OwnerEvidenceReference),
            "OwnerCorrectionEvidenceRequired");
        var amount = ExactDecimalArithmetic.Multiply(quantity, source.Pin.NetUnitPrice);
        Require(ExactDecimalArithmetic.Subtract(balance.AvailableQuantity, quantity) >= consumption.ConsumedQuantity
            && ExactDecimalArithmetic.Subtract(balance.AvailableNetAmount, amount) >= consumption.ConsumedNetAmount, "ConsumedAcceptanceCannotBeRemoved");
        return Effect(source.Pin, ledger, policy, actor, original.AcceptanceRecordId, original.PhysicalObservationId,
            original.InspectionEvidenceId, original.Id, kind, -quantity, evidence, reason,
            original.DeliverableReference, original.PeriodFrom, original.PeriodTo, effectKey, at, checked(ledger.Sequence + 1),
            new(consumption.AcceptanceSequence, consumption.AcceptanceEffectsSha256, consumption.EvidenceReference,
                consumption.ConsumedQuantity, consumption.ConsumedNetAmount, consumption.OwnerState,
                effectKey, consumption.OwnerEvidenceReference!, authority.RequesterWorkerId, authority.AuthorizerWorkerId,
                authority.DecisionReference, authority.AuthorizedQuantity, kind == AcceptanceEffectKind.ReturnDispatched ? authority.DispatchEvidenceReference : null));
    }

    public static ReceivingAcceptanceBalance Balance(ReceivingLedger ledger, Guid acceptanceRecordId)
    {
        ValidateLedger(ledger);
        Require(ledger.AcceptanceHistoryVerified, "ReceivingLegacyAcceptanceReconciliationRequired");
        return BalanceCore(ledger.Source, ledger.Sequence, ledger.Effects, acceptanceRecordId);
    }

    private static ReceivingAcceptanceBalance BalanceCore(ReceivingSourcePin source, long sequence,
        IReadOnlyList<ReceivingAcceptanceEffect> allEffects, Guid acceptanceRecordId)
    {
        var effects = allEffects.Where(x => x.AcceptanceRecordId == acceptanceRecordId).OrderBy(x => x.Id).ToArray();
        var positive = effects.Where(x => x.Kind == AcceptanceEffectKind.Acceptance).ToArray();
        var reversed = effects.Where(x => x.Kind == AcceptanceEffectKind.AdministrativeReversal).ToArray();
        var returned = effects.Where(x => x.Kind == AcceptanceEffectKind.ReturnDispatched).ToArray();
        var accepted = positive.Concat(reversed).ToArray();
        var acceptedQuantity = ExactDecimalArithmetic.Sum(accepted.Select(x => x.QuantityDelta));
        var acceptedNet = ExactDecimalArithmetic.Sum(accepted.Select(x => x.NetAmountDelta));
        var digest = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            Source = source, Sequence = sequence, acceptanceRecordId,
            effects = effects.Select(x => new { x.Id, x.LedgerSequence, x.EffectKey, x.Kind, x.OriginalAcceptanceEffectId,
                quantity = ProcurementEvidenceEncoding.Decimal(x.QuantityDelta), amount = ProcurementEvidenceEncoding.Decimal(x.NetAmountDelta),
                x.ActorWorkerId, x.CreatedBy, x.ReceivingPolicyVersionId, x.EvidenceReference, x.OccurredAt,
                x.PhysicalObservationId, x.InspectionEvidenceId, x.DeliverableReference, x.PeriodFrom, x.PeriodTo,
                x.CorrectionEvidence }),
        }));
        return new(acceptanceRecordId, sequence, acceptedQuantity, acceptedNet,
            -ExactDecimalArithmetic.Sum(returned.Select(x => x.QuantityDelta)), -ExactDecimalArithmetic.Sum(returned.Select(x => x.NetAmountDelta)),
            positive.Length > 0 && acceptedQuantity == 0 && returned.Length == 0, digest);
    }

    public static void VerifySource(ReceivingSourceProjection source)
    {
        var p = source.Pin;
        Require(!string.IsNullOrWhiteSpace(p.TenantId) && p.LegalEntityId != Guid.Empty && p.PurchaseOrderId != Guid.Empty
            && p.IssuedVersionId != Guid.Empty && p.PurchaseOrderLineId != Guid.Empty && p.PurchaseRequestLineId != Guid.Empty
            && p.IssuedRevision > 0 && Enum.IsDefined(p.Kind) && !string.IsNullOrWhiteSpace(p.CategoryCode)
            && !string.IsNullOrWhiteSpace(p.PurchaseType) && !string.IsNullOrWhiteSpace(p.CurrencyCode)
            && !string.IsNullOrWhiteSpace(p.UnitCode) && p.CurrencyCode.Length == 3
            && p.AuthorizedQuantity > 0 && QuantityPrecision(p.AuthorizedQuantity)
            && p.NetUnitPrice >= 0 && QuantityPrecision(p.NetUnitPrice)
            && p.NetPriceBasis == NetPriceBasis && source.BuyerWorkerId != Guid.Empty, "InvalidReceivingSource");
        var issued = source.Issued; var submitted = source.Submission; var delivery = source.Delivery;
        Require(source.CurrentIssuedAuthority && Scope(issued, p) && Scope(submitted, p) && Scope(delivery, p)
            && issued.Id == p.IssuedVersionId && issued.PurchaseOrderId == p.PurchaseOrderId
            && issued.RevisionNumber == p.IssuedRevision && issued.SubmissionId == submitted.Id
            && submitted.PurchaseOrderId == p.PurchaseOrderId && submitted.CommercialRevision == p.IssuedRevision
            && submitted.BuyerWorkerId == source.BuyerWorkerId
            && issued.CommercialSnapshotSha256 == p.IssuedSnapshotSha256
            && submitted.CommercialSnapshotSha256 == p.IssuedSnapshotSha256
            && issued.CommercialSnapshotJson == submitted.CommercialSnapshotJson
            && ProcurementEvidenceEncoding.Digest(issued.CommercialSnapshotJson) == p.IssuedSnapshotSha256
            && delivery.IssuedVersionId == issued.Id && delivery.Outcome == "Delivered"
            && delivery.VerifiedByWorkerId != Guid.Empty && !string.IsNullOrWhiteSpace(delivery.EvidenceReference)
            && submitted.SubmittedAt != default && issued.AuthorizedAt >= submitted.SubmittedAt
            && delivery.ObservedAt >= issued.AuthorizedAt,
            "IssuedDeliveryEvidenceMismatch");
        try
        {
            // Canonical validation rejects duplicate keys, lossy decimals and unsupported JSON representations.
            Require(ProcurementEvidenceEncoding.PolicyJsonDigest(issued.CommercialSnapshotJson).Length > 0,
                "InvalidIssuedSnapshot");
            using var json = JsonDocument.Parse(issued.CommercialSnapshotJson);
            var root = json.RootElement;
            Require(root.GetProperty("schemaVersion").GetInt32() == 1
                && root.GetProperty("TenantId").GetString() == p.TenantId
                && root.GetProperty("LegalEntityId").GetGuid() == p.LegalEntityId
                && root.GetProperty("Id").GetGuid() == p.PurchaseOrderId
                && root.GetProperty("revision").GetInt32() == p.IssuedRevision
                && NullableGuid(root.GetProperty("BranchRef")) == p.BranchId
                && root.GetProperty("CurrencyCode").GetString() == p.CurrencyCode, "IssuedScopeMismatch");
            Require(!string.IsNullOrWhiteSpace(submitted.ParticipantWorkersJson), "ReceivingRetainedWorkerIdentityRequired");
            var retainedWorkers = JsonSerializer.Deserialize<Guid[]>(submitted.ParticipantWorkersJson);
            var identityOrder = new PurchaseOrder { Id = p.PurchaseOrderId, TenantId = p.TenantId,
                LegalEntityId = p.LegalEntityId, BuyerSubjectId = root.GetProperty("BuyerSubjectId").GetString()! };
            Require(retainedWorkers is { Length: > 0 } && retainedWorkers.All(x => x != Guid.Empty)
                && retainedWorkers.Distinct().Count() == retainedWorkers.Length && retainedWorkers.Contains(source.BuyerWorkerId)
                && issued.ParticipantWorkersJson == submitted.ParticipantWorkersJson
                && submitted.WorkerIdentitySha256 == PurchaseOrderWorkerIdentity.Digest(identityOrder,
                    submitted.CommercialRevision, submitted.CoreApprovalCaseId, source.BuyerWorkerId, submitted.ParticipantWorkersJson),
                "ReceivingRetainedWorkerIdentityRequired");
            var lines = root.GetProperty("lines").EnumerateArray().ToArray();
            Require(lines.Select(x => x.GetProperty("Id").GetGuid()).Distinct().Count() == lines.Length,
                "DuplicateIssuedLineIdentity");
            var line = lines.Single(x => x.GetProperty("Id").GetGuid() == p.PurchaseOrderLineId);
            var knownLineFields = new HashSet<string>(StringComparer.Ordinal)
            { "Id", "LineNumber", "PurchaseRequestLineId", "CatalogItemId", "Description", "UnitCode", "quantity",
                "unitPrice", "tax", "CostCenterRef", "ProjectRef", "ExpectedDeliveryOn" };
            Require(line.EnumerateObject().All(x => knownLineFields.Contains(x.Name)), "UnsupportedSealedPricingSchema");
            Require(line.GetProperty("PurchaseRequestLineId").GetGuid() == p.PurchaseRequestLineId
                && line.GetProperty("UnitCode").GetString() == p.UnitCode
                && ExactDecimal(line.GetProperty("quantity")) == p.AuthorizedQuantity
                && ExactDecimal(line.GetProperty("unitPrice")) == p.NetUnitPrice, "IssuedLineMismatch");
            var wrappedSources = "{\"sources\":" + submitted.SourceEvidenceJson + "}";
            Require(ProcurementEvidenceEncoding.PolicyJsonDigest(wrappedSources) == p.SourceEvidenceSha256
                && p.SourceEvidenceSha256.Length > 0, "SourceLineageDigestMismatch");
            using var sourceJson = JsonDocument.Parse(submitted.SourceEvidenceJson);
            var sources = sourceJson.RootElement.EnumerateArray().ToArray();
            Require(sources.Length == lines.Length && sources.Select(x => x.GetProperty("orderLineId").GetGuid())
                .Order().SequenceEqual(lines.Select(x => x.GetProperty("Id").GetGuid()).Order()), "SourceLineageIdentityMismatch");
            var lineage = sources.Single(x => x.GetProperty("orderLineId").GetGuid() == p.PurchaseOrderLineId);
            Require(lineage.GetProperty("requestLineId").GetGuid() == p.PurchaseRequestLineId
                && lineage.GetProperty("CategoryCode").GetString() == p.CategoryCode
                && lineage.GetProperty("PurchaseType").GetString() == p.PurchaseType
                && lineage.GetProperty("UnitCode").GetString() == p.UnitCode
                && ExactDecimal(lineage.GetProperty("quantity")) == p.AuthorizedQuantity, "SourceClassificationMismatch");
            Require(!string.IsNullOrWhiteSpace(p.ClassificationReference)
                && ProcurementEvidenceEncoding.PolicyJsonDigest(source.ClassificationEvidenceJson) == p.ClassificationSha256
                && p.ClassificationSha256.Length > 0, "ClassificationEvidenceRequired");
            using var classification = JsonDocument.Parse(source.ClassificationEvidenceJson);
            var c = classification.RootElement;
            Require(c.GetProperty("tenantId").GetString() == p.TenantId
                && c.GetProperty("legalEntityId").GetGuid() == p.LegalEntityId
                && c.GetProperty("issuedVersionId").GetGuid() == p.IssuedVersionId
                && c.GetProperty("orderLineId").GetGuid() == p.PurchaseOrderLineId
                && c.GetProperty("sourceEvidenceSha256").GetString() == p.SourceEvidenceSha256
                && c.GetProperty("reference").GetString() == p.ClassificationReference
                && c.GetProperty("kind").GetString() == p.Kind.ToString()
                && c.GetProperty("netPriceBasis").GetString() == NetPriceBasis, "ClassificationEvidenceMismatch");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or OverflowException or InvalidOperationException)
        { throw new InvalidOperationException("ReceivingSealedEvidenceInvalid", ex); }
    }

    public static void ValidateLedger(ReceivingLedger ledger)
    {
        Require(ledger.Complete && ledger.Sequence >= 0, "CompleteReceivingLedgerRequired");
        var p = ledger.Source;
        var rows = ledger.Observations.Cast<ProcurementRecord>().Concat(ledger.Inspections).Concat(ledger.Effects).ToArray();
        Require(rows.All(x => Scope(x, p) && x.Id != Guid.Empty)
            && rows.Select(x => x.Id).Distinct().Count() == rows.Length, "ReceivingLedgerScopeOrIdentityMismatch");
        var sequences = ledger.Observations.Select(x => x.LedgerSequence).Concat(ledger.Inspections.Select(x => x.LedgerSequence))
            .Concat(ledger.Effects.Select(x => x.LedgerSequence)).Order().ToArray();
        Require(sequences.LongLength == ledger.Sequence
            && sequences.Select((value, index) => value == (long)index + 1).All(x => x), "ReceivingLedgerSequenceMismatch");
        var facts = Facts(ledger).OrderBy(x => x.Sequence).ToArray();
        Require(facts.All(x => x.At != default && Time(x.At) == x.At)
            && facts.Zip(facts.Skip(1), (previous, next) => previous.At <= next.At).All(x => x),
            "ReceivingFactChronologyMismatch");
        var keys = ledger.Observations.Select(x => x.EffectKey).Concat(ledger.Inspections.Select(x => x.EffectKey))
            .Concat(ledger.Effects.Select(x => x.EffectKey)).ToArray();
        Require(keys.All(x => x != Guid.Empty) && keys.Distinct().Count() == keys.Length, "ReceivingEffectKeyCollision");
        Require(ledger.Observations.Select(x => x.GoodsReceiptLineId).Distinct().Count() == ledger.Observations.Count
            && ledger.Inspections.Select(x => x.PhysicalObservationId).Distinct().Count() == ledger.Inspections.Count,
            "DuplicatePhysicalOrInspectionIdentity");
        foreach (var observation in ledger.Observations)
            Require(observation.Source == p && p.Kind == ReceivingKind.Goods && observation.GoodsReceiptId != Guid.Empty
                && observation.GoodsReceiptLineId != Guid.Empty && observation.SiteId != Guid.Empty
                && observation.ReceiverWorkerId != Guid.Empty && observation.ReceivingPolicyVersionId != Guid.Empty
                && observation.ReceivedQuantity > 0 && QuantityPrecision(observation.ReceivedQuantity)
                && QuantityPrecision(observation.InitiallyRejectedQuantity) && observation.InitiallyRejectedQuantity >= 0
                && observation.InitiallyRejectedQuantity <= observation.ReceivedQuantity, "InvalidPhysicalEvidence");
        foreach (var inspection in ledger.Inspections)
        {
            var observation = Observation(ledger, inspection.PhysicalObservationId);
            Require(inspection.Source == p && inspection.GoodsReceiptLineId == observation.GoodsReceiptLineId
                && inspection.InspectedQuantity == ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity, observation.InitiallyRejectedQuantity)
                && inspection.QualifiedQuantity >= 0 && QuantityPrecision(inspection.QualifiedQuantity)
                && inspection.QualifiedQuantity <= inspection.InspectedQuantity
                && inspection.InspectorWorkerId != Guid.Empty && observation.LedgerSequence < inspection.LedgerSequence
                && inspection.ReceivingPolicyVersionId == observation.ReceivingPolicyVersionId
                && !string.IsNullOrWhiteSpace(inspection.ChecklistReference), "InvalidInspectionEvidence");
            var expected = inspection.QualifiedQuantity == 0 ? ReceivingInspectionOutcome.Rejected
                : inspection.QualifiedQuantity == inspection.InspectedQuantity ? ReceivingInspectionOutcome.Passed
                : ReceivingInspectionOutcome.PartiallyPassed;
            Require(inspection.Outcome == expected, "InspectionOutcomeMismatch");
        }
        foreach (var effect in ledger.Effects)
        {
            Require(effect.Source == p && effect.AcceptanceRecordId != Guid.Empty && effect.ActorWorkerId != Guid.Empty
                && effect.ReceivingPolicyVersionId != Guid.Empty && !string.IsNullOrWhiteSpace(effect.EvidenceReference)
                && QuantityPrecision(effect.QuantityDelta) && effect.NetAmountDelta == ExactDecimalArithmetic.Multiply(effect.QuantityDelta, p.NetUnitPrice), "AcceptanceEffectBasisMismatch");
            if (effect.Kind == AcceptanceEffectKind.Acceptance)
            {
                Require(effect.QuantityDelta > 0 && effect.OriginalAcceptanceEffectId is null && effect.CorrectionEvidence is null, "InvalidPositiveAcceptance");
                if (p.Kind == ReceivingKind.Goods)
                {
                    Require(effect.PhysicalObservationId.HasValue, "AcceptanceObservationRequired");
                    var observation = Observation(ledger, effect.PhysicalObservationId!.Value);
                    Require(observation.GoodsReceiptLineId == effect.AcceptanceRecordId && observation.LedgerSequence < effect.LedgerSequence, "AcceptancePhysicalIdentityMismatch");
                    var available = ExactDecimalArithmetic.Subtract(observation.ReceivedQuantity, observation.InitiallyRejectedQuantity);
                    if (effect.InspectionEvidenceId is { } inspectionId)
                    {
                        var inspection = ledger.Inspections.SingleOrDefault(x => x.Id == inspectionId);
                        Require(inspection is not null && inspection.PhysicalObservationId == observation.Id
                            && inspection.LedgerSequence < effect.LedgerSequence,
                            "AcceptanceInspectionMismatch");
                        available = inspection!.QualifiedQuantity;
                    }
                    Require(ExactDecimalArithmetic.Sum(ledger.Effects.Where(x => x.PhysicalObservationId == observation.Id
                        && x.Kind == AcceptanceEffectKind.Acceptance).Select(x => x.QuantityDelta)) <= available,
                        "PhysicalAcceptanceCapacityExceeded");
                }
                else Require(p.Kind == ReceivingKind.Service && effect.PhysicalObservationId is null
                    && effect.InspectionEvidenceId is null && !string.IsNullOrWhiteSpace(effect.DeliverableReference)
                    && effect.PeriodFrom.HasValue && effect.PeriodTo.HasValue && effect.PeriodFrom <= effect.PeriodTo,
                    "InvalidServiceAcceptanceEvidence");
            }
            else
            {
                Require(effect.Kind is AcceptanceEffectKind.AdministrativeReversal or AcceptanceEffectKind.ReturnDispatched
                    && effect.QuantityDelta < 0 && !string.IsNullOrWhiteSpace(effect.Reason)
                    && (effect.Kind != AcceptanceEffectKind.ReturnDispatched || p.Kind == ReceivingKind.Goods), "InvalidNegativeAcceptance");
                var parent = ledger.Effects.SingleOrDefault(x => x.Id == effect.OriginalAcceptanceEffectId
                    && x.Kind == AcceptanceEffectKind.Acceptance);
                Require(parent is not null && parent.LedgerSequence < effect.LedgerSequence && parent.AcceptanceRecordId == effect.AcceptanceRecordId
                    && parent.PhysicalObservationId == effect.PhysicalObservationId
                    && parent.InspectionEvidenceId == effect.InspectionEvidenceId
                    && effect.ActorWorkerId != parent.ActorWorkerId
                    && -ExactDecimalArithmetic.Sum(ledger.Effects.Where(x => x.OriginalAcceptanceEffectId == parent.Id).Select(x => x.QuantityDelta))
                        <= parent.QuantityDelta, "CorrectionParentCapacityMismatch");
                var correction = effect.CorrectionEvidence;
                Require(correction is not null && correction.AcceptanceSequence == effect.LedgerSequence - 1
                    && correction.CorrectionEffectKey == effect.EffectKey
                    && correction.OwnerState is ReceivingOwnerCorrectionState.Unprocessed or ReceivingOwnerCorrectionState.CompensationConfirmed
                    && !string.IsNullOrWhiteSpace(correction.OwnerEvidenceReference)
                    && !string.IsNullOrWhiteSpace(correction.ConsumptionEvidenceReference)
                    && correction.ConsumedQuantity >= 0 && correction.ConsumedNetAmount >= 0
                    && correction.RequesterWorkerId != Guid.Empty && correction.AuthorizerWorkerId != correction.RequesterWorkerId
                    && correction.AuthorizerWorkerId != Guid.Empty && correction.AuthorizerWorkerId != parent!.ActorWorkerId
                    && !string.IsNullOrWhiteSpace(correction.AuthorityDecisionReference) && correction.AuthorizedQuantity >= -effect.QuantityDelta
                    && (effect.Kind != AcceptanceEffectKind.ReturnDispatched || !string.IsNullOrWhiteSpace(correction.DispatchEvidenceReference)),
                    "CorrectionConsumptionEvidenceRequired");
                var prior = BalanceCore(p, correction!.AcceptanceSequence,
                    ledger.Effects.Where(x => x.LedgerSequence < effect.LedgerSequence).ToArray(), effect.AcceptanceRecordId);
                Require(prior.EffectsSha256 == correction.AcceptanceEffectsSha256
                    && ExactDecimalArithmetic.Add(prior.AvailableQuantity, effect.QuantityDelta) >= correction.ConsumedQuantity
                    && ExactDecimalArithmetic.Add(prior.AvailableNetAmount, effect.NetAmountDelta) >= correction.ConsumedNetAmount,
                    "CorrectionConsumptionEvidenceMismatch");
            }
        }
        Require(ExactDecimalArithmetic.Sum(ledger.Effects.Select(x => x.QuantityDelta)) >= 0
            && ExactDecimalArithmetic.Sum(ledger.Effects.Select(x => x.QuantityDelta)) <= p.AuthorizedQuantity, "IssuedAcceptanceCapacityExceeded");
        decimal runningQuantity = 0;
        foreach (var historical in ledger.Effects.OrderBy(x => x.LedgerSequence))
        {
            runningQuantity = ExactDecimalArithmetic.Add(runningQuantity, historical.QuantityDelta);
            Require(runningQuantity >= 0 && runningQuantity <= p.AuthorizedQuantity, "HistoricalIssuedAcceptanceCapacityExceeded");
        }
        // Decimal-valued projections/counters must also represent aggregate values exactly.
        _ = ExactDecimalArithmetic.Sum(ledger.Effects.Select(x => x.NetAmountDelta));
        _ = ExactDecimalArithmetic.Sum(ledger.Effects.Where(x => x.Kind != AcceptanceEffectKind.ReturnDispatched).Select(x => x.NetAmountDelta));
        _ = ExactDecimalArithmetic.Sum(ledger.Effects.Where(x => x.Kind == AcceptanceEffectKind.ReturnDispatched).Select(x => x.NetAmountDelta));
        foreach (var recordId in ledger.Effects.Select(x => x.AcceptanceRecordId).Distinct())
            _ = BalanceCore(p, ledger.Sequence, ledger.Effects, recordId);
        var service = ledger.Effects.Where(x => x.Kind == AcceptanceEffectKind.Acceptance && p.Kind == ReceivingKind.Service).ToArray();
        Require(service.Select(x => x.AcceptanceRecordId).Distinct().Count() == service.Length
            && service.Select(x => x.DeliverableReference).Distinct(StringComparer.Ordinal).Count() == service.Length,
            "DuplicateServiceCertification");
    }

    private static IEnumerable<(long Sequence, DateTimeOffset At)> Facts(ReceivingLedger ledger) =>
        ledger.Observations.Select(x => (x.LedgerSequence, x.RecordedAt))
            .Concat(ledger.Inspections.Select(x => (x.LedgerSequence, x.InspectedAt)))
            .Concat(ledger.Effects.Select(x => (x.LedgerSequence, x.OccurredAt)));

    private static void Guard(ReceivingSourceProjection source, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor, ReceivingLedger ledger, string permission, DateTimeOffset at, Guid? site = null)
    {
        VerifySource(source); ValidateLedger(ledger);
        Require(source.Delivery.ObservedAt <= Time(at), "ReceivingDeliveryEvidenceFromFuture");
        Require(Facts(ledger).All(x => x.At >= source.Delivery.ObservedAt && x.At <= Time(at)),
            "ReceivingFactChronologyMismatch");
        var p = source.Pin;
        Require(ledger.Source == p, "ReceivingLedgerSourceMismatch");
        Require(actor.Active && actor.WorkerId != Guid.Empty && !string.IsNullOrWhiteSpace(actor.PrincipalId)
            && actor.TenantId == p.TenantId && actor.LegalEntityId == p.LegalEntityId
            && actor.Permissions.Any(x => string.Equals(x, permission, StringComparison.Ordinal))
            && (!actor.SelectedBranchId.HasValue || actor.SelectedBranchId == p.BranchId)
            && (actor.OrganisationWide || p.BranchId.HasValue && actor.BranchIds.Contains(p.BranchId.Value))
            && (!site.HasValue || actor.SiteIds.Contains(site.Value)), "ReceivingActorDenied");
        Require(policy.Published && policy.VersionId != Guid.Empty && policy.TenantId == p.TenantId
            && policy.LegalEntityId == p.LegalEntityId && (!policy.BranchId.HasValue || policy.BranchId == p.BranchId)
            && policy.CategoryCode == p.CategoryCode && policy.Kind == p.Kind
            && policy.EffectiveFrom <= at && (!policy.EffectiveTo.HasValue || at < policy.EffectiveTo)
            && (!policy.InspectionRequired || p.Kind == ReceivingKind.Goods)
            && (!site.HasValue || policy.ReceivingSiteIds.Contains(site.Value))
            && (p.Kind != ReceivingKind.Service || p.BranchId is { } serviceSite && actor.SiteIds.Contains(serviceSite)
                && policy.ReceivingSiteIds.Contains(serviceSite)), "ReceivingPublishedPolicyRequired");
    }
    private static void CertificationActor(ReceivingSourceProjection source, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor) => Require((policy.AllowBuyerCertification || actor.WorkerId != source.BuyerWorkerId)
            && (policy.AllowRequesterCertification || actor.WorkerId == source.BuyerWorkerId
                || PurchaseOrderWorkerIdentity.IsIndependent(source.Submission.ParticipantWorkersJson, actor.WorkerId)), "CertificationMakerCheckerDenied");
    private static void Capacity(ReceivingSourcePin source, ReceivingLedger ledger, decimal quantity) =>
        Require(ledger.AcceptanceHistoryVerified && ExactDecimalArithmetic.Sum(ledger.Effects.Select(x => x.QuantityDelta).Append(quantity)) <= source.AuthorizedQuantity,
            ledger.AcceptanceHistoryVerified ? "IssuedAcceptanceCapacityExceeded" : "ReceivingLegacyAcceptanceReconciliationRequired");
    private static void NewKey(ReceivingLedger ledger, Guid key, string evidence) => Require(key != Guid.Empty
        && !string.IsNullOrWhiteSpace(evidence) && !ledger.Observations.Any(x => x.EffectKey == key)
        && !ledger.Inspections.Any(x => x.EffectKey == key) && !ledger.Effects.Any(x => x.EffectKey == key), "ReceivingEffectKeyCollision");
    private static GoodsPhysicalObservation Observation(ReceivingLedger ledger, Guid id) =>
        ledger.Observations.SingleOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("PhysicalObservationRequired");
    private static ReceivingAcceptanceEffect Effect(ReceivingSourcePin source, ReceivingLedger ledger, ReceivingPolicyProjection policy,
        ReceivingActorProjection actor, Guid recordId, Guid? physical, Guid? inspection, Guid? original,
        AcceptanceEffectKind kind, decimal quantity, string evidence, string? reason, string? deliverable,
        DateOnly? from, DateOnly? to, Guid key, DateTimeOffset at, long sequence,
        ReceivingCorrectionPin? correction = null)
    {
        var effect = new ReceivingAcceptanceEffect
        {
            TenantId = source.TenantId, LegalEntityId = source.LegalEntityId, CreatedBy = actor.PrincipalId,
            CreatedAt = Time(at), Source = source, AcceptanceRecordId = recordId, PhysicalObservationId = physical,
            InspectionEvidenceId = inspection, OriginalAcceptanceEffectId = original, CorrectionEvidence = correction, Kind = kind,
            QuantityDelta = quantity, NetAmountDelta = ExactDecimalArithmetic.Multiply(quantity, source.NetUnitPrice),
            ActorWorkerId = actor.WorkerId, ReceivingPolicyVersionId = policy.VersionId,
            EvidenceReference = evidence, Reason = reason, DeliverableReference = deliverable,
            PeriodFrom = from, PeriodTo = to, LedgerSequence = sequence, EffectKey = key, OccurredAt = Time(at),
        };
        // Never propose a fact whose resulting decimal balance would silently round or
        // become unreadable. Persistence still owns atomic CAS and idempotent recovery.
        ValidateLedger(ledger with { Sequence = sequence, Effects = ledger.Effects.Append(effect).ToArray() });
        return effect;
    }
    private static bool Scope(ProcurementRecord row, ReceivingSourcePin source) =>
        row.TenantId == source.TenantId && row.LegalEntityId == source.LegalEntityId;
    private static decimal ExactDecimal(JsonElement value)
    {
        Require(value.ValueKind == JsonValueKind.String, "UnsupportedSealedPriceRepresentation");
        var literal = value.GetString()!;
        var number = decimal.Parse(literal, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        var original = ProcurementEvidenceEncoding.PolicyJsonDigest("{\"value\":" + literal + "}");
        Require(original.Length > 0 && original == ProcurementEvidenceEncoding.PolicyJsonDigest(
            "{\"value\":" + ProcurementEvidenceEncoding.Decimal(number) + "}"), "LossySealedPriceRepresentation");
        return number;
    }
    // Existing PO/source quantity and rate columns are decimal(18,4); their exact product needs
    // eight fractional digits. Never round individual acceptance/correction fragments to money decimals.
    private static bool QuantityPrecision(decimal value) => decimal.Round(value, 4) == value;
    private static Guid? NullableGuid(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetGuid();
    private static DateTimeOffset Time(DateTimeOffset at) => ProcurementEvidenceEncoding.DatabaseTimestamp(at)!.Value;
    private static void Require([DoesNotReturnIf(false)] bool condition, string code)
    { if (!condition) throw new InvalidOperationException(code); }
}
