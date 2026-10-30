using System.Text.Json;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalManualQuotationDraftTests
{
    [Fact]
    public void Workerless_buyer_validates_without_changing_any_input_or_claiming_submission()
    {
        var f = new Fixture();
        var before = JsonSerializer.Serialize(new { f.Event, f.Invitation, f.Bid, f.Sources, f.Lines });
        f.Validate();
        var snapshot = PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines);
        Assert.Equal(before, JsonSerializer.Serialize(new { f.Event, f.Invitation, f.Bid, f.Sources, f.Lines }));
        Assert.Null(f.Event.MakerWorkerId);
        Assert.Null(f.Bid.SubmittedAt);
        Assert.Equal("Draft", f.Bid.Status);
        Assert.DoesNotContain("SubmittedAt", snapshot);
        Assert.DoesNotContain("DraftRevision", snapshot);
    }

    [Fact]
    public void Snapshot_is_sorted_and_decimal_scale_and_UTC_offset_independent()
    {
        var f = new Fixture();
        f.AddLine(Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"));
        f.Sources[0].Id = Guid.Parse("00000001-0000-4000-8000-000000000001");
        f.Lines[0].SourcingLineId = f.Sources[0].Id;
        f.Bid.ReceivedAt = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        f.Lines[0].Quantity = 1m; f.Lines[0].TaxAmount = 0m; f.Lines[0].DeliveryAmount = 0m;
        var before = PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines);
        f.Lines[0].Quantity = 1.0000m; f.Lines[0].TaxAmount = 0.0000m; f.Lines[0].DeliveryAmount = 0.0000m;
        f.Bid.ReceivedAt = f.Bid.ReceivedAt.Value.ToOffset(TimeSpan.FromHours(2));
        f.Lines.Reverse();
        var after = PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines);
        Assert.Equal(before, after);
        Assert.Equal(ProcurementEvidenceEncoding.Digest(before), ProcurementEvidenceEncoding.Digest(after));
        using var document = JsonDocument.Parse(after);
        Assert.Equal(f.Sources[0].Id, document.RootElement.GetProperty("lines")[0].GetProperty("SourcingLineId").GetGuid());
        Assert.Equal("1", document.RootElement.GetProperty("lines")[0].GetProperty("quantityExact").GetString());
    }

    [Fact]
    public void Snapshot_binds_scope_line_identity_optional_details_and_real_value_changes()
    {
        var f = new Fixture();
        var before = PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines);
        f.Lines[0].UnitPrice = 1.0002m;
        Assert.NotEqual(before, PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines));
        f.Lines[0].UnitPrice = 1.0001m;
        f.Lines[0].Notes = "";
        Assert.NotEqual(before, PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines));
        f.Lines[0].Notes = null;
        f.Lines[0].Id = Guid.NewGuid();
        Assert.NotEqual(before, PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines));
        f.Bid.TenantId = "another-tenant"; f.Lines[0].TenantId = f.Bid.TenantId;
        var otherScope = PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines);
        Assert.NotEqual(before, otherScope);
        using var document = JsonDocument.Parse(otherScope);
        Assert.Equal(f.Bid.TenantId, document.RootElement.GetProperty("TenantId").GetString());
        Assert.Equal(f.Bid.LegalEntityId, document.RootElement.GetProperty("LegalEntityId").GetGuid());
    }

    [Fact]
    public void Receipt_timestamp_is_optional_but_explicit_zero_is_invalid()
    {
        var f = new Fixture();
        f.Bid.ReceivedAt = null;
        f.Validate();
        using var json = JsonDocument.Parse(PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("receivedAt").ValueKind);
        f.Bid.ReceivedAt = default(DateTimeOffset);
        AssertInvalid(f.Validate, "ManualQuotationDraftInvalid");
    }

    [Fact]
    public void Null_inputs_and_elements_fail_before_projection_or_dictionary_creation()
    {
        var f = new Fixture();
        AssertInvalid(() => PrincipalManualQuotationDraft.Validate(null, f.Invitation, f.Bid, f.Sources, f.Lines, f.Actor), "ManualQuotationDraftInvalid");
        AssertInvalid(() => PrincipalManualQuotationDraft.Validate(f.Event, null, f.Bid, f.Sources, f.Lines, f.Actor), "ManualQuotationDraftInvalid");
        AssertInvalid(() => PrincipalManualQuotationDraft.Validate(f.Event, f.Invitation, null, f.Sources, f.Lines, f.Actor), "ManualQuotationDraftInvalid");
        AssertInvalid(() => PrincipalManualQuotationDraft.Validate(f.Event, f.Invitation, f.Bid, null, f.Lines, f.Actor), "ManualQuotationSourceInvalid");
        AssertInvalid(() => PrincipalManualQuotationDraft.Validate(f.Event, f.Invitation, f.Bid, [null!], f.Lines, f.Actor), "ManualQuotationSourceInvalid");
        AssertInvalid(() => PrincipalManualQuotationDraft.Validate(f.Event, f.Invitation, f.Bid, f.Sources, [null!], f.Actor), "ManualQuotationDraftInvalid");
        AssertInvalid(() => PrincipalManualQuotationSnapshot.Build(f.Bid, [null!]), "ManualQuotationDraftInvalid");
        AssertInvalid(() => PrincipalManualQuotationSnapshot.Build(null, f.Lines), "ManualQuotationDraftInvalid");
    }

    [Theory]
    [InlineData("legacy-event")]
    [InlineData("unpublished-event")]
    [InlineData("contact-not-recorded")]
    [InlineData("portal-source")]
    [InlineData("submitted-at")]
    [InlineData("submitted-state")]
    [InlineData("submission-version")]
    [InlineData("invalid-version")]
    [InlineData("wrong-actor")]
    [InlineData("noncanonical-actor")]
    public void New_draft_rejects_unsupported_states_and_actor_bindings(string change)
    {
        var f = new Fixture();
        switch (change)
        {
            case "legacy-event": f.Event.ExecutionVersion = 0; break;
            case "unpublished-event": f.Event.Status = "Draft"; break;
            case "contact-not-recorded": f.Invitation.Status = "ReadyForManualDelivery"; break;
            case "portal-source": f.Bid.SourceChannel = "Portal"; break;
            case "submitted-at": f.Bid.SubmittedAt = DateTimeOffset.UtcNow; break;
            case "submitted-state": f.Bid.Status = "Submitted"; break;
            case "submission-version": f.Bid.SubmissionVersion = 2; break;
            case "invalid-version": f.Bid.Version = 0; break;
            case "wrong-actor": f.Bid.SurrogateEnteredBy = Guid.NewGuid().ToString("D"); break;
            case "noncanonical-actor": f.Actor = f.Actor.ToUpperInvariant(); break;
        }
        AssertInvalid(f.Validate, "ManualQuotationDraftInvalid");
    }

    [Theory]
    [InlineData("event-tenant")]
    [InlineData("event-company")]
    [InlineData("invitation-event")]
    [InlineData("invitation-tenant")]
    [InlineData("invitation-company")]
    [InlineData("bid-supplier")]
    [InlineData("bid-invitation")]
    [InlineData("bid-currency")]
    [InlineData("bid-tenant")]
    [InlineData("bid-company")]
    public void New_draft_rejects_foreign_event_invitation_or_supplier_scope(string change)
    {
        var f = new Fixture();
        switch (change)
        {
            case "event-tenant": f.Event.TenantId = "foreign"; break;
            case "event-company": f.Event.LegalEntityId = Guid.NewGuid(); break;
            case "invitation-event": f.Invitation.SourcingEventId = Guid.NewGuid(); break;
            case "invitation-tenant": f.Invitation.TenantId = "foreign"; break;
            case "invitation-company": f.Invitation.LegalEntityId = Guid.NewGuid(); break;
            case "bid-supplier": f.Bid.SupplierId = Guid.NewGuid(); break;
            case "bid-invitation": f.Bid.InvitationId = Guid.NewGuid(); break;
            case "bid-currency": f.Bid.CurrencyCode = "USD"; break;
            case "bid-tenant": f.Bid.TenantId = "foreign"; break;
            case "bid-company": f.Bid.LegalEntityId = Guid.NewGuid(); break;
        }
        AssertInvalid(f.Validate, "ManualQuotationDraftInvalid");
    }

    [Theory]
    [InlineData("duplicate-source")]
    [InlineData("inactive-source")]
    [InlineData("foreign-source-event")]
    [InlineData("foreign-source-tenant")]
    [InlineData("foreign-source-company")]
    public void Supplied_source_rows_are_validated_without_silently_filtering_them(string change)
    {
        var f = new Fixture();
        switch (change)
        {
            case "duplicate-source": f.Sources.Add(f.Sources[0]); break;
            case "inactive-source": f.Sources[0].Status = "Cancelled"; break;
            case "foreign-source-event": f.Sources[0].SourcingEventId = Guid.NewGuid(); break;
            case "foreign-source-tenant": f.Sources[0].TenantId = "foreign"; break;
            case "foreign-source-company": f.Sources[0].LegalEntityId = Guid.NewGuid(); break;
        }
        AssertInvalid(f.Validate, "ManualQuotationSourceInvalid");
    }

    [Theory]
    [InlineData("duplicate-source-ref")]
    [InlineData("duplicate-line-id")]
    [InlineData("unknown-source-ref")]
    [InlineData("foreign-bid")]
    [InlineData("foreign-tenant")]
    [InlineData("foreign-company")]
    [InlineData("over-requested-quantity")]
    public void Quote_lines_cannot_duplicate_or_escape_their_owned_source(string change)
    {
        var f = new Fixture();
        f.AddLine(Guid.NewGuid());
        switch (change)
        {
            case "duplicate-source-ref": f.Lines[1].SourcingLineId = f.Lines[0].SourcingLineId; break;
            case "duplicate-line-id": f.Lines[1].Id = f.Lines[0].Id; break;
            case "unknown-source-ref": f.Lines[0].SourcingLineId = Guid.NewGuid(); break;
            case "foreign-bid": f.Lines[0].SupplierBidId = Guid.NewGuid(); break;
            case "foreign-tenant": f.Lines[0].TenantId = "foreign"; break;
            case "foreign-company": f.Lines[0].LegalEntityId = Guid.NewGuid(); break;
            case "over-requested-quantity": f.Lines[0].Quantity = f.Sources[0].Quantity + 0.0001m; break;
        }
        AssertInvalid(f.Validate, "ManualQuotationLineInvalid");
    }

    [Theory]
    [InlineData("quantity", "0")]
    [InlineData("quantity", "0.00001")]
    [InlineData("price", "-0.0001")]
    [InlineData("price", "0.00001")]
    [InlineData("price", "100000000000000")]
    [InlineData("tax", "-0.0001")]
    [InlineData("tax", "0.00001")]
    [InlineData("tax", "100000000000000")]
    [InlineData("delivery", "-0.0001")]
    [InlineData("delivery", "0.00001")]
    [InlineData("delivery", "100000000000000")]
    public void Line_amounts_require_exact_numeric_18_4_without_rounding(string field, string text)
    {
        var f = new Fixture();
        var amount = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        switch (field)
        {
            case "quantity": f.Lines[0].Quantity = amount; break;
            case "price": f.Lines[0].UnitPrice = amount; break;
            case "tax": f.Lines[0].TaxAmount = amount; break;
            case "delivery": f.Lines[0].DeliveryAmount = amount; break;
        }
        AssertInvalid(f.Validate, "ManualQuotationLineInvalid");
    }

    [Fact]
    public void Exact_four_digit_limits_and_eight_digit_product_are_accepted()
    {
        var f = new Fixture();
        f.Lines[0].Quantity = 1.0001m; f.Lines[0].UnitPrice = 1.0001m;
        f.Lines[0].TaxAmount = 99999999999999.9999m; f.Lines[0].DeliveryAmount = 0m;
        f.Validate();
        Assert.Equal(1.00020001m, ExactDecimalArithmetic.Multiply(f.Lines[0].Quantity, f.Lines[0].UnitPrice));
    }

    [Fact]
    public void Product_that_CLR_decimal_would_round_is_rejected()
    {
        var f = new Fixture();
        f.Sources[0].Quantity = f.Lines[0].Quantity = 99999999999999.9999m;
        f.Lines[0].UnitPrice = 99999999999999.9999m;
        AssertInvalid(f.Validate, "ManualQuotationTotalInvalid");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(200)]
    public void Aggregate_overflow_of_exact_CLR_or_database_total_is_rejected(int count)
    {
        var f = new Fixture();
        for (var i = 1; i < count; i++) f.AddLine(Guid.NewGuid());
        foreach (var source in f.Sources) source.Quantity = 90000000000000m;
        foreach (var line in f.Lines)
        {
            line.Quantity = 90000000000000m; line.UnitPrice = 90000000000000m;
            line.TaxAmount = 0m; line.DeliveryAmount = 0m;
        }
        AssertInvalid(f.Validate, "ManualQuotationTotalInvalid");
    }

    [Fact]
    public void Cancelled_snapshot_is_retained_but_cannot_be_new_draft_input()
    {
        var f = new Fixture(); f.Bid.Status = "Cancelled"; f.Bid.CorrectionReason = "No longer needed";
        using var json = JsonDocument.Parse(PrincipalManualQuotationSnapshot.Build(f.Bid, f.Lines));
        Assert.Equal("Cancelled", json.RootElement.GetProperty("Status").GetString());
        AssertInvalid(f.Validate, "ManualQuotationDraftInvalid");
    }

    private static void AssertInvalid(Action action, string code) =>
        Assert.Equal(code, Assert.Throws<InvalidOperationException>(action).Message);

    private sealed class Fixture
    {
        public string Actor = "acdbca87-25cb-4cb7-85bf-49313e4e06c3";
        public SourcingEvent Event { get; }
        public SourcingInvitation Invitation { get; }
        public SupplierBid Bid { get; }
        public List<SourcingLine> Sources { get; } = [];
        public List<SupplierBidLine> Lines { get; } = [];

        public Fixture()
        {
            var company = Guid.NewGuid();
            Event = new() { TenantId = "m7a-quotation-test", LegalEntityId = company,
                ExecutionVersion = 2, Status = "Published", CurrencyCode = "ZMW",
                OwnerSubjectId = Actor, MakerWorkerId = null };
            Invitation = new() { TenantId = Event.TenantId, LegalEntityId = company,
                SourcingEventId = Event.Id, SupplierId = Guid.NewGuid(), Status = "ManualContactRecorded" };
            Bid = new() { TenantId = Event.TenantId, LegalEntityId = company, SourcingEventId = Event.Id,
                SupplierId = Invitation.SupplierId, InvitationId = Invitation.Id, CurrencyCode = "ZMW",
                Status = "Draft", SourceChannel = "Manual", SurrogateEnteredBy = Actor };
            AddLine(Guid.NewGuid());
        }

        public void AddLine(Guid sourceId)
        {
            Sources.Add(new() { Id = sourceId, TenantId = Event.TenantId, LegalEntityId = Event.LegalEntityId,
                SourcingEventId = Event.Id, Status = "Active", Quantity = 10.1256m });
            Lines.Add(new() { TenantId = Bid.TenantId, LegalEntityId = Bid.LegalEntityId,
                SupplierBidId = Bid.Id, SourcingLineId = sourceId, Quantity = 1.0001m,
                UnitPrice = 1.0001m, TaxAmount = 0.1234m, DeliveryAmount = 0.0001m });
        }

        public void Validate() => PrincipalManualQuotationDraft.Validate(Event, Invitation, Bid, Sources, Lines, Actor);
    }
}
