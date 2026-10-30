using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public static class InvoiceMatchExecutionEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string AllocationsDigest(IEnumerable<InvoiceMatchConsumption> allocations)
        => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new { schemaVersion = 1, allocations = allocations.OrderBy(x => x.Id).Select(x => new
        {
            x.Id, x.InvoiceId, x.InvoiceLineId, x.TenantId, x.LegalEntityId, x.BranchRef, x.SupplierId,
            x.PurchaseOrderId, x.RevisionId, x.PurchaseOrderLineId, x.AcceptanceId, x.CurrencyCode,
            kind = x.Kind.ToString(), x.OriginalDebitId, quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity),
            netAmount = ProcurementEvidenceEncoding.Decimal(x.NetAmount), taxAmount = ProcurementEvidenceEncoding.Decimal(x.TaxAmount),
            x.ValidationReference,
        }).ToArray() }, Json));

    public static bool OwnerCovers(InvoiceHistoricalExposure owner, InvoiceExposureReadContext context)
    {
        if (!owner.Complete || owner.State != "Verified" || string.IsNullOrWhiteSpace(owner.OwnerReference)
            || string.IsNullOrWhiteSpace(owner.Watermark) || string.IsNullOrWhiteSpace(owner.EvidenceJson)
            || owner.PolicyRulesSha256 != context.PolicyRulesSha256 || owner.ValidUntil is not { } until || until <= context.At
            || context.At == default || context.PolicyRulesSha256 is not { Length: 64 }
            || owner.Allocations is null || owner.Allocations.Any(x => x is null) || owner.Allocations.Select(x => x.Id).Distinct().Count() != owner.Allocations.Count
            || owner.Allocations.Any(x => x.TenantId != context.TenantId || x.LegalEntityId != context.LegalEntityId
                || x.BranchRef != context.BranchId || x.PurchaseOrderId != context.PurchaseOrderId
                || x.RevisionId != context.IssuedVersionId || x.SupplierId != context.SupplierId || x.CurrencyCode != context.CurrencyCode)) return false;
        try
        {
            if (owner.EvidenceSha256 is not { Length: 64 } || owner.EvidenceSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(owner.EvidenceJson)) return false;
            using var document = JsonDocument.Parse(owner.EvidenceJson);
            if(document.RootElement.ValueKind!=JsonValueKind.Object
                || !document.RootElement.TryGetProperty("schemaVersion",out var schema)
                || schema.ValueKind!=JsonValueKind.Number)return false;
            if(schema.GetInt32()==2)
            {
                var fields=document.RootElement.EnumerateObject().Select(x=>x.Name)
                    .Order(StringComparer.Ordinal).ToArray();
                if(!fields.SequenceEqual(new[]{"allocationsSha256","baselineId","baselineSetSha256",
                    "branchId","catalogManifestSha256","coreCaseId","currencyCode",
                    "currentSetSha256","exposureSequence","historicalClaimsSha256","issuedVersionId",
                    "legalEntityId","localClaimsSha256","ownerReference","policyRulesSha256",
                    "purchaseOrderId","schemaVersion","supplierId","tenantId",
                    "unionClaimsSha256","validUntil","watermark"}))return false;
                var current=JsonSerializer.Deserialize<InvoiceProcurementOwnerExposureEnvelope>(owner.EvidenceJson,Json);
                static bool Sha(string? value)=>value is {Length:64}
                    && value.All(x=>x is >= '0' and <= '9' or >= 'a' and <= 'f');
                return current is not null && current.SchemaVersion==2
                    && current.TenantId==context.TenantId && current.LegalEntityId==context.LegalEntityId
                    && current.BranchId==context.BranchId && current.PurchaseOrderId==context.PurchaseOrderId
                    && current.IssuedVersionId==context.IssuedVersionId && current.SupplierId==context.SupplierId
                    && current.CurrencyCode==context.CurrencyCode
                    && current.PolicyRulesSha256==context.PolicyRulesSha256
                    && current.OwnerReference==owner.OwnerReference && current.Watermark==owner.Watermark
                    && current.ValidUntil==until && current.AllocationsSha256==AllocationsDigest(owner.Allocations)
                    && current.BaselineId!=Guid.Empty && current.CoreCaseId!=Guid.Empty
                    && current.BaselineId!=current.CoreCaseId
                    && Sha(current.CatalogManifestSha256) && Sha(current.BaselineSetSha256)
                    && Sha(current.CurrentSetSha256)
                    && current.HistoricalClaimsSha256==AllocationsDigest(owner.Allocations)
                    && Sha(current.LocalClaimsSha256) && Sha(current.UnionClaimsSha256)
                    && current.ExposureSequence>=0;
            }
            var evidence = JsonSerializer.Deserialize<InvoiceOwnerExposureEnvelope>(owner.EvidenceJson, Json);
            return evidence is not null && evidence.SchemaVersion == 1 && evidence.TenantId == context.TenantId
                && evidence.LegalEntityId == context.LegalEntityId && evidence.BranchId == context.BranchId
                && evidence.PurchaseOrderId == context.PurchaseOrderId && evidence.IssuedVersionId == context.IssuedVersionId
                && evidence.SupplierId == context.SupplierId && evidence.CurrencyCode == context.CurrencyCode
                && evidence.PolicyRulesSha256 == context.PolicyRulesSha256 && evidence.ValidUntil == until
                && evidence.OwnerReference == owner.OwnerReference && evidence.Watermark == owner.Watermark
                && evidence.AllocationsSha256 == AllocationsDigest(owner.Allocations);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or FormatException)
        { return false; }
    }

    public static InvoiceConsumptionEvidence CompleteHistory(InvoiceHistoricalExposure owner, InvoiceExposureReadContext context,
        InvoiceMatchExposureState exposure, IReadOnlyList<InvoiceActiveLocalClaim> activeLocalClaims)
    {
        Need(OwnerCovers(owner, context) && exposure.Id != Guid.Empty && exposure.Sequence >= 0
            && exposure.TenantId == context.TenantId && exposure.LegalEntityId == context.LegalEntityId
            && exposure.PurchaseOrderId == context.PurchaseOrderId && exposure.IssuedVersionId == context.IssuedVersionId
            && exposure.SupplierId == context.SupplierId && exposure.CurrencyCode == context.CurrencyCode,
            "InvoiceHistoricalExposureUnavailable");
        var local = activeLocalClaims.Select(item =>
        {
            var row = item.Allocation; var invoice = item.Invoice;
            Need(invoice.ActiveMatchClaimResultId == row.MatchResultId && invoice.Id == row.SupplierInvoiceId
                && row.TenantId == context.TenantId && row.LegalEntityId == context.LegalEntityId
                && invoice.TenantId == context.TenantId && invoice.LegalEntityId == context.LegalEntityId && invoice.BranchRef == context.BranchId
                && invoice.SupplierId == context.SupplierId && invoice.CurrencyCode == context.CurrencyCode
                && row.ExposureStateId == exposure.Id && row.ExposureSequence > 0 && row.ExposureSequence <= exposure.Sequence
                && row.PurchaseOrderId == context.PurchaseOrderId && row.IssuedVersionId == context.IssuedVersionId
                && row.SupplierId == context.SupplierId && row.CurrencyCode == context.CurrencyCode,
                "InvoiceLocalClaimHistoryChanged");
            return Consumption(row, invoice.BranchRef);
        }).ToArray();
        var all = owner.Allocations.Concat(local).OrderBy(x => x.Id).ToArray();
        // This slice has no reconciled owner/local deduplication protocol. Ambiguous overlap blocks capacity.
        Need(all.Select(x => x.Id).Distinct().Count() == all.Length
            && all.Select(x => (x.InvoiceId, x.InvoiceLineId)).Distinct().Count() == all.Length, "InvoiceExposureOverlapRequiresReconciliation");
        if(owner.EvidenceJson is { } ownerJson)
        {
            using var ownerDocument=JsonDocument.Parse(ownerJson);
            if(ownerDocument.RootElement.TryGetProperty("schemaVersion",out var schema)
                && schema.GetInt32()==2)
            {
                var checkpoint=JsonSerializer.Deserialize<InvoiceProcurementOwnerExposureEnvelope>(ownerJson,Json);
                Need(checkpoint is not null && checkpoint.ExposureSequence==exposure.Sequence
                    && checkpoint.LocalClaimsSha256==AllocationsDigest(local)
                    && checkpoint.UnionClaimsSha256==AllocationsDigest(all),
                    "InvoiceOwnerPartitionChanged");
            }
        }
        var watermark = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        { schemaVersion = 1, owner.OwnerReference, owner.Watermark, exposure.Id, exposure.Sequence, allocationsSha256 = AllocationsDigest(all) }, Json));
        return new(watermark, true, all);
    }

    public static string SourceWatermarks(InvoiceMatchingInput input, InvoiceMatchMappingVersion mapping, InvoiceMappingReview review)
        => JsonSerializer.Serialize(new { schemaVersion = 1, mappingVersionId = mapping.Id, mapping.MappingSha256,
            mapping.InvoiceOriginalSha256, invoiceId = input.Invoice.Id, invoiceVersion = input.Invoice.Version,
            purchaseOrderId = input.Order.Id, issuedVersionId = input.Order.RevisionId, orderVersion = input.Order.Version,
            orderLines = input.Order.Lines.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArray(),
            accepted = input.Acceptances.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArray(),
            policyId = input.Policy.Id, policyVersion = input.Policy.Version,
            reviewSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(review.EvidenceJson),
            consumptionWatermark = input.Consumption.Watermark, consumptionSha256 = AllocationsDigest(input.Consumption.Allocations),
            duplicatesWatermark = input.Duplicates.Watermark }, Json);

    public static IReadOnlyList<InvoiceMatchAllocation> Claims(InvoiceMatchingEvaluation comparison,
        InvoiceMatchingInput input, InvoiceMatchResult result, InvoiceMatchExposureState exposure,
        InvoiceHistoricalExposure owner, IReadOnlyDictionary<Guid, Guid> acceptanceStateIds, DateTimeOffset at,
        SupplierInvoice original, IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceMatchMappingVersion mapping,
        InvoiceMappingReview review, InvoiceMappingReviewContext reviewContext, InvoiceMatchCommand command,
        IProcurementScope scope, IReadOnlyList<InvoiceActiveLocalClaim> activeLocalClaims, PolicyVersion currentPolicy)
    {
        var policyProjection = InvoiceMatchingPolicyRules.Project(currentPolicy);
        Need(currentPolicy.TenantId == original.TenantId && currentPolicy.LegalEntityId == original.LegalEntityId
            && currentPolicy.Id == reviewContext.PolicyId && currentPolicy.PublishedAt <= at
            && reviewContext.PolicyRulesSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(currentPolicy.RulesJson)
            && InvoiceMatchingPolicyRules.ProjectionDigest(policyProjection) == InvoiceMatchingPolicyRules.ProjectionDigest(input.Policy),
            "InvoiceCurrentPublishedPolicyRequired");
        var projected = InvoiceMappingEvidence.Project(mapping, original, originalLines, review, reviewContext);
        Need(review.Verified && original.ActiveMatchClaimResultId is null && original.CurrentMappingVersionId == mapping.Id
            && original.MappingRevision == mapping.MappingRevision && result.MappingVersionId == mapping.Id
            && result.InvoiceVersion == original.Version && result.InvoiceOriginalSha256 == InvoiceMappingEvidence.OriginalDigest(original, originalLines)
            && result.TenantId == original.TenantId && result.LegalEntityId == original.LegalEntityId
            && result.ActorPrincipalId == scope.SubjectId && result.ActorWorkerId == reviewContext.WorkerId
            && reviewContext.PrincipalId == scope.SubjectId && reviewContext.PolicyId == input.Policy.Id
            && reviewContext.PolicyRulesSha256 == result.PolicyRulesSha256
            && ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(projected, Json))
                == ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(input.Invoice, Json)), "InvoiceCurrentMappingRequired");
        var context = new InvoiceExposureReadContext(input.Invoice.TenantId, input.Invoice.LegalEntityId, input.Invoice.BranchRef,
            input.Order.Id, input.Order.RevisionId, input.Order.SupplierId, input.Order.CurrencyCode, result.PolicyRulesSha256!, at);
        var history = CompleteHistory(owner, context, exposure, activeLocalClaims);
        Need(input.Consumption.Complete && input.Consumption.Watermark == history.Watermark
            && AllocationsDigest(input.Consumption.Allocations) == AllocationsDigest(history.Allocations), "InvoiceConsumptionCheckpointChanged");
        var current = InvoiceMatchingFoundation.Evaluate(command, scope, input);
        Need(comparison.ComparisonPassed && current.ComparisonPassed && !comparison.CanPostToFinance
            && comparison.InputFingerprint == current.InputFingerprint && comparison.EvidenceJson == current.EvidenceJson
            && comparison.EvidenceSha256 == current.EvidenceSha256 && comparison.InvoiceId == input.Invoice.Id
            && comparison.CommandKey == command.CommandKey && result.CommandKey == command.CommandKey
            && result.Outcome == "Compared" && result.MatchedAt == at
            && result.SourceWatermarksJson is { } sourceWatermarks
            && ProcurementEvidenceEncoding.PolicyJsonDigest(sourceWatermarks).Length == 64
            && ProcurementEvidenceEncoding.PolicyJsonDigest(sourceWatermarks) == ProcurementEvidenceEncoding.PolicyJsonDigest(SourceWatermarks(input, mapping, review))
            && command.EvaluatedAt == at && reviewContext.At == at && comparison.ActorSubjectId == scope.SubjectId && comparison.InvoiceVersion == input.Invoice.Version && result.ExecutionVersion is 1 or 2
            && result.PurchaseOrderId == input.Order.Id && result.IssuedVersionId == input.Order.RevisionId
            && result.PolicyVersionId == input.Policy.Id && result.Id != Guid.Empty && result.InputFingerprint == comparison.InputFingerprint
            && result.EvidenceSha256 == comparison.EvidenceSha256 && result.EvidenceJson == comparison.EvidenceJson
            && result.SupplierInvoiceId == input.Invoice.Id && !activeLocalClaims.Any(x => x.Invoice.Id == original.Id), "InvoiceQualifiedClaimAuthorityRequired");
        var sequence = checked(exposure.Sequence + 1);
        return current.Lines.Select(line =>
        {
            var source = input.Invoice.Lines.Single(x => x.Id == line.InvoiceLineId);
            // Foundation diagnostics may tolerate quantity variance; live receiving entitlement is a hard claim ceiling.
            if (line.AcceptanceId is { } acceptanceId)
            {
                Need(acceptanceStateIds.TryGetValue(acceptanceId, out var stateId) && stateId != Guid.Empty, "InvoiceAcceptedStateBindingRequired");
                var acceptance = input.Acceptances.Single(x => x.Id == acceptanceId);
                var used = input.Consumption.Allocations.Where(x => x.AcceptanceId == acceptanceId).Select(x => x.Kind == InvoiceConsumptionKind.Debit ? x.Quantity : -x.Quantity);
                var proposed = current.Lines.Where(x => x.AcceptanceId == acceptanceId).Select(x => Parse(x.Quantity, 18, 4));
                Need(ExactDecimalArithmetic.Sum(used.Concat(proposed)) <= ExactDecimalArithmetic.Subtract(acceptance.AcceptedQuantity, acceptance.ReturnedQuantity),
                    "InvoiceClaimRequiresAcceptedQuantity");
            }
            return new InvoiceMatchAllocation { TenantId = input.Invoice.TenantId, LegalEntityId = input.Invoice.LegalEntityId,
                SupplierInvoiceId = input.Invoice.Id, SupplierInvoiceLineId = line.InvoiceLineId, MatchResultId = result.Id,
                ExposureStateId = exposure.Id, PurchaseOrderId = input.Order.Id, IssuedVersionId = input.Order.RevisionId,
                PurchaseOrderLineId = line.PurchaseOrderLineId, SupplierId = input.Invoice.SupplierId,
                ReceivingSourceStateId = line.AcceptanceId is { } acceptedId ? acceptanceStateIds[acceptedId] : null,
                GoodsReceiptLineId = source.GoodsReceiptLineId, ServiceAcceptanceId = source.ServiceAcceptanceId,
                CurrencyCode = input.Invoice.CurrencyCode, UnitCode = source.UnitCode, Quantity = Parse(line.Quantity, 18, 4),
                NetAmount = Parse(line.NetAmount, 38, 8), TaxAmount = Parse(line.TaxAmount, 38, 8), ExposureSequence = sequence,
                ValidationReference = $"procurement.match.{result.Id:D}", OwnerCoverageReference = owner.OwnerReference! };
        }).ToArray();
    }
    public static InvoiceMatchConsumption Consumption(InvoiceMatchAllocation row, Guid? branch)
    {
        Need(row.Kind is "Debit" or "Credit" && (row.Kind == "Debit" ? row.OriginalDebitId is null : row.OriginalDebitId is not null),
            "InvoiceAllocationKindInvalid");
        return new(row.Id, row.SupplierInvoiceId, row.SupplierInvoiceLineId, row.TenantId, row.LegalEntityId, branch,
            row.SupplierId, row.PurchaseOrderId, row.IssuedVersionId, row.PurchaseOrderLineId,
            row.PrincipalReceivingFactId ?? row.GoodsReceiptLineId ?? row.ServiceAcceptanceId,
            row.CurrencyCode, row.Kind == "Credit" ? InvoiceConsumptionKind.Credit : InvoiceConsumptionKind.Debit,
            row.OriginalDebitId, row.Quantity, row.NetAmount, row.TaxAmount, row.ValidationReference);
    }
    private static decimal Parse(string value, int precision, int scale) => ExactDecimalJsonCodec.Parse(value, precision, scale, "InvoiceMatchAmount");
    private static void Need(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
