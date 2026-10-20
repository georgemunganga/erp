using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public enum InvoiceMatchingMethod { TwoWay, ThreeWay }
public enum InvoiceAcceptanceKind { Goods, Service }
public enum InvoiceToleranceCombination { AllLimits, AnyLimit }
public enum InvoiceMoneyRounding { ToEven, AwayFromZero }
public enum InvoiceConsumptionKind { Debit, Credit }

/// <summary>Explicit field limits; zero/zero means exact. No implicit global tolerance.</summary>
public sealed record InvoiceFieldTolerance(decimal Absolute, decimal Percent, InvoiceToleranceCombination Combination);
public sealed record InvoiceCategoryMatchRule(string CategoryCode, string CurrencyCode, InvoiceMatchingMethod Method,
    InvoiceAcceptanceKind? AcceptanceKind, InvoiceFieldTolerance Quantity, InvoiceFieldTolerance UnitPrice,
    InvoiceFieldTolerance Tax, InvoiceFieldTolerance Total);
public sealed record InvoiceMatchingPolicy(Guid Id, int Version, string TenantId, Guid LegalEntityId, Guid? BranchRef,
    string State, string ApprovedReference, DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil, int MoneyScale, InvoiceMoneyRounding Rounding,
    IReadOnlyList<InvoiceCategoryMatchRule> Rules);

/// <summary>Immutable projection of existing intake; not a second invoice master.</summary>
public sealed record InvoiceMatchDocument(Guid Id, long Version, string TenantId, Guid LegalEntityId, Guid? BranchRef,
    Guid SupplierId, Guid? PurchaseOrderId, string InvoiceNumber, string NormalizedInvoiceNumber, string CurrencyCode, string Status, DateOnly InvoiceDate, DateOnly? DueDate,
    decimal NetAmount, decimal TaxAmount, decimal GrossAmount, Guid? EvidenceDocumentRefId, string? CreditForInvoiceRef,
    string? FinancePostingRef, IReadOnlyList<InvoiceMatchDocumentLine> Lines)
{
    public static InvoiceMatchDocument From(SupplierInvoice invoice, IEnumerable<SupplierInvoiceLine> lines,
        IReadOnlyDictionary<Guid, string>? verifiedInvoiceUnits = null) => new(
        invoice.Id, invoice.Version, invoice.TenantId, invoice.LegalEntityId, invoice.BranchRef, invoice.SupplierId,
        invoice.PurchaseOrderId, invoice.InvoiceNumber, invoice.NormalizedInvoiceNumber, invoice.CurrencyCode, invoice.Status,
        invoice.InvoiceDate, invoice.DueDate, invoice.NetAmount, invoice.TaxAmount, invoice.GrossAmount, invoice.EvidenceDocumentRefId, invoice.CreditForInvoiceRef,
        invoice.FinancePostingRef, lines.Select(x => new InvoiceMatchDocumentLine(
            x.Id, x.Version, x.SupplierInvoiceId, x.TenantId, x.LegalEntityId, x.PurchaseOrderLineId,
            x.GoodsReceiptLineId, x.ServiceAcceptanceId, x.Quantity, x.UnitPrice, x.TaxAmount, x.Description,
            verifiedInvoiceUnits?.GetValueOrDefault(x.Id) ?? "", x.TaxCodeRef, x.CostCenterRef)).ToArray());
}
public sealed record InvoiceMatchDocumentLine(Guid Id, long Version, Guid SupplierInvoiceId, string TenantId,
    Guid LegalEntityId, Guid? PurchaseOrderLineId, Guid? GoodsReceiptLineId, Guid? ServiceAcceptanceId,
    decimal Quantity, decimal UnitPrice, decimal TaxAmount, string Description, string UnitCode,
    string? TaxCodeRef, string? CostCenterRef);

/// <summary>Only an issued-revision adapter may produce operational evidence. No such adapter is activated here.</summary>
public sealed record InvoiceIssuedOrder(Guid Id, Guid RevisionId, int RevisionNumber, long Version, string TenantId,
    Guid LegalEntityId, Guid? BranchRef, Guid SupplierId, string CurrencyCode, string RevisionState, string IssuedReference,
    DateTimeOffset IssuedAt, IReadOnlyList<InvoiceIssuedOrderLine> Lines);
public sealed record InvoiceIssuedOrderLine(Guid Id, long Version, string CategoryCode, string UnitCode,
    decimal Quantity, decimal UnitPrice, decimal TaxAmount);
public sealed record InvoiceAcceptedFulfilment(Guid Id, long Version, string TenantId, Guid LegalEntityId,
    Guid? BranchRef, Guid PurchaseOrderId, Guid RevisionId, Guid PurchaseOrderLineId, string CurrencyCode,
    string UnitCode, InvoiceAcceptanceKind Kind, string State, string AcceptanceReference, DateTimeOffset AcceptedAt,
    bool Reversed, decimal AcceptedQuantity, decimal ReturnedQuantity, decimal AcceptedNetAmount, decimal ReturnedNetAmount);

/// <summary>Prior validated matching consumption; no Finance ledger or inferred PO counter.</summary>
public sealed record InvoiceMatchConsumption(Guid Id, Guid InvoiceId, Guid InvoiceLineId, string TenantId,
    Guid LegalEntityId, Guid? BranchRef, Guid SupplierId, Guid PurchaseOrderId, Guid RevisionId, Guid PurchaseOrderLineId,
    Guid? AcceptanceId, string CurrencyCode, InvoiceConsumptionKind Kind, Guid? OriginalDebitId,
    decimal Quantity, decimal NetAmount, decimal TaxAmount, string ValidationReference);
public sealed record InvoiceConsumptionEvidence(string Watermark, bool Complete, IReadOnlyList<InvoiceMatchConsumption> Allocations);
public sealed record InvoiceDuplicateIdentity(Guid InvoiceId, string TenantId, Guid LegalEntityId, Guid SupplierId,
    string NormalizedInvoiceNumber, string CurrencyCode);
public sealed record InvoiceDuplicateEvidence(string Watermark, bool Complete, IReadOnlyList<InvoiceDuplicateIdentity> Identities);
public sealed record InvoiceMatchingInput(InvoiceMatchDocument Invoice, InvoiceIssuedOrder Order,
    IReadOnlyList<InvoiceAcceptedFulfilment> Acceptances, InvoiceMatchingPolicy Policy,
    InvoiceConsumptionEvidence Consumption, InvoiceDuplicateEvidence Duplicates);
public sealed record InvoiceMatchCommand(Guid CommandKey, long ExpectedInvoiceVersion, DateTimeOffset EvaluatedAt, string? Reason);
public sealed record InvoiceMatchDiscrepancy(Guid? InvoiceLineId, string Code);
public sealed record InvoiceMatchLineComparison(Guid InvoiceLineId, Guid PurchaseOrderLineId, Guid? AcceptanceId,
    InvoiceMatchingMethod Method, string Quantity, string NetAmount, string TaxAmount, string ExpectedUnitPrice);

/// <summary>Pure comparison only. Persistence, authorization and accepted-source verification are separate release gates.</summary>
public sealed record InvoiceMatchingEvaluation(Guid CommandKey, Guid InvoiceId, long InvoiceVersion, string ActorSubjectId,
    string InputFingerprint, string Outcome, IReadOnlyList<InvoiceMatchDiscrepancy> Discrepancies,
    IReadOnlyList<InvoiceMatchLineComparison> Lines, string EvidenceJson, string EvidenceSha256)
{
    public bool ComparisonPassed => Outcome == "Compared";
    public bool CanPostToFinance => false;
}
