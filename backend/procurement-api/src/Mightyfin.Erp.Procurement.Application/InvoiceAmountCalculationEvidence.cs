using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Pure retained arithmetic verification, not a Core authority reader.
/// Caller must verify the exact Core ID/revision/payload in the owning transaction.
/// Original lines must be the complete authoritative database/intake set.</summary>
public static class InvoiceAmountCalculationEvidence
{
    public const string AuditAction = "InvoiceAmountCalculationCaptured";
    public const string EventType = "supplier_invoice.amount_calculated.v2";
    public const int MaximumJsonBytes = 262144;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static InvoiceAmountCalculation Create(SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines, InvoiceCurrencyPrecisionSnapshot precision,
        string coreSourcePayloadSha256, DateTimeOffset corePublishedAtUtc,
        DateTimeOffset observedAtUtc, string actor)
    {
        Need(invoice is not null && originalLines is not null && precision is not null && ProcurementPrincipalEvidence.Canonical(actor), "InvoiceCalculationAuthorityRequired");
        Need(originalLines.All(x => x is not null) && Utc(corePublishedAtUtc) && Utc(observedAtUtc)
            && corePublishedAtUtc <= observedAtUtc,
            "InvoiceCalculationReferenceObservationRequired");
        corePublishedAtUtc = ProcurementEvidenceEncoding.DatabaseTimestamp(corePublishedAtUtc)!.Value;
        observedAtUtc = ProcurementEvidenceEncoding.DatabaseTimestamp(observedAtUtc)!.Value;
        var row = new InvoiceAmountCalculation
        {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, SchemaVersion = 2,
            CoreCurrencyReferenceId = precision.ReferenceId,
            CoreCurrencyReferenceRevision = precision.Revision,
            CurrencyCode = precision.CurrencyCode, MinorUnits = precision.MinorUnits,
            RoundingMode = precision.Rounding.ToString(), CoreSourcePayloadSha256 = coreSourcePayloadSha256,
            CorePublishedAtUtc = corePublishedAtUtc, ObservedAtUtc = observedAtUtc,
            InvoiceOriginalSha256 = InvoiceMappingEvidence.OriginalDigest(invoice, originalLines),
            CreatedAt = observedAtUtc, CreatedBy = actor
        };
        row.CalculationJson = Canonical(row, invoice, originalLines);
        row.CalculationSha256 = Sha(row.CalculationJson);
        row.CalculationPolicySha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(row.CalculationJson);
        Verify(row, invoice, originalLines);
        return row;
    }

    public static InvoiceCurrencyCalculationResult Verify(InvoiceAmountCalculation row,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines)
    {
        Need(row is not null && row.Id != Guid.Empty && row.SchemaVersion == 2 && row.Version == 1
            && row.UpdatedAt is null && row.UpdatedBy is null
            && ProcurementPrincipalEvidence.Canonical(row.CreatedBy)
            && invoice is not null && row.CreatedBy == invoice.CreatedBy
            && Utc(row.CreatedAt) && row.CreatedAt >= row.ObservedAtUtc,
            "InvoiceCalculationImmutableEvidenceChanged");
        var expected = Canonical(row, invoice, originalLines);
        // Exact authored canonical text admits no duplicate/extra/renamed keys,
        // alternate numeric types, trailing data or reordered string fragments.
        Need(row.CalculationJson is not null
            && Encoding.UTF8.GetByteCount(row.CalculationJson) <= MaximumJsonBytes
            && row.CalculationJson == expected && IsSha(row.CalculationSha256)
            && row.CalculationSha256 == Sha(expected)
            && IsSha(row.CalculationPolicySha256)
            && row.CalculationPolicySha256 == ProcurementEvidenceEncoding.PolicyJsonDigest(expected), "InvoiceCalculationPayloadChanged");
        return Replay(row, invoice, originalLines);
    }

    /// <summary>The same immutable envelope is recorded in both capture audit and
    /// outbox in the intake transaction; owner inventory must require both links.
    /// Presence of either marker without this child is a missing-v2-evidence denial.</summary>
    public static string MarkerJson(InvoiceAmountCalculation row) => JsonSerializer.Serialize(new
    {
        schemaVersion = 2, invoiceId = row.SupplierInvoiceId, calculationId = row.Id,
        tenantId = row.TenantId, legalEntityId = row.LegalEntityId,
        invoiceOriginalSha256 = row.InvoiceOriginalSha256,
        calculationSha256 = row.CalculationSha256, calculationPolicySha256 = row.CalculationPolicySha256,
        coreReferenceId = row.CoreCurrencyReferenceId,
        coreReferenceRevision = row.CoreCurrencyReferenceRevision,
        coreSourcePayloadSha256 = row.CoreSourcePayloadSha256
    }, Json);

