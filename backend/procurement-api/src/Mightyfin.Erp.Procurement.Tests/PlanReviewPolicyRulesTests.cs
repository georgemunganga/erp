using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PlanReviewPolicyRulesTests
{
    private static readonly Guid Reviewer = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Approver = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static string Rules(Guid review, Guid final, string reviewMode = "Any",
        int reviewQuorum = 1, string finalMode = "All", int finalQuorum = 1) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            stages = new object[]
            {
                new { purpose = "Review", mode = reviewMode, requiredApprovals = reviewQuorum,
                    assigneePrincipalIds = new[] { review.ToString("D") } },
                new { purpose = "Final", mode = finalMode, requiredApprovals = finalQuorum,
                    assigneePrincipalIds = new[] { final.ToString("D") } },
            },
        });

    [Fact]
    public void Accepts_exact_two_stage_workerless_principal_configuration()
    {
        var definition = PlanReviewPolicyRules.Read(Rules(Reviewer, Approver));
        Assert.NotNull(definition);
        Assert.True(PlanReviewPolicyRules.IsDocument(Rules(Reviewer, Approver)));
        Assert.Equal(new[] { "Review", "Final" }, definition.Stages.Select(x => x.Purpose));
        Assert.Equal(Reviewer, Assert.Single(definition.Stages[0].AssigneePrincipalIds));
        Assert.Equal(Approver, Assert.Single(definition.Stages[1].AssigneePrincipalIds));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"stages\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"stages\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"stages\":null}")]
    public void Missing_or_legacy_rules_do_not_publish(string rules) =>
        Assert.Null(PlanReviewPolicyRules.Read(rules));

    [Fact]
    public void Rejects_one_person_as_review_and_final_approver() =>
        Assert.Null(PlanReviewPolicyRules.Read(Rules(Reviewer, Reviewer)));

    [Fact]
    public void Rejects_unmet_quorum_or_wrong_mode() =>
        Assert.Null(PlanReviewPolicyRules.Read(Rules(Reviewer, Approver, reviewQuorum: 2)));

    [Fact]
    public void Rejects_extra_rule_properties_instead_of_silently_ignoring_policy()
    {
        var valid = Rules(Reviewer, Approver);
        var extra = valid.Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"autoApprove\":true");
        Assert.Null(PlanReviewPolicyRules.Read(extra));
    }

    [Fact]
    public void Invalid_plan_document_still_routes_to_plan_validator()
    {
        const string rules = "{\"schemaVersion\":2,\"stages\":[{\"purpose\":\"Review\"}]}";
        Assert.True(PlanReviewPolicyRules.IsDocument(rules));
        Assert.NotEmpty(PlanReviewPolicyRules.Validate(rules));
        Assert.False(PlanReviewPolicyRules.IsDocument("[]"));
    }
}
