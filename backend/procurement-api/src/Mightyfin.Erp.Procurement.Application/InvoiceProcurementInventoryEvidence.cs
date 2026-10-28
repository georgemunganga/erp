using System.Text.Json;
using System.Text;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Canonicalize the actual server-read inventory before classification.
/// This proves neither Core approval nor which old rows can bear a claim.</summary>
public static class InvoiceProcurementInventoryEvidence
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static InvoiceProcurementInventorySummary Summarize(InvoiceProcurementInventoryCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var expected=InvoiceProcurementOwnerTables.Names;
        if(string.IsNullOrWhiteSpace(capture.TenantId) || capture.LegalEntityId==Guid.Empty
            || capture.Tables is null || capture.Tables.Count!=expected.Count
            || capture.Tables.Any(x=>x is null)
            || !capture.Tables.Select(x=>x.TableName).SequenceEqual(expected))
            throw new InvalidOperationException("InvoiceOwnerInventoryManifestChanged");
        var summaries=new List<InvoiceProcurementBaselineTable>(expected.Count);
        var total=0;
        var totalBytes=0L;
        foreach(var table in capture.Tables)
        {
            if(table.Rows is null || table.Rows.Count>100_000
                || table.Rows.Any(x=>x is null || x.Id==Guid.Empty || x.Version<1
                    || x.RowJson is not {Length:>0 and <=1_048_576}
                    || Encoding.UTF8.GetByteCount(x.RowJson)>1_048_576
                    || x.CanonicalRowSha256 is not {Length:64}
                    || ProcurementEvidenceEncoding.PolicyJsonDigest(x.RowJson)!=x.CanonicalRowSha256)
                || table.Rows.Select(x=>x.Id).Distinct().Count()!=table.Rows.Count)
                throw new InvalidOperationException("InvoiceOwnerInventoryRowChanged");
            total=checked(total+table.Rows.Count);
            totalBytes=checked(totalBytes+table.Rows.Sum(x=>(long)Encoding.UTF8.GetByteCount(x.RowJson)));
            if(total>100_000 || totalBytes>100_000_000)
                throw new InvalidOperationException("InvoiceOwnerInventoryBoundExceeded");
            foreach(var row in table.Rows)
            {
                try
                {
                    using var json=JsonDocument.Parse(row.RowJson);
                    var root=json.RootElement;
                    if(root.ValueKind!=JsonValueKind.Object
                        || root.GetProperty("id").GetGuid()!=row.Id
                        || root.GetProperty("version").GetInt64()!=row.Version
                        || root.GetProperty("tenant_id").GetString()!=capture.TenantId
                        || root.GetProperty("legal_entity_id").GetGuid()!=capture.LegalEntityId)
                        throw new InvalidOperationException("InvoiceOwnerInventoryScopeChanged");
                }
                catch(Exception error) when(error is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
                { throw new InvalidOperationException("InvoiceOwnerInventoryScopeChanged",error); }
            }
            var ordered=table.Rows.OrderBy(x=>x.Id).ToArray();
            var ids=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            {schemaVersion=1,tableName=table.TableName,ids=ordered.Select(x=>x.Id).ToArray()},Json));
            var rows=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
            {schemaVersion=1,tableName=table.TableName,rows=ordered.Select(x=>new{x.Id,x.Version,x.CanonicalRowSha256}).ToArray()},Json));
            summaries.Add(new(table.TableName,table.Rows.Count,ids,rows));
        }
        var names=expected.ToArray();
        var manifest=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {schemaVersion=1,tableNames=names},Json));
        var complete=ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {schemaVersion=1,tables=summaries.ToArray()},Json));
        return new(capture.TenantId,capture.LegalEntityId,manifest,summaries,complete,total);
    }
}
