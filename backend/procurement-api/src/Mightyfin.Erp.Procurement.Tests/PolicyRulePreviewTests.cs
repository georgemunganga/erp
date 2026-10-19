using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PolicyRulePreviewTests
{
    private const string Rules = """
        {
          "schemaVersion": 1,
          "transactionType": "PurchaseRequest",
          "currencyCode": "ZMW",
          "amountBasis": "EstimatedGross",
          "rules": [
            {
              "code": "DEFAULT", "priority": 0,
              "when": { "minimumAmountInclusive": null, "maximumAmountExclusive": null,
                "categoryCodes": [], "purchaseTypes": [], "branchIds": [] },
              "result": { "budgetReservation": "Required", "sourcingMethodCode": null,
                "minimumQuoteCount": null,
                "approvalStages": [{ "code": "NEED", "assignee": "LineManager", "mode": "One" }] }
            },
            {
              "code": "IT_HIGH", "priority": 10,
              "when": { "minimumAmountInclusive": 5000, "maximumAmountExclusive": null,
                "categoryCodes": ["IT"], "purchaseTypes": [], "branchIds": [] },
              "result": { "budgetReservation": "Required", "sourcingMethodCode": "RFQ",
                "minimumQuoteCount": 3,
                "approvalStages": [{ "code": "BUYING", "assignee": "ProcurementManager", "mode": "One" }] }
            }
          ]
        }
        """;

    [Fact]
    public void Preview_uses_inclusive_start_exclusive_end_and_explains_controls()
    {
        Assert.Empty(PolicyRulePreview.AnalyzeCoverage(Rules));
        var below = PolicyRulePreview.Evaluate(Rules, new("ZMW", 4999.9999m, "IT", null, null));
        Assert.Equal("Match", below.Status);
        Assert.Equal("DEFAULT", below.RuleCode);
        var at = PolicyRulePreview.Evaluate(Rules, new("ZMW", 5000m, "IT", null, null));
        Assert.Equal("IT_HIGH", at.RuleCode);
        Assert.Equal("RFQ", at.SourcingMethodCode);
        Assert.Equal(3, at.MinimumQuoteCount);
        Assert.Equal("ProcurementManager", Assert.Single(at.ApprovalStages).Assignee);
        Assert.Equal("DEFAULT", PolicyRulePreview.Evaluate(Rules,
            new("ZMW", 6000m, "OFFICE", null, null)).RuleCode);
        Assert.Equal("CurrencyMismatch", PolicyRulePreview.Evaluate(Rules,
            new("USD", 6000m, "IT", null, null)).Status);
    }

    [Fact]
    public void Coverage_flags_absent_fallback_and_equal_priority_overlap()
    {
        var noFallback = Rules.Replace("\"categoryCodes\": [], \"purchaseTypes\": []",
            "\"categoryCodes\": [\"OFFICE\"], \"purchaseTypes\": []", StringComparison.Ordinal);
        Assert.Contains(PolicyRulePreview.AnalyzeCoverage(noFallback), issue =>
            issue.Message.Contains("fallback", StringComparison.OrdinalIgnoreCase));
        var overlap = Rules.Replace("\"code\": \"DEFAULT\", \"priority\": 0",
            "\"code\": \"DEFAULT\", \"priority\": 10", StringComparison.Ordinal);
        Assert.Contains(PolicyRulePreview.AnalyzeCoverage(overlap), issue =>
            issue.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("AmbiguousRule", PolicyRulePreview.Evaluate(overlap,
            new("ZMW", 5000m, "IT", null, null)).Status);
    }
}
