using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>
/// Pins a published rule version and its line decisions to one submitted request
/// version. A match is evidence for a later workflow, never an approval or budget
/// reservation.
/// </summary>
internal static class ProcurementPolicyEvaluation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal sealed record Assessment(string Status, bool IsAuthority,
        PolicyEvaluation? Evaluation, string? Message);

    public static async Task<Assessment> Assess(PurchaseRequest request,
        ProcurementDbContext db, ProcurementRequestScope scope, string trigger, CancellationToken ct)
    {
        var requestVersion = request.SubmittedVersion ?? request.Version;
        var existing = await db.PolicyEvaluations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PurchaseRequestId == request.Id
                && x.RequestVersion == requestVersion, ct);
        if (existing is not null) return new(existing.Outcome, false, existing, null);

        var instant = DateTimeOffset.UtcNow;
        var policies = await db.PolicyVersions.AsNoTracking()
            .Where(x => x.PolicyKey == "PURCHASE_REQUEST" && x.State == "Published"
                && x.EffectiveFrom <= instant && (x.EffectiveTo == null || x.EffectiveTo > instant))
            .OrderByDescending(x => x.PolicyVersionNumber).Take(2).ToListAsync(ct);
        if (policies.Count == 0)
            return new("MissingPolicy", false, null, "No published buying rule is active for this company yet.");
        if (policies.Count > 1)
            return new("AmbiguousPolicy", false, null, "More than one published buying rule is active. Procurement must resolve the overlap.");

        var policy = policies[0];
        var issues = PolicyRuleValidator.Validate(policy.RulesJson);
        if (issues.Count != 0)
            return new("InvalidPolicy", false, null, "The published buying rule needs correction by Procurement.");
        using var document = JsonDocument.Parse(policy.RulesJson);
        var root = document.RootElement;
        if (root.GetProperty("amountBasis").GetString() != "EstimatedLineBase")
            return new("UnsupportedAmountBasis", false, null,
                "This rule uses an amount that the current request cannot establish.");
        if (!string.Equals(root.GetProperty("currencyCode").GetString(), request.CurrencyCode,
                StringComparison.Ordinal))
            return new("CurrencyMismatch", false, null,
                "The request currency differs from the published buying rule.");

        var lines = request.Lines.Where(x => x.ApprovalStatus != "Cancelled")
            .OrderBy(x => x.LineNumber).ToArray();
        var decisions = new List<object>(lines.Length);
        string? firstFailure = null;
        foreach (var line in lines)
        {
            PolicyPreviewResult result;
            decimal lineBase;
            try { lineBase = line.RequestedQuantity * line.EstimatedUnitPrice; }
            catch (OverflowException)
            { return new("AmountOverflow", false, null, "A request line amount is too large."); }
            var category = line.CategoryCode?.Trim().ToUpperInvariant();
            var purchaseType = line.PurchaseType?.Trim().ToUpperInvariant();
            if (category is not null
                && !await db.ProcurementCategories.AsNoTracking()
                    .AnyAsync(x => x.Code == category && x.Status == "Active", ct))
                result = Blocked("InactiveReference", "Activate the request line's buying category before review.");
            else
            {
                result = PolicyRulePreview.Evaluate(policy.RulesJson,
                    new PolicyScenario(request.CurrencyCode, lineBase,
                        category, purchaseType, request.BranchRef));
                if (result.Status == "Match" && result.SourcingMethodCode is { } method
                    && !await db.ProcurementMethods.AsNoTracking()
                        .AnyAsync(x => x.Code == method && x.Status == "Active", ct))
                    result = Blocked("InactiveReference", "The required buying method is no longer active.");
            }
            if (result.Status != "Match") firstFailure ??= result.Status;
            decisions.Add(new { lineId = line.Id, lineNumber = line.LineNumber,
                lineBaseAmount = lineBase, decision = result });
        }

        var outcome = firstFailure is null ? "Matched" : "Blocked";
        var decisionJson = JsonSerializer.Serialize(new
        {
            requestId = request.Id, requestVersion, policyId = policy.Id,
            policyVersionNumber = policy.PolicyVersionNumber,
            amountBasis = "EstimatedLineBase", request.CurrencyCode, lines = decisions,
            trigger, isAuthority = false,
        }, JsonOptions);
        if (Encoding.UTF8.GetByteCount(decisionJson) > 65536)
            return new("DecisionTooLarge", false, null,
                "This request has more rule detail than the decision record can hold. Procurement must review it.");
        var evaluation = new PolicyEvaluation
        {
            PurchaseRequestId = request.Id,
            RequestVersion = requestVersion,
            PolicyVersionId = policy.Id,
            PolicyVersionNumber = policy.PolicyVersionNumber,
            AmountBasis = "EstimatedLineBase",
            DecisionJson = decisionJson,
            Outcome = outcome,
            ResultCode = firstFailure ?? "RulesMatched",
            Summary = firstFailure is null
                ? "Buying rules matched. Funding and independent approvals are still required."
                : "Buying rules need correction or an authorized exception before the request can proceed.",
            EvaluatedAt = instant,
        };
        db.AddScoped(evaluation);
        db.AddScoped(new AuditEvent
        {
            RecordType = "PurchaseRequest", RecordId = request.Id, Action = "PolicyEvaluated",
            ActorSubjectId = scope.SubjectId,
            ChangedFieldsJson = JsonSerializer.Serialize(new
            {
                requestVersion, policyId = policy.Id, policyVersionNumber = policy.PolicyVersionNumber,
                outcome, resultCode = evaluation.ResultCode, trigger, isAuthority = false,
            }),
        });
        return new(outcome, false, evaluation, evaluation.Summary);
    }

    private static PolicyPreviewResult Blocked(string code, string message) =>
        new(code, null, null, null, null, null, [], [new PolicyRuleIssue("$line", message)]);
}
