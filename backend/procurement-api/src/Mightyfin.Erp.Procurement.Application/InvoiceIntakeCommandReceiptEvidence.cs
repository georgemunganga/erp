using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Verifies saved 201 bytes against the owning invoice/calculation graph.
/// Host computes RequestSha256 from its canonical request before invoking Create.</summary>
public static class InvoiceIntakeCommandReceiptEvidence
{
    public const string Operation = "InvoiceCaptureV2";
    public const int MaximumResponseBytes = 262144;

    /// <summary>Complete persisted set proof for the owning snapshot. A missing
    /// receipt is a gap even when a calculation row and its audit still exist.</summary>
    public static void VerifySet(IReadOnlyList<InvoiceIntakeCommandReceipt> receipts,
        IReadOnlyList<InvoiceAmountCalculation> calculations,
        IReadOnlyList<SupplierInvoice> invoices,
        IReadOnlyList<SupplierInvoiceLine> lines)
    {
        Need(receipts is not null && calculations is not null && invoices is not null
            && lines is not null && receipts.Count == calculations.Count
            && receipts.Count <= 100_000
            && receipts.Select(x => x.Id).Distinct().Count() == receipts.Count
            && receipts.Select(x => (x.TenantId,x.LegalEntityId,x.ActorPrincipalId,x.CommandKey))
                .Distinct().Count() == receipts.Count
            && receipts.Select(x => (x.TenantId,x.LegalEntityId,x.CalculationId))
                .Distinct().Count() == receipts.Count,
            "InvoiceIntakeReceiptSetChanged");
        var byCalculation = calculations.ToDictionary(x => (x.TenantId,x.LegalEntityId,x.Id));
        var byInvoice = invoices.ToDictionary(x => (x.TenantId,x.LegalEntityId,x.Id));
        foreach (var receipt in receipts)
        {
            if (!byCalculation.TryGetValue((receipt.TenantId,receipt.LegalEntityId,receipt.CalculationId),
                    out var calculation)
                || !byInvoice.TryGetValue((receipt.TenantId,receipt.LegalEntityId,receipt.SupplierInvoiceId),
                    out var invoice))
                throw new InvalidOperationException("InvoiceIntakeReceiptParentMissing");
            Verify(receipt, invoice, calculation, lines.Where(x => x.TenantId == receipt.TenantId
                && x.LegalEntityId == receipt.LegalEntityId && x.SupplierInvoiceId == receipt.SupplierInvoiceId)
                .ToArray());
        }
    }

    public static InvoiceIntakeCommandReceipt Create(SupplierInvoice invoice,
        InvoiceAmountCalculation calculation, IReadOnlyList<SupplierInvoiceLine> lines,
        Guid commandKey, string actor, string requestSha256, string responseJson,
        DateTimeOffset completedAtUtc)
    {
        var receipt = new InvoiceIntakeCommandReceipt {
            TenantId = invoice.TenantId, LegalEntityId = invoice.LegalEntityId,
            SupplierInvoiceId = invoice.Id, CalculationId = calculation.Id,
            CommandKey = commandKey, ActorPrincipalId = actor,
            RequestSha256 = requestSha256, ResponseJson = responseJson,
            ResponseSha256 = Sha(responseJson), ResponseStatus = 201,
            CompletedAtUtc = completedAtUtc, CreatedAt = completedAtUtc, CreatedBy = actor
        };
        Verify(receipt, invoice, calculation, lines);
        return receipt;
    }

