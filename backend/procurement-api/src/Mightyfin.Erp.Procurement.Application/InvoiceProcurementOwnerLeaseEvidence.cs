using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Encodes a reviewed current partition. This pure encoder is never an
/// owner producer: the caller must first prove stored Core approval, baseline,
/// complete live inventory, local claims and target financial exclusions.</summary>
public static class InvoiceProcurementOwnerLeaseEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static InvoiceHistoricalExposure Encode(
        InvoiceExposureReadContext context, InvoiceProcurementOwnerBaseline baseline,
        Guid coreDecisionId, InvoiceProcurementInventorySummary current,
        IReadOnlyList<InvoiceMatchConsumption> localClaims,
        Guid exposureId, long exposureSequence, DateTimeOffset? policyEnd)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(localClaims);
        if (baseline.Id == Guid.Empty || baseline.CoreCaseId == Guid.Empty
            || coreDecisionId == Guid.Empty || exposureId == Guid.Empty || exposureSequence < 0
            || context.At == default || context.At.Offset != TimeSpan.Zero
            || context.BranchId is null || context.BranchId == Guid.Empty
            || baseline.TenantId != context.TenantId
            || baseline.LegalEntityId != context.LegalEntityId
            || current.TenantId != context.TenantId
            || current.LegalEntityId != context.LegalEntityId
            || baseline.CatalogManifestSha256 != current.ManifestSha256
            || localClaims.Any(x => x is null || x.Kind != InvoiceConsumptionKind.Debit
                || x.TenantId != context.TenantId || x.LegalEntityId != context.LegalEntityId
                || x.BranchRef != context.BranchId || x.PurchaseOrderId != context.PurchaseOrderId
                || x.RevisionId != context.IssuedVersionId || x.SupplierId != context.SupplierId
                || x.CurrencyCode != context.CurrencyCode)
            || localClaims.Select(x => x.Id).Distinct().Count() != localClaims.Count
            || localClaims.Select(x => (x.InvoiceId, x.InvoiceLineId)).Distinct().Count() != localClaims.Count
            || exposureSequence == 0 && localClaims.Count != 0
            || exposureSequence > 0 && localClaims.Count == 0)
            throw new InvalidOperationException("InvoiceOwnerLeasePartitionInvalid");
        var validUntil = ProcurementEvidenceEncoding.DatabaseTimestamp(context.At.AddMinutes(5))!.Value;
        if (policyEnd is { } end && end < validUntil) validUntil = end;
        if (validUntil <= context.At)
            throw new InvalidOperationException("InvoiceOwnerLeasePolicyExpired");
        var empty = InvoiceMatchExecutionEvidence.AllocationsDigest([]);
        var localSha = InvoiceMatchExecutionEvidence.AllocationsDigest(localClaims);
        var ownerReference = $"M6:{baseline.Id:D}:{coreDecisionId:D}";
        var watermark = ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion = 2, baselineId = baseline.Id, coreCaseId = baseline.CoreCaseId,
            currentSetSha256 = current.CompleteSetSha256,
            localClaimsSha256 = localSha, exposureId, exposureSequence
        }, Json));
        var envelope = new InvoiceProcurementOwnerExposureEnvelope(2,
            context.TenantId, context.LegalEntityId, context.BranchId,
            context.PurchaseOrderId, context.IssuedVersionId, context.SupplierId,
            context.CurrencyCode, context.PolicyRulesSha256, ownerReference,
            watermark, validUntil, empty, baseline.Id, baseline.CoreCaseId,
            baseline.CatalogManifestSha256, baseline.InventorySetSha256,
            current.CompleteSetSha256, empty, localSha, localSha, exposureSequence);
        var json = JsonSerializer.Serialize(envelope, Json);
        var owner = new InvoiceHistoricalExposure(true, "Verified", ownerReference, watermark,
            json, ProcurementEvidenceEncoding.PolicyJsonDigest(json),
            context.PolicyRulesSha256, validUntil, []);
        if (!InvoiceMatchExecutionEvidence.OwnerCovers(owner, context))
            throw new InvalidOperationException("InvoiceOwnerLeaseEvidenceChanged");
        return owner;
    }
}