    private static string Canonical(InvoiceAmountCalculation row, SupplierInvoice invoice,
        IReadOnlyList<SupplierInvoiceLine> originalLines)
    {
        var result = Replay(row, invoice, originalLines);
        var value = JsonSerializer.Serialize(new
        {
            schemaVersion = 2, invoiceId = invoice.Id, tenantId = invoice.TenantId,
            legalEntityId = invoice.LegalEntityId, invoiceOriginalSha256 = row.InvoiceOriginalSha256,
            precision = new
            {
                referenceId = row.CoreCurrencyReferenceId, revision = row.CoreCurrencyReferenceRevision,
                currencyCode = row.CurrencyCode, minorUnits = row.MinorUnits, roundingMode = row.RoundingMode,
                sourcePayloadSha256 = row.CoreSourcePayloadSha256,
                publishedAtUtc = row.CorePublishedAtUtc, observedAtUtc = row.ObservedAtUtc
            },
            lines = result.Lines.Select(x => new
            {
                originalLineId = x.OriginalLineId, quantity = Exact(x.Quantity), unitRate = Exact(x.UnitRate),
                rawNet = Exact(x.RawNet), roundedNet = Exact(x.RoundedNet), tax = Exact(x.EnteredTax),
                gross = Exact(x.Gross), roundingAdjustment = Exact(x.RoundingAdjustment)
            }).ToArray(),
            rawNet = Exact(result.RawNet), roundedNet = Exact(result.RoundedNet),
            tax = Exact(result.Tax), gross = Exact(result.Gross),
            roundingAdjustment = Exact(result.RoundingAdjustment)
        }, Json);
        Need(Encoding.UTF8.GetByteCount(value) <= MaximumJsonBytes, "InvoiceCalculationPayloadTooLarge");
        return value;
    }

    private static InvoiceCurrencyCalculationResult Replay(InvoiceAmountCalculation row,
        SupplierInvoice invoice, IReadOnlyList<SupplierInvoiceLine> originalLines)
    {
        Need(invoice is not null && invoice.Id != Guid.Empty && !string.IsNullOrWhiteSpace(invoice.TenantId)
            && invoice.LegalEntityId != Guid.Empty && row.SupplierInvoiceId == invoice.Id
            && row.TenantId == invoice.TenantId && row.LegalEntityId == invoice.LegalEntityId
            && row.CurrencyCode == invoice.CurrencyCode && row.CoreCurrencyReferenceId != Guid.Empty
            && row.CoreCurrencyReferenceRevision > 0 && IsSha(row.CoreSourcePayloadSha256)
            && Utc(row.CorePublishedAtUtc) && Utc(row.ObservedAtUtc)
            && row.CorePublishedAtUtc.Ticks % 10 == 0 && row.ObservedAtUtc.Ticks % 10 == 0
            && row.CorePublishedAtUtc <= row.ObservedAtUtc && IsSha(row.InvoiceOriginalSha256)
            && originalLines is { Count: > 0 and <= 200 }
            && originalLines.All(x => x is not null && x.Id != Guid.Empty && x.IsActive
                && x.SupplierInvoiceId == invoice.Id && x.TenantId == invoice.TenantId
                && x.LegalEntityId == invoice.LegalEntityId)
            && originalLines.Select(x => x.Id).Distinct().Count() == originalLines.Count
            && row.InvoiceOriginalSha256 == InvoiceMappingEvidence.OriginalDigest(invoice, originalLines),
            "InvoiceCalculationOriginalEvidenceChanged");
        Need(Enum.TryParse<InvoiceMoneyRounding>(row.RoundingMode, out var rounding)
            && Enum.IsDefined(rounding) && rounding.ToString() == row.RoundingMode,
            "InvoiceCalculationRoundingInvalid");
        var result = InvoiceCurrencyRoundedCalculation.Calculate(new(row.CurrencyCode,
            row.CoreCurrencyReferenceId, row.CoreCurrencyReferenceRevision, row.MinorUnits, rounding),
            originalLines.OrderBy(x => x.Id).Select(x => new InvoiceCurrencyCalculationLineInput(
                x.Id, x.Quantity, x.UnitPrice, x.TaxAmount)).ToArray());
        Need(invoice.NetAmount == result.RoundedNet && invoice.TaxAmount == result.Tax
            && invoice.GrossAmount == result.Gross, "InvoiceCalculationHeaderChanged");
        return result;
    }

    private static string Exact(decimal value) => value.ToString("0.00000000", CultureInfo.InvariantCulture);
    private static string Sha(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool IsSha(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Utc(DateTimeOffset value) => value != default && value.Offset == TimeSpan.Zero;
    private static void Need([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
