using System.Text.Json.Nodes;
using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqOfferOpeningPolicyTests
{
    private static readonly string Officer = Guid.NewGuid().ToString("D");
    private static readonly string Reader = Guid.NewGuid().ToString("D");
    private static readonly string Closer = Guid.NewGuid().ToString("D");
    private static readonly string Reviewer = Guid.NewGuid().ToString("D");
    internal static string Rules(string officer, string reader, string closer, string reviewer) => JsonSerializer.Serialize(new
    {
        schemaVersion = 2, identitySchemaVersion = 2, policyKey = "SOURCING_COMMERCIAL_ROUND",
        transactionType = "RfqCommercialRound", submissionMode = "ManualSurrogateOnly",
        requireOriginalOfferDocument = true, requireIndependentReview = true,
        closingOfficerPrincipalId = closer,
        stages = new[] { new { code = "REVIEW", mode = "One", principalIds = new[] { reviewer } } },
        offerOpening = new { officerPrincipalId = officer, readerPrincipalIds = new[] { reader },
            allowClosingOfficer = false, delayAfterCloseSeconds = 0,
            reasonRequired = false, referenceRequired = false },
    });

    [Fact]
    public void Explicit_zero_false_and_named_open_read_controls_survive_parse()
    {
        var rule = PrincipalRfqCommercialRoundPolicyRules.Parse(Rules(Officer, Reader, Closer, Reviewer));
        Assert.Equal(2, rule.SchemaVersion); var opening = Assert.IsType<PrincipalRfqOfferOpeningControls>(rule.OfferOpening);
        Assert.Equal(Officer, opening.OfficerPrincipalId); Assert.Equal([Reader], opening.ReaderPrincipalIds);
        Assert.False(opening.AllowClosingOfficer); Assert.False(opening.ReasonRequired);
        Assert.False(opening.ReferenceRequired); Assert.Equal(0, opening.DelayAfterCloseSeconds);
    }

    [Fact]
    public void Legacy_policy_bytes_do_not_acquire_opening_or_reader_authority()
    {
        var json = JsonNode.Parse(Rules(Officer, Reader, Closer, Reviewer))!.AsObject();
        json["schemaVersion"] = 1; json.Remove("offerOpening");
        var text = json.ToJsonString();
        Assert.Null(PrincipalRfqCommercialRoundPolicyRules.Parse(text).OfferOpening);
        Assert.Equal(text, json.ToJsonString());
        json["schemaVersion"] = 2;
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqCommercialRoundPolicyRules.Parse(json.ToJsonString()));
    }

    [Theory]
    [InlineData("reasonRequired")]
    [InlineData("referenceRequired")]
    [InlineData("delayAfterCloseSeconds")]
    [InlineData("allowClosingOfficer")]
    public void Omitted_controls_never_supply_guessed_defaults(string name)
    {
        var json = JsonNode.Parse(Rules(Officer, Reader, Closer, Reviewer))!;
        json["offerOpening"]!.AsObject().Remove(name);
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqCommercialRoundPolicyRules.Parse(json.ToJsonString()));
    }

    [Theory]
    [InlineData("\"reasonRequired\":false", "\"reasonRequired\":\"false\"")]
    [InlineData("\"delayAfterCloseSeconds\":0", "\"delayAfterCloseSeconds\":0.0")]
    [InlineData("\"delayAfterCloseSeconds\":0", "\"delayAfterCloseSeconds\":-1")]
    [InlineData("\"reasonRequired\":false", "\"reasonRequired\":false,\"reasonRequired\":true")]
    public void Wrong_types_numeric_forms_duplicate_fields_and_negative_delay_deny(string from, string to)
    {
        var text = Rules(Officer, Reader, Closer, Reviewer).Replace(from, to, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqCommercialRoundPolicyRules.Parse(text));
    }

    [Fact]
    public void Officer_is_not_inferred_from_closer_or_commercial_reviewer()
    {
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqCommercialRoundPolicyRules.Parse(Rules(Closer, Reader, Closer, Reviewer)));
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqCommercialRoundPolicyRules.Parse(Rules(Reviewer, Reader, Closer, Reviewer)));
    }
}
