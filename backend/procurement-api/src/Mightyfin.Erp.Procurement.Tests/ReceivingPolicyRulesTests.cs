using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;
public sealed class ReceivingPolicyRulesTests
{
    internal static string Rules(string kind = "Goods") => JsonSerializer.Serialize(new { schemaVersion = 1, transactionType = "Receiving", currencyCode = "ZMW",
        rules = new[] { new { code = "EXPLICIT", categoryCode = "IT", purchaseType = kind, kind,
            siteIds = new[] { Guid.Parse("10000000-0000-4000-8000-000000000001") }, inspectionRequired = kind == "Goods",
            checklistReference = kind == "Goods" ? "APPROVED-CHECKLIST-1" : null,
            inspectorWorkerIds = kind == "Goods" ? new[] { Guid.Parse("10000000-0000-4000-8000-000000000002") } : [],
            serviceOwnerWorkerIds = kind == "Service" ? new[] { Guid.Parse("10000000-0000-4000-8000-000000000003") } : [],
            allowRequesterCertification = false, allowBuyerCertification = false, allowReceiverCertification = false,
            requireIndependentInspector = true, evidenceMode = "ControlledManualReference", evidenceReferencePrefix = "APPROVED-REF:" } } });
    [Theory] [InlineData("Goods")] [InlineData("Service")]
    public void Parses_only_explicit_supported_named_configuration(string kind)
    {
        var document = ReceivingPolicyRules.Parse(Rules(kind)); var rule = ReceivingPolicyRules.Select(document, "IT", kind);
        Assert.Equal(kind, rule.Kind.ToString()); Assert.Single(rule.SiteIds);
        Assert.Empty(ReceivingPolicyRules.Validate(Rules(kind)));
        Assert.Throws<InvalidOperationException>(() => ReceivingPolicyRules.Select(document, "UNKNOWN", kind));
        Assert.Throws<InvalidOperationException>(() => ReceivingPolicyRules.Select(document, "IT", "Asset"));
    }
    [Theory]
    [InlineData("\"schemaVersion\":1", "\"schemaVersion\":2")]
    [InlineData("\"kind\":\"Goods\"", "\"kind\":\"Asset\"")]
    [InlineData("\"evidenceMode\":\"ControlledManualReference\"", "\"evidenceMode\":\"TrustClient\"")]
    [InlineData("\"allowBuyerCertification\":false", "\"unknownGuard\":false")]
    public void Unknown_or_missing_controls_cannot_be_silently_defaulted(string old, string replacement)
        => Assert.NotEmpty(ReceivingPolicyRules.Validate(Rules().Replace(old, replacement)));
    [Fact]
    public void Duplicate_classification_is_not_resolved_by_order()
    {
        var document = JsonSerializer.Deserialize<JsonElement>(Rules());
        var rule = document.GetProperty("rules")[0].GetRawText();
        var duplicated = document.GetRawText().Replace("[" + rule + "]", "[" + rule + "," + rule.Replace("EXPLICIT", "SECOND") + "]");
        Assert.NotEmpty(ReceivingPolicyRules.Validate(duplicated));
    }
    [Fact]
    public void Draft_or_fabricated_published_label_never_becomes_authority()
    {
        var policy = new PolicyVersion { PolicyKey = "RECEIVING", RulesJson = Rules(), State = "Draft" };
        var rule = ReceivingPolicyRules.Parse(policy.RulesJson).Rules.Single();
        Assert.Throws<InvalidOperationException>(() => ReceivingPolicyRules.Project(policy, rule));
        policy.State = "Published";
        Assert.Throws<InvalidOperationException>(() => ReceivingPolicyRules.Project(policy, rule));
        policy.PublishedAt = DateTimeOffset.UtcNow; policy.CreatedBy = "maker"; policy.PublishedBy = "checker"; policy.PublicationReason = "Independent review";
        Assert.True(ReceivingPolicyRules.Project(policy, rule).Published);
        Assert.Throws<InvalidOperationException>(() => ReceivingPolicyRules.Project(policy, rule with { AllowBuyerCertification = true }));
    }
    [Fact]
    public void Evidence_reference_is_the_exact_published_structured_source_not_client_authority()
    {
        var rule = ReceivingPolicyRules.Parse(Rules()).Rules.Single();
        ReceivingPolicyRules.VerifyReference(rule, "APPROVED-REF:INBOUND-9");
        foreach (var bad in new[] { "APPROVED-REF:", "approved-ref:9", "unrelated-reference", " APPROVED-REF:9" })
            Assert.Throws<InvalidOperationException>(() => ReceivingPolicyRules.VerifyReference(rule, bad));
    }
}
