using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record PolicyRuleIssue(string Path, string Message);

/// <summary>Validates a bounded v1 rule document; it does not evaluate or publish policy.</summary>
public static class PolicyRuleValidator
{
    private const int MaximumBytes = 65536;
    private static readonly string[] Assignees = ["LineManager", "BudgetOwner", "ProcurementManager"];

    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json)
    {
        var issues = new List<PolicyRuleIssue>();
        if (string.IsNullOrWhiteSpace(json))
            return [new("$", "Enter the buying rules for review.")];
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            return [new("$", "Buying rules must be no more than 64 KB.")];
        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException) { return [new("$", "Buying rules must be valid JSON.")]; }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return [new("$", "Buying rules must be a JSON object.")];
            Only(root, "$", issues, "schemaVersion", "transactionType", "currencyCode", "amountBasis", "rules");
            if (!Integer(root, "schemaVersion", 1, 1)) Add(issues, "$.schemaVersion", "Use rule schema version 1.");
            var transaction = String(root, "transactionType");
            if (transaction is not ("PurchaseRequest" or "PurchaseOrder"))
                Add(issues, "$.transactionType", "Choose PurchaseRequest or PurchaseOrder.");
            var currency = String(root, "currencyCode");
            if (currency is null || currency.Length != 3 || !currency.All(c => c is >= 'A' and <= 'Z'))
                Add(issues, "$.currencyCode", "Use a three-letter uppercase currency code.");
            var amountBasis = String(root, "amountBasis");
            if (transaction == "PurchaseRequest" && amountBasis != "EstimatedGross"
                || transaction == "PurchaseOrder" && amountBasis != "ApprovedGross")
                Add(issues, "$.amountBasis", "Use the amount basis for this transaction type.");
            if (!root.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array
                || rules.GetArrayLength() is < 1 or > 100)
            {
                Add(issues, "$.rules", "Add between 1 and 100 rules.");
                return issues;
            }
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var rule in rules.EnumerateArray())
            {
                var path = $"$.rules[{index++}]";
                if (rule.ValueKind != JsonValueKind.Object)
                { Add(issues, path, "A rule must be an object."); continue; }
                Only(rule, path, issues, "code", "priority", "when", "result");
                var code = String(rule, "code");
                if (!ValidCode(code)) Add(issues, path + ".code", "Use a unique code of up to 100 letters, numbers or underscores.");
                else if (!codes.Add(code!)) Add(issues, path + ".code", "Rule codes cannot repeat.");
                if (!Integer(rule, "priority", 0, 100000))
                    Add(issues, path + ".priority", "Priority must be a whole number from 0 to 100000.");
                ValidateWhen(rule, path, issues);
                ValidateResult(rule, path, issues);
            }
        }
        return issues;
    }

    private static void ValidateWhen(JsonElement rule, string path, List<PolicyRuleIssue> issues)
    {
        if (!rule.TryGetProperty("when", out var when) || when.ValueKind != JsonValueKind.Object)
        { Add(issues, path + ".when", "Add matching conditions, even when all are unrestricted."); return; }
        var at = path + ".when";
        Only(when, at, issues, "minimumAmountInclusive", "maximumAmountExclusive",
            "categoryCodes", "purchaseTypes", "branchIds");
        var minimum = Money(when, "minimumAmountInclusive", at, issues);
        var maximum = Money(when, "maximumAmountExclusive", at, issues);
        if (minimum.HasValue && maximum.HasValue && minimum >= maximum)
            Add(issues, at + ".maximumAmountExclusive", "The upper amount must exceed the lower amount.");
        CodeArray(when, "categoryCodes", at, issues);
        CodeArray(when, "purchaseTypes", at, issues);
        if (!when.TryGetProperty("branchIds", out var branches) || branches.ValueKind != JsonValueKind.Array
            || branches.GetArrayLength() > 100)
        { Add(issues, at + ".branchIds", "Use a list of up to 100 branch IDs; an empty list means any branch."); return; }
        var seen = new HashSet<Guid>();
        foreach (var branch in branches.EnumerateArray())
            if (branch.ValueKind != JsonValueKind.String || !Guid.TryParse(branch.GetString(), out var id) || !seen.Add(id))
                Add(issues, at + ".branchIds", "Branch IDs must be distinct valid IDs.");
    }

    private static void ValidateResult(JsonElement rule, string path, List<PolicyRuleIssue> issues)
    {
        if (!rule.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
        { Add(issues, path + ".result", "Add the required buying controls."); return; }
        var at = path + ".result";
        Only(result, at, issues, "budgetReservation", "sourcingMethodCode", "minimumQuoteCount", "approvalStages");
        if (String(result, "budgetReservation") is not ("Required" or "ExceptionReview"))
            Add(issues, at + ".budgetReservation", "Choose Required or ExceptionReview.");
        if (result.TryGetProperty("sourcingMethodCode", out var method)
            && method.ValueKind != JsonValueKind.Null && !ValidCode(method.ValueKind == JsonValueKind.String ? method.GetString() : null))
            Add(issues, at + ".sourcingMethodCode", "Choose a valid buying method code or leave it empty.");
        if (!result.TryGetProperty("minimumQuoteCount", out var quotes)
            || quotes.ValueKind != JsonValueKind.Null && (quotes.ValueKind != JsonValueKind.Number
                || !quotes.TryGetInt32(out var count) || count is < 0 or > 20))
            Add(issues, at + ".minimumQuoteCount", "Use 0 to 20 quotes, or leave this unset.");
        if (!result.TryGetProperty("approvalStages", out var stages) || stages.ValueKind != JsonValueKind.Array
            || stages.GetArrayLength() is < 1 or > 20)
        { Add(issues, at + ".approvalStages", "Add between 1 and 20 approval stages."); return; }
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var stage in stages.EnumerateArray())
        {
            var stagePath = $"{at}.approvalStages[{index++}]";
            if (stage.ValueKind != JsonValueKind.Object)
            { Add(issues, stagePath, "An approval stage must be an object."); continue; }
            Only(stage, stagePath, issues, "code", "assignee", "mode");
            var code = String(stage, "code");
            if (!ValidCode(code)) Add(issues, stagePath + ".code", "Enter a stage code of up to 100 letters, numbers or underscores.");
            else if (!codes.Add(code!)) Add(issues, stagePath + ".code", "Stage codes cannot repeat in a rule.");
            if (!Assignees.Contains(String(stage, "assignee")))
                Add(issues, stagePath + ".assignee", "Choose a supported approver assignment.");
            if (String(stage, "mode") is not ("One" or "All"))
                Add(issues, stagePath + ".mode", "Choose One or All.");
        }
    }

    private static decimal? Money(JsonElement parent, string name, string path, List<PolicyRuleIssue> issues)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var amount)
            || amount < 0 || amount > 99_999_999_999_999.9999m || decimal.Round(amount, 4) != amount)
        { Add(issues, path + "." + name, "Use a nonnegative amount with up to four decimal places, or leave it unset."); return null; }
        return amount;
    }

    private static void CodeArray(JsonElement parent, string name, string path, List<PolicyRuleIssue> issues)
    {
        if (!parent.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 100)
        { Add(issues, path + "." + name, "Use a list of up to 100 codes; an empty list means any."); return; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in array.EnumerateArray())
            if (value.ValueKind != JsonValueKind.String || !ValidCode(value.GetString()) || !seen.Add(value.GetString()!))
                Add(issues, path + "." + name, "Codes must be distinct and valid.");
    }

    private static void Only(JsonElement element, string path, List<PolicyRuleIssue> issues, params string[] allowed)
    {
        foreach (var property in element.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                Add(issues, path + "." + property.Name, "This field is not supported in rule schema v1.");
    }
    private static bool Integer(JsonElement parent, string name, int minimum, int maximum) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number) && number >= minimum && number <= maximum;
    private static string? String(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool ValidCode(string? code) => !string.IsNullOrWhiteSpace(code) && code.Length <= 100
        && code.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
    private static void Add(List<PolicyRuleIssue> issues, string path, string message)
    { if (issues.Count < 100) issues.Add(new(path, message)); }
}
