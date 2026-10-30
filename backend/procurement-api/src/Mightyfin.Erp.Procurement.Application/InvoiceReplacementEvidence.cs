using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Typed, append-only evidence for a distinct-number replacement.
/// Historical no-claim/owner completeness is a separate same-transaction proof.</summary>
public static class InvoiceReplacementEvidence
{
    public const string Operation = "InvoiceReplaceV2";
    public const string AuditAction = "InvoiceReplacementRecorded";
    public const string EventType = "supplier_invoice.replacement_recorded.v1";
    public const int MaximumResponseBytes = 262144;
    private static readonly Guid CaptureNamespace = Guid.Parse("4d6a7720-95db-5e4b-af8f-30f9ee12bd5c");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Guid CaptureCommandKey(string tenant, Guid company, Guid sourceInvoiceId, Guid replacementKey)
    {
        if (string.IsNullOrWhiteSpace(tenant) || company == Guid.Empty || sourceInvoiceId == Guid.Empty
            || replacementKey == Guid.Empty) throw new InvalidOperationException("InvoiceReplacementKeyInvalid");
        var namespaceBytes = Convert.FromHexString(CaptureNamespace.ToString("N"));
        var name = Encoding.UTF8.GetBytes($"m6e-v1|{tenant}|{company:D}|{sourceInvoiceId:D}|{replacementKey:D}");
        var combined = new byte[namespaceBytes.Length + name.Length];
        namespaceBytes.CopyTo(combined, 0); name.CopyTo(combined, namespaceBytes.Length);
        var hash = SHA1.HashData(combined);
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        var hex = Convert.ToHexStringLower(bytes);
        return Guid.ParseExact($"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}", "D");
    }

    public static string MarkerJson(InvoiceReplacementLink link)
        => JsonSerializer.Serialize(new { schemaVersion = 1, linkId = link.Id, link.SourceInvoiceId,
            link.SuccessorInvoiceId, link.CalculationId, link.CaptureReceiptId,
            link.CommandKey, link.MakerPrincipalId, link.NoClaimProofSha256,
            canPostToFinance = false }, Json);

