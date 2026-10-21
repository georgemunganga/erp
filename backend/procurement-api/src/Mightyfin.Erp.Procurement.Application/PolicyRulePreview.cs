using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PolicyScenario(string CurrencyCode, decimal LineBaseAmount,
    string? CategoryCode, string? PurchaseType, Guid? BranchId);
public sealed record PolicyPreviewStage(string Code, string Assignee, string Mode);
public sealed record PolicyPreviewResult(string Status, string? RuleCode, int? Priority,
    string? BudgetReservation, string? SourcingMethodCode, int? MinimumQuoteCount,
    IReadOnlyList<PolicyPreviewStage> ApprovalStages, IReadOnlyList<PolicyRuleIssue> Issues);

/// <summary>Pure rule matching for preview and pinned request evidence; neither output grants spending authority.</summary>
public static class PolicyRulePreview
{
    public static IReadOnlyList<PolicyRuleIssue> AnalyzeCoverage(string json)
    {
        var structural = PolicyRuleValidator.Validate(json);
        if (structural.Count != 0) return structural;
        using var document = JsonDocument.Parse(json);
        return AnalyzeValidatedCoverage(document.RootElement.GetProperty("rules"));
    }

    // Shared conditions/priority engine. Callers must strictly validate their own typed document first.
    internal static IReadOnlyList<PolicyRuleIssue> AnalyzeValidatedCoverage(JsonElement ruleArray)
    {
        var rules = ruleArray.EnumerateArray().ToArray();
        var issues = new List<PolicyRuleIssue>();
        var fallbacks = rules.Where(rule => IsFallback(rule.GetProperty("when"))).ToArray();
        if (fallbacks.Length != 1)
            issues.Add(new("$.rules", "Add exactly one unrestricted fallback rule so no purchase is left without a route."));
        else if (rules.Any(rule => !IsFallback(rule.GetProperty("when"))
            && rule.GetProperty("priority").GetInt32() <= fallbacks[0].GetProperty("priority").GetInt32()))
            issues.Add(new("$.rules", "The fallback must have a lower priority than every specific rule."));
        for (var left = 0; left < rules.Length; left++)
        for (var right = left + 1; right < rules.Length; right++)
        {
            if (rules[left].GetProperty("priority").GetInt32() == rules[right].GetProperty("priority").GetInt32()
                && Overlap(rules[left].GetProperty("when"), rules[right].GetProperty("when")))
                issues.Add(new($"$.rules[{right}].priority",
                    $"Rules {rules[left].GetProperty("code").GetString()} and {rules[right].GetProperty("code").GetString()} overlap at the same priority."));
            if (issues.Count == 100) return issues;
        }
        return issues;
    }

