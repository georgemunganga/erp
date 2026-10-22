using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class SourcingAudiencePolicyLifecycleTests
{
    private static JsonObject Policy(int version = 2) => JsonNode.Parse("""
        {"schemaVersion":1,"policyKey":"SOURCING","transactionType":"SourcingPublication",
         "action":"SourcingEvent.Publish","permission":"procurement-sourcing-publish","currencyCode":"ZMW","amountBasis":"ApprovedLineBase",
         "rules":[{"code":"DEFAULT","priority":0,"when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,
         "categoryCodes":[],"purchaseTypes":[],"branchIds":[]},"result":{"publication":{"minimumQuoteCount":0,"minimumInvitedSuppliers":0,
         "requirementsPackRequired":false,"evaluationCriteriaRequired":false,"closingDateRequired":false},
         "approvalStages":[{"code":"NAMED","assignee":"ProcurementManager","mode":"All","principalIds":["reviewer"]}]}}]}
        """) is JsonObject root ? Version(root, version) : throw new InvalidOperationException();
    private static JsonObject Version(JsonObject root, int version)
    { root["schemaVersion"] = version; if (version == 2) root["rules"]![0]!["result"]!["audience"] = null; return root; }
    private static JsonNode Complete() => JsonNode.Parse("""
        {"supplierLifecycle":"RecordedActiveDecision","categoryMode":"Ignore","qualificationTypes":[],"qualificationDocuments":"NotRequired",
        "validThrough":"PreparationInstant","allowNoQualificationExpiry":false,"allowOpenEndedSupplierValidity":false,
        "contactSelection":"OneActiveSelected","contactRequiredFields":[]}
        """)!;

    [Fact]
    public void Incomplete_schema2_can_be_a_draft_but_not_strict_review_coverage_or_positive_preview()
    {
        var json = Policy().ToJsonString();
        Assert.Empty(SourcingPublicationPolicyRules.ValidateDraft(json));
        var issue = Assert.Single(SourcingPublicationPolicyRules.Validate(json));
        Assert.Equal("$.rules[0].result.audience", issue.Path);
        Assert.NotEmpty(SourcingPublicationPolicyRules.AnalyzeCoverage(json));
        var preview = SourcingPublicationPolicyRules.Evaluate(json, new("ZMW", 1, "IT", "GOODS", Guid.NewGuid()));
        Assert.Equal("InvalidRules", preview.Status); Assert.False(preview.CanPublish);
    }

    [Fact]
    public void Completed_explicit_choices_validate_without_becoming_published_supplier_or_Core_authority()
    {
        var root = Policy(); root["rules"]![0]!["result"]!["audience"] = Complete();
        var json = root.ToJsonString();
        Assert.Empty(SourcingPublicationPolicyRules.ValidateDraft(json)); Assert.Empty(SourcingPublicationPolicyRules.Validate(json));
        Assert.Empty(SourcingPublicationPolicyRules.AnalyzeCoverage(json));
        var preview = SourcingPublicationPolicyRules.Evaluate(json, new("ZMW", 1, "IT", "GOODS", Guid.NewGuid()));
        Assert.Equal("Match", preview.Status); Assert.False(preview.CanPublish);
    }

    [Fact]
    public void Schema1_original_contract_and_bytes_stay_unchanged_and_reject_new_fields()
    {
        var root = Policy(1); var json = root.ToJsonString(); var hash = ProcurementEvidenceEncoding.PolicyJsonDigest(json);
        Assert.Empty(SourcingPublicationPolicyRules.Validate(json)); Assert.Empty(SourcingPublicationPolicyRules.ValidateDraft(json));
        Assert.Equal(hash, ProcurementEvidenceEncoding.PolicyJsonDigest(json));
        root["rules"]![0]!["result"]!["audience"] = null;
        Assert.NotEmpty(SourcingPublicationPolicyRules.ValidateDraft(root.ToJsonString()));
        Assert.Equal(new[] { 1, 2 }, SourcingPublicationPolicyRules.SupportedSchemaVersions);
    }

    [Theory]
    [InlineData("missing")][InlineData("unknown")][InlineData("version")][InlineData("quoted-version")]
    public void Missing_unknown_or_unsupported_schema2_shape_is_not_accepted_as_incomplete_draft(string invalid)
    {
        var root = Policy();
        if (invalid == "missing") root["rules"]![0]!["result"]!.AsObject().Remove("audience");
        if (invalid == "unknown") root["rules"]![0]!["result"]!["audience"] = JsonNode.Parse("{\"bankVerified\":true}");
        if (invalid == "version") root["schemaVersion"] = 3;
        if (invalid == "quoted-version") root["schemaVersion"] = "2";
        Assert.NotEmpty(SourcingPublicationPolicyRules.ValidateDraft(root.ToJsonString()));
        Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(root.ToJsonString()));
    }

    [Fact]
    public void Original_incomplete_created_identity_survives_completion_but_submitted_and_published_hashes_stay_strict()
    {
        var maker = Guid.NewGuid().ToString("D"); var reviewer = Guid.NewGuid().ToString("D");
        var makerWorker = Guid.NewGuid(); var reviewerWorker = Guid.NewGuid(); var at = DateTimeOffset.Parse("2026-10-21T10:00:00Z");
        var policy = new PolicyVersion { TenantId = "tenant", LegalEntityId = Guid.NewGuid(), CreatedBy = maker,
            PolicyKey = "SOURCING", PolicyVersionNumber = 1, RulesJson = Policy().ToJsonString(), Version = 1, State = "Draft" };
        Assert.Empty(SourcingPublicationPolicyRules.ValidateDraft(policy.RulesJson)); Assert.NotEmpty(SourcingPublicationPolicyRules.Validate(policy.RulesJson));
        var created = SourcingPolicyIdentity.Capture(policy, policy.TenantId, policy.LegalEntityId, "Created", maker, makerWorker, 1, at);
        var originalHash = created.EvidenceSha256;
        var complete = Policy(); complete["rules"]![0]!["result"]!["audience"] = Complete(); policy.RulesJson = complete.ToJsonString();
        policy.State = "PendingReview"; policy.Version = 3; policy.ReviewRequestedBy = maker; policy.ReviewRequestedAt = at.AddMinutes(1);
        var submitted = SourcingPolicyIdentity.Capture(policy, policy.TenantId, policy.LegalEntityId, "Submitted", maker, makerWorker, 3, policy.ReviewRequestedAt.Value, created);
        SourcingPolicyIdentity.VerifySubmission(policy, created, submitted);
        policy.State = "Published"; policy.Version = 4; policy.PublishedBy = reviewer; policy.PublishedAt = at.AddMinutes(2);
        var published = SourcingPolicyIdentity.Capture(policy, policy.TenantId, policy.LegalEntityId, "Published", reviewer, reviewerWorker, 4, policy.PublishedAt.Value, created, submitted);
        SourcingPolicyIdentity.VerifyPublication(policy, created, submitted, published);
        Assert.NotEqual(created.RulesSha256, submitted.RulesSha256); Assert.Equal(submitted.RulesSha256, published.RulesSha256);
        Assert.Equal(originalHash, created.EvidenceSha256);
        policy.RulesJson = Policy().ToJsonString();
        Assert.Throws<InvalidOperationException>(() => SourcingPolicyIdentity.VerifyPublication(policy, created, submitted, published));
        Assert.Equal(originalHash, created.EvidenceSha256);
    }
}
