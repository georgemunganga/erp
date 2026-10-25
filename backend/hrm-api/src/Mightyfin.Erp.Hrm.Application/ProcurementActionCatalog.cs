namespace Mightyfin.Erp.Hrm.Application;

/// <summary>Server action names and conservative bootstrap grants. Core role
/// assignments can replace these defaults with explicit Procurement actions.</summary>
public static class ProcurementActionCatalog
{
    public sealed record AdminAction(string Key, string Group, string Label);

    public static bool RequiresExplicitConfiguredGrant(string action) => action is
        "procurement-vendor-export" or "procurement-item-export";

    public static readonly IReadOnlyDictionary<string, string[]> BootstrapGrants =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["procurement-access"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor", "finance_approver"],
            ["procurement-vendor-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin", "ap_processor"],
            ["procurement-vendor-export"] = [], // Explicit Core grant; no legacy bootstrap.
            ["procurement-vendor-create"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-vendor-edit"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-vendor-manage"] = ["procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-vendor-approve"] = ["procurement_manager"],
            ["procurement-vendor-suspend"] = ["procurement_manager"],
            ["procurement-item-read"] = ["employee", "manager", "procurement_requester", "procurement_buyer", "procurement_manager", "supplier_admin"],
            ["procurement-item-manage"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-item-export"] = [], // Managed drafts only; not the employee catalog.
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
            ["procurement-recurring-read"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-recurring-create"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-recurring-edit"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-sourcing-read"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-sourcing-create"] = ["procurement_buyer", "procurement_manager"],
            ["procurement-sourcing-edit"] = ["procurement_buyer", "procurement_manager"],
            // Source reservations/releases require explicit Core grants; legacy draft rights do not imply them.
            ["procurement-sourcing-convert"] = [],
            ["procurement-sourcing-cancel"] = [],
            // Explicit configured grant only; catalog registration adds no publication endpoint or limit.
            ["procurement-sourcing-publish"] = [],
            ["procurement-invoice-read"] = ["ap_processor", "procurement_buyer", "procurement_manager"],
            ["procurement-invoice-document-retire"] = [], // Explicit configured grant only; upload/read is not retirement authority.
            ["procurement-invoice-capture"] = ["ap_processor"],
            ["procurement-invoice-match"] = [],
            // Independent original-document unit review: explicit configured grants only.
            ["procurement-invoice-unit-review-request"] = [],
            ["procurement-invoice-unit-review-read"] = [],
            ["procurement-invoice-unit-review"] = [],
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

    /// <summary>The deployed P24 role-row interpretation shared by session grants
    /// and Core role readback. The only bootstrap trigger is an exact legacy seed.</summary>
    public static string[] ResolveConfiguredActionsForRole(string roleKey, string permissionsCsv)
    {
        var explicitActions = permissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(BootstrapGrants.ContainsKey).ToArray();
        var legacySeed = string.Equals(permissionsCsv.Trim(), roleKey, StringComparison.OrdinalIgnoreCase);
        var actions = explicitActions.Length > 0 ? explicitActions : legacySeed
            ? roleKey.Equals("erp_admin", StringComparison.OrdinalIgnoreCase)
                ? BootstrapGrants.Keys.Where(action => !RequiresExplicitConfiguredGrant(action)).ToArray() : ForRole(roleKey)
            : [];
        return actions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Presentation metadata for the existing P24 actions only; no grants.</summary>
    public static AdminAction[] AdminReadCatalog() => BootstrapGrants.Keys
        .Order(StringComparer.Ordinal)
        .Select(key => new AdminAction(key, GroupFor(key), LabelFor(key)))
        .ToArray();

    private static string GroupFor(string key)
    {
        if (key == "procurement-access") return "General";
        var part = key["procurement-".Length..].Split('-')[0];
        return part switch
        {
            "po" => "Purchase orders",
            "vendor" => "Vendors",
            "item" or "catalog" => "Items and catalog",
            "request" or "policy" or "budget" => "Requests and policy",
            "plan" => "Plans",
            "receipt" or "service" => "Receiving and returns",
            "authority" or "approval" => "Approvals and authority",
            "configuration" => "Configuration",
            "contract" => "Contracts",
            "recurring" => "Recurring purchases",
            "sourcing" => "Sourcing and RFQs",
            "invoice" => "Invoices",
            _ => "Other Procurement"
        };
    }

    private static string LabelFor(string key)
    {
        if (key == "procurement-vendor-export") return "Export vendors";
        if (key == "procurement-item-export") return "Export items";
        var words = key["procurement-".Length..].Split('-').Select(word => word switch
        {
            "po" => "PO", "rfq" => "RFQ", "api" => "API", _ => char.ToUpperInvariant(word[0]) + word[1..]
        });
        return string.Join(" ", words);
    }

    public static string[] ForRole(string role) => BootstrapGrants
        .Where(x => x.Value.Contains(role, StringComparer.OrdinalIgnoreCase))
        .Select(x => x.Key).ToArray();
}
