using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure configuration syntax/completeness only. No owning supplier proof, route or live schema2 policy.</summary>
public sealed class SourcingAudienceConfigurationTests
{
    private const string Complete = """
        {"supplierLifecycle":"RecordedActiveDecision","categoryMode":"Ignore","qualificationTypes":[],
         "qualificationDocuments":"NotRequired","validThrough":"PreparationInstant",
         "allowNoQualificationExpiry":false,"allowOpenEndedSupplierValidity":false,
         "contactSelection":"OneActiveSelected","contactRequiredFields":[]}
        """;

    [Theory]
    [InlineData("null", 1)]
    [InlineData("{}", 9)]
    [InlineData("{\"qualificationTypes\":null,\"contactRequiredFields\":null,\"allowNoQualificationExpiry\":null}", 9)]
    public void Unconfigured_draft_is_valid_but_never_complete_and_gains_no_defaults(string json, int missing)
    {
        Assert.Empty(SourcingAudienceConfiguration.ValidateDraft(2, json));
        var draft = SourcingAudienceConfiguration.ParseDraft(2, json);
        Assert.Null(draft.QualificationTypes); Assert.Null(draft.ContactRequiredFields); Assert.Null(draft.AllowNoQualificationExpiry);
        Assert.Null(draft.SupplierLifecycle); Assert.Null(draft.CategoryMode);
        Assert.Equal(missing, SourcingAudienceConfiguration.ReviewIssues(draft).Count); Assert.False(draft.CanPublish);
        Assert.Equal(64, draft.ConfigurationSha256.Length);
    }

    [Fact]
    public void Explicit_empty_and_false_choices_are_complete_without_becoming_publication_authority()
    {
        var draft = SourcingAudienceConfiguration.ParseDraft(2, Complete);
        Assert.True(draft.IsSpecified); Assert.Equal(2, draft.PolicySchemaVersion);
        Assert.Empty(draft.QualificationTypes!); Assert.Empty(draft.ContactRequiredFields!);
        Assert.Equal(false, draft.AllowNoQualificationExpiry); Assert.Equal(false, draft.AllowOpenEndedSupplierValidity);
        Assert.Equal(SourcingAudienceCategoryMode.Ignore, draft.CategoryMode);
        Assert.Empty(SourcingAudienceConfiguration.ReviewIssues(draft)); Assert.False(draft.CanPublish);
        var missing = SourcingAudienceConfiguration.ParseDraft(2, Complete.Replace("\"qualificationTypes\":[]", "\"qualificationTypes\":null"));
        Assert.Equal("$.audience.qualificationTypes", Assert.Single(SourcingAudienceConfiguration.ReviewIssues(missing)).Path);
        Assert.NotEqual(draft.ConfigurationSha256, missing.ConfigurationSha256);
    }

