using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PrincipalInvoiceAllocationSourceInput(
    InvoiceMatchAllocation Allocation, InvoiceMatchResult Result,
    InvoiceMatchMappingVersion Mapping, SupplierInvoice Invoice,
    IReadOnlyList<SupplierInvoiceLine> OriginalLines);

/// <summary>Verifies source-schema-3 allocation identity and cumulative accepted
/// capacity against complete replayed M5 histories. The caller must load the
/// complete active allocation set and histories in its serializable transaction;
/// passing a subset is never a qualified billing-capacity proof.</summary>
public static class PrincipalInvoiceAllocationSourceEvidence
{
    public static void VerifySet(
        IReadOnlyList<PrincipalInvoiceAllocationSourceInput> activeClaims,
        IReadOnlyList<PrincipalInvoiceAcceptedHistory> histories)
    {
        ArgumentNullException.ThrowIfNull(activeClaims);
        ArgumentNullException.ThrowIfNull(histories);
        if (activeClaims.Count > 100_000 || activeClaims.Any(x => x is null)
            || activeClaims.Select(x => x.Allocation.Id).Distinct().Count() != activeClaims.Count)
            throw new InvalidOperationException("PrincipalInvoiceClaimSetInvalid");
        var selectedSources = activeClaims.Where(x => x.Allocation.PrincipalReceivingSourceStateId is not null)
            .Select(x => x.Allocation.PrincipalReceivingSourceStateId!.Value).Distinct().Order().ToArray();
        if (histories.Count > 200 || histories.Any(x => x is null)
            || histories.Count != selectedSources.Length
            || !histories.Select(x => x.SourceStateId).Order().SequenceEqual(selectedSources))
            throw new InvalidOperationException("PrincipalInvoiceClaimSourceSetIncomplete");
        foreach (var group in activeClaims.GroupBy(x => x.Result.Id))
        {
            var first = group.First();
            var snapshot = PrincipalInvoiceMappingEvidence.VerifyStored(first.Mapping,
                first.Invoice, first.OriginalLines);
            if (group.Any(x => x.Mapping.Id != first.Mapping.Id
                    || x.Invoice.Id != first.Invoice.Id)
                || !group.Select(x => x.Allocation.SupplierInvoiceLineId).Order()
                    .SequenceEqual(snapshot.Lines.Select(x => x.InvoiceLineId).Order()))
                throw new InvalidOperationException("PrincipalInvoiceClaimLineSetIncomplete");
        }
        var selected = new List<(InvoiceMatchAllocation Claim, PrincipalInvoiceAcceptedFact Fact)>();
        foreach (var input in activeClaims)
            if (Verify(input, histories) is { } fact)
                selected.Add((input.Allocation, fact));
        foreach (var group in selected.GroupBy(x => x.Fact.FactId))
        {
            var fact = group.First().Fact;
            if (group.Any(x => x.Fact != fact)
                || ExactDecimalArithmetic.Sum(group.Select(x => x.Claim.Quantity)) > fact.AcceptedQuantity
                || ExactDecimalArithmetic.Sum(group.Select(x => x.Claim.NetAmount)) > fact.AcceptedNetAmount)
                throw new InvalidOperationException("PrincipalInvoiceClaimExceedsAcceptedFact");
        }
    }

