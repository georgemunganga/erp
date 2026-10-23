using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalInvoiceMappingLine(Guid InvoiceLineId, Guid PurchaseOrderLineId,
    Guid? PrincipalReceivingSourceStateId, Guid? PrincipalAcceptanceFactId,
    long? AcceptanceSequence, string InvoiceUnitCode,
    Guid DocumentVersionId, string DocumentLineReference);

public sealed record PrincipalInvoiceMappingSnapshot(int SchemaVersion, Guid InvoiceId,
    Guid PurchaseOrderId, Guid PrincipalIssuedVersionId, int Revision,
    string OriginalInvoiceSha256, string IssuedSnapshotSha256,
    string MakerPrincipalId, Guid? MakerWorkerAliasId,
    IReadOnlyList<PrincipalInvoiceMappingLine> Lines);

/// <summary>Complete typed mapping proposal. It proves identity and source
/// membership only; the original-unit reviewer and authenticated exposure
/// owner separately determine whether a comparison may claim capacity.</summary>
public static class PrincipalInvoiceMappingEvidence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 24,
    };

    public static void VerifyRows(PrincipalInvoiceMappingVersion mapping,
        IReadOnlyList<PrincipalInvoiceMappingLine> rows)
    {
        if (mapping.MappingSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(mapping.MappingJson))
            throw new InvalidOperationException("PrincipalInvoiceMappingDigestChanged");
        PrincipalInvoiceMappingSnapshot snapshot;
        try { snapshot = JsonSerializer.Deserialize<PrincipalInvoiceMappingSnapshot>(mapping.MappingJson, Json)
            ?? throw new InvalidOperationException("PrincipalInvoiceMappingMissing"); }
        catch (JsonException error) { throw new InvalidOperationException("PrincipalInvoiceMappingMalformed", error); }
        if (snapshot.SchemaVersion != 2 || snapshot.InvoiceId != mapping.SupplierInvoiceId
            || snapshot.PurchaseOrderId != mapping.PurchaseOrderId
            || snapshot.PrincipalIssuedVersionId != mapping.PrincipalIssuedVersionId
            || snapshot.Revision != mapping.MappingRevision
            || snapshot.OriginalInvoiceSha256 != mapping.OriginalInvoiceSha256
            || snapshot.IssuedSnapshotSha256 != mapping.IssuedSnapshotSha256
            || snapshot.MakerPrincipalId != mapping.MakerPrincipalId
            || snapshot.MakerWorkerAliasId != mapping.MakerWorkerAliasId
            || snapshot.Lines.Count != rows.Count || snapshot.Lines.Count is < 1 or > 200
            || snapshot.Lines.Select(x => x.InvoiceLineId).Distinct().Count() != rows.Count)
            throw new InvalidOperationException("PrincipalInvoiceMappingHeaderChanged");
        var byLine = rows.ToDictionary(x => x.SupplierInvoiceLineId);
        foreach (var line in snapshot.Lines)
        {
            if (!byLine.TryGetValue(line.InvoiceLineId, out var row)
                || row.MappingVersionId != mapping.Id
                || row.SupplierInvoiceId != mapping.SupplierInvoiceId
                || row.PurchaseOrderId != mapping.PurchaseOrderId
                || row.PrincipalIssuedVersionId != mapping.PrincipalIssuedVersionId
                || row.PurchaseOrderLineId != line.PurchaseOrderLineId
                || row.PrincipalReceivingSourceStateId != line.PrincipalReceivingSourceStateId
                || row.PrincipalAcceptanceFactId != line.PrincipalAcceptanceFactId
                || row.AcceptanceSequence != line.AcceptanceSequence
                || row.InvoiceUnitCode != line.InvoiceUnitCode
                || row.DocumentVersionId != line.DocumentVersionId
                || row.DocumentLineReference != line.DocumentLineReference)
                throw new InvalidOperationException("PrincipalInvoiceMappingLineChanged");
        }
    }

    public static PrincipalInvoiceMappingVersion Create(SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceIssuedOrder issued,
        IReadOnlyList<InvoiceAcceptedFulfilment> accepted,
        IReadOnlyDictionary<Guid, Guid> acceptanceSourceStates,
        IReadOnlyList<PrincipalInvoiceMappingLine> lines, int revision,
        string maker, Guid? workerAlias, DateTimeOffset at)
    {
        Validate(invoice, originalLines, issued, accepted, acceptanceSourceStates, lines, revision, maker, workerAlias);
        var original = InvoiceMappingEvidence.OriginalDigest(invoice, originalLines);
        var snapshot = new PrincipalInvoiceMappingSnapshot(2, invoice.Id, issued.Id,
            issued.RevisionId, revision, original, issued.IssuedReference,
            maker, workerAlias, lines.OrderBy(x => x.InvoiceLineId).ToArray());
        var json = JsonSerializer.Serialize(snapshot, Json);
        return new PrincipalInvoiceMappingVersion
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, PurchaseOrderId = issued.Id,
            PrincipalIssuedVersionId = issued.RevisionId, BranchId = invoice.BranchRef!.Value,
            SupplierId = invoice.SupplierId, CurrencyCode = invoice.CurrencyCode,
            InvoiceVersion = invoice.Version, MappingRevision = revision,
            OriginalInvoiceSha256 = original, IssuedSnapshotSha256 = issued.IssuedReference,
            MappingJson = json, MappingSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(json),
            MakerPrincipalId = maker, MakerWorkerAliasId = workerAlias,
            MappedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(at)!.Value,
        };
    }

    public static PrincipalInvoiceMappingSnapshot Verify(PrincipalInvoiceMappingVersion row,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines,
        InvoiceIssuedOrder issued, IReadOnlyList<InvoiceAcceptedFulfilment> accepted,
        IReadOnlyDictionary<Guid, Guid> acceptanceSourceStates,
        IReadOnlyList<PrincipalInvoiceMappingLine> normalizedRows)
    {
        VerifyRows(row, normalizedRows);
        if (row.MappingSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(row.MappingJson))
            throw new InvalidOperationException("PrincipalInvoiceMappingDigestChanged");
        PrincipalInvoiceMappingSnapshot snapshot;
        try { snapshot = JsonSerializer.Deserialize<PrincipalInvoiceMappingSnapshot>(row.MappingJson, Json)
            ?? throw new InvalidOperationException("PrincipalInvoiceMappingMissing"); }
        catch (JsonException error) { throw new InvalidOperationException("PrincipalInvoiceMappingMalformed", error); }
        if (snapshot.SchemaVersion != 2 || snapshot.InvoiceId != row.SupplierInvoiceId
            || snapshot.PurchaseOrderId != row.PurchaseOrderId
            || snapshot.PrincipalIssuedVersionId != row.PrincipalIssuedVersionId
            || snapshot.Revision != row.MappingRevision
            || snapshot.OriginalInvoiceSha256 != row.OriginalInvoiceSha256
            || snapshot.IssuedSnapshotSha256 != row.IssuedSnapshotSha256
            || snapshot.MakerPrincipalId != row.MakerPrincipalId
            || snapshot.MakerWorkerAliasId != row.MakerWorkerAliasId
            || row.TenantId != invoice.TenantId || row.LegalEntityId != invoice.LegalEntityId
            || row.SupplierInvoiceId != invoice.Id || row.BranchId != invoice.BranchRef
            || row.SupplierId != invoice.SupplierId || row.CurrencyCode != invoice.CurrencyCode
            || row.InvoiceVersion != invoice.Version || row.PurchaseOrderId != issued.Id
            || row.PrincipalIssuedVersionId != issued.RevisionId
            || row.IssuedSnapshotSha256 != issued.IssuedReference
            || row.OriginalInvoiceSha256 != InvoiceMappingEvidence.OriginalDigest(invoice, originalLines))
            throw new InvalidOperationException("PrincipalInvoiceMappingChanged");
        Validate(invoice, originalLines, issued, accepted, acceptanceSourceStates, snapshot.Lines, snapshot.Revision,
            snapshot.MakerPrincipalId, snapshot.MakerWorkerAliasId);
        return snapshot;
    }

    private static void Validate(SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceIssuedOrder issued,
        IReadOnlyList<InvoiceAcceptedFulfilment> accepted,
        IReadOnlyDictionary<Guid, Guid> acceptanceSourceStates,
        IReadOnlyList<PrincipalInvoiceMappingLine> lines, int revision,
        string maker, Guid? workerAlias)
    {
        if (invoice.Id == Guid.Empty || invoice.Version < 1 || invoice.BranchRef is null
            || invoice.FinancePostingRef is not null
            || invoice.Status is not ("Received" or "MatchPending" or "Exception")
            || invoice.PurchaseOrderId is { } expected && expected != issued.Id
            || issued.Id == Guid.Empty || issued.RevisionId == Guid.Empty
            || issued.RevisionState != "Issued" || issued.RevisionNumber != 1
            || issued.TenantId != invoice.TenantId || issued.LegalEntityId != invoice.LegalEntityId
            || issued.BranchRef != invoice.BranchRef || issued.SupplierId != invoice.SupplierId
            || issued.CurrencyCode != invoice.CurrencyCode
            || issued.IssuedReference is not { Length: 64 }
            || issued.IssuedReference.Any(x => !Uri.IsHexDigit(x) || char.IsUpper(x))
            || !ProcurementPrincipalEvidence.Canonical(maker)
            || !ProcurementPrincipalEvidence.OptionalWorker(workerAlias)
            || revision < 1 || originalLines.Count is < 1 or > 200
            || originalLines.Count != lines.Count || issued.Lines.Count is < 1 or > 100
            || originalLines.Any(x => !x.IsActive || x.SupplierInvoiceId != invoice.Id
                || x.TenantId != invoice.TenantId || x.LegalEntityId != invoice.LegalEntityId)
            || originalLines.Select(x => x.Id).Distinct().Count() != originalLines.Count
            || lines.Select(x => x.InvoiceLineId).Distinct().Count() != lines.Count
            || !lines.Select(x => x.InvoiceLineId).Order().SequenceEqual(originalLines.Select(x => x.Id).Order()))
            throw new InvalidOperationException("PrincipalInvoiceMappingInputInvalid");
        foreach (var line in lines)
        {
            var orderLine = issued.Lines.SingleOrDefault(x => x.Id == line.PurchaseOrderLineId);
            var originalLine = originalLines.Single(x => x.Id == line.InvoiceLineId);
            if (orderLine is null || originalLine.PurchaseOrderLineId is { } originalOrderLineId && originalOrderLineId != orderLine.Id
                || originalLine.GoodsReceiptLineId is not null || originalLine.ServiceAcceptanceId is not null
                || line.InvoiceLineId == Guid.Empty
                || line.InvoiceUnitCode is not { Length: > 0 and <= 20 }
                || line.InvoiceUnitCode != line.InvoiceUnitCode.Trim()
                || line.DocumentVersionId == Guid.Empty
                || string.IsNullOrWhiteSpace(line.DocumentLineReference)
                || line.DocumentLineReference.Length > 200
                || line.PrincipalAcceptanceFactId.HasValue != line.PrincipalReceivingSourceStateId.HasValue
                || line.PrincipalAcceptanceFactId.HasValue != line.AcceptanceSequence.HasValue
                || line.PrincipalAcceptanceFactId == Guid.Empty
                || line.PrincipalReceivingSourceStateId == Guid.Empty
                || line.AcceptanceSequence is <= 0)
                throw new InvalidOperationException("PrincipalInvoiceMappingLineInvalid");
            if (line.PrincipalAcceptanceFactId is not { } factId) continue;
            var fact = accepted.SingleOrDefault(x => x.Id == factId);
            if (!acceptanceSourceStates.TryGetValue(factId, out var sourceStateId)
                || sourceStateId != line.PrincipalReceivingSourceStateId
                || fact is null || fact.PurchaseOrderId != issued.Id || fact.RevisionId != issued.RevisionId
                || fact.PurchaseOrderLineId != orderLine.Id || fact.Version != line.AcceptanceSequence
                || fact.TenantId != invoice.TenantId || fact.LegalEntityId != invoice.LegalEntityId
                || fact.BranchRef != invoice.BranchRef || fact.CurrencyCode != invoice.CurrencyCode
                || fact.State != "Accepted" || fact.Reversed || fact.AcceptedQuantity <= 0
                || fact.ReturnedQuantity != 0 || fact.ReturnedNetAmount != 0)
                throw new InvalidOperationException("PrincipalInvoiceAcceptanceBindingInvalid");
        }
    }
}
