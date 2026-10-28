using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Target-scoped exclusion of unclassified financial effects. A current
/// mapping or pending unit review is not a payment, but a credit, handoff or
/// manual-payment row cannot be silently treated as zero exposure.</summary>
public static class PrincipalInvoiceCurrentOwnerScope
{
    public static void RequireNoUnclassifiedEffect(InvoiceProcurementInventoryCapture current,
        Guid purchaseOrderId, Guid supplierId, Guid branchId, string currencyCode,
        IReadOnlySet<Guid> verifiedActiveAllocationIds)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(verifiedActiveAllocationIds);
        _ = InvoiceProcurementInventoryEvidence.Summarize(current);
        if (purchaseOrderId == Guid.Empty || supplierId == Guid.Empty || branchId == Guid.Empty
            || currencyCode is not { Length: 3 })
            throw new InvalidOperationException("PrincipalInvoiceOwnerScopeInvalid");
        var tables = current.Tables.ToDictionary(x => x.TableName, StringComparer.Ordinal);
        var linkedInvoices = new HashSet<Guid>();
        foreach (var row in tables["supplier_invoices"].Rows)
            if (GuidOf(row.RowJson, "purchase_order_id") == purchaseOrderId)
                linkedInvoices.Add(row.Id);
        foreach (var name in new[] { "invoice_match_mapping_versions", "invoice_match_results",
            "invoice_match_allocations" })
            foreach (var row in tables[name].Rows)
                if (GuidOf(row.RowJson, "purchase_order_id") == purchaseOrderId
                    && GuidOf(row.RowJson, "supplier_invoice_id") is { } invoiceId)
                    linkedInvoices.Add(invoiceId);
        foreach (var row in tables["supplier_credit_note_refs"].Rows)
        {
            using var document = JsonDocument.Parse(row.RowJson);
            var value = document.RootElement;
            var linkedInvoice = RequiredOptionalGuid(value, "supplier_invoice_id");
            var creditSupplier = GuidValue(value, "supplier_id");
            if (creditSupplier is null || creditSupplier == Guid.Empty)
                throw new InvalidOperationException("PrincipalInvoiceCreditScopeUnclassifiable");
            if (linkedInvoice is { } invoiceId && linkedInvoices.Contains(invoiceId))
                throw new InvalidOperationException("PrincipalInvoiceTargetCreditRequiresReconciliation");
            if (linkedInvoice is null && creditSupplier == supplierId)
            {
                var creditCurrency = StringValue(value, "currency_code");
                if (creditCurrency is not { Length: 3 }
                    || creditCurrency.Any(c => c is < 'A' or > 'Z')
                    || string.IsNullOrWhiteSpace(StringValue(value, "credit_number")))
                    throw new InvalidOperationException("PrincipalInvoiceCreditScopeUnclassifiable");
                if (creditCurrency == currencyCode)
                    throw new InvalidOperationException("PrincipalInvoiceTargetCreditRequiresReconciliation");
            }
        }
        foreach (var table in current.Tables)
            foreach (var row in table.Rows)
            {
                using var document = JsonDocument.Parse(row.RowJson);
                var value = document.RootElement;
                var relevant = GuidValue(value, "purchase_order_id") == purchaseOrderId
                    || GuidValue(value, "supplier_invoice_id") is { } invoiceId
                        && linkedInvoices.Contains(invoiceId)
                    || table.TableName == "supplier_invoices" && linkedInvoices.Contains(row.Id)
                    || table.TableName == "attachment_refs"
                        && GuidValue(value, "owner_id") is { } ownerId
                        && linkedInvoices.Contains(ownerId);
                if (!relevant) continue;
                switch (table.TableName)
                {
                    case "supplier_invoices":
                        if (GuidValue(value, "supplier_id") != supplierId
                            || GuidValue(value, "branch_ref") != branchId
                            || StringValue(value, "currency_code") != currencyCode
                            || !IsNull(value, "finance_posting_ref")
                            || !IsNull(value, "credit_for_invoice_ref")
                            || StringValue(value, "status") is not ("Received" or "MatchPending" or "Exception"))
                            throw new InvalidOperationException("PrincipalInvoiceTargetInvoiceEffectUnknown");
                        break;
                    case "supplier_invoice_lines":
                    case "invoice_unit_review_sources":
                    case "invoice_unit_review_assignments":
                    case "invoice_unit_review_outcomes":
                    case "invoice_unit_review_receipts":
                    case "attachment_refs":
                        break;
                    case "invoice_match_mapping_versions":
                        if (IntValue(value, "source_schema_version") != 3
                            || IntValue(value, "identity_schema_version") != 2)
                            throw new InvalidOperationException("PrincipalInvoiceTargetMappingUnknown");
                        break;
                    case "invoice_match_results":
                        // A compared result without a current claim needs an explicit
                        // reconciliation before a later owner can reuse capacity.
                        if (!tables["invoice_match_allocations"].Rows.Any(x =>
                            GuidOf(x.RowJson, "match_result_id") == row.Id
                            && verifiedActiveAllocationIds.Contains(x.Id)))
                            throw new InvalidOperationException("PrincipalInvoiceTargetResultUnclassified");
                        break;
                    case "invoice_match_allocations":
                        if (!verifiedActiveAllocationIds.Contains(row.Id))
                            throw new InvalidOperationException("PrincipalInvoiceTargetReleasedClaimUnknown");
                        break;
                    case "invoice_match_exposure_states":
                        break;
                    default:
                        throw new InvalidOperationException("PrincipalInvoiceTargetFinancialEffectUnknown");
                }
            }
    }

    private static Guid? GuidOf(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        return GuidValue(document.RootElement, name);
    }
    private static Guid? RequiredOptionalGuid(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var field))
            throw new InvalidOperationException("PrincipalInvoiceCreditScopeUnclassifiable");
        if (field.ValueKind == JsonValueKind.Null) return null;
        if (field.ValueKind == JsonValueKind.String
            && Guid.TryParse(field.GetString(), out var parsed) && parsed != Guid.Empty)
            return parsed;
        throw new InvalidOperationException("PrincipalInvoiceCreditScopeUnclassifiable");
    }
    private static Guid? GuidValue(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String
            && Guid.TryParse(field.GetString(), out var parsed) ? parsed : null;
    private static string? StringValue(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String
            ? field.GetString() : null;
    private static int? IntValue(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number
            && field.TryGetInt32(out var parsed) ? parsed : null;
    private static bool IsNull(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Null;
}