    public static PrincipalInvoiceAcceptedFact? Verify(
        PrincipalInvoiceAllocationSourceInput input,
        IReadOnlyList<PrincipalInvoiceAcceptedHistory> histories)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(histories);
        var claim = input.Allocation;
        var result = input.Result;
        var mapping = input.Mapping;
        var invoice = input.Invoice;
        if (claim is null || result is null || mapping is null || invoice is null
            || input.OriginalLines is null || claim.Id == Guid.Empty
            || claim.TenantId != invoice.TenantId || claim.LegalEntityId != invoice.LegalEntityId
            || result.TenantId != invoice.TenantId || result.LegalEntityId != invoice.LegalEntityId
            || mapping.TenantId != invoice.TenantId || mapping.LegalEntityId != invoice.LegalEntityId
            || claim.SupplierInvoiceId != invoice.Id || claim.SupplierId != invoice.SupplierId
            || claim.CurrencyCode != invoice.CurrencyCode || claim.MatchResultId != result.Id
            || invoice.ActiveMatchClaimResultId != result.Id
            || invoice.CurrentMappingVersionId != mapping.Id
            || result.SupplierInvoiceId != invoice.Id || result.MappingVersionId != mapping.Id
            || result.InvoiceOriginalSha256 != mapping.InvoiceOriginalSha256
            || result.PurchaseOrderId != claim.PurchaseOrderId
            || result.IssuedVersionId != claim.IssuedVersionId
            || result.Outcome != "Compared" || result.ExecutionVersion != 2
            || result.SourceSchemaVersion != 3
            || mapping.SourceSchemaVersion != 3 || mapping.IdentitySchemaVersion != 2
            || claim.Kind != "Debit" || claim.OriginalDebitId is not null
            || claim.Quantity <= 0 || claim.NetAmount < 0 || claim.TaxAmount < 0
            || claim.ReceivingSourceStateId is not null
            || claim.GoodsReceiptLineId is not null || claim.ServiceAcceptanceId is not null)
            throw new InvalidOperationException("PrincipalInvoiceClaimSourceInvalid");
        var snapshot = PrincipalInvoiceMappingEvidence.VerifyStored(mapping,
            invoice, input.OriginalLines);
        if (snapshot.PurchaseOrderId != claim.PurchaseOrderId
            || snapshot.IssuedVersionId != claim.IssuedVersionId)
            throw new InvalidOperationException("PrincipalInvoiceClaimSourceInvalid");
        var selectedSources = snapshot.Lines.Where(x => x.PrincipalReceivingFactId is not null)
            .Select(x => x.PrincipalReceivingSourceStateId!.Value).ToHashSet();
        var relevantHistories = histories.Where(x => selectedSources.Contains(x.SourceStateId)).ToArray();
        PrincipalInvoiceMappingEvidence.VerifyAcceptedSources(snapshot, relevantHistories);
        var line = snapshot.Lines.SingleOrDefault(x => x.InvoiceLineId == claim.SupplierInvoiceLineId
            && x.PurchaseOrderLineId == claim.PurchaseOrderLineId)
            ?? throw new InvalidOperationException("PrincipalInvoiceClaimMappingLineChanged");
        if (claim.PrincipalReceivingSourceStateId != line.PrincipalReceivingSourceStateId
            || claim.PrincipalReceivingFactId != line.PrincipalReceivingFactId
            || claim.PrincipalReceivingFactId.HasValue != claim.PrincipalReceivingSourceStateId.HasValue)
            throw new InvalidOperationException("PrincipalInvoiceClaimFactBindingChanged");
        if (claim.PrincipalReceivingFactId is not { } factId)
            return null; // A two-way comparison has no physical acceptance claim.
        var history = relevantHistories.Single(x => x.SourceStateId == line.PrincipalReceivingSourceStateId);
        var fact = history.Facts.Single(x => x.FactId == factId);
        if (line.InvoiceUnitCode != history.UnitCode)
            throw new InvalidOperationException("PrincipalInvoiceUnitConversionReviewRequired");
        if (fact.PurchaseOrderLineId != claim.PurchaseOrderLineId
            || fact.PurchaseOrderId != claim.PurchaseOrderId
            || fact.IssuedVersionId != claim.IssuedVersionId
            || history.SupplierId == Guid.Empty || history.SupplierId != invoice.SupplierId
            || history.BranchId == Guid.Empty || history.BranchId != invoice.BranchRef
            || history.CurrencyCode != invoice.CurrencyCode
            || claim.UnitCode != history.UnitCode
            || claim.Quantity > fact.AcceptedQuantity
            || claim.NetAmount > fact.AcceptedNetAmount)
            throw new InvalidOperationException("PrincipalInvoiceClaimExceedsAcceptedFact");
        return fact;
    }
}
