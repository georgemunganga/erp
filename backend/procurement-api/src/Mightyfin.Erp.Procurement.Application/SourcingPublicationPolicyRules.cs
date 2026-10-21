using System.Text.Json;

namespace Mightyfin.Erp.Procurement.Application;

public sealed record SourcingPublicationControls(int MinimumQuoteCount, int MinimumInvitedSuppliers,
    bool RequirementsPackRequired, bool EvaluationCriteriaRequired, bool ClosingDateRequired);
public sealed record SourcingPublicationPolicyStage(PolicyPreviewStage Stage, IReadOnlyList<string> PrincipalIds);
public sealed record SourcingPublicationPolicyPreview(string Status, string? RuleCode, string? PolicyDigest,
    SourcingPublicationControls? Controls, IReadOnlyList<SourcingPublicationPolicyStage> Stages,
    IReadOnlyList<PolicyRuleIssue> Issues)
{
    public bool CanPublish => false;
}

/// <summary>Pure proposed SOURCING schema, not registered in the live owned policy lifecycle.
/// Reuses the existing matching/coverage engine. A match is configuration preview only: it proves
/// neither independently published policy, canonical identities, supplier/documents nor Core votes.</summary>
public static class SourcingPublicationPolicyRules
{
    public static IReadOnlyList<PolicyRuleIssue> Validate(string? json)
    {
        try { using var document = Parse(json); return []; }
        catch (Exception error) when (error is InvalidOperationException or JsonException or FormatException or KeyNotFoundException)
        { return [new("$", error.Message)]; }
    }

    public static IReadOnlyList<PolicyRuleIssue> AnalyzeCoverage(string json)
    {
        var issues = Validate(json); if (issues.Count != 0) return issues;
        using var document = Parse(json);
        return PolicyRulePreview.AnalyzeValidatedCoverage(document.RootElement.GetProperty("rules"));
    }

    public static SourcingPublicationPolicyPreview Evaluate(string json, PolicyScenario scenario)
    {
        var issues = Validate(json); if (issues.Count != 0) return Empty("InvalidRules", issues);
        using var document = Parse(json); var root = document.RootElement;
        if (scenario.LineBaseAmount < 0 || !ExactDecimalJsonCodec.IsRepresentable(scenario.LineBaseAmount, 38, 8)
            || scenario.BranchId == Guid.Empty || scenario.CategoryCode is not null && !ProcurementPolicyCode.IsValid(scenario.CategoryCode)
            || scenario.PurchaseType is not null && !ProcurementPolicyCode.IsValid(scenario.PurchaseType))
            return Empty("InvalidScenario", [new("$scenario", "Use exact selected original line base and valid classification/site codes.")]);
        if (scenario.CurrencyCode != root.GetProperty("currencyCode").GetString()) return Empty("CurrencyMismatch", []);
        // Coverage is a publication prerequisite, even when this particular sample happens to match.
        var coverage = PolicyRulePreview.AnalyzeValidatedCoverage(root.GetProperty("rules"));
        if (coverage.Count != 0) return Empty("IncompleteCoverage", coverage);
        var matches = root.GetProperty("rules").EnumerateArray()
            .Where(x => PolicyRulePreview.Matches(x.GetProperty("when"), scenario))
            .OrderByDescending(x => x.GetProperty("priority").GetInt32()).ToArray();
        if (matches.Length == 0) return Empty("NoMatchingRule", []);
        var winner = matches[0]; var result = winner.GetProperty("result"); var controls = result.GetProperty("publication");
        var stages = result.GetProperty("approvalStages").EnumerateArray().Select(x => new SourcingPublicationPolicyStage(
            new(x.GetProperty("code").GetString()!, x.GetProperty("assignee").GetString()!, x.GetProperty("mode").GetString()!),
            Array.AsReadOnly(x.GetProperty("principalIds").EnumerateArray().Select(p => p.GetString()!).ToArray()))).ToArray();
        return new("Match", winner.GetProperty("code").GetString(), ProcurementEvidenceEncoding.PolicyJsonDigest(json),
            new(controls.GetProperty("minimumQuoteCount").GetInt32(), controls.GetProperty("minimumInvitedSuppliers").GetInt32(),
                controls.GetProperty("requirementsPackRequired").GetBoolean(), controls.GetProperty("evaluationCriteriaRequired").GetBoolean(),
                controls.GetProperty("closingDateRequired").GetBoolean()), Array.AsReadOnly(stages), []);
    }

    /// <summary>Names only: the caller supplies freshly canonical-resolved actors, not client identities.
    /// The existing authority prerequisite still checks workers, permissions, maker/requester independence
    /// and finite grants. This method deliberately cannot publish or attest policy approval.</summary>
    public static IReadOnlyList<SourcingPublicationNamedStage> BindNamedReviewers(string json, PolicyScenario scenario,
        IReadOnlyList<RfqCurrentActor> serverResolvedActors)
    {
        var preview = Evaluate(json, scenario);
        Require(preview.Status == "Match", "SourcingPolicyPreviewRequired");
        var expected = preview.Stages.SelectMany(x => x.PrincipalIds).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        Require(serverResolvedActors is not null && serverResolvedActors.All(x => x is not null)
            && serverResolvedActors.Count == expected.Count
            && serverResolvedActors.Select(x => x.SubjectId).ToHashSet(StringComparer.Ordinal).SetEquals(expected)
            && serverResolvedActors.All(x => x.WorkerId != Guid.Empty)
            && serverResolvedActors.Select(x => x.WorkerId).Distinct().Count() == serverResolvedActors.Count,
            "SourcingCanonicalNamedReviewersRequired");
        return Array.AsReadOnly(preview.Stages.Select(stage => new SourcingPublicationNamedStage(stage.Stage,
            Array.AsReadOnly(stage.PrincipalIds.Select(id => serverResolvedActors.Single(x => x.SubjectId == id)).ToArray()))).ToArray());
    }

