using System.Text.Json;
using System.Text.Json.Serialization;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record InvoiceMatchSummary(string Outcome,string? EvidenceSha256,string? InputFingerprint,
    string SourceWatermarkSha256,IReadOnlyList<InvoiceMatchDiscrepancy> Discrepancies,IReadOnlyList<InvoiceMatchLineComparison> Lines);

/// <summary>Ordinary invoice-read projection. Immutable raw evidence contains other invoices'
/// allocations and full source facts; it is never an ordinary frontend history payload.</summary>
public static class InvoiceMatchReadProjection
{
    public static InvoiceMatchSummary Summary(InvoiceMatchResult result,IReadOnlySet<Guid> originalInvoiceLineIds)
    {
        if(result.ExecutionVersion!=1 || result.EvidenceJson is null || result.EvidenceSha256!=ProcurementEvidenceEncoding.Digest(result.EvidenceJson))
            throw new InvalidOperationException("InvoiceMatchReadEvidenceInvalid");
        using var evidence=JsonDocument.Parse(result.EvidenceJson); var root=evidence.RootElement;
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web); json.Converters.Add(new JsonStringEnumConverter());
        var issues=JsonSerializer.Deserialize<InvoiceMatchDiscrepancy[]>(root.GetProperty("discrepancies").GetRawText(),json) ?? [];
        var lines=JsonSerializer.Deserialize<InvoiceMatchLineComparison[]>(root.GetProperty("lines").GetRawText(),json) ?? [];
        return new(result.Outcome,result.EvidenceSha256,result.InputFingerprint,
            ProcurementEvidenceEncoding.PolicyJsonDigest(result.SourceWatermarksJson),
            issues.Where(x=>x.InvoiceLineId is null || originalInvoiceLineIds.Contains(x.InvoiceLineId.Value)).ToArray(),
            lines.Where(x=>originalInvoiceLineIds.Contains(x.InvoiceLineId)).ToArray());
    }
}