    public static void Verify(InvoiceIntakeCommandReceipt receipt, SupplierInvoice invoice,
        InvoiceAmountCalculation calculation, IReadOnlyList<SupplierInvoiceLine> lines)
    {
        Need(receipt is not null && invoice is not null && calculation is not null && lines is not null
            && receipt.Operation == Operation && receipt.CommandKey != Guid.Empty
            && receipt.Id != Guid.Empty && receipt.Version == 1
            && receipt.UpdatedAt is null && receipt.UpdatedBy is null
            && receipt.SupplierInvoiceId == invoice.Id && receipt.CalculationId == calculation.Id
            && receipt.TenantId == invoice.TenantId && receipt.LegalEntityId == invoice.LegalEntityId
            && calculation.TenantId == receipt.TenantId && calculation.LegalEntityId == receipt.LegalEntityId
            && calculation.SupplierInvoiceId == invoice.Id
            && ProcurementPrincipalEvidence.Canonical(receipt.ActorPrincipalId)
            && receipt.ActorPrincipalId == receipt.CreatedBy
            && receipt.ActorPrincipalId == invoice.CreatedBy
            && receipt.ActorPrincipalId == calculation.CreatedBy
            && Hex64(receipt.RequestSha256) && Hex64(receipt.ResponseSha256)
            && receipt.ResponseStatus == 201 && receipt.CompletedAtUtc.Offset == TimeSpan.Zero
            && receipt.CompletedAtUtc >= calculation.ObservedAtUtc
            && receipt.CreatedAt >= receipt.CompletedAtUtc
            && receipt.ResponseJson is not null
            && Encoding.UTF8.GetByteCount(receipt.ResponseJson) is > 0 and <= MaximumResponseBytes
            && Sha(receipt.ResponseJson) == receipt.ResponseSha256,
            "InvoiceIntakeReceiptIdentityChanged");
        var result = InvoiceAmountCalculationEvidence.Verify(calculation, invoice, lines);
        try
        {
            using var document = JsonDocument.Parse(receipt.ResponseJson);
            var body = document.RootElement;
            Keys(body, "id", "invoiceNumber", "status", "grossAmount", "currencyCode",
                "branchRef", "version", "intakeSchemaVersion", "calculationId", "amounts");
            Need(body.GetProperty("id").GetGuid() == invoice.Id
                && body.GetProperty("calculationId").GetGuid() == calculation.Id
                && body.GetProperty("invoiceNumber").GetString() == invoice.InvoiceNumber
                && body.GetProperty("status").GetString() == "Received"
                && body.GetProperty("currencyCode").GetString() == invoice.CurrencyCode
                && body.GetProperty("grossAmount").GetDecimal() == invoice.GrossAmount
                && body.GetProperty("version").GetInt64() == 1
                && body.GetProperty("intakeSchemaVersion").GetInt32() == 2
                && Branch(body.GetProperty("branchRef")) == invoice.BranchRef,
                "InvoiceIntakeReceiptResponseChanged");
            var totals = body.GetProperty("amounts");
            Keys(totals, "rawNet", "net", "tax", "gross", "adjustment", "lines");
            Need(Text(totals,"rawNet",result.RawNet)
                && Text(totals,"net",result.RoundedNet)
                && Text(totals,"tax",result.Tax)
                && Text(totals,"gross",result.Gross)
                && Text(totals,"adjustment",result.RoundingAdjustment),
                "InvoiceIntakeReceiptAmountChanged");
            var responseLines = totals.GetProperty("lines");
            Need(responseLines.ValueKind == JsonValueKind.Array
                && responseLines.GetArrayLength() == result.Lines.Count,
                "InvoiceIntakeReceiptLinesChanged");
            var originalById = lines.ToDictionary(x => x.Id);
            var expected = result.Lines.OrderBy(x => originalById[x.OriginalLineId].LineNumber).ToArray();
            for (var index = 0; index < expected.Length; index++)
            {
                var line = responseLines[index]; var amount = expected[index];
                Keys(line, "lineNumber", "quantity", "unitPrice", "rawNet", "net", "tax", "gross", "adjustment");
                Need(line.GetProperty("lineNumber").GetInt32() == originalById[amount.OriginalLineId].LineNumber
                    && Text(line,"quantity",amount.Quantity)
                    && Text(line,"unitPrice",amount.UnitRate)
                    && Text(line,"rawNet",amount.RawNet)
                    && Text(line,"net",amount.RoundedNet)
                    && Text(line,"tax",amount.EnteredTax)
                    && Text(line,"gross",amount.Gross)
                    && Text(line,"adjustment",amount.RoundingAdjustment),
                    "InvoiceIntakeReceiptLinesChanged");
            }
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException
            or InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new InvalidOperationException("InvoiceIntakeReceiptResponseChanged", error);
        }
    }

    private static Guid? Branch(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetGuid();
    private static bool Text(JsonElement parent, string key, decimal expected)
        => parent.GetProperty(key).ValueKind == JsonValueKind.String
            && parent.GetProperty(key).GetString() == expected.ToString("G29", CultureInfo.InvariantCulture);
    private static void Keys(JsonElement value, params string[] keys)
    {
        Need(value.ValueKind == JsonValueKind.Object, "InvoiceIntakeReceiptResponseChanged");
        var names = value.EnumerateObject().Select(x => x.Name).ToArray();
        Need(names.Length == keys.Length && names.Distinct(StringComparer.Ordinal).Count() == names.Length
            && names.Order(StringComparer.Ordinal).SequenceEqual(keys.Order(StringComparer.Ordinal)),
            "InvoiceIntakeReceiptResponseChanged");
    }
    private static string Sha(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool Hex64(string? value) => value is { Length: 64 }
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Need(bool valid, string error)
    {
        if (!valid) throw new InvalidOperationException(error);
    }
}
