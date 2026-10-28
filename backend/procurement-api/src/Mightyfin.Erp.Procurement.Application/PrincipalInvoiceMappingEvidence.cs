using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>New principal-source payload. Legacy InvoiceMappingLine JSON is never extended.</summary>
public sealed record PrincipalInvoiceMappingLine(
    Guid InvoiceLineId, Guid PurchaseOrderLineId,
    Guid? PrincipalReceivingSourceStateId, Guid? PrincipalReceivingFactId,
    string InvoiceUnitCode, Guid DocumentVersionId, string DocumentLineReference);

public sealed record PrincipalInvoiceMappingSnapshot(
    int SchemaVersion, Guid InvoiceId, Guid PurchaseOrderId, Guid IssuedVersionId,
    int Revision, string InvoiceOriginalSha256, string ActorPrincipalId,
    Guid? ActorWorkerId, IReadOnlyList<PrincipalInvoiceMappingLine> Lines);

public sealed record PrincipalInvoiceMappingPayload(
    string Json, string Sha256, PrincipalInvoiceMappingSnapshot Snapshot);

public static class PrincipalInvoiceMappingEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 16
    };

    public static PrincipalInvoiceMappingPayload Create(
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines,
        Guid purchaseOrderId, Guid issuedVersionId,
        IReadOnlyList<PrincipalInvoiceMappingLine> lines,
        string actorPrincipalId, Guid? actorWorkerId)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(originalLines);
        ArgumentNullException.ThrowIfNull(lines);
        var snapshot = new PrincipalInvoiceMappingSnapshot(3, invoice.Id, purchaseOrderId,
            issuedVersionId, checked(invoice.MappingRevision + 1),
            InvoiceMappingEvidence.OriginalDigest(invoice, originalLines), actorPrincipalId,
            actorWorkerId, lines.OrderBy(x => x.InvoiceLineId).ToArray());
        Validate(snapshot, invoice, originalLines);
        var json = JsonSerializer.Serialize(snapshot, Json);
        return new(json, ProcurementEvidenceEncoding.PolicyJsonDigest(json), snapshot);
    }

    public static PrincipalInvoiceMappingSnapshot Verify(
        string json, string sha256, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines,
        Guid purchaseOrderId, Guid issuedVersionId, int revision)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1_048_576
            || ProcurementEvidenceEncoding.PolicyJsonDigest(json) != sha256)
            throw new InvalidOperationException("PrincipalInvoiceMappingEvidenceChanged");
        PrincipalInvoiceMappingSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<PrincipalInvoiceMappingSnapshot>(json, Json)
                ?? throw new InvalidOperationException("PrincipalInvoiceMappingEvidenceChanged");
        }
        catch (JsonException error)
        { throw new InvalidOperationException("PrincipalInvoiceMappingEvidenceChanged", error); }
        if (JsonSerializer.Serialize(snapshot, Json) != json
            || snapshot.PurchaseOrderId != purchaseOrderId
            || snapshot.IssuedVersionId != issuedVersionId || snapshot.Revision != revision)
            throw new InvalidOperationException("PrincipalInvoiceMappingEvidenceChanged");
        Validate(snapshot, invoice, originalLines);
        return snapshot;
    }

    private static void Validate(PrincipalInvoiceMappingSnapshot snapshot,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines)
    {
        var originals = originalLines.Where(x => x.IsActive).ToArray();
        if (snapshot.SchemaVersion != 3 || invoice.Id == Guid.Empty
            || snapshot.InvoiceId != invoice.Id || snapshot.PurchaseOrderId == Guid.Empty
            || snapshot.IssuedVersionId == Guid.Empty || snapshot.Revision <= 0
            || invoice.Status is not ("Received" or "MatchPending" or "Exception")
            || invoice.FinancePostingRef is not null
            || invoice.PurchaseOrderId is { } originalOrder && originalOrder != snapshot.PurchaseOrderId
            || invoice.BranchRef is null || invoice.BranchRef == Guid.Empty
            || snapshot.InvoiceOriginalSha256 != InvoiceMappingEvidence.OriginalDigest(invoice, originalLines)
            || !ProcurementPrincipalEvidence.Canonical(snapshot.ActorPrincipalId)
            || !ProcurementPrincipalEvidence.OptionalWorker(snapshot.ActorWorkerId)
            || snapshot.Lines is null || originals.Length is < 1 or > 200
            || snapshot.Lines.Count != originals.Length
            || snapshot.Lines.Select(x => x.InvoiceLineId).Distinct().Count() != originals.Length
            || !snapshot.Lines.Select(x => x.InvoiceLineId).Order()
                .SequenceEqual(originals.Select(x => x.Id).Order())
            || originals.Any(x => x.SupplierInvoiceId != invoice.Id
                || x.TenantId != invoice.TenantId || x.LegalEntityId != invoice.LegalEntityId))
            throw new InvalidOperationException("PrincipalInvoiceMappingInputInvalid");
        foreach (var line in snapshot.Lines)
            if (line.PurchaseOrderLineId == Guid.Empty
                || line.PrincipalReceivingFactId.HasValue != line.PrincipalReceivingSourceStateId.HasValue
                || line.PrincipalReceivingFactId == Guid.Empty
                || line.PrincipalReceivingSourceStateId == Guid.Empty
                || (line.PrincipalReceivingFactId.HasValue
                    && (line.DocumentVersionId == Guid.Empty || string.IsNullOrWhiteSpace(line.DocumentLineReference)))
                || (!line.PrincipalReceivingFactId.HasValue && line.DocumentVersionId != Guid.Empty)
                || string.IsNullOrWhiteSpace(line.InvoiceUnitCode)
                || line.InvoiceUnitCode.Length > 20 || line.InvoiceUnitCode != line.InvoiceUnitCode.Trim()
                || line.DocumentLineReference is null || line.DocumentLineReference.Length > 200)
                throw new InvalidOperationException("PrincipalInvoiceMappingInputInvalid");
    }
}
