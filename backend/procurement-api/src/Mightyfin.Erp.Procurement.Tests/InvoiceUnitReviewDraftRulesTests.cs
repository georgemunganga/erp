using Mightyfin.Erp.Procurement.Application;
namespace Mightyfin.Erp.Procurement.Tests;
public sealed class InvoiceUnitReviewDraftRulesTests
{
    [Fact]
    public void Explicit_empty_stage_draft_is_configurable_but_never_review_complete()
    {
        const string json = "{\"schemaVersion\":1,\"transactionType\":\"InvoiceUnitReview\",\"stages\":[]}";
        Assert.Empty(InvoiceUnitReviewPolicyRules.ParseDraft(json).Stages);
        Assert.Empty(InvoiceUnitReviewPolicyRules.Validate(json, true));
        Assert.NotEmpty(InvoiceUnitReviewPolicyRules.Validate(json));
        Assert.Throws<InvalidOperationException>(() => InvoiceUnitReviewPolicyRules.Parse(json));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":2,\"transactionType\":\"InvoiceUnitReview\",\"stages\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"transactionType\":\"InvoiceUnitReview\",\"stages\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"transactionType\":\"InvoiceUnitReview\",\"stages\":[],\"complete\":true}")]
    public void Draft_syntax_does_not_relax_version_unknown_field_or_duplicate_guards(string json)
        => Assert.NotEmpty(InvoiceUnitReviewPolicyRules.Validate(json, true));

    [Fact]
    public void Manual_attestation_requires_explicit_second_policy_schema_and_mode()
    {
        const string manual = "{\"schemaVersion\":2,\"transactionType\":\"InvoiceUnitReview\",\"evidenceMode\":\"ManualAttestation\",\"stages\":[{\"code\":\"REVIEW\",\"mode\":\"One\",\"principals\":[\"11111111-1111-1111-1111-111111111111\"]}]}";
        var parsed = InvoiceUnitReviewPolicyRules.Parse(manual);
        Assert.Equal(2, parsed.SchemaVersion);
        Assert.Equal("ManualAttestation", parsed.EvidenceMode);
        Assert.NotEmpty(InvoiceUnitReviewPolicyRules.Validate(manual.Replace("ManualAttestation", "DocumentReview")));
        Assert.NotEmpty(InvoiceUnitReviewPolicyRules.Validate(manual.Replace(",\"evidenceMode\":\"ManualAttestation\"", "")));
        Assert.NotEmpty(InvoiceUnitReviewPolicyRules.Validate(manual.Replace("\"schemaVersion\":2", "\"schemaVersion\":1")));
    }
}
