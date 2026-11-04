namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Trusted, locked PO projection. CommercialVersion is the approved commercial
/// revision, not the mutable row version. This value is not authorization or proof of approval.</summary>
public sealed record ManualPaymentOrderAdvanceSource(Guid OrderId, string TenantId, Guid LegalEntityId,
    Guid BranchId, Guid SupplierId, string CurrencyCode, long CommercialVersion,
    decimal ApprovedGrossAmount, string Status);

/// <summary>Resolved published terms, not a company default or a permission grant.
/// MaximumOrderGross covers the whole order to prevent splitting around an authority ceiling.</summary>
public sealed record ManualPaymentAdvanceTerms(string CurrencyCode, decimal MaximumOrderGross,
    decimal MaximumAdvancePercent);

/// <summary>Projection of retained PO-backed payment evidence; not a second payment record.
/// An owning adapter must read ALL same-order claims, including cancelled and disputed ones.</summary>
public sealed record ManualPaymentAdvanceCommitment(Guid RequestId, string TenantId, Guid LegalEntityId,
    Guid BranchId, Guid SupplierId, Guid OrderId, string CurrencyCode, long CommercialVersion,
    decimal ApprovedOrderGross, decimal Amount, string Status, decimal? ApprovedAmount, decimal? RecordedAmount);

public enum ManualPaymentOrderSettlementReview { Unknown = 0, NoUnreconciledRegisteredSettlements = 1,
    ReconciliationRequired = 2 }

public sealed record ManualPaymentAdvanceCapacitySnapshot(decimal ApprovedOrderGross, decimal AdvanceCeiling,
    decimal OtherReservedAdvances, decimal RemainingExcludingThisRequest);

/// <summary>Exact registered-evidence capacity, never a ledger/bank balance. The future
/// owning adapter must authenticate current scope, verify the genuine approved PO/policy,
/// lock the PO before invoice/payment rows in a serializable transaction, read complete
/// evidence and persist the command in that transaction. No route/store is registered here.</summary>
public static class ManualPaymentOrderAdvanceCapacity
{
    public static ManualPaymentAdvanceCapacitySnapshot Calculate(ManualPaymentOrderAdvanceSource source,
        ManualPaymentAdvanceTerms terms, IReadOnlyCollection<ManualPaymentAdvanceCommitment> claims,
        ManualPaymentOrderSettlementReview settlementReview, Guid? excludingRequestId = null)
    {
        ValidateSource(source);
        Need(source.Status is "Approved" or "Issued" or "PartiallyReceived" or "FullyReceived",
            "ManualPaymentAdvanceOrderNotEligible");
        Need(terms.CurrencyCode == source.CurrencyCode && Money(terms.MaximumOrderGross, 18, 4)
            && terms.MaximumAdvancePercent > 0 && terms.MaximumAdvancePercent <= 100
            && ExactDecimalJsonCodec.IsRepresentable(terms.MaximumAdvancePercent, 7, 4),
            "ManualPaymentAdvancePolicyInvalid");
        Need(source.ApprovedGrossAmount <= terms.MaximumOrderGross, "ManualPaymentAdvanceWholeOrderLimitExceeded");
        Need(settlementReview == ManualPaymentOrderSettlementReview.NoUnreconciledRegisteredSettlements,
            "ManualPaymentAdvanceReconciliationRequired");
        Need(excludingRequestId != Guid.Empty && claims.Select(x => x.RequestId).Distinct().Count() == claims.Count,
            "ManualPaymentAdvanceHistoryInvalid");
        foreach (var claim in claims) ValidateClaim(source, claim);
        Need(excludingRequestId is null || claims.Any(x => x.RequestId == excludingRequestId),
            "ManualPaymentAdvanceExclusionInvalid");
        try
        {
            var ceiling = ExactDecimalArithmetic.PercentOf(source.ApprovedGrossAmount, terms.MaximumAdvancePercent);
            var reserved = ExactDecimalArithmetic.Sum(claims.Where(x => x.RequestId != excludingRequestId && Reserves(x.Status)).Select(x => x.Amount));
            var remaining = ExactDecimalArithmetic.Subtract(ceiling, reserved);
            Need(remaining >= 0 && ExactDecimalJsonCodec.IsRepresentable(remaining, 38, 14),
                "ManualPaymentAdvanceRegisteredAmountExceedsOrder");
            return new(source.ApprovedGrossAmount, ceiling, reserved, remaining);
        }
        catch (InvalidOperationException exception) when (exception.Message == "ExactDecimalNotRepresentable")
        { throw new InvalidOperationException("ManualPaymentAdvanceCapacityNotRepresentable"); }
    }