    [Fact]
    public void Complete_config_retains_explicit_categories_qualifications_contact_and_horizon_without_normalizing_labels()
    {
        var node = JsonNode.Parse(Complete)!;
        node["categoryMode"] = "CoverAllSelectedCategories"; node["qualificationTypes"] = new JsonArray("Tax Clearance", "Licence");
        node["qualificationDocuments"] = "CoreAvailableVersionRequired"; node["validThrough"] = "ClosingInstant";
        node["allowNoQualificationExpiry"] = true; node["allowOpenEndedSupplierValidity"] = true;
        node["contactSelection"] = "OneActivePrimarySelected"; node["contactRequiredFields"] = new JsonArray("Phone", "Email");
        var draft = SourcingAudienceConfiguration.ParseDraft(2, node.ToJsonString());
        Assert.Equal(new[] { "Tax Clearance", "Licence" }, draft.QualificationTypes);
        Assert.Equal(new[] { SourcingAudienceContactField.Phone, SourcingAudienceContactField.Email }, draft.ContactRequiredFields);
        Assert.Equal(SourcingAudienceCategoryMode.CoverAllSelectedCategories, draft.CategoryMode);
        Assert.Equal(SourcingAudienceQualificationDocuments.CoreAvailableVersionRequired, draft.QualificationDocuments);
        Assert.Equal(SourcingAudienceValidityHorizon.ClosingInstant, draft.ValidThrough);
        Assert.Equal(SourcingAudienceContactSelection.OneActivePrimarySelected, draft.ContactSelection);
        Assert.Empty(SourcingAudienceConfiguration.ReviewIssues(draft)); Assert.False(draft.CanPublish);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)draft.QualificationTypes!)[0] = "Tampered");
    }

    [Theory]
    [InlineData("supplierLifecycle")][InlineData("categoryMode")][InlineData("qualificationTypes")]
    [InlineData("qualificationDocuments")][InlineData("validThrough")][InlineData("allowNoQualificationExpiry")]
    [InlineData("allowOpenEndedSupplierValidity")][InlineData("contactSelection")][InlineData("contactRequiredFields")]
    public void Every_unconfigured_choice_blocks_only_its_own_configuration_completeness(string field)
    {
        var node = JsonNode.Parse(Complete)!; node[field] = null;
        var draft = SourcingAudienceConfiguration.ParseDraft(2, node.ToJsonString());
        Assert.Equal("$.audience." + field, Assert.Single(SourcingAudienceConfiguration.ReviewIssues(draft)).Path);
        Assert.False(draft.CanPublish);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(3)][InlineData(int.MaxValue)]
    public void Explicit_supported_fragment_version_does_not_upgrade_any_other_policy_schema(int version)
        => Assert.Equal("SourcingAudiencePolicySchemaUnsupported", Assert.Throws<InvalidOperationException>(() => SourcingAudienceConfiguration.ParseDraft(version, Complete)).Message);

    [Theory]
    [InlineData(null)][InlineData("")][InlineData("[]")][InlineData("true")][InlineData("\"null\"")]
    [InlineData("{},\"injected\":true")][InlineData("{\"categoryMode\":\"Ignore\",\"categoryMode\":\"Ignore\"}")]
    [InlineData("{\"categoryMode\":\"Ignore\",\"CategoryMode\":\"Ignore\"}")]
    [InlineData("{\"bankVerified\":true}")][InlineData("{\"schemaVersion\":2}")][InlineData("{\"supplierLifecycle\":\"0\"}")]
    [InlineData("{\"allowNoQualificationExpiry\":0}")][InlineData("{\"allowNoQualificationExpiry\":\"false\"}")]
    [InlineData("{\"categoryMode\":\"AnySelectedLine\"}")][InlineData("{\"categoryMode\":\"ignore\"}")]
    [InlineData("{\"validThrough\":\"Tomorrow\"}")][InlineData("{\"qualificationDocuments\":\"GuidIsEnough\"}")]
    [InlineData("{\"contactSelection\":\"First\"}")][InlineData("{\"contactRequiredFields\":[\"Email\",\"Email\"]}")]
    [InlineData("{\"contactRequiredFields\":[\"email\"]}")][InlineData("{\"contactRequiredFields\":[\"0\"]}")][InlineData("{\"contactRequiredFields\":[\"Email, Phone\"]}")]
    [InlineData("{\"contactRequiredFields\":[null]}")][InlineData("{\"qualificationTypes\":[\"Licence\",\"LICENCE\"]}")]
    [InlineData("{\"qualificationTypes\":[\" Licence\"]}")][InlineData("{\"qualificationTypes\":[\"\"]}")]
    [InlineData("{\"qualificationTypes\":[7]}")][InlineData("{\"qualificationTypes\":\"Licence\"}")]
    public void Malformed_ambiguous_or_unsupported_choices_fail_syntax_before_completeness(string? json)
    {
        Assert.Throws<InvalidOperationException>(() => SourcingAudienceConfiguration.ParseDraft(2, json));
        Assert.Single(SourcingAudienceConfiguration.ValidateDraft(2, json));
    }

    [Fact]
    public void Parser_bounds_text_list_size_and_depth_before_canonical_evidence_work()
    {
        Assert.Throws<InvalidOperationException>(() => SourcingAudienceConfiguration.ParseDraft(2, new string(' ', SourcingAudienceConfiguration.MaximumUtf8Bytes + 1)));
        Assert.Throws<InvalidOperationException>(() => SourcingAudienceConfiguration.ParseDraft(2,
            "{\"qualificationTypes\":[\"" + new string('a', 81) + "\"]}"));
        var node = JsonNode.Parse(Complete)!;
        node["qualificationTypes"] = new JsonArray(Enumerable.Range(0, 101).Select(i => JsonValue.Create("Type " + i)).Cast<JsonNode?>().ToArray());
        Assert.Throws<InvalidOperationException>(() => SourcingAudienceConfiguration.ParseDraft(2, node.ToJsonString()));
        // Use unescaped UTF-8 input; default JsonNode output escapes Unicode.
        var wide = System.Text.Json.JsonSerializer.Serialize(new { qualificationTypes = Enumerable.Range(0, 100).Select(i => new string('界', 70) + i).ToArray() },
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.True(wide.Length < SourcingAudienceConfiguration.MaximumUtf8Bytes);
        Assert.Equal("SourcingAudienceRequiredOrTooLarge", Assert.Throws<InvalidOperationException>(() => SourcingAudienceConfiguration.ParseDraft(2, wide)).Message);
        var deep = new string('[', 9) + "0" + new string(']', 9);
        Assert.Throws<InvalidOperationException>(() => SourcingAudienceConfiguration.ParseDraft(2, deep));
    }

    [Fact]
    public void Canonical_digest_tolerates_object_formatting_but_preserves_explicit_array_selection_order()
    {
        var json = JsonNode.Parse(Complete)!.AsObject();
        var reordered = new JsonObject(json.Reverse().Select(x => new KeyValuePair<string, JsonNode?>(x.Key, x.Value?.DeepClone())));
        Assert.Equal(SourcingAudienceConfiguration.ParseDraft(2, Complete).ConfigurationSha256,
            SourcingAudienceConfiguration.ParseDraft(2, reordered.ToJsonString()).ConfigurationSha256);
        json["qualificationTypes"] = new JsonArray("Tax Clearance", "Licence");
        var first = SourcingAudienceConfiguration.ParseDraft(2, json.ToJsonString());
        json["qualificationTypes"] = new JsonArray("Licence", "Tax Clearance");
        Assert.NotEqual(first.ConfigurationSha256, SourcingAudienceConfiguration.ParseDraft(2, json.ToJsonString()).ConfigurationSha256);
    }
}
