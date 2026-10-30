using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Source-schema-3 bridge into the existing pure comparison engine.
/// Histories and review authorities must come from the owning enlisted loaders.
/// Temporary foundation selectors are never persisted as legacy receiving FKs.
/// Current actor authorization, transaction and owner admission remain Host duties.</summary>
public static class PrincipalInvoiceMatchingBridge
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool ReviewCovers3(InvoiceMatchMappingVersion mapping, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceMappingReview review,
        InvoiceMappingReviewContext context)
    {
        if (mapping.SourceSchemaVersion != 3 || !review.Verified || review.State != "Verified"
            || review.RetainedAuthority is not { } authority || review.CurrentConsumer is not { } consumer
            || context is null || review.VerifiedUnits is null || authority.MatchingPolicy is null
            || context.DocumentVersionHashes is null || consumer.DocumentVersionHashes is null
            || review.EvidenceJson != authority.EvidenceJson
            || authority.MatchingPolicy.Id != context.PolicyId
            || ProcurementEvidenceEncoding.PolicyJsonDigest(authority.MatchingPolicy.RulesJson) != context.PolicyRulesSha256
            || consumer.TenantId != context.TenantId || consumer.LegalEntityId != context.LegalEntityId
            || consumer.BranchId != context.BranchId || consumer.PrincipalId != context.PrincipalId
            || consumer.WorkerId != context.WorkerId || consumer.At != context.At
            || consumer.DocumentVersionHashes.Count != context.DocumentVersionHashes.Count
            || consumer.DocumentVersionHashes.Any(x => !context.DocumentVersionHashes.TryGetValue(x.Key, out var hash) || hash != x.Value))
            return false;
        try
        {
            _ = PrincipalInvoiceMappingEvidence.VerifyStored(mapping, invoice, originalLines);
            // Existing verifier dispatches source3 for original-document review and
            // rechecks actual Core completion, independence, documents and consumer.
            var units = review.ManualSource is { } manual
                ? InvoiceManualUnitReviewEvidence.Verify(authority, manual, invoice, originalLines, mapping, consumer)
                : InvoiceRetainedUnitReview.Verify(authority, invoice, originalLines, mapping, consumer);
            return units.Count == review.VerifiedUnits.Count
                && units.All(x => review.VerifiedUnits.TryGetValue(x.Key, out var unit) && unit == x.Value);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException
            or JsonException or FormatException or OverflowException or KeyNotFoundException)
        { return false; }
    }

    public static InvoiceMatchDocument Project(InvoiceMatchMappingVersion mapping, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceMappingReview review,
        InvoiceMappingReviewContext context, IReadOnlyList<PrincipalInvoiceAcceptedHistory> histories)
    {
        var snapshot = PrincipalInvoiceMappingEvidence.VerifyStored(mapping, invoice, originalLines);
        Need(ReviewCovers3(mapping, invoice, originalLines, review, context), "PrincipalInvoiceUnitReviewUnavailable");
        var selectedIds = snapshot.Lines.Where(x => x.PrincipalReceivingFactId.HasValue)
            .Select(x => x.PrincipalReceivingSourceStateId!.Value).ToHashSet();
        var selected = histories.Where(x => selectedIds.Contains(x.SourceStateId)).ToArray();
        PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(snapshot, selected);
        Need(selected.All(x => x.SupplierId == invoice.SupplierId && x.BranchId == invoice.BranchRef
            && x.CurrencyCode == invoice.CurrencyCode), "PrincipalInvoiceAcceptedScopeChanged");
        var document = InvoiceMatchDocument.From(invoice, originalLines);
        return document with { PurchaseOrderId = mapping.PurchaseOrderId,
            Lines = document.Lines.Select(line =>
            {
                var link = snapshot.Lines.Single(x => x.InvoiceLineId == line.Id);
                PrincipalInvoiceAcceptedFact? fact = link.PrincipalReceivingFactId is { } id
                    ? selected.Single(x => x.SourceStateId == link.PrincipalReceivingSourceStateId).Facts.Single(x => x.FactId == id)
                    : null;
                // These are transient foundation kind selectors, not database FKs.
                return line with { PurchaseOrderLineId = link.PurchaseOrderLineId,
                    GoodsReceiptLineId = fact?.Kind == ReceivingKind.Goods ? fact.FactId : null,
                    ServiceAcceptanceId = fact?.Kind == ReceivingKind.Service ? fact.FactId : null,
                    UnitCode = review.VerifiedUnits[line.Id] };
            }).ToArray() };
    }

    public static IReadOnlyList<InvoiceAcceptedFulfilment> ProjectAcceptances(
        IReadOnlyList<PrincipalInvoiceAcceptedHistory> histories, string tenant, Guid company)
    {
        Need(histories.Count <= 200 && histories.Select(x => x.SourceStateId).Distinct().Count() == histories.Count,
            "PrincipalInvoiceAcceptedHistoryAmbiguous");
        return histories.SelectMany(history => history.Facts.Select(fact =>
        {
            Need(history.SourceStateId != Guid.Empty && history.SupplierId != Guid.Empty
                && history.BranchId != Guid.Empty && !string.IsNullOrWhiteSpace(history.UnitCode)
                && !string.IsNullOrWhiteSpace(history.CurrencyCode) && fact.SourceStateId == history.SourceStateId
                && fact.SourceSequence > 0 && fact.SourceSequence <= history.SourceSequence
                && fact.Kind is ReceivingKind.Goods or ReceivingKind.Service,
                "PrincipalInvoiceAcceptedHistoryAmbiguous");
            return new InvoiceAcceptedFulfilment(fact.FactId, fact.SourceSequence, tenant, company,
                history.BranchId, fact.PurchaseOrderId, fact.IssuedVersionId, fact.PurchaseOrderLineId,
                history.CurrencyCode, history.UnitCode,
                fact.Kind == ReceivingKind.Goods ? InvoiceAcceptanceKind.Goods : InvoiceAcceptanceKind.Service,
                "Accepted", $"PrincipalReceiving:{history.SourceStateId:D}:{fact.FactId:D}", fact.AcceptedAt,
                false, fact.AcceptedQuantity, 0, fact.AcceptedNetAmount, 0);
        })).OrderBy(x => x.Id).ToArray();
    }

    public static IReadOnlyList<InvoiceMatchAllocation> Claims(InvoiceMatchingEvaluation comparison,
        InvoiceMatchingInput input, InvoiceMatchResult result, InvoiceMatchExposureState exposure,
        InvoiceHistoricalExposure owner, DateTimeOffset at, SupplierInvoice original,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceMatchMappingVersion mapping,
        InvoiceMappingReview review, InvoiceMappingReviewContext reviewContext, InvoiceMatchCommand command,
        IProcurementScope scope, IReadOnlyList<PrincipalInvoiceAllocationSourceInput> activeClaims,
        PolicyVersion currentPolicy, IReadOnlyList<PrincipalInvoiceAcceptedHistory> histories)
    {
        Need(result.SourceSchemaVersion == 3 && exposure.SourceSchemaVersion == 3,
            "PrincipalInvoiceIssuedFamilyRequired");
        var policyProjection = InvoiceMatchingPolicyRules.Project(currentPolicy);
        Need(currentPolicy.TenantId == original.TenantId && currentPolicy.LegalEntityId == original.LegalEntityId
            && currentPolicy.State == "Published" && currentPolicy.Id == reviewContext.PolicyId
            && currentPolicy.PublishedAt is { } published && published <= at && currentPolicy.EffectiveFrom <= at
            && (currentPolicy.EffectiveTo is null || at < currentPolicy.EffectiveTo)
            && reviewContext.PolicyRulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(currentPolicy.RulesJson)
            && InvoiceMatchingPolicyRules.ProjectionDigest(policyProjection) == InvoiceMatchingPolicyRules.ProjectionDigest(input.Policy),
            "InvoiceCurrentPublishedPolicyRequired");
        var projected = Project(mapping, original, originalLines, review, reviewContext, histories);
        var snapshot = PrincipalInvoiceMappingEvidence.VerifyStored(mapping, original, originalLines);
        Need(original.ActiveMatchClaimResultId is null && original.CurrentMappingVersionId == mapping.Id
            && original.MappingRevision == mapping.MappingRevision && result.MappingVersionId == mapping.Id
            && result.InvoiceVersion == original.Version && result.InvoiceOriginalSha256 == mapping.InvoiceOriginalSha256
            && result.TenantId == original.TenantId && result.LegalEntityId == original.LegalEntityId
            && ProcurementPrincipalEvidence.Canonical(scope.SubjectId)
            && result.ActorPrincipalId == scope.SubjectId && result.ActorWorkerId == reviewContext.WorkerId
            && reviewContext.PrincipalId == scope.SubjectId && reviewContext.PolicyId == input.Policy.Id
            && reviewContext.PolicyRulesSha256 == result.PolicyRulesSha256
            && Same(projected, input.Invoice)
            && Same(ProjectAcceptances(histories, original.TenantId, original.LegalEntityId), input.Acceptances.OrderBy(x => x.Id).ToArray()),
            "PrincipalInvoiceCurrentMappingRequired");
        var selectedActive = activeClaims.Where(x => x.Allocation.PrincipalReceivingSourceStateId.HasValue)
            .Select(x => x.Allocation.PrincipalReceivingSourceStateId!.Value).ToHashSet();
        PrincipalInvoiceAllocationSourceEvidence.VerifySet(activeClaims,
            histories.Where(x => selectedActive.Contains(x.SourceStateId)).ToArray());
        var locals = activeClaims.Select(x => new InvoiceActiveLocalClaim(x.Allocation, x.Invoice)).ToArray();
        var context = new InvoiceExposureReadContext(input.Invoice.TenantId, input.Invoice.LegalEntityId,
            input.Invoice.BranchRef, input.Order.Id, input.Order.RevisionId, input.Order.SupplierId,
            input.Order.CurrencyCode, result.PolicyRulesSha256!, at);
        var history = InvoiceMatchExecutionEvidence.CompleteHistory(owner, context, exposure, locals);
        Need(input.Consumption.Complete && input.Consumption.Watermark == history.Watermark
            && InvoiceMatchExecutionEvidence.AllocationsDigest(input.Consumption.Allocations)
                == InvoiceMatchExecutionEvidence.AllocationsDigest(history.Allocations), "InvoiceConsumptionCheckpointChanged");
        var current = InvoiceMatchingFoundation.Evaluate(command, scope, input);
        Need(comparison.ComparisonPassed && current.ComparisonPassed && !comparison.CanPostToFinance
            && comparison.InputFingerprint == current.InputFingerprint && comparison.EvidenceJson == current.EvidenceJson
            && comparison.EvidenceSha256 == current.EvidenceSha256 && comparison.InvoiceId == input.Invoice.Id
            && comparison.CommandKey == command.CommandKey && result.CommandKey == command.CommandKey
            && result.Outcome == "Compared" && result.MatchedAt == at && command.EvaluatedAt == at && reviewContext.At == at
            && comparison.ActorSubjectId == scope.SubjectId && comparison.InvoiceVersion == input.Invoice.Version
            && result.ExecutionVersion == 2 && result.PurchaseOrderId == input.Order.Id && result.IssuedVersionId == input.Order.RevisionId
            && result.PolicyVersionId == input.Policy.Id && result.Id != Guid.Empty
            && result.InputFingerprint == comparison.InputFingerprint && result.EvidenceSha256 == comparison.EvidenceSha256
            && result.EvidenceJson == comparison.EvidenceJson && result.SupplierInvoiceId == input.Invoice.Id
            && result.SourceWatermarksJson is { } watermarks
            && ProcurementEvidenceEncoding.PolicyJsonDigest(watermarks)
                == ProcurementEvidenceEncoding.PolicyJsonDigest(InvoiceMatchExecutionEvidence.SourceWatermarks(input, mapping, review))
            && !activeClaims.Any(x => x.Invoice.Id == original.Id), "InvoiceQualifiedClaimAuthorityRequired");
        RequireHardAcceptedCeilings(current, input);
        var sequence = checked(exposure.Sequence + 1);
        return current.Lines.Select(line =>
        {
            var link = snapshot.Lines.Single(x => x.InvoiceLineId == line.InvoiceLineId);
            Need(link.PrincipalReceivingFactId == line.AcceptanceId, "PrincipalInvoiceClaimFactBindingChanged");
            return new InvoiceMatchAllocation
            {
                TenantId = original.TenantId, LegalEntityId = original.LegalEntityId,
                SupplierInvoiceId = original.Id, SupplierInvoiceLineId = line.InvoiceLineId, MatchResultId = result.Id,
                ExposureStateId = exposure.Id, PurchaseOrderId = input.Order.Id, IssuedVersionId = input.Order.RevisionId,
                PurchaseOrderLineId = line.PurchaseOrderLineId, SupplierId = original.SupplierId,
                PrincipalReceivingSourceStateId = link.PrincipalReceivingSourceStateId,
                PrincipalReceivingFactId = link.PrincipalReceivingFactId,
                ReceivingSourceStateId = null, GoodsReceiptLineId = null, ServiceAcceptanceId = null,
                CurrencyCode = original.CurrencyCode, UnitCode = input.Invoice.Lines.Single(x => x.Id == line.InvoiceLineId).UnitCode,
                Quantity = Parse(line.Quantity,18,4), NetAmount = Parse(line.NetAmount,38,8), TaxAmount = Parse(line.TaxAmount,38,8),
                ExposureSequence = sequence, ValidationReference = $"procurement.match.{result.Id:D}",
                OwnerCoverageReference = owner.OwnerReference!
            };
        }).ToArray();
    }

    public static void RequireHardAcceptedCeilings(InvoiceMatchingEvaluation compared, InvoiceMatchingInput input)
    {
        foreach (var group in compared.Lines.Where(x => x.AcceptanceId.HasValue).GroupBy(x => x.AcceptanceId!.Value))
        {
            var fact = input.Acceptances.Single(x => x.Id == group.Key);
            var used = input.Consumption.Allocations.Where(x => x.AcceptanceId == group.Key).ToArray();
            decimal Signed(InvoiceMatchConsumption x, decimal value) => x.Kind == InvoiceConsumptionKind.Debit ? value : -value;
            Need(ExactDecimalArithmetic.Sum(used.Select(x => Signed(x,x.Quantity)).Concat(group.Select(x => Parse(x.Quantity,18,4))))
                    <= ExactDecimalArithmetic.Subtract(fact.AcceptedQuantity,fact.ReturnedQuantity)
                && ExactDecimalArithmetic.Sum(used.Select(x => Signed(x,x.NetAmount)).Concat(group.Select(x => Parse(x.NetAmount,38,8))))
                    <= ExactDecimalArithmetic.Subtract(fact.AcceptedNetAmount,fact.ReturnedNetAmount),
                "PrincipalInvoiceClaimExceedsAcceptedFact");
        }
    }
    private static decimal Parse(string value,int precision,int scale)
        => ExactDecimalJsonCodec.Parse(value,precision,scale,"InvoiceMatchAmount");
    private static bool Same<T>(T left,T right) => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(left,Json))
        == ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(right,Json));
    private static void Need(bool value,string code){if(!value)throw new InvalidOperationException(code);}
}
