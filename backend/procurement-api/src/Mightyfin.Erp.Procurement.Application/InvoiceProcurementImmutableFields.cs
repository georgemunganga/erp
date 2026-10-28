using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Freeze monetary identity while allowing a later owner to examine
/// status/pointer transitions separately. Matching digests alone never approve
/// such a transition; typed receipts and sequence remain required.</summary>
public static class InvoiceProcurementImmutableFields
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static string Digest(string tableName, string rowJson)
    {
        if(rowJson is null || rowJson.Length is < 1 or > 1_048_576)
            throw new InvalidOperationException("InvoiceOwnerImmutableProjectionInvalid");
        try
        {
            using var document=JsonDocument.Parse(rowJson);
            var row=document.RootElement;
            if(row.ValueKind!=JsonValueKind.Object)
                throw new InvalidOperationException("InvoiceOwnerImmutableProjectionInvalid");
            if(tableName=="supplier_invoices")
                return ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
                {
                    schemaVersion=1,tableName,
                    id=row.GetProperty("id"),tenantId=row.GetProperty("tenant_id"),
                    companyId=row.GetProperty("legal_entity_id"),
                    supplierId=row.GetProperty("supplier_id"),
                    invoiceNumber=row.GetProperty("invoice_number"),
                    normalizedInvoiceNumber=row.GetProperty("normalized_invoice_number"),
                    currencyCode=row.GetProperty("currency_code"),
                    netAmount=row.GetProperty("net_amount"),taxAmount=row.GetProperty("tax_amount"),
                    grossAmount=row.GetProperty("gross_amount"),invoiceDate=row.GetProperty("invoice_date")
                },Json));
            if(tableName=="manual_payment_requests")
                return ProcurementEvidenceEncoding.PolicyJsonDigest(JsonSerializer.Serialize(new
                {
                    schemaVersion=1,tableName,
                    id=row.GetProperty("id"),tenantId=row.GetProperty("tenant_id"),
                    companyId=row.GetProperty("legal_entity_id"),
                    branchId=row.GetProperty("branch_id"),supplierInvoiceId=row.GetProperty("supplier_invoice_id"),
                    supplierId=row.GetProperty("supplier_id"),currencyCode=row.GetProperty("currency_code"),
                    amount=row.GetProperty("amount")
                },Json));
            if(!InvoiceProcurementOwnerTables.Names.Contains(tableName,StringComparer.Ordinal))
                throw new InvalidOperationException("InvoiceOwnerImmutableProjectionInvalid");
            var full=ProcurementEvidenceEncoding.PolicyJsonDigest(rowJson);
            if(full.Length!=64) throw new InvalidOperationException("InvoiceOwnerImmutableProjectionInvalid");
            return full;
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or FormatException)
        { throw new InvalidOperationException("InvoiceOwnerImmutableProjectionInvalid",error); }
    }
}
