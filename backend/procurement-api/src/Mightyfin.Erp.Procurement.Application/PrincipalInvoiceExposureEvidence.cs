using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalInvoiceActiveLocalClaim(PrincipalInvoiceClaim Claim,
    SupplierInvoice Invoice, PrincipalInvoiceState State);
public sealed record PrincipalInvoiceOwnedComparison(PrincipalInvoiceComparison Comparison,
    PrincipalInvoiceMappingVersion Mapping, PrincipalInvoiceUnitReviewOutcome Review,
    SupplierInvoice Invoice, PrincipalInvoiceState State, PolicyVersion MatchingPolicy,
    IReadOnlyList<SupplierInvoiceLine> OriginalLines,
    IReadOnlyList<PrincipalInvoiceMappingLine> MappingLines,
    IReadOnlyList<PrincipalInvoiceClaim> Claims);
public sealed record PrincipalInvoiceFrozenComparison(int SchemaVersion, Guid InvoiceId,
    Guid MappingVersionId, Guid OutcomeId, Guid IssuedVersionId, Guid PolicyId,
    string PolicySha256, string OwnerReference, string OwnerWatermark,
    string OwnerEvidenceSha256,
    string ConsumptionWatermark, string DuplicatesWatermark,
    IReadOnlyList<PrincipalInvoiceComparisonLine> Lines, string? Reason);
public sealed record PrincipalInvoiceOwnedHistory(InvoiceHistoricalExposure Owner,
    InvoiceConsumptionEvidence Consumption);

