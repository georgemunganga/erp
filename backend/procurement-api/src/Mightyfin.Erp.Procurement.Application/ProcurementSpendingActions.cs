namespace Mightyfin.Erp.Procurement.Application;

public sealed record ProcurementSpendingAction(string Action, string Permission, string AmountBasis);

/// <summary>Exact supported authority contracts; matching a role or another transaction never substitutes.</summary>
public static class ProcurementSpendingActions
{
    public static readonly ProcurementSpendingAction PurchaseRequest =
        new("PurchaseRequest.Approve", "procurement-approval-decide", "EstimatedLineBase");
    public static readonly ProcurementSpendingAction PurchaseOrder =
        new("PurchaseOrder.Approve", "procurement-po-approve", "ApprovedLineBase");
    // Declared pure publication prerequisite ONLY. Deliberately excluded from Resolve/Matches
    // until the governed configuration, policy, state/evidence/API gates are implemented.
    public static readonly ProcurementSpendingAction SourcingPublication =
        new("SourcingEvent.Publish", "procurement-sourcing-publish", "ApprovedLineBase");

    public static ProcurementSpendingAction Resolve(string action) => action switch
    {
        "PurchaseRequest.Approve" => PurchaseRequest,
        "PurchaseOrder.Approve" => PurchaseOrder,
        _ => throw new InvalidOperationException("AuthorityActionUnsupported"),
    };
    public static bool Matches(string action, string permission, string basis) =>
        (action == PurchaseRequest.Action && permission == PurchaseRequest.Permission && basis == PurchaseRequest.AmountBasis)
        || (action == PurchaseOrder.Action && permission == PurchaseOrder.Permission && basis == PurchaseOrder.AmountBasis);
}
