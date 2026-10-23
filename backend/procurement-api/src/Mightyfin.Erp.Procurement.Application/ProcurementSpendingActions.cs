namespace Mightyfin.Erp.Procurement.Application;

public sealed record ProcurementSpendingAction(string Action, string Permission, string AmountBasis);

/// <summary>Exact supported authority contracts; matching a role or another transaction never substitutes.</summary>
public static class ProcurementSpendingActions
{
    public static readonly ProcurementSpendingAction PurchaseRequest =
        new("PurchaseRequest.Approve", "procurement-approval-decide", "EstimatedLineBase");
    public static readonly ProcurementSpendingAction PurchaseOrder =
        new("PurchaseOrder.Approve", "procurement-po-approve", "ApprovedLineBase");
    // Configurable finite publication authority only. No execution handler/capability follows.
    public static readonly ProcurementSpendingAction SourcingPublication =
        new("SourcingEvent.Publish", "procurement-sourcing-publish", "ApprovedLineBase");

    public static ProcurementSpendingAction Resolve(string action) => action switch
    {
        "PurchaseRequest.Approve" => PurchaseRequest,
        "PurchaseOrder.Approve" => PurchaseOrder,
        "SourcingEvent.Publish" => SourcingPublication,
        _ => throw new InvalidOperationException("AuthorityActionUnsupported"),
    };
    public static bool Matches(string action, string permission, string basis) =>
        (action == PurchaseRequest.Action && permission == PurchaseRequest.Permission && basis == PurchaseRequest.AmountBasis)
        || (action == PurchaseOrder.Action && permission == PurchaseOrder.Permission && basis == PurchaseOrder.AmountBasis)
        || (action == SourcingPublication.Action && permission == SourcingPublication.Permission && basis == SourcingPublication.AmountBasis);

    // Identity evidence does not repeat AmountBasis; its owning grant binds that field.
    // Keep this finite list aligned with Matches, never infer an action from a role token.
    public static bool MatchesIdentity(string action, string permission) =>
        (action == PurchaseRequest.Action && permission == PurchaseRequest.Permission)
        || (action == PurchaseOrder.Action && permission == PurchaseOrder.Permission)
        || (action == SourcingPublication.Action && permission == SourcingPublication.Permission);
}