    public static void RequireAmount(decimal amount, ManualPaymentAdvanceCapacitySnapshot capacity)
    {
        Need(Money(amount, 18, 4), "ManualPaymentAdvanceAmountInvalid");
        Need(capacity.ApprovedOrderGross > 0 && capacity.AdvanceCeiling > 0
            && capacity.AdvanceCeiling <= capacity.ApprovedOrderGross && capacity.OtherReservedAdvances >= 0
            && capacity.RemainingExcludingThisRequest >= 0
            && ExactDecimalArithmetic.Add(capacity.OtherReservedAdvances, capacity.RemainingExcludingThisRequest) == capacity.AdvanceCeiling,
            "ManualPaymentAdvanceCapacityInvalid");
        Need(amount <= capacity.RemainingExcludingThisRequest, "ManualPaymentAdvanceAmountExceedsRemaining");
    }

    internal static void ValidateSource(ManualPaymentOrderAdvanceSource source) => Need(
        source.OrderId != Guid.Empty && !string.IsNullOrWhiteSpace(source.TenantId)
        && source.LegalEntityId != Guid.Empty && source.BranchId != Guid.Empty && source.SupplierId != Guid.Empty
        && source.CurrencyCode is { Length: 3 } && source.CurrencyCode.All(c => c is >= 'A' and <= 'Z')
        && source.CommercialVersion > 0 && Money(source.ApprovedGrossAmount, 38, 8), "ManualPaymentAdvanceSourceInvalid");

    internal static void ValidateClaim(ManualPaymentOrderAdvanceSource source, ManualPaymentAdvanceCommitment claim)
    {
        Need(claim.RequestId != Guid.Empty && claim.TenantId == source.TenantId && claim.LegalEntityId == source.LegalEntityId
            && claim.BranchId == source.BranchId && claim.SupplierId == source.SupplierId && claim.OrderId == source.OrderId
            && claim.CurrencyCode == source.CurrencyCode && claim.CommercialVersion == source.CommercialVersion
            && claim.ApprovedOrderGross == source.ApprovedGrossAmount && Money(claim.Amount, 18, 4)
            && claim.Amount <= source.ApprovedGrossAmount,
            "ManualPaymentAdvanceSourceChanged");
        Need(claim.Status is "Draft" or "PendingApproval" or "Approved" or "PaymentRecorded" or "ConfirmedManually" or "Disputed" or "Rejected" or "Cancelled",
            "ManualPaymentAdvanceHistoryInvalid");
        var approved = claim.Status is "Approved" or "PaymentRecorded" or "ConfirmedManually" or "Disputed";
        var recorded = claim.Status is "PaymentRecorded" or "ConfirmedManually" or "Disputed";
        Need((!approved || claim.ApprovedAmount == claim.Amount)
            && (claim.ApprovedAmount is null || claim.ApprovedAmount == claim.Amount)
            && (claim.Status is not ("Draft" or "PendingApproval" or "Rejected") || claim.ApprovedAmount is null)
            && (recorded ? claim.RecordedAmount == claim.Amount : claim.RecordedAmount is null),
            "ManualPaymentAdvanceHistoryInvalid");
    }
    private static bool Reserves(string state) => state is "PendingApproval" or "Approved" or "PaymentRecorded" or "ConfirmedManually" or "Disputed";
    internal static bool Money(decimal value, int precision, int scale) => value > 0 && ExactDecimalJsonCodec.IsRepresentable(value, precision, scale);
    internal static void Need(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