/// <summary>Completes a source-wide owner checkpoint with this module's active
/// typed claims. Unknown history, overlap or a stale active pointer is a hard
/// blocker; no local empty list is treated as complete Finance history.</summary>
public static class PrincipalInvoiceExposureEvidence
{
    /// <summary>Builds an authenticated Procurement owner envelope from a complete
    /// source-wide owning-store read. The caller must hold the source row lock in
    /// one serializable transaction and supply unfiltered same-company counts for
    /// every competing legacy/credit path. Any unknown path blocks zero claims.</summary>
    public static PrincipalInvoiceOwnedHistory Reconstruct(InvoiceExposureReadContext context,
        PrincipalInvoiceExposureState exposure,
        IReadOnlyList<PrincipalInvoiceOwnedComparison> comparisons,
        int allClaimRowsCount,
        int legacyAllocationCount, int legacyMatchCount, int unallocatedCreditCount,
        int legacyFulfilmentCount, DateTimeOffset observedAt)
    {
        if (context.At != observedAt || context.TenantId != exposure.TenantId
            || context.LegalEntityId != exposure.LegalEntityId
            || context.BranchId != exposure.BranchId
            || context.PurchaseOrderId != exposure.PurchaseOrderId
            || context.IssuedVersionId != exposure.PrincipalIssuedVersionId
            || context.SupplierId != exposure.SupplierId || context.CurrencyCode != exposure.CurrencyCode
            || context.PolicyRulesSha256 is not { Length: 64 }
            || exposure.Sequence < 0 || comparisons is null || allClaimRowsCount < 0
            || legacyAllocationCount != 0 || legacyMatchCount != 0
            || unallocatedCreditCount != 0 || legacyFulfilmentCount != 0)
            throw new InvalidOperationException("PrincipalInvoiceSourceHistoryIncomplete");
        var rows = comparisons.OrderBy(x => x.Comparison.Id).ToArray();
        if (rows.Select(x => x.Comparison.Id).Distinct().Count() != rows.Length
            || rows.Select(x => x.Invoice.Id).Distinct().Count() != rows.Length
            || rows.Select(x => x.Comparison.OwnerWatermark).Distinct().Count() != rows.Length
            || rows.Select(x => x.Claims.FirstOrDefault()?.ExposureSequence).Distinct().Count() != rows.Length
            || !rows.Select(x => x.Claims.FirstOrDefault()?.ExposureSequence).Order()
                .SequenceEqual(Enumerable.Range(1, rows.Length).Select(x => (long?)x))
            || rows.Length != exposure.Sequence)
            throw new InvalidOperationException("PrincipalInvoiceRetainedComparisonSetChanged");
        if (rows.Length == 0)
        {
            if (exposure.CoverageState != "Unknown" || exposure.OwnerReference is not null
                || exposure.OwnerWatermark is not null || exposure.OwnerEvidenceSha256 is not null
                || exposure.OwnerValidUntil is not null)
                throw new InvalidOperationException("PrincipalInvoiceEmptyExposureCursorChanged");
        }
        else
        {
            var last = rows.Single(x => x.Claims[0].ExposureSequence == exposure.Sequence);
            PrincipalInvoiceFrozenComparison lastEvidence;
            try { lastEvidence = JsonSerializer.Deserialize<PrincipalInvoiceFrozenComparison>(
                last.Comparison.EvidenceJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException("PrincipalInvoiceLastComparisonMissing"); }
            catch (JsonException error) { throw new InvalidOperationException("PrincipalInvoiceLastComparisonMalformed", error); }
            if (exposure.CoverageState != "Verified"
                || exposure.OwnerReference != last.Comparison.OwnerReference
                || exposure.OwnerWatermark != last.Comparison.OwnerWatermark
                || exposure.OwnerEvidenceSha256 != lastEvidence.OwnerEvidenceSha256
                || exposure.OwnerValidUntil is not { } until || until <= last.Comparison.ComparedAt)
                throw new InvalidOperationException("PrincipalInvoiceExposureCursorChanged");
        }
        var consumption = new List<InvoiceMatchConsumption>();
        foreach (var row in rows)
        {
            var compare = row.Comparison; var invoice = row.Invoice;
            var mapping = row.Mapping; var review = row.Review; var state = row.State;
            PrincipalInvoiceFrozenComparison frozen;
            try { frozen = JsonSerializer.Deserialize<PrincipalInvoiceFrozenComparison>(compare.EvidenceJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException("PrincipalInvoiceFrozenComparisonMissing"); }
            catch (JsonException error) { throw new InvalidOperationException("PrincipalInvoiceFrozenComparisonMalformed", error); }
            if (compare.Outcome != "Compared" || compare.SupplierInvoiceId != invoice.Id
                || compare.MappingVersionId != mapping.Id || compare.ReviewOutcomeId != review.Id
                || compare.ReviewSourceId != review.ReviewSourceId
                || compare.ExposureStateId != exposure.Id || compare.PurchaseOrderId != exposure.PurchaseOrderId
                || compare.PrincipalIssuedVersionId != exposure.PrincipalIssuedVersionId
                || compare.InvoiceVersion != invoice.Version
                || compare.OriginalInvoiceSha256 != InvoiceMappingEvidence.OriginalDigest(invoice, row.OriginalLines)
                || compare.OriginalInvoiceSha256 != mapping.OriginalInvoiceSha256
                || compare.EvidenceSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(compare.EvidenceJson)
                || frozen.SchemaVersion != 2 || frozen.InvoiceId != invoice.Id
                || frozen.MappingVersionId != mapping.Id || frozen.OutcomeId != review.Id
                || frozen.IssuedVersionId != context.IssuedVersionId
                || frozen.PolicyId != row.MatchingPolicy.Id
                || frozen.PolicySha256 != compare.PolicyRulesSha256
                || frozen.OwnerReference != compare.OwnerReference
                || frozen.OwnerWatermark != compare.OwnerWatermark
                || frozen.OwnerEvidenceSha256 is not { Length: 64 }
                || frozen.Lines is null || frozen.Lines.Count != row.Claims.Count
                || frozen.Lines.Select(x => x.InvoiceLineId).Distinct().Count() != frozen.Lines.Count
                || string.IsNullOrWhiteSpace(compare.OwnerReference)
                || compare.OwnerWatermark is not { Length: 64 }
                || review.State != "Verified" || state.SupplierInvoiceId != invoice.Id
                || state.ActiveClaimComparisonId != compare.Id || state.LastComparisonId != compare.Id
                || state.CurrentMappingId != mapping.Id || state.State != "Compared"
                || invoice.TenantId != context.TenantId || invoice.LegalEntityId != context.LegalEntityId
                || invoice.BranchRef != context.BranchId || invoice.SupplierId != context.SupplierId
                || invoice.CurrencyCode != context.CurrencyCode || row.Claims.Count != row.OriginalLines.Count
                || row.Claims.Select(x => x.SupplierInvoiceLineId).Distinct().Count() != row.Claims.Count
                || row.MappingLines.Count != row.Claims.Count
                || row.MappingLines.Select(x => x.SupplierInvoiceLineId).Distinct().Count() != row.MappingLines.Count
                || !row.Claims.Select(x => x.SupplierInvoiceLineId).Order()
                    .SequenceEqual(row.OriginalLines.Select(x => x.Id).Order()))
                throw new InvalidOperationException("PrincipalInvoiceRetainedClaimGraphChanged");
            foreach (var claim in row.Claims)
            {
                var binding = row.MappingLines.Single(x => x.SupplierInvoiceLineId == claim.SupplierInvoiceLineId);
                var line = frozen.Lines.Single(x => x.InvoiceLineId == claim.SupplierInvoiceLineId);
                var original = row.OriginalLines.Single(x => x.Id == claim.SupplierInvoiceLineId);
                var policy = InvoiceMatchingPolicyRules.Project(row.MatchingPolicy);
                var calculatedNet = InvoiceMatchExact.ProductMoney(InvoiceMatchExact.Units(original.Quantity),
                    InvoiceMatchExact.Units(original.UnitPrice), policy);
                if (claim.ComparisonId != compare.Id || claim.ExposureStateId != exposure.Id
                    || claim.MappingVersionId != mapping.Id
                    || claim.SupplierInvoiceId != invoice.Id
                    || claim.PurchaseOrderId != context.PurchaseOrderId
                    || claim.PrincipalIssuedVersionId != context.IssuedVersionId
                    || claim.SupplierId != context.SupplierId || claim.CurrencyCode != context.CurrencyCode
                    || claim.ExposureSequence <= 0 || claim.ExposureSequence > exposure.Sequence
                    || claim.ExposureSequence != row.Claims[0].ExposureSequence
                    || claim.OwnerCoverageReference != compare.OwnerReference
                    || claim.PurchaseOrderLineId != binding.PurchaseOrderLineId
                    || claim.PrincipalAcceptanceFactId != binding.PrincipalAcceptanceFactId
                    || claim.PrincipalReceivingSourceStateId != binding.PrincipalReceivingSourceStateId
                    || claim.AcceptanceSequence != binding.AcceptanceSequence
                    || claim.UnitCode != binding.InvoiceUnitCode
                    || claim.Quantity != original.Quantity
                    || InvoiceMatchExact.Units(claim.NetAmount) != calculatedNet
                    || claim.TaxAmount != original.TaxAmount
                    || line.PurchaseOrderLineId != claim.PurchaseOrderLineId
                    || line.PrincipalAcceptanceFactId != claim.PrincipalAcceptanceFactId
                    || line.QuantityExact != InvoiceMatchExact.Text(InvoiceMatchExact.Units(claim.Quantity))
                    || line.NetExact != InvoiceMatchExact.Text(InvoiceMatchExact.Units(claim.NetAmount))
                    || line.TaxExact != InvoiceMatchExact.Text(InvoiceMatchExact.Units(claim.TaxAmount))
                    || claim.Quantity <= 0 || claim.NetAmount < 0 || claim.TaxAmount < 0
                    || !InvoiceMatchExact.Input(claim.Quantity, true)
                    || !InvoiceMatchExact.AcceptedNetInput(claim.NetAmount)
                    || !InvoiceMatchExact.AcceptedNetInput(claim.TaxAmount))
                    throw new InvalidOperationException("PrincipalInvoiceRetainedClaimChanged");
                consumption.Add(new InvoiceMatchConsumption(claim.Id, invoice.Id,
                    claim.SupplierInvoiceLineId, claim.TenantId, claim.LegalEntityId, invoice.BranchRef,
                    claim.SupplierId, claim.PurchaseOrderId, claim.PrincipalIssuedVersionId,
                    claim.PurchaseOrderLineId, claim.PrincipalAcceptanceFactId, claim.CurrencyCode,
                    InvoiceConsumptionKind.Debit, null, claim.Quantity, claim.NetAmount,
                    claim.TaxAmount, claim.OwnerCoverageReference));
            }
        }
        if (consumption.Count != allClaimRowsCount
            || consumption.Select(x => x.Id).Distinct().Count() != consumption.Count)
            throw new InvalidOperationException("PrincipalInvoiceRetainedClaimRepeated");
        var ordered = consumption.OrderBy(x => x.Id).ToArray();
        var digest = InvoiceMatchExecutionEvidence.AllocationsDigest(ordered);
        var watermark = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion = 2, exposure.Id, exposure.Sequence, context.PurchaseOrderId,
            context.IssuedVersionId, comparisonIds = rows.Select(x => x.Comparison.Id).ToArray(),
            allocationsSha256 = digest
        }));
        var reference = $"procurement-principal-invoice:{exposure.Id:D}";
        var until = observedAt.AddMinutes(5);
        var envelope = new InvoiceOwnerExposureEnvelope(1, context.TenantId, context.LegalEntityId,
            context.BranchId, context.PurchaseOrderId, context.IssuedVersionId,
            context.SupplierId, context.CurrencyCode, context.PolicyRulesSha256,
            reference, watermark, until, digest);
        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var owner = new InvoiceHistoricalExposure(true, "Verified", reference, watermark,
            json, ProcurementEvidenceEncoding.PolicyJsonDigest(json), context.PolicyRulesSha256,
            until, ordered);
        if (!InvoiceMatchExecutionEvidence.OwnerCovers(owner, context))
            throw new InvalidOperationException("PrincipalInvoiceOwnerEnvelopeInvalid");
        return new(owner, new(watermark, true, ordered));
    }

    public static InvoiceConsumptionEvidence CompleteHistory(InvoiceHistoricalExposure owner,
        InvoiceExposureReadContext context, PrincipalInvoiceExposureState exposure,
        IReadOnlyList<PrincipalInvoiceActiveLocalClaim> activeLocal)
    {
        if (!InvoiceMatchExecutionEvidence.OwnerCovers(owner, context)
            || exposure.Id == Guid.Empty || exposure.Sequence < 0
            || exposure.TenantId != context.TenantId || exposure.LegalEntityId != context.LegalEntityId
            || exposure.PurchaseOrderId != context.PurchaseOrderId
            || exposure.PrincipalIssuedVersionId != context.IssuedVersionId
            || exposure.SupplierId != context.SupplierId || exposure.BranchId != context.BranchId
            || exposure.CurrencyCode != context.CurrencyCode)
            throw new InvalidOperationException("PrincipalInvoiceHistoricalExposureUnavailable");
        var local = activeLocal.Select(row =>
        {
            var claim = row.Claim; var invoice = row.Invoice; var state = row.State;
            if (claim.Id == Guid.Empty || invoice.Id != claim.SupplierInvoiceId
                || state.SupplierInvoiceId != invoice.Id
                || state.ActiveClaimComparisonId != claim.ComparisonId
                || claim.TenantId != context.TenantId || claim.LegalEntityId != context.LegalEntityId
                || invoice.TenantId != context.TenantId || invoice.LegalEntityId != context.LegalEntityId
                || invoice.BranchRef != context.BranchId || invoice.SupplierId != context.SupplierId
                || invoice.CurrencyCode != context.CurrencyCode
                || claim.PurchaseOrderId != context.PurchaseOrderId
                || claim.PrincipalIssuedVersionId != context.IssuedVersionId
                || claim.MappingVersionId == Guid.Empty
                || claim.ExposureStateId != exposure.Id || claim.ExposureSequence <= 0
                || claim.ExposureSequence > exposure.Sequence || claim.SupplierId != context.SupplierId
                || claim.CurrencyCode != context.CurrencyCode || claim.SupplierInvoiceLineId == Guid.Empty
                || claim.PurchaseOrderLineId == Guid.Empty || claim.Quantity <= 0
                || claim.NetAmount < 0 || claim.TaxAmount < 0
                || !InvoiceMatchExact.Input(claim.Quantity, true)
                || !InvoiceMatchExact.AcceptedNetInput(claim.NetAmount)
                || !InvoiceMatchExact.AcceptedNetInput(claim.TaxAmount))
                throw new InvalidOperationException("PrincipalInvoiceLocalClaimChanged");
            return new InvoiceMatchConsumption(claim.Id, invoice.Id, claim.SupplierInvoiceLineId,
                claim.TenantId, claim.LegalEntityId, invoice.BranchRef, claim.SupplierId,
                claim.PurchaseOrderId, claim.PrincipalIssuedVersionId, claim.PurchaseOrderLineId,
                claim.PrincipalAcceptanceFactId, claim.CurrencyCode, InvoiceConsumptionKind.Debit,
                null, claim.Quantity, claim.NetAmount, claim.TaxAmount, claim.OwnerCoverageReference);
        }).ToArray();
        var all = owner.Allocations.Concat(local).OrderBy(x => x.Id).ToArray();
        if (all.Select(x => x.Id).Distinct().Count() != all.Length
            || all.Select(x => (x.InvoiceId, x.InvoiceLineId)).Distinct().Count() != all.Length)
            throw new InvalidOperationException("PrincipalInvoiceOwnerLocalHistoryOverlap");
        var watermark = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion = 2, owner.OwnerReference, owner.Watermark,
            exposure.Id, exposure.Sequence,
            allocations = all.Select(x => new { x.Id, x.InvoiceId, x.InvoiceLineId, x.PurchaseOrderLineId,
                x.AcceptanceId, x.Kind, x.OriginalDebitId,
                quantity = ProcurementEvidenceEncoding.Decimal(x.Quantity),
                netAmount = ProcurementEvidenceEncoding.Decimal(x.NetAmount),
                taxAmount = ProcurementEvidenceEncoding.Decimal(x.TaxAmount) }).ToArray()
        }));
        return new(watermark, true, all);
    }
}
