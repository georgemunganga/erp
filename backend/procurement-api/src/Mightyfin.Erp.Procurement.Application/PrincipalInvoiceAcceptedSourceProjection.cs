using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>A verified historical M5 receiving graph for one issued order line.
/// The caller must first verify the retained issued order, physical ownership FKs and
/// absence of unknown earlier legacy receiving history. This projection proves
/// accepted facts only; it grants no invoice capacity or AP authority.</summary>
public sealed record PrincipalInvoiceAcceptedFact(
    Guid FactId, Guid SourceStateId, long SourceSequence, Guid PurchaseOrderId,
    Guid IssuedVersionId, Guid PurchaseOrderLineId, ReceivingKind Kind,
    decimal AcceptedQuantity, decimal AcceptedNetAmount, string PayloadSha256,
    DateTimeOffset AcceptedAt);

public sealed record PrincipalInvoiceAcceptedHistory(
    Guid SourceStateId, string SourceSha256, long SourceSequence, string UnitCode,
    IReadOnlyList<PrincipalInvoiceAcceptedFact> Facts)
{
    public Guid SupplierId { get; init; }
    public Guid BranchId { get; init; }
    public string CurrencyCode { get; init; } = "";
}

public static class PrincipalInvoiceAcceptedSourceProjection
{
    public static PrincipalInvoiceAcceptedHistory Verify(
        PrincipalReceivingSource expectedSource, PrincipalReceivingSourceState state,
        IReadOnlyList<PrincipalReceivingFact> facts,
        IReadOnlyList<PrincipalReceivingDraftLineBinding> lineBindings,
        IReadOnlyList<PrincipalReceivingCommandReceipt> receipts,
        IReadOnlyList<PolicyVersion> retainedPolicies, Guid supplierId)
    {
        ArgumentNullException.ThrowIfNull(expectedSource);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(lineBindings);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(retainedPolicies);
        if (supplierId == Guid.Empty || expectedSource.Commercial.BranchId is not { } sourceBranch
            || sourceBranch == Guid.Empty || string.IsNullOrWhiteSpace(expectedSource.Commercial.CurrencyCode)
            || facts.Count > 2_000 || lineBindings.Count > 2_000 || receipts.Count > 2_000
            || retainedPolicies.Count > 2_000 || state.Sequence > 2_000)
            throw new InvalidOperationException("PrincipalInvoiceReceivingHistoryBoundExceeded");
        if (state.Id == Guid.Empty || PrincipalReceivingStorageCodec.Source(state) != expectedSource)
            throw new InvalidOperationException("PrincipalInvoiceOriginalReceivingSourceChanged");

        // The codec replays every fact, retained policy and exact counter. A selected
        // acceptance row alone cannot establish complete physical history.
        var ledger = PrincipalReceivingStorageCodec.Ledger(state, facts, retainedPolicies);
        if (lineBindings.Select(x => x.Id).Distinct().Count() != lineBindings.Count
            || lineBindings.Count != facts.Select(x => x.DraftLineBindingId).Distinct().Count()
            || receipts.Select(x => x.Id).Distinct().Count() != receipts.Count
            || receipts.Select(x => x.CommandKey).Distinct().Count() != receipts.Count
            || receipts.Count != facts.Select(x => x.CommandKey).Distinct().Count()
            || facts.Select(x => x.Id).Distinct().Count() != facts.Count)
            throw new InvalidOperationException("PrincipalInvoiceReceivingOwnerGraphAmbiguous");

        foreach (var fact in facts)
        {
            var binding = lineBindings.SingleOrDefault(x => x.Id == fact.DraftLineBindingId);
            if (binding is null || binding.Id == Guid.Empty || binding.TenantId != state.TenantId
                || binding.LegalEntityId != state.LegalEntityId || binding.DraftId != fact.DraftId
                || binding.SourceStateId != state.Id || binding.BranchId != state.BranchId
                || binding.PurchaseOrderId != state.PurchaseOrderId
                || binding.IssuedVersionId != state.IssuedVersionId
                || binding.PurchaseOrderLineId != state.PurchaseOrderLineId)
                throw new InvalidOperationException("PrincipalInvoiceReceivingLineBindingChanged");
            var receipt = receipts.SingleOrDefault(x => x.CommandKey == fact.CommandKey);
            if (receipt is { Id: var receiptId } && receiptId == Guid.Empty)
                throw new InvalidOperationException("PrincipalInvoiceReceivingReceiptIdentityInvalid");
            PrincipalReceivingStoredReceipt.Verify(fact, receipt, ledger.Source);
        }

        var accepted = ledger.Acceptances.OrderBy(x => x.Sequence).Select(value =>
        {
            var row = facts.Single(x => x.Id == value.Id && x.Kind == "Acceptance");
            return new PrincipalInvoiceAcceptedFact(row.Id, state.Id, row.Sequence,
                state.PurchaseOrderId, state.IssuedVersionId, state.PurchaseOrderLineId,
                ledger.Source.Commercial.Kind, value.Quantity, value.NetAmount,
                row.PayloadSha256, value.AcceptedAt);
        }).ToArray();
        return new PrincipalInvoiceAcceptedHistory(state.Id, state.SourceSha256, state.Sequence,
            expectedSource.Commercial.UnitCode, accepted)
        { SupplierId = supplierId, BranchId = sourceBranch, CurrencyCode = expectedSource.Commercial.CurrencyCode };
    }
}
