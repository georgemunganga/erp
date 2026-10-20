using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public static class InvoiceMappingEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string OriginalDigest(SupplierInvoice invoice, IEnumerable<SupplierInvoiceLine> lines)
        => ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, invoice.Id, invoice.TenantId, invoice.LegalEntityId, invoice.BranchRef, invoice.SupplierId,
            invoice.PurchaseOrderId, invoice.InvoiceNumber, invoice.NormalizedInvoiceNumber, invoice.InvoiceDate, invoice.DueDate,
            invoice.CurrencyCode, net = Exact(invoice.NetAmount), tax = Exact(invoice.TaxAmount), gross = Exact(invoice.GrossAmount),
            invoice.EvidenceDocumentRefId, invoice.CreditForInvoiceRef,
            lines = lines.OrderBy(x => x.Id).Select(x => new { x.Id, x.SupplierInvoiceId, x.TenantId, x.LegalEntityId,
                x.LineNumber, x.Description, quantity = Exact(x.Quantity), unitPrice = Exact(x.UnitPrice), tax = Exact(x.TaxAmount),
                x.TaxCodeRef, x.CostCenterRef }).ToArray(),
        }, Json));

    public static InvoiceMatchMappingVersion Create(SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines,
        Guid orderId, Guid issuedId, IReadOnlyList<InvoiceMappingLine> lines, string actor, Guid worker, DateTimeOffset at)
    {
        Need(invoice.Id != Guid.Empty && orderId != Guid.Empty && issuedId != Guid.Empty && worker != Guid.Empty
            && !string.IsNullOrWhiteSpace(actor) && at != default && invoice.FinancePostingRef is null
            && invoice.Status is "Received" or "MatchPending" or "Exception", "InvoiceMappingSourceUnavailable");
        Need(invoice.PurchaseOrderId is null || invoice.PurchaseOrderId == orderId, "InvoiceMappingOrderChanged");
        Need(originalLines.Count is > 0 and <= 200 && originalLines.All(x => x.SupplierInvoiceId == invoice.Id
                && x.TenantId == invoice.TenantId && x.LegalEntityId == invoice.LegalEntityId)
            && originalLines.Select(x => x.Id).Distinct().Count() == originalLines.Count
            && lines.Count == originalLines.Count && lines.Select(x => x.InvoiceLineId).Distinct().Count() == lines.Count
            && lines.Select(x => x.InvoiceLineId).Order().SequenceEqual(originalLines.Select(x => x.Id).Order()), "InvoiceMappingLineSetInvalid");
        Need(lines.All(x => x.PurchaseOrderLineId != Guid.Empty && !(x.GoodsReceiptLineId.HasValue && x.ServiceAcceptanceId.HasValue)
            && x.InvoiceUnitCode is { Length: > 0 and <= 20 } && x.InvoiceUnitCode == x.InvoiceUnitCode.Trim()
            && x.DocumentLineReference is not null && x.DocumentLineReference.Length <= 200), "InvoiceMappingInputInvalid");
        var revision = checked(invoice.MappingRevision + 1);
        var digest = OriginalDigest(invoice, originalLines);
        var snapshot = new InvoiceMappingSnapshot(1, invoice.Id, orderId, issuedId, revision, digest, lines.OrderBy(x => x.InvoiceLineId).ToArray());
        var json = JsonSerializer.Serialize(snapshot, Json);
        return new() { TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId, SupplierInvoiceId = invoice.Id,
            PurchaseOrderId = orderId, IssuedVersionId = issuedId, MappingRevision = revision, InvoiceOriginalSha256 = digest,
            MappingJson = json, MappingSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(json), VerificationState = "Unverified",
            ActorPrincipalId = actor, ActorWorkerId = worker, ReviewedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(at)!.Value };
    }

    public static InvoiceMappingSnapshot Verify(InvoiceMatchMappingVersion mapping, SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> lines)
    {
        Need(mapping.SupplierInvoiceId == invoice.Id && mapping.TenantId == invoice.TenantId && mapping.LegalEntityId == invoice.LegalEntityId
            && mapping.MappingRevision > 0 && mapping.InvoiceOriginalSha256 == OriginalDigest(invoice, lines)
            && mapping.MappingSha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(mapping.MappingJson), "InvoiceMappingEvidenceChanged");
        var snapshot = JsonSerializer.Deserialize<InvoiceMappingSnapshot>(mapping.MappingJson, Json) ?? throw new InvalidOperationException("InvoiceMappingEvidenceChanged");
        Need(snapshot.SchemaVersion == 1 && snapshot.InvoiceId == invoice.Id && snapshot.PurchaseOrderId == mapping.PurchaseOrderId
            && snapshot.IssuedVersionId == mapping.IssuedVersionId && snapshot.Revision == mapping.MappingRevision
            && snapshot.InvoiceOriginalSha256 == mapping.InvoiceOriginalSha256
            && snapshot.Lines.Count == lines.Count && snapshot.Lines.Select(x => x.InvoiceLineId).Distinct().Count() == lines.Count
            && snapshot.Lines.Select(x => x.InvoiceLineId).Order().SequenceEqual(lines.Select(x => x.Id).Order()), "InvoiceMappingEvidenceChanged");
        return snapshot;
    }

    public static InvoiceMatchDocument Project(InvoiceMatchMappingVersion mapping, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceMappingReview review)
    {
        var snapshot = Verify(mapping, invoice, originalLines);
        var verified = review.Verified && review.State == "Verified" && review.EvidenceJson != "{}"
            && ProcurementEvidenceEncoding.PolicyJsonDigest(review.EvidenceJson).Length == 64
            && review.VerifiedUnits.Count == snapshot.Lines.Count && snapshot.Lines.All(x => x.DocumentVersionId != Guid.Empty
                && !string.IsNullOrWhiteSpace(x.DocumentLineReference)
                && review.VerifiedUnits.TryGetValue(x.InvoiceLineId, out var unit) && unit == x.InvoiceUnitCode);
        Need(!review.Verified || verified, "InvoiceUnitReviewEvidenceInvalid");
        var document = InvoiceMatchDocument.From(invoice, originalLines);
        return document with { PurchaseOrderId = mapping.PurchaseOrderId, Lines = document.Lines.Select(line =>
        {
            var link = snapshot.Lines.Single(x => x.InvoiceLineId == line.Id);
            return line with { PurchaseOrderLineId = link.PurchaseOrderLineId, GoodsReceiptLineId = link.GoodsReceiptLineId,
                ServiceAcceptanceId = link.ServiceAcceptanceId, UnitCode = verified ? review.VerifiedUnits[line.Id] : "" };
        }).ToArray() };
    }
    private static string Exact(decimal value) => ProcurementEvidenceEncoding.Decimal(value);
    private static void Need(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