    public static string NoClaimProofDigest(SupplierInvoice source, InvoiceMatchMappingVersion mapping,
        InvoiceMatchResult blocked, InvoiceUnitReviewSource reviewSource,
        InvoiceUnitReviewOutcome reviewOutcome, IReadOnlyList<Guid> exceptionIds)
        => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new {
            schemaVersion = 1, sourceInvoiceId = source.Id, source.Version,
            mappingId = mapping.Id, mapping.MappingSha256, blockedResultId = blocked.Id,
            blocked.EvidenceSha256, blocked.InputFingerprint, blocked.SourceWatermarksJson,
            reviewSourceId = reviewSource.Id, reviewOutcomeId = reviewOutcome.Id,
            reviewEvidenceSha256 = reviewOutcome.EvidenceSha256,
            exceptionIds = exceptionIds.Order().ToArray(), allocations = Array.Empty<Guid>()
        }, Json));

    public static string ResponseJson(InvoiceReplacementLink link, SupplierInvoice successor)
        => JsonSerializer.Serialize(new { schemaVersion = 1, sourceInvoiceId = link.SourceInvoiceId,
            successorInvoiceId = successor.Id, linkId = link.Id, calculationId = link.CalculationId,
            captureReceiptId = link.CaptureReceiptId, commandKey = link.CommandKey,
            successor.InvoiceNumber, successor.Version, successor.Status,
            canPostToFinance = false }, Json);

    public static InvoiceReplacementCommandReceipt CreateReceipt(InvoiceReplacementLink link,
        SupplierInvoice successor, string requestSha256, DateTimeOffset at)
    {
        var response = ResponseJson(link, successor);
        return new InvoiceReplacementCommandReceipt {
            TenantId = link.TenantId, LegalEntityId = link.LegalEntityId,
            LinkId = link.Id, SourceInvoiceId = link.SourceInvoiceId,
            SuccessorInvoiceId = successor.Id, ActorPrincipalId = link.MakerPrincipalId,
            CommandKey = link.CommandKey, RequestSha256 = requestSha256,
            ResponseJson = response, ResponseSha256 = ProcurementEvidenceEncoding.Digest(response),
            CompletedAtUtc = at, CreatedAt = at, CreatedBy = link.MakerPrincipalId
        };
    }

    public static void Verify(InvoiceReplacementLink link, InvoiceReplacementCommandReceipt receipt,
        SupplierInvoice source, SupplierInvoice successor, InvoiceMatchMappingVersion mapping,
        InvoiceMatchResult blocked, InvoiceUnitReviewSource reviewSource, InvoiceUnitReviewOutcome reviewOutcome,
        InvoiceAmountCalculation calculation, InvoiceIntakeCommandReceipt capture)
    {
        Need(link is not null && receipt is not null && source is not null && successor is not null
            && mapping is not null && blocked is not null && reviewSource is not null && reviewOutcome is not null
            && calculation is not null && capture is not null, "InvoiceReplacementGraphIncomplete");
        var tenant = link.TenantId; var company = link.LegalEntityId;
        Need(tenant.Length is > 0 and <= 128 && company != Guid.Empty
            && new ProcurementRecord[] { receipt, source, successor, mapping, blocked, reviewSource,
                reviewOutcome, calculation, capture }.All(x => x.TenantId == tenant && x.LegalEntityId == company)
            && link.Id != Guid.Empty && link.Version == 1 && link.UpdatedAt is null && link.UpdatedBy is null
            && source.Id == link.SourceInvoiceId && successor.Id == link.SuccessorInvoiceId
            && source.Id != successor.Id && source.Version == link.SourceInvoiceVersion
            && source.CreatedBy == link.MakerPrincipalId && successor.CreatedBy == link.MakerPrincipalId
            && Guid.TryParseExact(link.MakerPrincipalId, "D", out var actor) && actor != Guid.Empty
            && source.SupplierId == successor.SupplierId && source.SupplierId == link.SupplierId
            && source.CurrencyCode == successor.CurrencyCode && source.CurrencyCode == link.CurrencyCode
            && source.BranchRef == successor.BranchRef && source.BranchRef == link.BranchId
            && (source.PurchaseOrderId is null || source.PurchaseOrderId == link.PurchaseOrderId)
            && successor.PurchaseOrderId == link.PurchaseOrderId
            && source.Status == "Received" && successor.Status == "Received"
            && successor.Version == 1 && successor.MappingRevision == 0
            && successor.CurrentMappingVersionId is null && successor.LastMatchResultId is null
            && successor.ActiveMatchClaimResultId is null && successor.FinancePostingRef is null
            && source.NormalizedInvoiceNumber != successor.NormalizedInvoiceNumber
            && source.ActiveMatchClaimResultId is null && source.FinancePostingRef is null
            && mapping.Id == link.SourceMappingVersionId && mapping.SupplierInvoiceId == source.Id
            && mapping.MappingRevision == link.SourceMappingRevision && mapping.SourceSchemaVersion == 3
            && mapping.PurchaseOrderId == link.PurchaseOrderId && mapping.IssuedVersionId == link.IssuedVersionId
            && mapping.InvoiceOriginalSha256 == link.SourceOriginalSha256
            && source.CurrentMappingVersionId == mapping.Id && source.MappingRevision == mapping.MappingRevision
            && blocked.Id == link.BlockedResultId && blocked.SupplierInvoiceId == source.Id
            && blocked.MappingVersionId == mapping.Id && blocked.PurchaseOrderId == link.PurchaseOrderId
            && blocked.IssuedVersionId == link.IssuedVersionId && blocked.SourceSchemaVersion == 3
            && blocked.ExecutionVersion == 2 && blocked.Outcome == "Blocked"
            && source.LastMatchResultId == blocked.Id && source.MatchingState == "ComparisonBlocked"
            && reviewSource.Id == link.ReviewSourceId && reviewSource.SupplierInvoiceId == source.Id
            && reviewSource.MappingVersionId == mapping.Id && reviewSource.PurchaseOrderId == link.PurchaseOrderId
            && reviewOutcome.Id == link.ReviewOutcomeId && reviewOutcome.ReviewSourceId == reviewSource.Id
            && reviewOutcome.State == "Verified"
            && calculation.Id == link.CalculationId && calculation.SupplierInvoiceId == successor.Id
            && capture.Id == link.CaptureReceiptId && capture.SupplierInvoiceId == successor.Id
            && capture.CalculationId == calculation.Id && capture.CommandKey == link.CaptureCommandKey
            && capture.ActorPrincipalId == link.MakerPrincipalId
            && link.CaptureCommandKey == CaptureCommandKey(tenant, company, source.Id, link.CommandKey)
            && link.CommandKey != Guid.Empty && link.CaptureCommandKey != link.CommandKey
            && Hex64(link.SourceOriginalSha256) && Hex64(link.NoClaimProofSha256)
            && !string.IsNullOrWhiteSpace(link.Reason) && link.Reason == link.Reason.Trim()
            && link.Reason.Length <= 1000 && link.RecordedAtUtc.Offset == TimeSpan.Zero
            && link.CreatedAt >= link.RecordedAtUtc && link.CreatedBy == link.MakerPrincipalId,
            "InvoiceReplacementLinkInvalid");
        Need(receipt.Id != Guid.Empty && receipt.Version == 1 && receipt.UpdatedAt is null
            && receipt.UpdatedBy is null && receipt.Operation == Operation
            && receipt.LinkId == link.Id && receipt.SourceInvoiceId == source.Id
            && receipt.SuccessorInvoiceId == successor.Id && receipt.CommandKey == link.CommandKey
            && receipt.ActorPrincipalId == link.MakerPrincipalId && receipt.CreatedBy == link.MakerPrincipalId
            && receipt.ResponseStatus == 201 && Hex64(receipt.RequestSha256)
            && Hex64(receipt.ResponseSha256) && receipt.CompletedAtUtc.Offset == TimeSpan.Zero
            && receipt.CompletedAtUtc >= link.RecordedAtUtc && receipt.CreatedAt >= receipt.CompletedAtUtc
            && Encoding.UTF8.GetByteCount(receipt.ResponseJson) is > 0 and <= MaximumResponseBytes
            && receipt.ResponseSha256 == ProcurementEvidenceEncoding.Digest(receipt.ResponseJson)
            && receipt.ResponseJson == ResponseJson(link, successor), "InvoiceReplacementReceiptInvalid");
    }

    private static bool Hex64(string? text) => text is { Length: 64 }
        && text.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Need(bool accepted, string code)
    { if (!accepted) throw new InvalidOperationException(code); }
}
