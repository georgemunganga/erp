using Mightyfin.Erp.Core.Workflows.Application;

namespace Mightyfin.Erp.Procurement.Application;

public sealed class ProcurementWorkflowConfigurationProvider : IWorkflowConfigurationProvider
{
    public IReadOnlyList<WorkflowConfigurationType> GetTypes() =>
    [
        new("procurement.purchase-request.submitted", "procurement", "Procurement", "purchase-request", "Purchase request",
            "submitted", "When submitted", "Review the business need and authorise Procurement to start buying.",
            "Approver types are currently limited. Use named Core people or roles in Buying rules; current permissions and spending limits still apply.",
            [new("Buying rules", "/procurement/workspace/settings/buying-rules"),
             new("Approval limits", "/procurement/workspace/settings/modules/purchase-requests/approvals")]),
        new("procurement.purchase-order.submitted", "procurement", "Procurement", "purchase-order", "Purchase order",
            "submitted", "Before issue", "Approve the commercial order before it is issued to the supplier.",
            "Opening these settings does not enable ordering. Current permissions, policy and funding checks still apply.",
            [new("Approval limits", "/procurement/workspace/settings/modules/purchase-orders/approvals"),
             new("Buying rules", "/procurement/workspace/settings/buying-rules")]),
        new("procurement.payment-request.submitted", "procurement", "Procurement", "payment-request", "Payment request",
            "submitted", "Before payment is recorded", "Assign the Accounts approval for an invoice-linked payment request.",
            "This flow requires a supplier invoice. Purchase-order advances are not supported by this payment workflow yet.",
            [new("Payment approvals", "/procurement/workspace/settings/modules/payments-made/approvals")]),
        new("procurement.payment-request.recorded", "procurement", "Procurement", "payment-request", "Payment request",
            "recorded", "After an external payment is recorded", "Require independent confirmation of the recorded payment details.",
            "Recording or confirming a payment does not execute a bank transfer or post to Accounts Payable.",
            [new("Payment approval settings", "/procurement/workspace/settings/modules/payments-made/approvals")]),
        new("procurement.purchase-plan.submitted", "procurement", "Procurement", "purchase-plan", "Purchase plan",
            "submitted", "When submitted", "Choose the review and final approval steps for a procurement plan.",
            "Purchase plans and purchase requests have separate rules. Workflow availability is checked in Procurement.",
            [new("Plan approvals", "/procurement/workspace/settings/modules/purchase-plans/approvals")])
    ];
}
