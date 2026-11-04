using System.Globalization;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Application;

/// <summary>Exact, deterministic forecast evidence pinned to one Core approval round.</summary>
public static class PlanReviewSnapshot
{
    public sealed record Prepared(string Json, string Sha256, decimal EstimatedAmount);

    public static Prepared Create(ProcurementPlan plan, long submittedVersion)
    {
        if (plan.Id == Guid.Empty || string.IsNullOrWhiteSpace(plan.TenantId)
            || plan.LegalEntityId == Guid.Empty
            || plan.BranchRef is not { } branch || branch == Guid.Empty
            || string.IsNullOrWhiteSpace(plan.CurrencyCode) || submittedVersion < 1)
            throw new InvalidOperationException("PlanReviewSourceUnavailable");
        var lines = plan.Lines.Where(x => x.Status == "Active").OrderBy(x => x.LineNumber).ThenBy(x => x.Id).ToArray();
        if (lines.Length == 0 || lines.Select(x => x.Id).Distinct().Count() != lines.Length
            || lines.Select(x => x.LineNumber).Distinct().Count() != lines.Length
            || lines.Any(x => x.Id == Guid.Empty || x.Version < 1 || x.EstimatedQuantity <= 0 || x.EstimatedAmount < 0
                || decimal.Round(x.EstimatedQuantity, 4) != x.EstimatedQuantity
                || decimal.Round(x.EstimatedAmount, 4) != x.EstimatedAmount))
            throw new InvalidOperationException("PlanReviewLinesUnavailable");
        decimal amount;
        try { amount = lines.Sum(x => x.EstimatedAmount); }
        catch (OverflowException) { throw new InvalidOperationException("PlanReviewAmountOverflow"); }
        if (amount > 99_999_999_999_999.9999m)
            throw new InvalidOperationException("PlanReviewAmountOutOfRange");
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            tenantId = plan.TenantId,
            legalEntityId = plan.LegalEntityId,
            planId = plan.Id,
            planNumber = plan.Number,
            submittedVersion,
            branchRef = branch,
            ownerSubjectId = plan.OwnerSubjectId,
            departmentRef = plan.DepartmentRef,
            financialPeriodRef = plan.FinancialPeriodRef,
            currencyCode = plan.CurrencyCode,
            fundingSourceRef = plan.FundingSourceRef,
            estimatedAmount = Decimal(amount),
            lines = lines.Select(x => new
            {
                id = x.Id, version = x.Version, lineNumber = x.LineNumber, catalogItemId = x.CatalogItemId,
                description = x.Description, categoryCode = x.CategoryCode, unitCode = x.UnitCode,
                estimatedQuantity = Decimal(x.EstimatedQuantity),
                estimatedAmount = Decimal(x.EstimatedAmount),
                expectedOn = x.ExpectedOn, fundingSourceRef = x.FundingSourceRef,
            }).ToArray(),
        });
        if (json.Length > 250_000) throw new InvalidOperationException("PlanReviewSnapshotTooLarge");
        return new(json, ProcurementEvidenceEncoding.Digest(json), amount);
    }

    public static bool Matches(ProcurementPlan plan, PlanReviewSubmission submission)
    {
        try
        {
            var now = Create(plan, submission.SubmittedPlanVersion);
            return submission.ProcurementPlanId == plan.Id
                && submission.TenantId == plan.TenantId
                && submission.LegalEntityId == plan.LegalEntityId
                && submission.BranchRef == plan.BranchRef
                && submission.CurrencyCode == plan.CurrencyCode
                && submission.EstimatedAmount == now.EstimatedAmount
                && submission.SnapshotJson == now.Json
                && submission.SnapshotSha256 == now.Sha256;
        }
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>Historical review evidence remains viewable after a return/edit/new
    /// round. Validate the sealed payload against its retained owner pins, never
    /// against the current mutable plan lines.</summary>
    public static bool TryReadRetained(PlanReviewSubmission submission, out JsonElement snapshot)
    {
        snapshot = default;
        if (submission.SnapshotJson is not { Length: > 0 and <= 250_000 }
            || submission.SnapshotSha256 != ProcurementEvidenceEncoding.Digest(submission.SnapshotJson)
            || submission.SubmittedPlanVersion < 1 || submission.BranchRef == Guid.Empty
            || submission.EstimatedAmount < 0) return false;
        try
        {
            using var document = JsonDocument.Parse(submission.SnapshotJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !Integer(root, "schemaVersion", 1)
                || !Text(root, "tenantId", submission.TenantId)
                || !GuidValue(root, "legalEntityId", submission.LegalEntityId)
                || !GuidValue(root, "planId", submission.ProcurementPlanId)
                || !Integer(root, "submittedVersion", submission.SubmittedPlanVersion)
                || !GuidValue(root, "branchRef", submission.BranchRef)
                || !Text(root, "ownerSubjectId", submission.MakerSubjectId)
                || !Text(root, "currencyCode", submission.CurrencyCode)
                || !Amount(root, "estimatedAmount", submission.EstimatedAmount)
                || !root.TryGetProperty("lines", out var lines)
                || lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() == 0)
                return false;
            decimal sum = 0;
            var lineIds = new HashSet<Guid>();
            var lineNumbers = new HashSet<int>();
            foreach (var line in lines.EnumerateArray())
            {
                if (line.ValueKind != JsonValueKind.Object
                    || !line.TryGetProperty("id", out var idValue)
                    || idValue.ValueKind != JsonValueKind.String
                    || !idValue.TryGetGuid(out var lineId) || lineId == Guid.Empty
                    || !lineIds.Add(lineId)
                    || !line.TryGetProperty("version", out var versionValue)
                    || versionValue.ValueKind != JsonValueKind.Number
                    || !versionValue.TryGetInt64(out var lineVersion) || lineVersion < 1
                    || !line.TryGetProperty("lineNumber", out var numberValue)
                    || numberValue.ValueKind != JsonValueKind.Number
                    || !numberValue.TryGetInt32(out var lineNumber) || lineNumber < 1
                    || !lineNumbers.Add(lineNumber)
                    || !line.TryGetProperty("estimatedAmount", out var amountValue)
                    || amountValue.ValueKind != JsonValueKind.String
                    || !decimal.TryParse(amountValue.GetString(), NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var lineAmount)
                    || lineAmount < 0 || Decimal(lineAmount) != amountValue.GetString()) return false;
                sum = checked(sum + lineAmount);
            }
            if (sum != submission.EstimatedAmount) return false;
            snapshot = root.Clone();
            return true;
        }
        catch (Exception error) when (error is JsonException or FormatException or OverflowException
            or InvalidOperationException) { return false; }
    }

    private static bool Text(JsonElement value, string key, string expected) =>
        value.TryGetProperty(key, out var found) && found.ValueKind == JsonValueKind.String
        && found.GetString() == expected;
    private static bool GuidValue(JsonElement value, string key, Guid expected) =>
        value.TryGetProperty(key, out var found) && found.ValueKind == JsonValueKind.String
        && found.TryGetGuid(out var id) && id == expected;
    private static bool Integer(JsonElement value, string key, long expected) =>
        value.TryGetProperty(key, out var found) && found.ValueKind == JsonValueKind.Number
        && found.TryGetInt64(out var number) && number == expected;
    private static bool Amount(JsonElement value, string key, decimal expected) =>
        value.TryGetProperty(key, out var found) && found.ValueKind == JsonValueKind.String
        && found.GetString() == Decimal(expected);

    private static string Decimal(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
