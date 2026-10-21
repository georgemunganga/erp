namespace Mightyfin.Erp.Hrm.Application;

/// <summary>Server action names and conservative bootstrap grants. Core role
/// assignments can replace these defaults with explicit Procurement actions.</summary>
public static class ProcurementActionCatalog
{
    public static readonly IReadOnlyDictionary<string, string[]> BootstrapGrants =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["procurement-access"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor", "finance_approver"],
            ["procurement-vendor-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor"],
            ["procurement-vendor-create"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-vendor-edit"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-vendor-manage"] = ["procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-vendor-approve"] = ["procurement_manager"],
            ["procurement-vendor-suspend"] = ["procurement_manager"],
            ["procurement-item-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-item-manage"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-item-approve"] = ["procurement_manager"],
            ["procurement-catalog-approve"] = ["procurement_manager"],
            ["procurement-request-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager"],
            ["procurement-request-create"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager"],
            ["procurement-request-edit"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager"],
            ["procurement-request-submit"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager"],
            ["procurement-policy-evaluate"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-request-return"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-budget-check"] = ["finance_approver"],
            ["procurement-plan-read"] = ["manager", "procurement_buyer", "procurement_manager"],
            ["procurement-plan-create"] = ["manager", "procurement_buyer", "procurement_manager"],
            ["procurement-plan-edit"] = ["manager", "procurement_buyer", "procurement_manager"],
            // These actions require explicit configured grants (or existing ERP administrator
            // governance access). Neither role bootstrap nor this catalog grants spending limits.
            ["procurement-receipt-view"] = [],
            ["procurement-receipt-create"] = [],
            ["procurement-receipt-edit"] = [],
            ["procurement-receipt-record"] = [],
            ["procurement-receipt-inspect"] = [],
            ["procurement-receipt-accept"] = [],
            ["procurement-service-certify"] = [],
            ["procurement-authority-read"] = [],
            ["procurement-authority-configure"] = [],
            ["procurement-authority-review"] = [],
            ["procurement-authority-revoke"] = [],
            ["procurement-approval-decide"] = [],
            ["procurement-approval-open"] = [],
            ["procurement-approval-read"] = [],
            ["procurement-configuration-read"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-configuration-edit"] = ["procurement_manager"],
            ["procurement-configuration-approve"] = ["procurement_manager"],
            ["procurement-contract-read"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-contract-create"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-contract-edit"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-sourcing-read"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-sourcing-create"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-sourcing-edit"] = ["procurement_buyer", "procurement_manager"],
            // Source reservations/releases require explicit Core grants; legacy draft rights do not imply them.
            ["procurement-sourcing-convert"] = [],
            ["procurement-sourcing-cancel"] = [],
            // Explicit configured grant only; catalog registration adds no publication endpoint or limit.
            ["procurement-sourcing-publish"] = [],
            ["procurement-invoice-read"] = ["ap_processor", "procurement_buyer", "procurement_manager"],
            ["procurement-invoice-capture"] = ["ap_processor"],
            ["procurement-invoice-match"] = [],
            ["procurement-po-read"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-po-create"] = ["procurement_buyer"],
            ["procurement-po-edit"] = ["procurement_buyer"],
            ["procurement-po-convert"] = ["procurement_buyer"],
            ["procurement-po-submit"] = ["procurement_buyer"],
            ["procurement-po-approve"] = ["procurement_manager"],
            ["procurement-po-issue"] = ["procurement_buyer"],
            ["procurement-po-amend"] = ["procurement_buyer"],
            ["procurement-po-finance-confirm"] = ["finance_approver"],
            ["procurement-po-dispatch-confirm"] = ["procurement_manager"],
        };

    public static string[] ForRole(string role) => BootstrapGrants
        .Where(x => x.Value.Contains(role, StringComparer.OrdinalIgnoreCase))
        .Select(x => x.Key).ToArray();
}