    private static JsonDocument Parse(string? json)
    {
        Require(!string.IsNullOrWhiteSpace(json) && System.Text.Encoding.UTF8.GetByteCount(json!) <= 65536, "SourcingRulesRequiredOrTooLarge");
        Require(ProcurementEvidenceEncoding.PolicyJsonDigest(json!).Length == 64, "SourcingJsonInvalid"); // Existing canonical parser rejects duplicate property names.
        var document = JsonDocument.Parse(json!);
        try
        {
            var root = document.RootElement;
            Only(root, "schemaVersion", "policyKey", "transactionType", "action", "permission", "currencyCode", "amountBasis", "rules");
            var contract = ProcurementSpendingActions.SourcingPublication;
            Require(root.GetProperty("schemaVersion").GetInt32() == 1 && Text(root, "policyKey") == "SOURCING"
                && Text(root, "transactionType") == "SourcingPublication" && Text(root, "action") == contract.Action
                && Text(root, "permission") == contract.Permission && Text(root, "amountBasis") == contract.AmountBasis,
                "SourcingPublicationTupleUnsupported");
            var currency = Text(root, "currencyCode"); Require(currency.Length == 3 && currency.All(x => x is >= 'A' and <= 'Z'), "SourcingCurrencyInvalid");
            var rules = root.GetProperty("rules"); Require(rules.ValueKind == JsonValueKind.Array && rules.GetArrayLength() is >= 1 and <= 100, "SourcingRulesBoundExceeded");
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in rules.EnumerateArray())
            {
                Only(rule, "code", "priority", "when", "result");
                Require(ProcurementPolicyCode.IsValid(Text(rule, "code")) && codes.Add(Text(rule, "code")), "SourcingRuleCodeInvalid");
                Integer(rule, "priority", 0, 100000);
                var matchingIssues = new List<PolicyRuleIssue>(); PolicyRuleValidator.ValidateWhen(rule, "$rule", matchingIssues);
                Require(matchingIssues.Count == 0, "SourcingConditionsInvalid");
                var when = rule.GetProperty("when");
                Only(when, "minimumAmountInclusive", "maximumAmountExclusive", "categoryCodes", "purchaseTypes", "branchIds");
                foreach (var field in new[] { "minimumAmountInclusive", "maximumAmountExclusive" })
                    if (when.GetProperty(field).ValueKind != JsonValueKind.Null)
                        _ = ExactDecimalJsonCodec.Parse(when.GetProperty(field).GetRawText(), 18, 4, "SourcingThreshold");
                Require(when.GetProperty("branchIds").EnumerateArray().All(x => x.GetGuid() != Guid.Empty), "SourcingSiteInvalid");
                var result = rule.GetProperty("result"); Only(result, "publication", "approvalStages");
                var controls = result.GetProperty("publication");
                Only(controls, "minimumQuoteCount", "minimumInvitedSuppliers", "requirementsPackRequired", "evaluationCriteriaRequired", "closingDateRequired");
                Integer(controls, "minimumQuoteCount", 0, 20); Integer(controls, "minimumInvitedSuppliers", 0, 100);
                _ = controls.GetProperty("requirementsPackRequired").GetBoolean(); _ = controls.GetProperty("evaluationCriteriaRequired").GetBoolean(); _ = controls.GetProperty("closingDateRequired").GetBoolean();
                var stages = result.GetProperty("approvalStages"); Require(stages.ValueKind == JsonValueKind.Array && stages.GetArrayLength() is >= 1 and <= 20, "SourcingNamedStagesRequired");
                var stageCodes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var stage in stages.EnumerateArray())
                {
                    Only(stage, "code", "assignee", "mode", "principalIds");
                    Require(ProcurementPolicyCode.IsValid(Text(stage, "code")) && stageCodes.Add(Text(stage, "code"))
                        && Text(stage, "assignee") == "ProcurementManager" && Text(stage, "mode") is "One" or "All", "SourcingStageUnsupported");
                    var principals = stage.GetProperty("principalIds"); Require(principals.ValueKind == JsonValueKind.Array && principals.GetArrayLength() is >= 1 and <= 100, "SourcingNamedPrincipalsRequired");
                    var names = principals.EnumerateArray().Select(x => x.GetString()).ToArray();
                    Require(names.All(x => !string.IsNullOrWhiteSpace(x) && x == x.Trim() && x.Length <= 200)
                        && names.Distinct(StringComparer.Ordinal).Count() == names.Length, "SourcingPrincipalInvalid");
                }
            }
            return document;
        }
        catch { document.Dispose(); throw; }
    }
    private static string Text(JsonElement row, string name) => row.GetProperty(name).GetString() ?? throw new InvalidOperationException("SourcingTextRequired");
    private static void Integer(JsonElement row, string key, int min, int max)
        => Require(row.GetProperty(key).ValueKind == JsonValueKind.Number && row.GetProperty(key).TryGetInt32(out var value) && value >= min && value <= max, "SourcingIntegerInvalid:" + key);
    private static void Only(JsonElement row, params string[] fields)
        => Require(row.ValueKind == JsonValueKind.Object && row.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(fields.Order()), "SourcingUnknownOrMissingField");
    private static void Require(bool value, string error) { if (!value) throw new InvalidOperationException(error); }
    private static SourcingPublicationPolicyPreview Empty(string status, IReadOnlyList<PolicyRuleIssue> issues) => new(status, null, null, null, [], issues);
}
