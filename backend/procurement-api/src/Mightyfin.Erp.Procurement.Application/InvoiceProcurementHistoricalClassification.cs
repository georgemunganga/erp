using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record InvoiceProcurementClassifiedRow(string TableName, Guid RowId,
    string Classification, Guid? SupplierInvoiceId, Guid? PurchaseOrderId);

/// <summary>Conservative first classifier. An unprocessed manual invoice has no
/// billing claim only when every owning evidence table is physically absent.
/// Everything else remains blocked until its typed historical graph is reviewed.</summary>
public static class InvoiceProcurementHistoricalClassification
{
    public static IReadOnlyList<InvoiceProcurementClassifiedRow> Classify(
        InvoiceProcurementInventoryCapture capture)
    {
        InvoiceProcurementInventoryEvidence.Summarize(capture);
        var tables=capture.Tables.ToDictionary(x=>x.TableName,StringComparer.Ordinal);
        var classified=new Dictionary<(string,Guid),InvoiceProcurementClassifiedRow>();
        foreach(var table in capture.Tables)
            foreach(var row in table.Rows)
                classified.Add((table.TableName,row.Id),new(table.TableName,row.Id,
                    "BlockedAmbiguity",InvoiceId(table.TableName,row.RowJson),OrderId(table.TableName,row.RowJson)));
        var linesByInvoice=tables["supplier_invoice_lines"].Rows
            .Select(x=>(Row:x,InvoiceId:InvoiceId("supplier_invoice_lines",x.RowJson)))
            .Where(x=>x.InvoiceId is not null).GroupBy(x=>x.InvoiceId!.Value)
            .ToDictionary(x=>x.Key,x=>x.Select(y=>y.Row).ToArray());
        var linkedOther=new HashSet<Guid>();
        foreach(var table in capture.Tables.Where(x=>x.TableName is not ("supplier_invoices" or "supplier_invoice_lines")))
            foreach(var row in table.Rows)
            {
                var invoiceId=InvoiceId(table.TableName,row.RowJson);
                if(invoiceId is not null)linkedOther.Add(invoiceId.Value);
                if(table.TableName=="attachment_refs" && OwnerId(row.RowJson) is { } ownerId)
                    linkedOther.Add(ownerId);
            }
        foreach(var invoice in tables["supplier_invoices"].Rows)
        {
            using var doc=JsonDocument.Parse(invoice.RowJson);
            var root=doc.RootElement;
            var lines=linesByInvoice.GetValueOrDefault(invoice.Id)??[];
            if(String(root,"status")!="Received" || String(root,"intake_channel")!="Manual"
                || !Null(root,"finance_posting_ref") || !Null(root,"evidence_document_ref_id")
                || !Null(root,"credit_for_invoice_ref") || Number(root,"mapping_revision")!=0
                || !Null(root,"current_mapping_version_id") || !Null(root,"last_match_result_id")
                || !Null(root,"active_match_claim_result_id") || Number(root,"match_revision")!=0
                || String(root,"matching_state")!="NotCompared" || linkedOther.Contains(invoice.Id)
                || lines.Any(x=>!UnlinkedLine(x.RowJson))) continue;
            var original=classified[("supplier_invoices",invoice.Id)];
            classified[("supplier_invoices",invoice.Id)]=original with{Classification="MutableDraft"};
            foreach(var line in lines)
            {
                var current=classified[("supplier_invoice_lines",line.Id)];
                classified[("supplier_invoice_lines",line.Id)]=current with{Classification="MutableDraft"};
            }
        }
        return capture.Tables.SelectMany(x=>x.Rows.Select(row=>classified[(x.TableName,row.Id)])).ToArray();
    }

    private static Guid? InvoiceId(string table,string json)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        if(table=="supplier_invoices")return GuidOf(root,"id");
        if(table=="supplier_credit_note_refs")return GuidOf(root,"supplier_invoice_id");
        return GuidOf(root,"supplier_invoice_id");
    }
    private static Guid? OrderId(string table,string json)
    {
        using var doc=JsonDocument.Parse(json);
        return GuidOf(doc.RootElement,"purchase_order_id");
    }
    private static Guid? OwnerId(string json)
    {
        using var doc=JsonDocument.Parse(json);
        return GuidOf(doc.RootElement,"owner_id");
    }
    private static bool UnlinkedLine(string json)
    {
        using var doc=JsonDocument.Parse(json);var row=doc.RootElement;
        return Null(row,"purchase_order_line_id") && Null(row,"goods_receipt_line_id")
            && Null(row,"service_acceptance_id");
    }
    private static Guid? GuidOf(JsonElement row,string name) =>
        row.TryGetProperty(name,out var value) && value.ValueKind!=JsonValueKind.Null ? value.GetGuid() : null;
    private static bool Null(JsonElement row,string name) =>
        !row.TryGetProperty(name,out var value) || value.ValueKind==JsonValueKind.Null;
    private static string? String(JsonElement row,string name) =>
        row.TryGetProperty(name,out var value) && value.ValueKind!=JsonValueKind.Null ? value.GetString() : null;
    private static long Number(JsonElement row,string name) =>
        row.TryGetProperty(name,out var value) && value.ValueKind!=JsonValueKind.Null ? value.GetInt64() : -1;
}
