using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalManualQuotationEvidenceTests
{
    [Fact]
    public void Existing_ready_publication_is_readable_without_new_contact_evidence()
    {
        var f = new Fixture();
        PrincipalManualQuotationEvidence.VerifyPublishedInvitation(f.Event, f.Invitation,
            f.Opening, f.Decision, f.Receipt, null);
    }

    [Fact]
    public void Recorded_external_contact_requires_exact_owned_M4_transition_and_does_not_claim_email()
    {
        var f = new Fixture(); var contact = f.Contact();
        f.Invitation.Status = "ManualContactRecorded"; f.Invitation.Version++;
        PrincipalManualQuotationEvidence.VerifyPublishedInvitation(f.Event, f.Invitation,
            f.Opening, f.Decision, f.Receipt, contact);
        Assert.Null(f.Invitation.SentAt); Assert.Null(f.Invitation.DeliveryRef);
    }

    [Theory]
    [InlineData("no-receipt")]
    [InlineData("foreign-contact")]
    [InlineData("changed-invitation")]
    [InlineData("wrong-version")]
    [InlineData("status-only")]
    public void Published_invitation_rejects_unbacked_foreign_or_changed_contact(string fault)
    {
        var f = new Fixture(); var contact = f.Contact();
        f.Invitation.Status = "ManualContactRecorded"; f.Invitation.Version++;
        switch (fault)
        {
            case "no-receipt": contact = null!; break;
            case "foreign-contact": contact.TenantId = "other"; break;
            case "changed-invitation": f.Invitation.SupplierId = Guid.NewGuid(); break;
            case "wrong-version": f.Invitation.Version++; break;
            case "status-only": f.Invitation.Status = "Sent"; break;
        }
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.VerifyPublishedInvitation(
            f.Event, f.Invitation, f.Opening, f.Decision, f.Receipt, contact));
    }

    [Fact]
    public void Typed_V3_snapshot_restores_every_offer_and_exact_discount_without_employee_authority()
    {
        var f = new Fixture(); var contact = f.Contact(); var draft = f.Draft(contact);
        var offer = Offer(); var input = OfferedLines(draft);
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, offer, input);
        var snapshot = PrincipalQuotationStorage.Build(draft.Bid, draft.Lines, 1, [], []);
        var restored = PrincipalQuotationStorage.Restore(snapshot);
        Assert.Equal(snapshot, PrincipalQuotationStorage.Build(restored.Bid, restored.Lines, 1,
            restored.Offer.Documents!, restored.Pins));
        Assert.Equal("SUP-QUOTE-7", restored.Bid.SupplierQuoteReference);
        Assert.Equal("supplier-offered-only", restored.Bid.SupplierOfferedPaymentCode);
        Assert.Equal(12.5m, restored.Lines[0].DiscountValue);
        Assert.Equal("Inclusive", restored.Lines[0].TaxTreatment);
        Assert.Empty(restored.Pins);
    }

    [Theory]
    [InlineData("derived-total")]
    [InlineData("extra-member")]
    [InlineData("omitted-member")]
    [InlineData("changed-tax")]
    public void V3_history_rejects_incomplete_noncanonical_or_inconsistent_owned_snapshot(string fault)
    {
        var f = new Fixture(); var draft = f.Draft(f.Contact());
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, Offer(), OfferedLines(draft));
        var json = PrincipalQuotationStorage.Build(draft.Bid, draft.Lines, 1, [], []);
        var node = JsonNode.Parse(json)!.AsObject();
        switch (fault)
        {
            case "derived-total": node["totals"]!["payable"] = "999"; break;
            case "extra-member": node["authorityVerified"] = true; break;
            case "omitted-member": node["header"]!.AsObject().Remove("correctionReason"); break;
            case "changed-tax": node["lines"]![0]!["taxTreatment"] = "TaxLawApproved"; break;
        }
        var error = Record.Exception(() => PrincipalQuotationStorage.Restore(node.ToJsonString()));
        Assert.True(error is InvalidOperationException or System.Text.Json.JsonException,
            "Malformed owned evidence must fail with a typed validation/JSON error.");
    }

    [Fact]
    public void Failed_offer_validation_does_not_partially_convert_legacy_header_or_lines()
    {
        var f = new Fixture(); var draft = f.Draft(f.Contact());
        var input = OfferedLines(draft).Select(x => x with { DiscountValue = 101m }).ToArray();
        Assert.Throws<InvalidOperationException>(() => PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, Offer(), input));
        Assert.Equal(0, draft.Bid.PrincipalCaptureVersion); Assert.Null(draft.Bid.SupplierQuoteReference);
        Assert.All(draft.Lines, x => Assert.Equal(0, x.PrincipalCaptureVersion));
    }

    [Fact]
    public void New_typed_capture_owns_full_request_and_recovers_after_current_employee_alias_changes()
    {
        var f = new Fixture(); var contact = f.Contact(); var draft = f.Draft(contact);
        f.Invitation.Version = contact.InvitationVersionAtCapture;
        var offer = Offer(); var input = OfferedLines(draft);
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, offer, input);
        var command = new PrincipalQuotationCommand("Create", f.Event.Version, f.Invitation.Version,
            null, null, null, offer, input);
        var version = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact,
            draft.Bid, draft.Lines, f.Sources, f.Actor, Guid.NewGuid(), Guid.NewGuid(), f.At, "Create", command: command);
        var restored = PrincipalQuotationStorage.Restore(version.SnapshotJson);
        PrincipalManualQuotationEvidence.VerifyQuotation(version, restored.Bid, restored.Lines, contact);
        // Recovery asks for the same Core principal/key, not current HR alias.
        Assert.Equal(version.ResponseJson, PrincipalManualQuotationEvidence.RecoverQuotation(version,
            restored.Bid, restored.Lines, contact, f.Actor, version.CommandKey, version.CommandSha256));
        Assert.DoesNotContain("actorCoreLinkedWorkerId", version.RequestJson);
        Assert.Equal(3, version.SnapshotSchemaVersion);
    }

    [Fact]
    public void Another_current_principal_can_revise_cancel_but_cannot_recover_the_original_private_key()
    {
        var f = new Fixture(); var contact = f.Contact(); var draft = f.Draft(contact);
        f.Invitation.Version = contact.InvitationVersionAtCapture;
        var offer = Offer(); var input = OfferedLines(draft);
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, offer, input);
        var create = new PrincipalQuotationCommand("Create", f.Event.Version, f.Invitation.Version,
            null, null, null, offer, input);
        var first = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact,
            draft.Bid, draft.Lines, f.Sources, f.Actor, null, Guid.NewGuid(), f.At, "Create", command: create);
        var before = PrincipalQuotationStorage.Restore(first.SnapshotJson);
        var actorB = Guid.NewGuid().ToString("D");
        var candidate = PrincipalQuotationStorage.Restore(first.SnapshotJson);
        candidate.Bid.SurrogateEnteredBy = actorB; candidate.Bid.CorrectionReason = "Correct offered warranty";
        PrincipalQuotationStorage.Apply(candidate.Bid, candidate.Lines, offer with { WarrantyText = "Two years offered" }, input);
        var revise = new PrincipalQuotationCommand("Revise", f.Event.Version, f.Invitation.Version,
            1, candidate.Bid.CorrectionReason, null, offer with { WarrantyText = "Two years offered" }, input);
        var second = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact,
            candidate.Bid, candidate.Lines, f.Sources, actorB, null, Guid.NewGuid(), f.At.AddHours(1),
            "Revise", first, before.Bid, before.Lines, revise);
        var secondBefore = PrincipalQuotationStorage.Restore(second.SnapshotJson);
        var cancelled = PrincipalQuotationStorage.Restore(second.SnapshotJson);
        cancelled.Bid.Status = "Cancelled"; cancelled.Bid.CorrectionReason = "No longer required";
        f.Event.Status = "Cancelled"; f.Invitation.Status = "Cancelled";
        var cancel = new PrincipalQuotationCommand("Cancel", 0, 0, 2, cancelled.Bid.CorrectionReason, null, null, null);
        var third = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact,
            cancelled.Bid, cancelled.Lines, f.Sources, actorB, null, Guid.NewGuid(), f.At.AddHours(2),
            "Cancel", second, secondBefore.Bid, secondBefore.Lines, cancel);
        var retained = PrincipalQuotationStorage.Restore(third.SnapshotJson);
        PrincipalManualQuotationEvidence.VerifyQuotation(third, retained.Bid, retained.Lines, contact);
        Assert.Equal(f.Actor, first.ActorPrincipalId); Assert.Equal(actorB, second.ActorPrincipalId);
        Assert.Equal(actorB, third.ActorPrincipalId); Assert.Equal("Two years offered", retained.Bid.WarrantyText);
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.RecoverQuotation(
            first, before.Bid, before.Lines, contact, actorB, first.CommandKey, first.CommandSha256));
    }

    [Fact]
    public void Typed_snapshot_retains_fourteen_digit_discount_scale_without_numeric38_8_rounding()
    {
        var f = new Fixture(); var draft = f.Draft(f.Contact());
        var line = draft.Lines[0]; line.Quantity = 0.0001m; line.UnitPrice = 0.0001m;
        var values = new[] { new PrincipalQuotationStorage.LineInput(line.SourcingLineId, line.Quantity, line.UnitPrice,
            QuotationDiscount.Percent, 12.3456m, QuotationQuotedTax.Exclusive, 0m, 0m, null, null, null) };
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, Offer(), values);
        var snapshot = PrincipalQuotationStorage.Build(draft.Bid, draft.Lines, 1, [], []);
        var json = JsonNode.Parse(snapshot)!;
        Assert.Equal(0.00000000123456m, ExactDecimalJsonCodec.Parse(
            json["totals"]!["discountAmount"]!.GetValue<string>(), 38, 14));
        Assert.Equal(0.00000000876544m, ExactDecimalJsonCodec.Parse(
            json["totals"]!["payable"]!.GetValue<string>(), 38, 14));
        Assert.Equal(snapshot, PrincipalQuotationStorage.Build(PrincipalQuotationStorage.Restore(snapshot).Bid,
            PrincipalQuotationStorage.Restore(snapshot).Lines, 1, [], []));
    }

    [Fact]
    public void Null_snapshot_line_is_a_typed_validation_failure_not_a_null_reference()
    {
        var f = new Fixture(); var draft = f.Draft(f.Contact());
        PrincipalQuotationStorage.Apply(draft.Bid, draft.Lines, Offer(), OfferedLines(draft));
        var node = JsonNode.Parse(PrincipalQuotationStorage.Build(draft.Bid, draft.Lines, 1, [], []))!;
        node["lines"]![0] = null;
        Assert.Throws<InvalidOperationException>(() => PrincipalQuotationStorage.Restore(node.ToJsonString()));
    }

    [Fact]
    public void Retained_contact_read_survives_source_cancellation_without_becoming_new_commercial_authority()
    {
        var f = new Fixture(); var contact = f.Contact(); f.Invitation.Status = "ManualContactRecorded";
        f.Invitation.Version = contact.InvitationVersionAtCapture; f.Event.Status = "Cancelled"; f.Event.Version++;
        PrincipalManualQuotationEvidence.VerifyRetainedInvitation(f.Event, f.Invitation,
            f.Opening, f.Decision, f.Receipt, contact);
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.VerifyPublishedInvitation(
            f.Event, f.Invitation, f.Opening, f.Decision, f.Receipt, contact));
    }

    [Fact]
    public void Contact_original_request_match_uses_historical_alias_but_rejects_changed_private_body()
    {
        var f = new Fixture(); var row = PrincipalManualQuotationEvidence.Contact(f.Event, f.Invitation,
            f.Opening, f.Decision, f.Receipt, f.Actor, Guid.NewGuid(), "Physical", "Supplier contact", f.At, f.At, Guid.NewGuid());
        Assert.True(PrincipalManualQuotationEvidence.ContactRequestMatches(row, f.Event.Version,
            f.Invitation.Version, row.Channel, row.Reference, row.ReportedContactAt));
        Assert.False(PrincipalManualQuotationEvidence.ContactRequestMatches(row, f.Event.Version,
            f.Invitation.Version, row.Channel, "Different private body", row.ReportedContactAt));
    }

    private static QuotationCommercialTerms Offer() => new("SUP-QUOTE-7", new DateOnly(2026, 10, 30),
        QuotationValidity.Fixed, new DateOnly(2026, 11, 30),
        new SupplierOfferedPaymentTerms("supplier-offered-only", "30 days offered"), "One year offered",
        "Delivery offered", new DateOnly(2026, 11, 1), []);
    private static PrincipalQuotationStorage.LineInput[] OfferedLines(PrincipalManualQuotationEvidence.DraftProjection draft) =>
        draft.Lines.Select(x => new PrincipalQuotationStorage.LineInput(x.SourcingLineId, x.Quantity, x.UnitPrice,
            QuotationDiscount.Percent, 12.5m, QuotationQuotedTax.Inclusive, x.TaxAmount, x.DeliveryAmount,
            "unverified-supplier-tax", "Three days", "Offered only")).ToArray();
}
