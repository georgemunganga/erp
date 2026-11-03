using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using static Mightyfin.Erp.Hrm.Api.ProcurementSourcingExecutionRoutes;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProcurementPrincipalQuotationContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("procurement-sourcing-contact")]
    [InlineData("procurement-quotation-read")]
    [InlineData("procurement-quotation-capture")]
    [InlineData("procurement-quotation-cancel")]
    public void New_quotation_actions_need_explicit_current_Core_grants_even_for_ERP_admin(string action)
    {
        Assert.True(ProcurementActionCatalog.RequiresExplicitConfiguredGrant(action));
        Assert.Empty(ProcurementActionCatalog.BootstrapGrants[action]);
        Assert.Contains(ProcurementActionCatalog.AdminReadCatalog(), x => x.Key == action && x.Group == "Sourcing and RFQs");
        Assert.False(ProcurementPermissions.Allows(action, [new Claim(ClaimTypes.Role, "erp_admin")]));
        Assert.True(ProcurementPermissions.Allows(action, [new Claim(ProcurementPermissions.ConfiguredClaim, "true"),
            new Claim(ProcurementPermissions.ActionClaim, "procurement-access"), new Claim(ProcurementPermissions.ActionClaim, action)]));
    }

    [Theory]
    [InlineData("1.00000000000000000000000000000001")]
    [InlineData("0.00001")]
    [InlineData("100000000000000")]
    public void HTTP_decimal_lexemes_are_rejected_before_CLR_can_round_them(string amount)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(Valid(), Json))!;
        node["lines"]![0]!["unitPrice"] = JsonNode.Parse(amount);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QuotationDraftInput>(node.ToJsonString(), Json));
    }

    [Fact]
    public void HTTP_exact_string_and_numeric_amounts_map_to_the_same_owned_canonical_request()
    {
        var original = JsonSerializer.Serialize(Valid(), Json); var stringNode = JsonNode.Parse(original)!;
        stringNode["lines"]![0]!["quantity"] = "2.0000";
        stringNode["lines"]![0]!["unitPrice"] = "1.2500";
        var first = QuotationInput("Create", JsonSerializer.Deserialize<QuotationDraftInput>(original, Json));
        var second = QuotationInput("Create", JsonSerializer.Deserialize<QuotationDraftInput>(stringNode.ToJsonString(), Json));
        var actor = Guid.NewGuid().ToString("D"); var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        var key = Guid.NewGuid(); var source = Guid.NewGuid(); var invitation = Guid.NewGuid();
        Assert.Equal(PrincipalQuotationCommandEvidence.Build("scope", company, branch, actor, key, source, invitation, null, first),
            PrincipalQuotationCommandEvidence.Build("scope", company, branch, actor, key, source, invitation, null, second));
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("actorPrincipalId")]
    [InlineData("actorCoreLinkedWorkerId")]
    [InlineData("tenantId")]
    [InlineData("verifiedDocumentPins")]
    public void Client_payload_cannot_supply_server_operation_identity_scope_or_verified_evidence(string field)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(Valid(), Json))!; node[field] = "untrusted";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QuotationDraftInput>(node.ToJsonString(), Json));
    }

    [Fact]
    public void Null_HTTP_line_is_a_typed_422_before_projection_dereferences_it()
    {
        var input = Valid() with { Lines = [null!] };
        var error = Assert.Throws<ProcurementSourcingExecutionRoutes.Problem>(() => QuotationInput("Create", input));
        Assert.Equal(422, error.Status);
    }

    [Fact]
    public void Cancellation_has_its_own_source_independent_payload_and_cannot_replace_commercial_terms()
    {
        var cancel = new QuotationDraftInput(0, 0, 2, "  No longer required  ", null, null, null);
        var command = QuotationInput("Cancel", cancel);
        Assert.Equal("Cancel", command.Operation); Assert.Equal("No longer required", command.Reason);
        Assert.Null(command.Offer); Assert.Null(command.Lines);
        Assert.Throws<InvalidOperationException>(() => QuotationInput("Cancel", cancel with { Offer = Valid().Offer }));
    }

    [Theory]
    [InlineData("ReadyForManualDelivery", false)]
    [InlineData("ManualContactRecorded", true)]
    public void Existing_publication_response_reports_the_verified_invitation_contact_state(string state, bool contacted)
    {
        var invitation = new SourcingInvitation { Status = state, Version = 2 };
        var view = JsonSerializer.SerializeToElement(PrincipalInvitationView(invitation), Json);
        Assert.Equal(state, view.GetProperty("status").GetString());
        Assert.Equal(contacted, view.GetProperty("contactRecorded").GetBoolean());
        Assert.Equal(invitation.Id, view.GetProperty("id").GetGuid());
        Assert.Equal(invitation.Version, view.GetProperty("version").GetInt64());
    }

    [Fact]
    public void Unknown_invitation_state_cannot_be_reported_as_ready_or_contacted()
    {
        Assert.Throws<ProcurementSourcingExecutionRoutes.Problem>(() =>
            PrincipalInvitationView(new SourcingInvitation { Status = "SentByUnverifiedTransport" }));
    }

    private static QuotationDraftInput Valid() => new(3, 2, null, null, null,
        new(null, null, QuotationValidity.Unspecified, null, null, null, null, null, []),
        [new(Guid.Parse("abf33211-5997-42c0-884a-f741177dcd50"), 2m, 1.25m,
            QuotationDiscount.None, 0m, QuotationQuotedTax.Exclusive, 0m, 0m, null, null, null)]);
}
