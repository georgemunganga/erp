using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

public static class InvoiceMatchExecutionEvidence
{
    public static bool OwnerCovers(InvoiceHistoricalExposure owner, InvoiceExposureReadContext context)
    {
        if (!owner.Complete || owner.State != "Verified" || string.IsNullOrWhiteSpace(owner.OwnerReference)
            || string.IsNullOrWhiteSpace(owner.Watermark) || string.IsNullOrWhiteSpace(owner.EvidenceJson)
            || owner.PolicyRulesSha256 != context.PolicyRulesSha256 || owner.ValidUntil is not { } until || until <= context.At
            || owner.EvidenceSha256 != ProcurementEvidenceEncoding.PolicyJsonDigest(owner.EvidenceJson)) return false;
        using var document = JsonDocument.Parse(owner.EvidenceJson); var root = document.RootElement;
        return root.GetProperty("schemaVersion").GetInt32() == 1 && root.GetProperty("tenantId").GetString() == context.TenantId
            && root.GetProperty("legalEntityId").GetGuid() == context.LegalEntityId
            && root.GetProperty("purchaseOrderId").GetGuid() == context.PurchaseOrderId
            && root.GetProperty("issuedVersionId").GetGuid() == context.IssuedVersionId
            && root.GetProperty("supplierId").GetGuid() == context.SupplierId
            && root.GetProperty("currencyCode").GetString() == context.CurrencyCode
            && root.GetProperty("ownerReference").GetString() == owner.OwnerReference
            && root.GetProperty("watermark").GetString() == owner.Watermark;
    }

    public static IReadOnlyList<InvoiceMatchAllocation> Claims(InvoiceMatchingEvaluation comparison,
        InvoiceMatchingInput input, InvoiceMatchResult result, InvoiceMatchExposureState exposure,
        InvoiceHistoricalExposure owner, DateTimeOffset at)
    {
        var context = new InvoiceExposureReadContext(input.Invoice.TenantId, input.Invoice.LegalEntityId, input.Invoice.BranchRef,
            input.Order.Id, input.Order.RevisionId, input.Order.SupplierId, input.Order.CurrencyCode, result.PolicyRulesSha256!, at);
        Need(comparison.ComparisonPassed && !comparison.CanPostToFinance && comparison.InvoiceId == input.Invoice.Id
            && comparison.InvoiceVersion == input.Invoice.Version && result.ExecutionVersion == 1
            && result.PurchaseOrderId == input.Order.Id && result.IssuedVersionId == input.Order.RevisionId
            && result.PolicyVersionId == input.Policy.Id && OwnerCovers(owner, context)
            && input.Consumption.Complete && result.Id != Guid.Empty && result.InputFingerprint == comparison.InputFingerprint
            && result.EvidenceSha256 == comparison.EvidenceSha256 && result.EvidenceJson == comparison.EvidenceJson
            && result.SupplierInvoiceId == input.Invoice.Id && result.MappingVersionId != null
            && exposure.TenantId == input.Invoice.TenantId && exposure.LegalEntityId == input.Invoice.LegalEntityId
            && exposure.PurchaseOrderId == input.Order.Id && exposure.IssuedVersionId == input.Order.RevisionId, "InvoiceQualifiedClaimAuthorityRequired");
        var sequence = checked(exposure.Sequence + 1);
        return comparison.Lines.Select(line =>
        {
            var source = input.Invoice.Lines.Single(x => x.Id == line.InvoiceLineId);
            // Foundation diagnostics may tolerate quantity variance; live receiving entitlement is a hard claim ceiling.
            if (line.AcceptanceId is { } acceptanceId)
            {
                var acceptance = input.Acceptances.Single(x => x.Id == acceptanceId);
                var used = input.Consumption.Allocations.Where(x => x.AcceptanceId == acceptanceId).Select(x => x.Kind == InvoiceConsumptionKind.Debit ? x.Quantity : -x.Quantity);
                var proposed = comparison.Lines.Where(x => x.AcceptanceId == acceptanceId).Select(x => Parse(x.Quantity, 18, 4));
                Need(ExactDecimalArithmetic.Sum(used.Concat(proposed)) <= ExactDecimalArithmetic.Subtract(acceptance.AcceptedQuantity, acceptance.ReturnedQuantity),
                    "InvoiceClaimRequiresAcceptedQuantity");
            }
            return new InvoiceMatchAllocation { TenantId = input.Invoice.TenantId, LegalEntityId = input.Invoice.LegalEntityId,
                SupplierInvoiceId = input.Invoice.Id, SupplierInvoiceLineId = line.InvoiceLineId, MatchResultId = result.Id,
                ExposureStateId = exposure.Id, PurchaseOrderId = input.Order.Id, IssuedVersionId = input.Order.RevisionId,
                PurchaseOrderLineId = line.PurchaseOrderLineId, SupplierId = input.Invoice.SupplierId,
                GoodsReceiptLineId = source.GoodsReceiptLineId, ServiceAcceptanceId = source.ServiceAcceptanceId,
                CurrencyCode = input.Invoice.CurrencyCode, UnitCode = source.UnitCode, Quantity = Parse(line.Quantity, 18, 4),
                NetAmount = Parse(line.NetAmount, 38, 8), TaxAmount = Parse(line.TaxAmount, 38, 8), ExposureSequence = sequence,
                ValidationReference = $"procurement.match.{result.Id:D}", OwnerCoverageReference = owner.OwnerReference! };
        }).ToArray();
    }
    public static InvoiceMatchConsumption Consumption(InvoiceMatchAllocation row, Guid? branch)
    {
        Need(row.Kind is "Debit" or "Credit" && (row.Kind == "Debit" ? row.OriginalDebitId is null : row.OriginalDebitId is not null),
            "InvoiceAllocationKindInvalid");
        return new(row.Id, row.SupplierInvoiceId, row.SupplierInvoiceLineId, row.TenantId, row.LegalEntityId, branch,
            row.SupplierId, row.PurchaseOrderId, row.IssuedVersionId, row.PurchaseOrderLineId,
            row.GoodsReceiptLineId ?? row.ServiceAcceptanceId, row.CurrencyCode, row.Kind == "Credit" ? InvoiceConsumptionKind.Credit : InvoiceConsumptionKind.Debit,
            row.OriginalDebitId, row.Quantity, row.NetAmount, row.TaxAmount, row.ValidationReference);
    }
    private static decimal Parse(string value, int precision, int scale) => ExactDecimalJsonCodec.Parse(value, precision, scale, "InvoiceMatchAmount");
    private static void Need(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
}
