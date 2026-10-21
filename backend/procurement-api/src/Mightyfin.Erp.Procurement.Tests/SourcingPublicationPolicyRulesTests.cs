using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SourcingPublicationPolicyRulesTests
{
    private static readonly Guid Site = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static JsonObject Rules() => JsonNode.Parse("""
        {"schemaVersion":1,"policyKey":"SOURCING","transactionType":"SourcingPublication",
         "action":"SourcingEvent.Publish","permission":"procurement-sourcing-publish",
         "currencyCode":"ZMW","amountBasis":"ApprovedLineBase","rules":[
          {"code":"EXPLICIT","priority":0,
           "when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,"categoryCodes":[],"purchaseTypes":[],"branchIds":[]},
           "result":{"publication":{"minimumQuoteCount":0,"minimumInvitedSuppliers":0,"requirementsPackRequired":false,"evaluationCriteriaRequired":true,"closingDateRequired":true},
            "approvalStages":[{"code":"COMMERCIAL","assignee":"ProcurementManager","mode":"All","principalIds":["reviewer-a","reviewer-b"]}]}}]}
        """)!.AsObject();
    private static JsonObject Rule(JsonObject doc) => doc["rules"]![0]!.AsObject();
    private static PolicyScenario Sample(decimal amount = 100m) => new("ZMW", amount, "IT", "GOODS", Site);
    [Theory] [InlineData(0)] [InlineData(20)]
    public void Explicit_zero_and_boundary_controls_and_names_are_retained_but_never_publish(int quotes)
    {
        var doc = Rules(); Rule(doc)["result"]!["publication"]!["minimumQuoteCount"] = quotes;
        var preview = SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample());
        Assert.Empty(SourcingPublicationPolicyRules.Validate(doc.ToJsonString()));
        Assert.Equal("Match", preview.Status); Assert.Equal(quotes, preview.Controls!.MinimumQuoteCount);
        Assert.False(preview.Controls.RequirementsPackRequired); Assert.True(preview.Controls.EvaluationCriteriaRequired);
        Assert.Equal(new[] { "reviewer-a", "reviewer-b" }, Assert.Single(preview.Stages).PrincipalIds);
        Assert.Equal("All", preview.Stages[0].Stage.Mode); Assert.Equal(64, preview.PolicyDigest!.Length);
        Assert.False(preview.CanPublish);
        // New document is NOT accepted by the live PR/PO policy registry.
        Assert.NotEmpty(PolicyRuleValidator.Validate(doc.ToJsonString()));
        Assert.Throws<InvalidOperationException>(() => ProcurementSpendingActions.Resolve("SourcingEvent.Publish"));
    }
    [Theory]
    [InlineData("schemaVersion", "2")] [InlineData("policyKey", "\"PR\"")]
    [InlineData("transactionType", "\"PurchaseOrder\"")] [InlineData("amountBasis", "\"EstimatedLineBase\"")]
    [InlineData("action", "\"PurchaseOrder.Approve\"")] [InlineData("permission", "\"procurement-po-approve\"")]
    [InlineData("currencyCode", "\"zmw\"")]
    public void No_wrong_version_or_contract_fallback(string field, string value)
    {
        var doc = Rules(); doc[field] = JsonNode.Parse(value);
        Assert.Equal("InvalidRules", SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample()).Status);
    }
    [Theory]
    [InlineData("minimumQuoteCount")] [InlineData("minimumInvitedSuppliers")]
    [InlineData("requirementsPackRequired")] [InlineData("evaluationCriteriaRequired")] [InlineData("closingDateRequired")]
    public void Every_control_is_explicit_no_missing_or_null_defaults(string field)
    {
        var doc = Rules(); var controls = Rule(doc)["result"]!["publication"]!.AsObject();
        controls[field] = null; Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(doc.ToJsonString()));
        controls.Remove(field); Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(doc.ToJsonString()));
    }
    [Theory] [InlineData(-1)] [InlineData(21)]
    public void Quote_bounds_are_same_as_existing_competitive_contract(int count)
    {
        var doc = Rules(); Rule(doc)["result"]!["publication"]!["minimumQuoteCount"] = count;
        Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(doc.ToJsonString()));
    }
    [Theory] [InlineData("LineManager", "One")] [InlineData("BudgetOwner", "All")]
    [InlineData("ProcurementManager", "Any")]
    public void Only_supported_named_stage_contract_is_accepted(string assignee, string mode)
    {
        var doc = Rules(); var stage = Rule(doc)["result"]!["approvalStages"]![0]!;
        stage["assignee"] = assignee; stage["mode"] = mode;
        Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(doc.ToJsonString()));
    }
    [Fact]
    public void Missing_empty_duplicate_or_unknown_names_and_fields_fail_closed()
    {
        foreach (var names in new[] { "[]", "[null]", "[\" \" ]", "[\"reviewer-a\",\"reviewer-a\"]" })
        {
            var doc = Rules(); Rule(doc)["result"]!["approvalStages"]![0]!["principalIds"] = JsonNode.Parse(names);
            Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(doc.ToJsonString()));
        }
        var unknown = Rules(); unknown["unlimited"] = true;
        Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(unknown.ToJsonString()));
        var duplicated = Rules().ToJsonString().Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1");
        Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(duplicated));
    }
    [Fact]
    public void Existing_coverage_priority_boundary_and_currency_semantics_are_reused()
    {
        var doc = Rules(); var specific = (JsonObject)Rule(doc).DeepClone(); specific["code"] = "SPECIFIC"; specific["priority"] = 1;
        specific["when"]!["minimumAmountInclusive"] = 100; specific["when"]!["maximumAmountExclusive"] = 200;
        specific["when"]!["categoryCodes"] = new JsonArray("IT");
        doc["rules"]!.AsArray().Add(specific);
        Assert.Equal("EXPLICIT", SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample(99)).RuleCode);
        Assert.Equal("SPECIFIC", SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample(100)).RuleCode);
        Assert.Equal("EXPLICIT", SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample(200)).RuleCode);
        Assert.Equal("CurrencyMismatch", SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample() with { CurrencyCode = "USD" }).Status);
        doc["rules"]!.AsArray().RemoveAt(0);
        Assert.Equal("IncompleteCoverage", SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample()).Status);
    }
    [Fact]
    public void Ambiguous_same_priority_and_silent_rounding_thresholds_are_denied()
    {
        var doc = Rules(); var duplicate = (JsonObject)Rule(doc).DeepClone(); duplicate["code"] = "OVERLAP"; doc["rules"]!.AsArray().Add(duplicate);
        Assert.NotEmpty(SourcingPublicationPolicyRules.AnalyzeCoverage(doc.ToJsonString()));
        var raw = Rules().ToJsonString().Replace("\"minimumAmountInclusive\":null", "\"minimumAmountInclusive\":100.000000000000000000000000001");
        Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(raw));
        Assert.Equal("InvalidScenario", SourcingPublicationPolicyRules.Evaluate(Rules().ToJsonString(), Sample(-1)).Status);
    }
    [Theory] [InlineData("One")] [InlineData("All")]
    public void Named_modes_retain_the_exact_existing_Core_semantics(string mode)
    {
        var doc = Rules(); Rule(doc)["result"]!["approvalStages"]![0]!["mode"] = mode;
        Assert.Equal(mode, Assert.Single(SourcingPublicationPolicyRules.Evaluate(doc.ToJsonString(), Sample()).Stages).Stage.Mode);
    }
    [Theory] [InlineData("PurchaseRequest", "EstimatedLineBase")] [InlineData("PurchaseOrder", "ApprovedLineBase")]
    public void Shared_helper_extraction_preserves_existing_policy_validation_preview_and_coverage(string transaction, string basis)
    {
        var doc = Rules();
        foreach (var key in new[] { "policyKey", "action", "permission" }) doc.Remove(key);
        doc["transactionType"] = transaction; doc["amountBasis"] = basis;
        Rule(doc)["result"] = JsonNode.Parse("""
            {"budgetReservation":"Required","sourcingMethodCode":"RFQ","minimumQuoteCount":0,
             "approvalStages":[{"code":"REVIEW","assignee":"ProcurementManager","mode":"One"}]}
            """);
        Assert.Empty(PolicyRuleValidator.Validate(doc.ToJsonString()));
        Assert.Empty(PolicyRulePreview.AnalyzeCoverage(doc.ToJsonString()));
        var preview = PolicyRulePreview.Evaluate(doc.ToJsonString(), Sample());
        Assert.Equal("Match", preview.Status); Assert.Equal("RFQ", preview.SourcingMethodCode);
        Assert.Equal(0, preview.MinimumQuoteCount); Assert.Equal("Required", preview.BudgetReservation);
    }
    private static RfqCurrentActor Actor(string name, Guid? worker = null) => new(name, worker ?? Guid.NewGuid(), "tenant", Guid.NewGuid(), Site,
        new HashSet<Guid> { Site }, false, new HashSet<string>(), true, new HashSet<Guid>());
    [Fact]
    public void Named_adapter_requires_exact_resolved_set_and_rejects_aliases_without_creating_authority()
    {
        var a = Actor("reviewer-a"); var b = Actor("reviewer-b");
        var stages = SourcingPublicationPolicyRules.BindNamedReviewers(Rules().ToJsonString(), Sample(), [b, a]);
        Assert.Equal(new[] { "reviewer-a", "reviewer-b" }, Assert.Single(stages).Reviewers.Select(x => x.SubjectId));
        foreach (var actors in new RfqCurrentActor[][] { [a], [a, a], [a, b, Actor("extra")], [a, b with { WorkerId = a.WorkerId }] })
            Assert.Throws<InvalidOperationException>(() => SourcingPublicationPolicyRules.BindNamedReviewers(Rules().ToJsonString(), Sample(), actors));
        // Binding contains no finite grants, active-account assertion or published-policy proof.
        Assert.Empty(stages[0].Reviewers[0].Permissions);
    }
}
