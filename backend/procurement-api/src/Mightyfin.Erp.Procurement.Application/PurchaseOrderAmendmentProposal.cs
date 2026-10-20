using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PurchaseOrderAmendmentLineDelta(Guid OrderLineId, Guid PurchaseRequestLineId,
    decimal CurrentOrderedQuantity, decimal ProposedQuantity, decimal QuantityHoldRequired,
    decimal QuantityReleaseOnApply, decimal AcceptedQuantity, decimal InvoicedQuantity,
    decimal LineBaseDelta, decimal GrossDelta);
public sealed record PurchaseOrderAmendmentDraft(PurchaseOrderRevision Revision,
    IReadOnlyList<PurchaseOrderAmendmentLineDelta> SourceDeltas);

/// <summary>
/// Proposal construction only. This does not allocate, approve, adjust Finance, issue, apply,
/// or modify the live order. Future execution MUST use existing Core approval/coordinator
/// and separate positive delta holds without reclassifying any existing Ordered allocation.
/// </summary>
public static class PurchaseOrderAmendmentProposal
{
    public static PurchaseOrderAmendmentDraft Create(PurchaseOrder current,
        IReadOnlyList<PurchaseOrderLine> currentLines, Supplier supplier, PurchaseOrderIssuedVersion baseline,
        PurchaseOrder proposed, IReadOnlyList<PurchaseOrderLine> proposedLines, string reason)
    {
        RequireBaseline(current, currentLines, supplier, baseline);
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            Fail("OrderAmendmentReasonRequired");
        if (proposed.Id != current.Id || proposed.Number != current.Number || proposed.TenantId != current.TenantId
            || proposed.LegalEntityId != current.LegalEntityId || proposed.SupplierId != current.SupplierId
            || proposed.CurrencyCode != current.CurrencyCode || proposed.BuyerSubjectId != current.BuyerSubjectId
            || proposed.BranchRef != current.BranchRef || proposed.SourcingAwardId != current.SourcingAwardId
            || proposed.SupplierContractVersionId != current.SupplierContractVersionId)
            Fail("OrderAmendmentIdentityChanged");
        if (proposedLines.Count != currentLines.Count || proposedLines.Select(x => x.Id).Distinct().Count() != proposedLines.Count
            || proposedLines.Any(x => currentLines.All(original => original.Id != x.Id)))
            Fail("OrderAmendmentLineSetChanged");
        var deltas = new List<PurchaseOrderAmendmentLineDelta>();
        foreach (var line in proposedLines.OrderBy(x => x.LineNumber))
        {
            var original = currentLines.Single(x => x.Id == line.Id);
            if (!line.IsActive || line.TenantId != current.TenantId || line.LegalEntityId != current.LegalEntityId
                || line.PurchaseOrderId != current.Id || line.PurchaseRequestLineId != original.PurchaseRequestLineId
                || line.CatalogItemId != original.CatalogItemId || line.LineNumber != original.LineNumber
                || line.UnitCode != original.UnitCode || string.IsNullOrWhiteSpace(line.Description)
                || line.Quantity <= 0 || decimal.Round(line.Quantity, 4) != line.Quantity
                || line.UnitPrice < 0 || decimal.Round(line.UnitPrice, 4) != line.UnitPrice
                || line.TaxAmount < 0 || decimal.Round(line.TaxAmount, 4) != line.TaxAmount)
                Fail("OrderAmendmentLineIdentityOrValueInvalid");
            if (original.AcceptedQuantity < 0 || original.InvoicedQuantity < 0
                || original.AcceptedQuantity > original.Quantity || original.InvoicedQuantity > original.Quantity
                || line.Quantity < Math.Max(original.AcceptedQuantity, original.InvoicedQuantity))
                Fail("OrderAmendmentBelowExistingObligations");
            if (original.InvoicedQuantity > 0 && (line.UnitPrice != original.UnitPrice || line.TaxAmount != original.TaxAmount))
                Fail("OrderAmendmentInvoicedRepricingNeedsSeparateResolution");
            var quantityDelta = checked(line.Quantity - original.Quantity);
            var valueDelta = checked(line.Quantity * line.UnitPrice - original.Quantity * original.UnitPrice);
            deltas.Add(new(line.Id, original.PurchaseRequestLineId!.Value, original.Quantity, line.Quantity,
                Math.Max(quantityDelta, 0), Math.Max(-quantityDelta, 0), original.AcceptedQuantity,
                original.InvoicedQuantity, valueDelta, checked(valueDelta + line.TaxAmount - original.TaxAmount)));
        }
        var nextRevision = checked(current.CurrentRevision + 1);
        var after = PurchaseOrderCommercialWorkflow.Snapshot(proposed, proposedLines, supplier, nextRevision);
        if (PurchaseOrderCommercialWorkflow.Snapshot(proposed, proposedLines, supplier, current.CurrentRevision)
            == baseline.CommercialSnapshotJson) Fail("OrderAmendmentHasNoMaterialChange");
        var revision = new PurchaseOrderRevision { TenantId = current.TenantId, LegalEntityId = current.LegalEntityId,
            PurchaseOrderId = current.Id, RevisionNumber = nextRevision, Status = "Draft", Reason = reason.Trim(),
            BaseIssuedVersionId = baseline.Id, BaseIssuedRevision = baseline.RevisionNumber,
            BaseIssuedSnapshotSha256 = baseline.CommercialSnapshotSha256,
            BeforeSnapshotJson = baseline.CommercialSnapshotJson, AfterSnapshotJson = after };
        return new(revision, deltas);
    }

    public static void RequireBaseline(PurchaseOrder current, IReadOnlyList<PurchaseOrderLine> lines,
        Supplier supplier, PurchaseOrderIssuedVersion baseline)
    {
        if (current.Status != "Issued" || current.IssuedAt is null || current.CurrentRevision < 1
            || baseline.PurchaseOrderId != current.Id || baseline.TenantId != current.TenantId
            || baseline.LegalEntityId != current.LegalEntityId || baseline.RevisionNumber != current.CurrentRevision
            || string.IsNullOrWhiteSpace(baseline.FinanceCommitmentReference)
            || current.FinanceCommitmentRef != baseline.FinanceCommitmentReference
            || baseline.CommercialSnapshotSha256 != ProcurementEvidenceEncoding.Digest(baseline.CommercialSnapshotJson)
            || supplier.Id != current.SupplierId || supplier.TenantId != current.TenantId || supplier.LegalEntityId != current.LegalEntityId
            || lines.Count == 0 || lines.Select(x => x.Id).Distinct().Count() != lines.Count
            || lines.Any(x => !x.IsActive || x.TenantId != current.TenantId || x.LegalEntityId != current.LegalEntityId
                || x.PurchaseOrderId != current.Id || x.PurchaseRequestLineId is null)
            || baseline.CommercialSnapshotJson != PurchaseOrderCommercialWorkflow.Snapshot(current, lines, supplier, current.CurrentRevision))
            Fail("OrderAmendmentIssuedBaselineChanged");
    }
    private static void Fail(string code) => throw new InvalidOperationException(code);
}
