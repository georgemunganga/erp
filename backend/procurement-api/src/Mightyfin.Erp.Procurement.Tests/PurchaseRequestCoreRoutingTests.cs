using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PurchaseRequestCoreRoutingTests
{
    public const string Rules = """
    {"schemaVersion":2,"transactionType":"PurchaseRequest","currencyCode":"ZMW","amountBasis":"EstimatedLineBase","rules":[
      {"code":"DEFAULT","priority":0,"when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,"categoryCodes":[],"purchaseTypes":[],"branchIds":[],"requesterRoleKeys":[]},"result":{"budgetReservation":"Required","sourcingMethodCode":null,"minimumQuoteCount":null,"approvalStages":[
        {"code":"SUPERVISOR","assignee":"ProcurementManager","mode":"One","binding":{"kind":"CoreRole","key":"supervisor"}},
        {"code":"EXECUTIVE","assignee":"ProcurementManager","mode":"One","binding":{"kind":"CoreRole","key":"executive"}}]}},
      {"code":"SUPERVISOR_REQUEST","priority":10,"when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,"categoryCodes":[],"purchaseTypes":[],"branchIds":[],"requesterRoleKeys":["supervisor"]},"result":{"budgetReservation":"Required","sourcingMethodCode":null,"minimumQuoteCount":null,"approvalStages":[
        {"code":"EXECUTIVE","assignee":"ProcurementManager","mode":"One","binding":{"kind":"CoreRole","key":"executive"}}]}}
    ]}
    """;

    [Fact]
    public void Requester_and_supervisor_have_distinct_explicit_routes_without_automatic_approval()
    {
        Assert.Empty(PolicyRuleValidator.Validate(Rules));
        Assert.Empty(PolicyRulePreview.AnalyzeCoverage(Rules));
        var sample = new PolicyScenario("ZMW", 100, null, null, null);
        var regular = PolicyRulePreview.Evaluate(Rules, sample);
        Assert.Equal(new[] { "SUPERVISOR", "EXECUTIVE" }, regular.ApprovalStages.Select(x => x.Code));
        var supervisor = PolicyRulePreview.Evaluate(Rules, sample with { RequesterRoleKeys = ["supervisor", "executive"] });
        Assert.Equal("SUPERVISOR_REQUEST", supervisor.RuleCode);
        Assert.Equal("executive", Assert.Single(supervisor.ApprovalStages).Binding!.Key);
        Assert.Equal("Required", supervisor.BudgetReservation);
        Assert.Equal("Match", supervisor.Status); // A matched rule does not open or approve a Core case.
    }

    [Fact]
    public void Multiple_roles_can_overlap_and_role_limited_rules_cannot_be_the_fallback()
    {
        var root = JsonNode.Parse(Rules)!;
        root["rules"]![0]!["when"]!["requesterRoleKeys"] = new JsonArray("buyer");
        Assert.Contains(PolicyRulePreview.AnalyzeCoverage(root.ToJsonString()), x => x.Message.Contains("fallback"));
        root["rules"]![0]!["priority"] = 10;
        Assert.Contains(PolicyRulePreview.AnalyzeCoverage(root.ToJsonString()), x => x.Message.Contains("overlap"));
        Assert.Equal("AmbiguousRule", PolicyRulePreview.Evaluate(root.ToJsonString(),
            new("ZMW", 10, null, null, null) { RequesterRoleKeys = ["supervisor", "buyer"] }).Status);
    }

    [Fact]
    public void Department_rules_select_the_right_head_and_disjoint_departments_do_not_overlap()
    {
        var engineering = Guid.NewGuid(); var sales = Guid.NewGuid();
        var root = JsonNode.Parse(Rules)!;
        var specific = root["rules"]![0]!.DeepClone();
        specific["code"] = "ENGINEERING"; specific["priority"] = 5;
        specific["when"]!["departmentIds"] = new JsonArray(engineering.ToString("D"));
        specific["result"]!["approvalStages"]![0]!["binding"]!["key"] = "engineering_head";
        root["rules"]!.AsArray().Add(specific);
        var next = specific.DeepClone(); next["code"] = "SALES";
        next["when"]!["departmentIds"] = new JsonArray(sales.ToString("D"));
        root["rules"]!.AsArray().Add(next);
        Assert.Empty(PolicyRulePreview.AnalyzeCoverage(root.ToJsonString()));
        var result = PolicyRulePreview.Evaluate(root.ToJsonString(), new("ZMW", 100, null, null, null) { DepartmentId = engineering });
        Assert.Equal("ENGINEERING", result.RuleCode);
        Assert.Equal("engineering_head", result.ApprovalStages[0].Binding!.Key);
        Assert.Equal("DEFAULT", PolicyRulePreview.Evaluate(root.ToJsonString(), new("ZMW", 100, null, null, null)).RuleCode);
        root["rules"]![3]!["when"]!["departmentIds"] = new JsonArray(engineering.ToString("D"));
        Assert.Contains(PolicyRulePreview.AnalyzeCoverage(root.ToJsonString()), issue => issue.Message.Contains("overlap"));
    }

    [Theory]
    [InlineData("CoreUser", "11111111-1111-4111-8111-111111111111", true)]
    [InlineData("CoreRole", "supervisor", true)]
    [InlineData("CoreUser", "00000000-0000-0000-0000-000000000000", false)]
    [InlineData("CoreUser", "A1111111-1111-4111-8111-111111111111", false)]
    [InlineData("CoreUser", "person@example.invalid", false)]
    [InlineData("CoreRole", "CEO", false)]
    [InlineData("CoreRole", " supervisor", false)]
    [InlineData("CoreRole", "", false)]
    [InlineData("DepartmentHead", "supervisor", false)]
    public void Bindings_require_explicit_canonical_Core_identifiers(string kind, string key, bool valid)
        => Assert.Equal(valid, new PurchaseRequestApproverBinding(kind, key).IsValid);

    [Fact]
    public void Role_membership_and_person_selection_are_filters_not_interchangeable_grants()
    {
        var person = "11111111-1111-4111-8111-111111111111";
        Assert.True(new PurchaseRequestApproverBinding("CoreUser", person).Matches(person, []));
        Assert.False(new PurchaseRequestApproverBinding("CoreUser", person).Matches(Guid.NewGuid().ToString("D"), ["executive"]));
        Assert.False(new PurchaseRequestApproverBinding("CoreRole", "executive").Matches(person, []));
        Assert.True(new PurchaseRequestApproverBinding("CoreRole", "executive").Matches(person, ["executive"]));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("unknown-field")]
    [InlineData("legacy-manager")]
    [InlineData("budget-owner")]
    [InlineData("missing-role-condition")]
    [InlineData("duplicate-role")]
    [InlineData("purchase-order")]
    public void Invalid_v2_rules_cannot_silently_fall_back_to_the_unfiltered_pool(string damage)
    {
        var root = JsonNode.Parse(Rules)!;
        var stage = root["rules"]![0]!["result"]!["approvalStages"]![0]!.AsObject();
        switch (damage)
        {
            case "missing": stage.Remove("binding"); break;
            case "null": stage["binding"] = null; break;
            case "unknown-field": stage["binding"]!["extra"] = true; break;
            case "legacy-manager": stage["assignee"] = "LineManager"; break;
            case "budget-owner": stage["assignee"] = "BudgetOwner"; break;
            case "missing-role-condition": root["rules"]![0]!["when"]!.AsObject().Remove("requesterRoleKeys"); break;
            case "duplicate-role": root["rules"]![0]!["when"]!["requesterRoleKeys"] = new JsonArray("supervisor", "supervisor"); break;
            case "purchase-order": root["transactionType"] = "PurchaseOrder"; root["amountBasis"] = "ApprovedLineBase"; break;
        }
        Assert.NotEmpty(PolicyRuleValidator.Validate(root.ToJsonString()));
        Assert.Equal("InvalidRules", PolicyRulePreview.Evaluate(root.ToJsonString(), new("ZMW", 1, null, null, null)).Status);
    }

    [Fact]
    public void Submission_snapshot_matches_the_same_instant_after_Postgres_microsecond_truncation()
    {
        var principal = "11111111-1111-4111-8111-111111111111";
        var original = DateTimeOffset.Parse("2026-11-04T15:00:00Z").AddTicks(7);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new { ruleSchemaVersion = 2,
            requesterPrincipalId = principal, requesterRoleKeys = new[] { "supervisor" }, requesterRoleSnapshotAt = original }));
        Assert.True(PurchaseRequestRoutingSnapshot.Valid(document.RootElement, principal, ProcurementEvidenceEncoding.DatabaseTimestamp(original)));
        Assert.False(PurchaseRequestRoutingSnapshot.Valid(document.RootElement, principal, original.AddTicks(10)));
        Assert.False(PurchaseRequestRoutingSnapshot.Valid(document.RootElement, Guid.NewGuid().ToString("D"), original));
    }

    [Fact]
    public void V1_serialization_and_constructor_contracts_remain_unchanged()
    {
        var stage = new PolicyPreviewStage("NEED", "LineManager", "One");
        Assert.Equal("{\"code\":\"NEED\",\"assignee\":\"LineManager\",\"mode\":\"One\"}",
            JsonSerializer.Serialize(stage, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.NotNull(typeof(PolicyPreviewStage).GetConstructor([typeof(string), typeof(string), typeof(string)]));
        Assert.NotNull(typeof(PolicyScenario).GetConstructor([typeof(string), typeof(decimal), typeof(string), typeof(string), typeof(Guid?)]));
        var root = JsonNode.Parse(Rules)!; root["schemaVersion"] = 1;
        Assert.NotEmpty(PolicyRuleValidator.Validate(root.ToJsonString())); // No v2 fields accepted as v1.
    }

    [Fact]
    public void Pinned_routes_preserve_binding_and_fail_closed_on_missing_snapshot_or_mixed_people()
    {
        var request = Guid.NewGuid(); var policy = Guid.NewGuid(); var line = Guid.NewGuid();
        var decision = PolicyRulePreview.Evaluate(Rules, new("ZMW", 1, null, null, null));
        var root = JsonSerializer.SerializeToNode(new { requestId = request, requestVersion = 2, policyId = policy,
            ruleSchemaVersion = 2, requesterPrincipalId = "11111111-1111-4111-8111-111111111111",
            requesterRoleKeys = Array.Empty<string>(), requesterRoleSnapshotAt = DateTimeOffset.UtcNow,
            lines = new[] { new { lineId = line, decision } } }, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var evaluation = new PolicyEvaluation { PurchaseRequestId = request, RequestVersion = 2, PolicyVersionId = policy, Outcome = "Matched", DecisionJson = root.ToJsonString() };
        var valid = ApprovalRoutePreview.FromEvaluation(evaluation, [line]);
        Assert.True(valid.HasSingleRoute);
        Assert.Equal("supervisor", valid.Lines[0].Stages[0].Binding!.Key);
        var second = root["lines"]![0]!.DeepClone(); second["lineId"] = Guid.NewGuid();
        second["decision"]!["approvalStages"]![0]!["binding"]!["key"] = "another_supervisor";
        root["lines"]!.AsArray().Add(second);
        evaluation.DecisionJson = root.ToJsonString();
        Assert.Contains("MixedLineApprovalRoutes", ApprovalRoutePreview.FromEvaluation(evaluation).Blockers);
        root.AsObject().Remove("requesterRoleKeys"); evaluation.DecisionJson = root.ToJsonString();
        Assert.Contains("RequesterRoleSnapshotInvalid", ApprovalRoutePreview.FromEvaluation(evaluation).Blockers);
    }
}
