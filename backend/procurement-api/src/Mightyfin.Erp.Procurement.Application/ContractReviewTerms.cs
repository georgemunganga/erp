using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Deterministic source snapshot. No approval, signature or supplier
/// eligibility is inferred from these bytes; those require current owner checks.</summary>
public static class ContractReviewTerms
{
    public sealed record Frozen(string Json, string Sha256);

    public static Frozen Capture(SupplierContract contract, SupplierContractVersion version,
        IReadOnlyCollection<ContractRate> rates)
    {
        Need(contract.Id != Guid.Empty && version.Id != Guid.Empty && version.SupplierContractId == contract.Id
            && contract.TenantId == version.TenantId && contract.LegalEntityId == version.LegalEntityId
            && contract.Status == "Draft" && version.Status == "Draft" && version.RevisionNumber == 1
            && contract.Version > 0 && version.Version > 0 && contract.SupplierId != Guid.Empty
            && contract.OwnerSubjectId is { Length: > 0 }
            && contract.CeilingAmount == version.CeilingAmount
            && contract.EffectiveFrom == version.EffectiveFrom && contract.ExpiresOn == version.ExpiresOn
            && ExactDecimalJsonCodec.IsRepresentable(contract.CeilingAmount, 18, 4), "ContractReviewTermsInvalid");
        var ordered = rates.OrderBy(x => x.LineNumber).ThenBy(x => x.Id).ToArray();
        Need(ordered.Any(x => x.Status == "Active") && ordered.All(x => x.Id != Guid.Empty && x.SupplierContractVersionId == version.Id
            && x.TenantId == contract.TenantId && x.LegalEntityId == contract.LegalEntityId
            && x.LineNumber > 0 && x.Version > 0 && x.Description is { Length: > 0 }
            && x.UnitCode is { Length: > 0 } && (x.Status is "Active" or "Removed")
            && ExactDecimalJsonCodec.IsRepresentable(x.UnitPrice, 18, 4)
            && (x.MaximumQuantity is null || ExactDecimalJsonCodec.IsRepresentable(x.MaximumQuantity.Value, 18, 4)))
            && ordered.Select(x => x.LineNumber).Distinct().Count() == ordered.Length,
            "ContractReviewTermsInvalid");
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, contractId = contract.Id, contractVersionId = version.Id,
            contractVersion = contract.Version, versionVersion = version.Version,
            contract.Number, contract.SupplierId, contract.Title, contract.ContractType,
            contract.CurrencyCode, ceilingAmountExact = ProcurementEvidenceEncoding.Decimal(contract.CeilingAmount),
            contract.EffectiveFrom, contract.ExpiresOn, contract.RenewalNoticeOn,
            rates = ordered.Select(x => new { x.Id, x.LineNumber, x.Version, x.Status, x.StatusReason,
                x.CatalogItemId, x.Description, x.UnitCode,
                unitPriceExact = ProcurementEvidenceEncoding.Decimal(x.UnitPrice),
                maximumQuantityExact = x.MaximumQuantity is null ? null : ProcurementEvidenceEncoding.Decimal(x.MaximumQuantity.Value) }).ToArray(),
        });
        return new(json, ProcurementEvidenceEncoding.Digest(json));
    }

    // Invoke only before Submit changes Draft to PendingReview. Historical review
    // reads verify retained snapshot bytes against the recorded digest instead.
    public static void VerifyCurrentDraft(ContractReviewSubmission submission, SupplierContract contract,
        SupplierContractVersion version, IReadOnlyCollection<ContractRate> rates)
    {
        var frozen = Capture(contract, version, rates);
        Need(submission.ContractId == contract.Id && submission.ContractVersionId == version.Id
            && submission.ContractBusinessVersion == contract.Version
            && submission.ContractVersionBusinessVersion == version.Version
            && submission.SupplierId == contract.SupplierId
            && submission.TenantId == contract.TenantId && submission.LegalEntityId == contract.LegalEntityId
            && submission.TermsJson == frozen.Json && submission.TermsSha256 == frozen.Sha256,
            "ContractReviewTermsChanged");
    }

    private static void Need(bool valid, string code)
    { if (!valid) throw new InvalidOperationException(code); }
}
