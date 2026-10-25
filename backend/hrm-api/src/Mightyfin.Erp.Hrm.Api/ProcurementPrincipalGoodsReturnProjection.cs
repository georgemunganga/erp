using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Hrm.Api;

public static partial class ProcurementPrincipalReceivingRoutes
{
    // Read-time hints only. POST/PUT callbacks independently re-resolve current
    // Core permissions and enforce original creator, branch, state and revision.
    internal static string[] ReturnAllowedActions(PrincipalGoodsReturnDraft head,
        PrincipalReceivingActor actor, bool editAuthorized)
        => editAuthorized && actor.Permissions.Contains(ReturnEdit)
            && actor.PrincipalId == head.CreatorPrincipalId && head.State == "Draft"
            && actor.TenantId == head.TenantId && actor.LegalEntityId == head.LegalEntityId
            && actor.AllowedBranches.Contains(head.BranchId)
            ? ["Edit", "Cancel"] : [];

    internal sealed record ReturnSourceLabels(string PurchaseOrderNumber, string? SupplierDisplayName,
        int LineNumber, string Description, string UnitCode, string CurrencyCode,
        string DeliveryNoteReference, DateTimeOffset ReceivedAt);

    // The only production caller receives Retained after SourceHistory has
    // verified issued commercial bytes, original PR/Core lineage and the entire
    // physical ledger. Supplier labels come from the original issued snapshot,
    // never today's supplier/catalog name or eligibility.
    internal static ReturnSourceLabels VerifiedReturnLabels(
        ProcurementPrincipalOrderRetainedIssued.Result retained, PrincipalGoodsObservation observation)
    {
        var selected = retained.SelectedLine ?? throw new InvalidOperationException("ReturnOriginalSourceChanged");
        if (observation.Source.Commercial.PurchaseOrderId != retained.Order.Id
            || observation.Source.Commercial.IssuedVersionId != retained.Issued.Id
            || observation.Source.Commercial.PurchaseOrderLineId != selected.OrderLine.Id)
            throw new InvalidOperationException("ReturnOriginalSourceChanged");
        using var snapshot = JsonDocument.Parse(retained.Issued.CommercialSnapshotJson);
        return new(retained.Order.Number, snapshot.RootElement.GetProperty("DisplayName").GetString(),
            selected.OrderLine.LineNumber, selected.OrderLine.Description,
            observation.Source.Commercial.UnitCode, observation.Source.Commercial.CurrencyCode,
            observation.DeliveryNoteReference, observation.ObservedAt);
    }

    // Dynamic actions and labels belong only on list/detail reads. Original
    // command acknowledgements remain PrincipalGoodsReturnRules.ResponseJson.
    internal static object ReturnView(PrincipalGoodsReturnDraft head, PrincipalGoodsReturnRevision revision,
        PrincipalReceivingActor actor, bool editAuthorized, ReturnSourceLabels labels) => new
    {
        id = head.Id, status = head.State, version = head.Revision, branchId = head.BranchId,
        receiptId = head.ReceiptId, acceptanceFactId = head.AcceptanceFactId,
        purchaseOrderId = head.PurchaseOrderId, purchaseOrderLineId = head.PurchaseOrderLineId,
        acceptedFactQuantityExact = Exact(head.AcceptedFactQuantity), quantityExact = Exact(revision.Quantity),
        revision.Reason, revision.EvidenceReference, labels,
        allowedActions = ReturnAllowedActions(head, actor, editAuthorized),
        draftOnly = true, stockChanged = false, financeChanged = false, supplierNotified = false
    };
}
