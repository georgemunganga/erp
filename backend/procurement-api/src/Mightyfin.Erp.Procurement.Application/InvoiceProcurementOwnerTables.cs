namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Frozen old-row inventory for the first M6 owner admission. A schema
/// expansion needs reviewed coverage and a new manifest version.</summary>
public static class InvoiceProcurementOwnerTables
{
    public static readonly IReadOnlyList<string> Names =
    [
        "attachment_refs", "finance_invoice_handoffs", "finance_payment_status_refs", "invoice_exceptions",
        "invoice_match_allocations", "invoice_match_exposure_states", "invoice_match_mapping_versions",
        "invoice_match_results", "invoice_unit_review_assignments", "invoice_unit_review_outcomes",
        "invoice_unit_review_receipts", "invoice_unit_review_sources", "manual_payment_core_decisions",
        "manual_payment_date_observations", "manual_payment_decisions", "manual_payment_receipts",
        "manual_payment_requests", "manual_payment_workflow_cases", "supplier_credit_note_refs",
        "supplier_invoice_lines", "supplier_invoices"
    ];
}