    public static PolicyPreviewResult Evaluate(string json, PolicyScenario scenario)
    {
        var structural = PolicyRuleValidator.Validate(json);
        if (structural.Count != 0) return Empty("InvalidRules", structural);
        if (scenario.LineBaseAmount < 0 || decimal.Round(scenario.LineBaseAmount, 8) != scenario.LineBaseAmount
            || scenario.BranchId == Guid.Empty
            || !ValidOptionalCode(scenario.CategoryCode) || !ValidOptionalCode(scenario.PurchaseType))
            return Empty("InvalidScenario", [new("$scenario", "Use a nonnegative line base amount with up to eight decimals and valid category, purchase type and branch values.")]);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!string.Equals(root.GetProperty("currencyCode").GetString(), scenario.CurrencyCode, StringComparison.Ordinal))
            return Empty("CurrencyMismatch", [new("$scenario.currencyCode", "The sample currency differs from the rule currency.")]);
        var matches = root.GetProperty("rules").EnumerateArray()
            .Where(rule => Matches(rule.GetProperty("when"), scenario))
            .OrderByDescending(rule => rule.GetProperty("priority").GetInt32()).ToArray();
        if (matches.Length == 0) return Empty("NoMatchingRule", []);
        var highest = matches[0].GetProperty("priority").GetInt32();
        if (matches.Length > 1 && matches[1].GetProperty("priority").GetInt32() == highest)
            return Empty("AmbiguousRule", [new("$.rules", "More than one rule matches at the highest priority.")]);
        var winner = matches[0];
        var result = winner.GetProperty("result");
        var method = result.GetProperty("sourcingMethodCode");
        var quotes = result.GetProperty("minimumQuoteCount");
        var stages = result.GetProperty("approvalStages").EnumerateArray()
            .Select(stage => new PolicyPreviewStage(stage.GetProperty("code").GetString()!,
                stage.GetProperty("assignee").GetString()!, stage.GetProperty("mode").GetString()!))
            .ToArray();
        return new("Match", winner.GetProperty("code").GetString(), highest,
            result.GetProperty("budgetReservation").GetString(),
            method.ValueKind == JsonValueKind.Null ? null : method.GetString(),
            quotes.ValueKind == JsonValueKind.Null ? null : quotes.GetInt32(), stages, []);
    }

    internal static bool Matches(JsonElement when, PolicyScenario scenario)
    {
        var minimum = Amount(when, "minimumAmountInclusive");
        var maximum = Amount(when, "maximumAmountExclusive");
        return (!minimum.HasValue || scenario.LineBaseAmount >= minimum)
            && (!maximum.HasValue || scenario.LineBaseAmount < maximum)
            && CodeMatch(when.GetProperty("categoryCodes"), scenario.CategoryCode)
            && CodeMatch(when.GetProperty("purchaseTypes"), scenario.PurchaseType)
            && BranchMatch(when.GetProperty("branchIds"), scenario.BranchId);
    }
    private static bool IsFallback(JsonElement when) =>
        Amount(when, "minimumAmountInclusive") is null && Amount(when, "maximumAmountExclusive") is null
        && when.GetProperty("categoryCodes").GetArrayLength() == 0
        && when.GetProperty("purchaseTypes").GetArrayLength() == 0
        && when.GetProperty("branchIds").GetArrayLength() == 0;
    private static bool Overlap(JsonElement a, JsonElement b)
    {
        var startA = Amount(a, "minimumAmountInclusive") ?? 0;
        var startB = Amount(b, "minimumAmountInclusive") ?? 0;
        var endA = Amount(a, "maximumAmountExclusive") ?? decimal.MaxValue;
        var endB = Amount(b, "maximumAmountExclusive") ?? decimal.MaxValue;
        return startA < endB && startB < endA
            && ArrayOverlap(a.GetProperty("categoryCodes"), b.GetProperty("categoryCodes"))
            && ArrayOverlap(a.GetProperty("purchaseTypes"), b.GetProperty("purchaseTypes"))
            && ArrayOverlap(a.GetProperty("branchIds"), b.GetProperty("branchIds"));
    }
    private static bool ArrayOverlap(JsonElement a, JsonElement b) =>
        a.GetArrayLength() == 0 || b.GetArrayLength() == 0
        || a.EnumerateArray().Any(first => b.EnumerateArray().Any(second =>
            string.Equals(first.ToString(), second.ToString(), StringComparison.OrdinalIgnoreCase)));
    private static bool CodeMatch(JsonElement values, string? code) =>
        values.GetArrayLength() == 0 || code is not null
        && values.EnumerateArray().Any(value => value.GetString() == code);
    private static bool BranchMatch(JsonElement values, Guid? branch) =>
        values.GetArrayLength() == 0 || branch.HasValue
        && values.EnumerateArray().Any(value => value.GetGuid() == branch.Value);
    private static decimal? Amount(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetDecimal() : null;
    private static bool ValidOptionalCode(string? value) => value is null || ProcurementPolicyCode.IsValid(value);
    private static PolicyPreviewResult Empty(string status, IReadOnlyList<PolicyRuleIssue> issues) =>
        new(status, null, null, null, null, null, [], issues);
}
