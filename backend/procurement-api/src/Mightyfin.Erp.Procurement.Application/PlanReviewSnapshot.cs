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
            || lines.Any(x => x.Id == Guid.Empty || x.EstimatedQuantity <= 0 || x.EstimatedAmount < 0
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
                x.Id, x.LineNumber, x.CatalogItemId, x.Description, x.CategoryCode, x.UnitCode,
                estimatedQuantity = Decimal(x.EstimatedQuantity),
                estimatedAmount = Decimal(x.EstimatedAmount),
                x.ExpectedOn, x.FundingSourceRef,
            }).ToArray(),
        });
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

    private static string Decimal(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
