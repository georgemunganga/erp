using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Rules = Mightyfin.Erp.Procurement.Application.ManualPaymentReviewPolicyRules;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class ManualPaymentReviewPolicyRulesTests
{
    private const string DraftBase = """
        {"schemaVersion":1,"identitySchemaVersion":2,"policyKey":"MANUAL_PAYMENT_REVIEW",
         "transactionType":"ManualPaymentReview","approvalType":"SingleApprover",
         "independentCreatorApprover":true,"independentRecorderConfirmer":true}
        """;

    private static readonly string Base = WithMaximum("\"ZMW\":\"50000.0000\"");

    private static string WithMaximum(string entries) => DraftBase.Replace(
        "\"independentRecorderConfirmer\":true}",
        "\"independentRecorderConfirmer\":true,\"maximumByCurrency\":{" + entries + "}}");

    [Fact]
    public void Unfinished_core_account_draft_cannot_be_published_or_authorize_an_amount()
    {
        Assert.True(Rules.IsDocument(DraftBase));
        Assert.Empty(Rules.Validate(DraftBase, draft: true));
        Assert.Single(Rules.Validate(DraftBase));
        Assert.Equal("ManualPaymentReviewPolicyUnavailable",
            Assert.Throws<InvalidOperationException>(() => Rules.Parse(DraftBase)).Message);
        var parsed = Rules.ParseDraft(DraftBase);
        Assert.Equal(2, parsed.IdentitySchemaVersion);
        Assert.Equal("SingleApprover", parsed.ApprovalType);
        Assert.True(parsed.IndependentCreatorApprover && parsed.IndependentRecorderConfirmer);
        Assert.Null(parsed.MaximumByCurrency);
        Assert.False(Rules.WithinConfiguredMaximum(parsed, "ZMW", 1m));
        Assert.False(Rules.WithinConfiguredMaximum(parsed, "ZMW", 0));
        Assert.False(Rules.WithinConfiguredMaximum(parsed, "ZMW", 0.00001m));
        Assert.Empty(Rules.Validate(Base));
    }

    [Fact]
    public void Present_limits_are_exact_positive_bounded_and_require_currency_coverage()
    {
        var rules = Rules.Parse(WithMaximum("\"ZMW\":\"50000.0000\",\"USD\":\"0.0001\""));
        Assert.NotNull(rules.MaximumByCurrency);
        Assert.Equal(50000m, rules.MaximumByCurrency["ZMW"]);
        Assert.Equal(0.0001m, rules.MaximumByCurrency["USD"]);
        Assert.True(Rules.WithinConfiguredMaximum(rules, "ZMW", 50000m));
        Assert.False(Rules.WithinConfiguredMaximum(rules, "ZMW", 50000.0001m));
        Assert.False(Rules.WithinConfiguredMaximum(rules, "EUR", 1m));
        Assert.False(Rules.WithinConfiguredMaximum(rules, "zmw", 1m));
        var invalidTypedLimit = rules with
        {
            MaximumByCurrency = new Dictionary<string, decimal> { ["ZMW"] = 0.00001m }
        };
        Assert.False(Rules.WithinConfiguredMaximum(invalidTypedLimit, "ZMW", 0.0001m));
        invalidTypedLimit = rules with
        {
            MaximumByCurrency = new Dictionary<string, decimal> { ["ZMW"] = -1m }
        };
        Assert.False(Rules.WithinConfiguredMaximum(invalidTypedLimit, "ZMW", 0.0001m));
    }

    [Theory]
    [InlineData("\"USD\":\"0\"")]
    [InlineData("\"USD\":\"0.0000\"")]
    [InlineData("\"USD\":\"0.00001\"")]
    [InlineData("\"USD\":\"100000000000000\"")]
    [InlineData("\"USD\":\"1e2\"")]
    [InlineData("\"USD\":\"1.00\\n\"")]
    [InlineData("\"USD\":100")]
    [InlineData("\"usd\":\"1\"")]
    [InlineData("\"USDD\":\"1\"")]
    [InlineData("\"USD\":\"-1\"")]
    [InlineData("\"USD\":\"1.0000\",\"USD\":\"2.0000\"")]
    public void Invalid_or_ambiguous_currency_maximum_is_rejected(string entries)
    {
        Assert.Equal("ManualPaymentReviewPolicyUnavailable",
            Assert.Throws<InvalidOperationException>(() => Rules.Parse(WithMaximum(entries))).Message);
    }

    [Theory]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"identitySchemaVersion\":2", "\"identitySchemaVersion\":1")]
    [InlineData("\"approvalType\":\"SingleApprover\"", "\"approvalType\":\"NoApproval\"")]
    [InlineData("\"independentCreatorApprover\":true", "\"independentCreatorApprover\":false")]
    [InlineData("\"independentRecorderConfirmer\":true", "\"independentRecorderConfirmer\":false")]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":\"1\"")]
    [InlineData("\"policyKey\":\"MANUAL_PAYMENT_REVIEW\"", "\"policyKey\":\"INVOICE_UNIT_REVIEW\"")]
    [InlineData("\"transactionType\":\"ManualPaymentReview\"", "\"transactionType\":\"PurchaseRequest\"")]
    public void Weaker_or_wrong_schema_is_rejected_in_draft_and_published_parse(string from, string to)
    {
        var json = Base.Replace(from, to);
        Assert.Equal("ManualPaymentReviewPolicyUnavailable",
            Assert.Throws<InvalidOperationException>(() => Rules.Parse(json)).Message);
        Assert.Single(Rules.Validate(json, draft: true));
    }

    [Fact]
    public void Duplicate_unknown_missing_and_wrong_type_fields_are_rejected()
    {
        var cases = new[]
        {
            Base.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"),
            Base.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"allowSelfApproval\":true"),
            Base.Replace("\"independentRecorderConfirmer\":true", "\"independentRecorderConfirmer\":null"),
            Base.Replace(",\"approvalType\":\"SingleApprover\"", ""),
            WithMaximum(""),
            Base + "garbage",
            new string(' ', 16385) + Base,
        };
        foreach (var json in cases)
        {
            Assert.Equal("ManualPaymentReviewPolicyUnavailable",
                Assert.Throws<InvalidOperationException>(() => Rules.Parse(json)).Message);
            Assert.Single(Rules.Validate(json));
        }
    }

    [Fact]
    public void Effective_projection_requires_current_owned_published_policy()
    {
        var company = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var policy = new PolicyVersion
        {
            TenantId = "tenant-a", LegalEntityId = company, PolicyKey = Rules.PolicyKey,
            PolicyVersionNumber = 1, State = "Published", RulesJson = Base,
            EffectiveFrom = now.AddDays(-1), PublishedAt = now.AddHours(-1),
            CreatedBy = Guid.NewGuid().ToString("D"), PublishedBy = Guid.NewGuid().ToString("D"),
            PublicationReason = "Reviewed by policy checker"
        };
        var projection = Rules.ResolveEffective(policy, "tenant-a", company, now);
        Assert.Equal(policy.Id, projection.PolicyId);
        Assert.Equal(ProcurementEvidenceEncoding.PolicyJsonDigest(Base), projection.RulesSha256);
        policy.RulesJson = DraftBase;
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.RulesJson = Base;
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-b", company, now));
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", Guid.NewGuid(), now));
        policy.State = "Draft";
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.State = "Published"; policy.EffectiveTo = now;
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.EffectiveTo = null; policy.PublishedBy = policy.CreatedBy;
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.PublishedBy = Guid.NewGuid().ToString("D"); policy.PolicyVersionNumber = 0;
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.PolicyVersionNumber = 1; policy.PolicyKey = "INVOICE_UNIT_REVIEW";
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.PolicyKey = Rules.PolicyKey; policy.PublishedBy = "not-a-core-principal";
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.PublishedBy = Guid.NewGuid().ToString("D"); policy.PublishedAt = DateTimeOffset.MinValue;
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
        policy.PublishedAt = now.AddHours(-1); policy.CreatedBy = "not-a-core-principal";
        Assert.Throws<InvalidOperationException>(() => Rules.ResolveEffective(policy, "tenant-a", company, now));
    }
}
