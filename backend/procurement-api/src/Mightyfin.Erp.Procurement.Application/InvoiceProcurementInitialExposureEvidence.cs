using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>A company baseline may contain many independent exposure sequences.
/// Freeze the entire physical set; each PO's sequence is validated separately.</summary>
public static class InvoiceProcurementInitialExposureEvidence
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static string Digest(InvoiceProcurementInventoryCapture capture)
    {
        var table=capture.Tables.Single(x=>x.TableName=="invoice_match_exposure_states");
        return ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
        {
            schemaVersion=1,tableName=table.TableName,
            states=table.Rows.OrderBy(x=>x.Id).Select(x=>new
            {x.Id,x.Version,x.CanonicalRowSha256}).ToArray()
        },Json));
    }
}
