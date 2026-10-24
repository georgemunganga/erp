using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqCommercialRoundPolicyTests
{
    private static readonly string Maker = Guid.NewGuid().ToString("D");
    private static readonly string Publisher = Guid.NewGuid().ToString("D");
    private static readonly string Reviewer = Guid.NewGuid().ToString("D");
    private static readonly string Closer = Guid.NewGuid().ToString("D");

    private static string Rules() => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, identitySchemaVersion = 2,
        policyKey = PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
        transactionType = PrincipalRfqCommercialRoundPolicyRules.TransactionType,
        submissionMode = "ManualSurrogateOnly",
        requireOriginalOfferDocument = true,
        requireIndependentReview = true,
        closingOfficerPrincipalId = Closer,
        stages = new[] { new { code = "CommercialReview", mode = "All",
            principalIds = new[] { Reviewer } } },
    });

    [Fact]
    public void Explicit_policy_retains_named_core_actors_without_default_grants()
    {
        var rules = PrincipalRfqCommercialRoundPolicyRules.Parse(Rules());
        Assert.Equal(Closer, rules.ClosingOfficerPrincipalId);
        Assert.Equal([Reviewer], rules.Stages.Single().PrincipalIds);
        Assert.Equal(64, rules.RulesSha256.Length);
        Assert.Equal("ManualSurrogateOnly", rules.SubmissionMode);
    }

    [Fact]
    public void Draft_or_same_actor_publication_cannot_be_commercial_authority()
    {
        var now = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
        var policy = new PolicyVersion
        {
            TenantId = "fixture", LegalEntityId = Guid.NewGuid(),
            PolicyKey = PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
            RulesJson = Rules(), PolicyVersionNumber = 1,
            State = "Draft", CreatedBy = Maker, PublishedBy = Publisher,
            PublishedAt = now.AddMinutes(-1), EffectiveFrom = now.AddMinutes(-1),
            PublicationReason = "Independent approval",
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundPolicyRules.ResolveEffective(policy,
                policy.TenantId, policy.LegalEntityId, now));
        policy.State = "Published";
        policy.PublishedBy = Maker;
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundPolicyRules.ResolveEffective(policy,
                policy.TenantId, policy.LegalEntityId, now));
        policy.PublishedBy = Publisher;
        Assert.Equal(Closer, PrincipalRfqCommercialRoundPolicyRules.ResolveEffective(
            policy, policy.TenantId, policy.LegalEntityId, now).ClosingOfficerPrincipalId);
    }

    [Fact]
    public void Unknown_fields_duplicate_json_and_false_document_requirement_deny()
    {
        var valid = Rules();
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundPolicyRules.Parse(valid.Replace(
                "\"requireOriginalOfferDocument\":true",
                "\"requireOriginalOfferDocument\":false", StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundPolicyRules.Parse(valid.Replace(
                "\"schemaVersion\":1", "\"schemaVersion\":1,\"extra\":1",
                StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundPolicyRules.Parse(valid.Replace(
                "\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1",
                StringComparison.Ordinal)));
    }

    [Fact]
    public void Named_closer_cannot_also_review_the_same_round()
    {
        var overlapping = Rules().Replace(Reviewer, Closer, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundPolicyRules.Parse(overlapping));
    }
}
