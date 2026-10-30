using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Mightyfin.Erp.Procurement.Infrastructure.Data;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalManualQuotationEvidenceTests
{
    [Fact]
    public void Contact_factory_pins_scope_and_real_M4_receipt_without_changing_invitation_or_sending()
    {
        var f = new Fixture(); var original = JsonSerializer.Serialize(new { f.Event, f.Invitation });
        var contact = f.Contact(); PrincipalManualQuotationEvidence.VerifyContact(contact);
        Assert.Equal(f.Event.TenantId, contact.TenantId); Assert.Equal(f.Event.LegalEntityId, contact.LegalEntityId);
        Assert.Equal(f.Opening.Id, contact.PublicationOpeningId); Assert.Equal(f.Decision.Id, contact.PublicationDecisionId);
        Assert.Equal(f.Event.BranchRef, contact.BranchId); Assert.Null(contact.ActorCoreLinkedWorkerId);
        Assert.Equal(original, JsonSerializer.Serialize(new { f.Event, f.Invitation }));
        using var response = JsonDocument.Parse(contact.ResponseJson);
        Assert.False(response.RootElement.GetProperty("emailSentBySystem").GetBoolean());
        Assert.True(response.RootElement.GetProperty("supplierDeliveryReported").GetBoolean());
    }

    [Fact]
    public void Contact_recovery_verifies_exact_reply_and_private_actor_key_before_returning_bytes()
    {
        var f = new Fixture();var row = f.Contact();
        Assert.Equal(row.ResponseJson, PrincipalManualQuotationEvidence.RecoverContact(row, f.Actor, row.CommandKey, row.CommandSha256));
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.RecoverContact(row,
            Guid.NewGuid().ToString("D"), row.CommandKey, row.CommandSha256));
        var changed = Change(row, node =>
        {
            var reply = JsonNode.Parse(node["ResponseJson"]!.GetValue<string>())!.AsObject();reply["emailSentBySystem"] = true;
            node["ResponseJson"] = reply.ToJsonString();node["ResponseSha256"] = ProcurementEvidenceEncoding.Digest(reply.ToJsonString());
        });
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.VerifyContact(changed));
    }

    [Fact]
    public void Null_draft_input_line_is_a_validation_error_and_allocates_no_returned_commercial_projection()
    {
        var f = new Fixture();var contact = f.Contact();f.Invitation.Status = "ManualContactRecorded";
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.Draft(f.Event, f.Invitation,
            contact, f.Sources, new PrincipalManualQuotationEvidence.LineInput[] { null! }, f.Actor));
    }

    [Fact]
    public void A_rehashed_status_only_publication_reply_is_not_M4_approval_evidence()
    {
        var f = new Fixture();f.Receipt.ResponseJson = "{\"status\":\"Published\"}";
        f.Receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(f.Receipt.ResponseJson);
        Assert.Throws<InvalidOperationException>(() => f.Contact());
    }

    [Theory]
    [InlineData("opening-tenant")]
    [InlineData("decision-company")]
    [InlineData("invitation-supplier-empty")]
    [InlineData("not-approved")]
    [InlineData("not-published")]
    [InlineData("sent-by-system")]
    [InlineData("before-approval")]
    [InlineData("future")]
    public void Contact_rejects_changed_parent_scope_delivery_or_observation(string fault)
    {
        var f = new Fixture();var reported = f.At;
        switch (fault)
        {
            case "opening-tenant":f.Opening.TenantId = "foreign";break;
            case "decision-company":f.Decision.LegalEntityId = Guid.NewGuid();break;
            case "invitation-supplier-empty":f.Invitation.SupplierId = Guid.Empty;break;
            case "not-approved":f.Decision.CaseState = "Pending";break;
            case "not-published":f.Event.Status = "Draft";break;
            case "sent-by-system":f.Invitation.SentAt = f.At;break;
            case "before-approval":reported = f.At.AddDays(-2);break;
            case "future":reported = f.At.AddDays(1);break;
        }
        Assert.Throws<InvalidOperationException>(() => f.Contact(reported));
    }

    [Fact]
    public async Task Actual_factory_existing_bid_and_lines_can_be_added_to_the_owning_context()
    {
        var f = new Fixture();var contact = f.Contact();var draft = f.Draft(contact);
        await using var db = f.Context();db.AddScoped(draft.Bid);
        foreach (var line in draft.Lines) db.AddScoped(line);
        await db.SaveChangesAsync();
        Assert.Equal(f.Actor, draft.Bid.SurrogateEnteredBy);Assert.Null(draft.Bid.SubmittedAt);
        Assert.Equal("Draft", draft.Bid.Status);Assert.Equal("Manual", draft.Bid.SourceChannel);
        Assert.Equal(f.Event.TenantId, draft.Bid.TenantId);Assert.Equal(f.Event.LegalEntityId, draft.Bid.LegalEntityId);
        Assert.Equal(1, await db.SupplierBids.CountAsync());Assert.Equal(1, await db.SupplierBidLines.CountAsync());
        Assert.Equal(draft.Bid.Id, (await db.SupplierBidLines.SingleAsync()).SupplierBidId);
        // Legacy factory rows remain legacy; mapped sidecars never adopt them.
        Assert.Equal(0, draft.Bid.PrincipalCaptureVersion);
        Assert.NotNull(db.Model.FindEntityType(typeof(SourcingManualContact)));
        Assert.NotNull(db.Model.FindEntityType(typeof(SourcingManualQuoteVersion)));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    public void Actual_AddScoped_never_relabels_foreign_factory_sidecars_even_before_mapping(string fault)
    {
        var f = new Fixture();var contact = f.Contact();var draft = f.Draft(contact);var version = f.Version(contact, draft);
        using var db = f.Context();
        var wrongTenant = fault == "tenant" ? "foreign" : f.Event.TenantId;
        var wrongCompany = fault == "company" ? Guid.NewGuid() : f.Event.LegalEntityId;
        contact.TenantId = wrongTenant;contact.LegalEntityId = wrongCompany;
        version.TenantId = wrongTenant;version.LegalEntityId = wrongCompany;
        Assert.Contains("outside the authenticated scope", Assert.Throws<InvalidOperationException>(() => db.AddScoped(contact)).Message);
        Assert.Contains("outside the authenticated scope", Assert.Throws<InvalidOperationException>(() => db.AddScoped(version)).Message);
        Assert.Equal(wrongTenant, contact.TenantId);Assert.Equal(wrongCompany, contact.LegalEntityId);
        Assert.Equal(wrongTenant, version.TenantId);Assert.Equal(wrongCompany, version.LegalEntityId);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void Complete_owned_create_receipt_recovers_exact_bytes_without_a_write_flag_or_employee()
    {
        var f = new Fixture();var contact = f.Contact();var draft = f.Draft(contact);var row = f.Version(contact, draft);
        var body = PrincipalManualQuotationEvidence.RecoverQuotation(row, draft.Bid, draft.Lines,
            contact, f.Actor, row.CommandKey, row.CommandSha256);
        Assert.Equal(row.ResponseJson, body);Assert.Equal(1, row.BidVersion);
        Assert.Null(row.ActorCoreLinkedWorkerId);Assert.Null(row.PreviousVersionId);
        using var response = JsonDocument.Parse(body);
        Assert.False(response.RootElement.GetProperty("supplierSubmitted").GetBoolean());
        Assert.False(response.RootElement.GetProperty("emailSentBySystem").GetBoolean());
        Assert.Equal("Draft", response.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain("DraftRevision", row.SnapshotJson);
    }

    [Theory]
    [InlineData("response")]
    [InlineData("snapshot")]
    [InlineData("actor")]
    [InlineData("scope")]
    [InlineData("contact")]
    public void Rehashing_changed_evidence_is_insufficient_without_complete_owned_producer_shape(string fault)
    {
        var f = new Fixture();var contact = f.Contact();var draft = f.Draft(contact);var original = f.Version(contact, draft);
        var row = Change(original, node =>
        {
            switch (fault)
            {
                case "response":
                    var body = JsonNode.Parse(node["ResponseJson"]!.GetValue<string>())!.AsObject();body["supplierSubmitted"] = true;
                    node["ResponseJson"] = body.ToJsonString();node["ResponseSha256"] = ProcurementEvidenceEncoding.Digest(body.ToJsonString());break;
                case "snapshot":
                    var snapshot = node["SnapshotJson"]!.GetValue<string>().Replace("1.25", "9.25", StringComparison.Ordinal);
                    node["SnapshotJson"] = snapshot;node["SnapshotSha256"] = ProcurementEvidenceEncoding.Digest(snapshot);break;
                case "actor":node["ActorPrincipalId"] = Guid.NewGuid().ToString("D");break;
                case "scope":node["TenantId"] = "foreign";break;
                case "contact":node["ManualContactId"] = Guid.NewGuid();break;
            }
        });
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.VerifyQuotation(row, draft.Bid, draft.Lines, contact));
    }

    [Theory]
    [InlineData("event")]
    [InlineData("supplier")]
    [InlineData("invitation")]
    public void Coherently_rehashed_snapshot_cannot_move_owned_bid_away_from_contact_header(string identity)
    {
        var f = new Fixture();var contact = f.Contact();var draft = f.Draft(contact);var original = f.Version(contact, draft);
        var foreign = Clone(draft.Bid);
        if (identity == "event") foreign.SourcingEventId = Guid.NewGuid();
        else if (identity == "supplier") foreign.SupplierId = Guid.NewGuid();
        else foreign.InvitationId = Guid.NewGuid();
        // Deliberately use the REAL producers to reseal this contradictory row.
        // A stale hash cannot be the reason this adversarial version is denied.
        var snapshot = PrincipalManualQuotationSnapshot.Build(foreign, draft.Lines);
        var snapshotSha = ProcurementEvidenceEncoding.Digest(snapshot);
        var command = Producer("QuoteCommand", original.TenantId, original.LegalEntityId,
            original.SourcingEventId, original.InvitationId, original.SupplierId, original.SupplierBidId,
            original.BranchId, original.ManualContactId, original.PreviousVersionId, original.BidVersion,
            original.Operation, original.ActorPrincipalId, original.ActorCoreLinkedWorkerId, original.CommandKey, snapshotSha);
        var response = Producer("QuoteResponse", original.Id, original.SourcingEventId, original.SupplierBidId,
            original.BidVersion, original.Operation, foreign.Status, original.ManualContactId, original.CommandKey, snapshotSha);
        var forged = Change(original, node =>
        {
            node["SnapshotJson"] = snapshot;node["SnapshotSha256"] = snapshotSha;
            node["CommandSha256"] = ProcurementEvidenceEncoding.Digest(command);
            node["ResponseJson"] = response;node["ResponseSha256"] = ProcurementEvidenceEncoding.Digest(response);
        });
        Assert.Equal(ProcurementEvidenceEncoding.Digest(forged.SnapshotJson), forged.SnapshotSha256);
        Assert.Equal(ProcurementEvidenceEncoding.Digest(forged.ResponseJson), forged.ResponseSha256);
        Assert.Equal(ProcurementEvidenceEncoding.Digest(command), forged.CommandSha256);
        Assert.Equal("ManualQuotationEvidenceInvalid", Assert.Throws<InvalidOperationException>(() =>
            PrincipalManualQuotationEvidence.VerifyQuotation(forged, foreign, draft.Lines, contact)).Message);
    }

    [Fact]
    public void A_Manual_legacy_bid_cannot_be_revised_without_owned_prior_protocol_evidence()
    {
        var f = new Fixture();var contact = f.Contact();var draft = f.Draft(contact);
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.Quotation(f.Event,
            f.Invitation, contact, draft.Bid, draft.Lines, f.Sources, f.Actor, null, Guid.NewGuid(), f.At,
            "Revise", previous: null, before: draft.Bid, beforeLines: draft.Lines));
    }

    [Fact]
    public void Revision_pins_existing_next_bid_version_and_keeps_original_receipt_immutable()
    {
        var f = new Fixture();var contact = f.Contact();var before = f.Draft(contact);var original = f.Version(contact, before);
        var originalJson = JsonSerializer.Serialize(original);var candidate = Clone(before.Bid);
        var lines = before.Lines.Select(Clone).ToArray();lines[0].UnitPrice = 1.50m;
        var row = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact, candidate,
            lines, f.Sources, f.Actor, null, Guid.NewGuid(), f.At.AddHours(1), "Revise", original, before.Bid, before.Lines);
        Assert.Equal(1, candidate.Version);Assert.Equal(2, row.BidVersion);Assert.Equal(original.Id, row.PreviousVersionId);
        Assert.Equal(originalJson, JsonSerializer.Serialize(original));
        // This assignment simulates only the existing EF version increment. Native
        // atomic version+sidecar persistence remains deferred until schema mapping.
        candidate.Version = 2;PrincipalManualQuotationEvidence.VerifyQuotation(row, candidate, lines, contact);
        Assert.NotEqual(original.SnapshotSha256, row.SnapshotSha256);
        Assert.Equal(original.ResponseJson, PrincipalManualQuotationEvidence.RecoverQuotation(original,
            before.Bid, before.Lines, contact, f.Actor, original.CommandKey, original.CommandSha256));
    }

    [Theory]
    [InlineData("Revise")]
    [InlineData("Cancel")]
    public void Version_recording_cannot_precede_previous_version_but_same_instant_is_allowed(string operation)
    {
        var f = new Fixture();var contact = f.Contact();var before = f.Draft(contact);
        var previousAt = f.At.AddHours(2);
        var previous = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact,
            before.Bid, before.Lines, f.Sources, f.Actor, null, Guid.NewGuid(), previousAt, "Create");
        var candidate = Clone(before.Bid);
        if (operation == "Cancel") { candidate.Status = "Cancelled";candidate.CorrectionReason = "No longer needed"; }
        Assert.Equal("ManualQuotationRevisionBackdated", Assert.Throws<InvalidOperationException>(() =>
            PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact, candidate,
                before.Lines, f.Sources, f.Actor, null, Guid.NewGuid(), previousAt.AddMinutes(-1), operation,
                previous, before.Bid, before.Lines)).Message);
        var sameInstant = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact,
            candidate, before.Lines, f.Sources, f.Actor, null, Guid.NewGuid(), previousAt, operation,
            previous, before.Bid, before.Lines);
        Assert.Equal(previous.RecordedAt, sameInstant.RecordedAt);Assert.Equal(2, sameInstant.BidVersion);
        Assert.Equal(previous.ResponseJson, PrincipalManualQuotationEvidence.RecoverQuotation(previous,
            before.Bid, before.Lines, contact, f.Actor, previous.CommandKey, previous.CommandSha256));
    }

    [Fact]
    public void Cancellation_preserves_terms_requires_reason_and_does_not_claim_recreation_policy()
    {
        var f = new Fixture();var contact = f.Contact();var before = f.Draft(contact);var original = f.Version(contact, before);
        var cancelled = Clone(before.Bid);cancelled.Status = "Cancelled";cancelled.CorrectionReason = "Requirement changed";
        var row = PrincipalManualQuotationEvidence.Quotation(f.Event, f.Invitation, contact, cancelled,
            before.Lines, f.Sources, f.Actor, null, Guid.NewGuid(), f.At.AddHours(1), "Cancel", original, before.Bid, before.Lines);
        Assert.Equal(2, row.BidVersion);Assert.Equal("Cancel", row.Operation);Assert.Equal("Draft", before.Bid.Status);
        cancelled.Version = 2;PrincipalManualQuotationEvidence.VerifyQuotation(row, cancelled, before.Lines, contact);
        cancelled.Version = 1;var changedLines = before.Lines.Select(Clone).ToArray();changedLines[0].UnitPrice++;
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.Quotation(f.Event,
            f.Invitation, contact, cancelled, changedLines, f.Sources, f.Actor, null, Guid.NewGuid(), f.At,
            "Cancel", original, before.Bid, before.Lines));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("key")]
    [InlineData("body")]
    public void Recovery_rejects_other_actor_key_or_command_even_for_a_complete_receipt(string fault)
    {
        var f = new Fixture();var contact = f.Contact();var draft = f.Draft(contact);var row = f.Version(contact, draft);
        Assert.Throws<InvalidOperationException>(() => PrincipalManualQuotationEvidence.RecoverQuotation(row,
            draft.Bid, draft.Lines, contact, fault == "actor" ? Guid.NewGuid().ToString("D") : f.Actor,
            fault == "key" ? Guid.NewGuid() : row.CommandKey, fault == "body" ? new string('a', 64) : row.CommandSha256));
    }

    private static string Producer(string name, params object?[] input) => (string)(typeof(PrincipalManualQuotationEvidence)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, input)
        ?? throw new InvalidOperationException("Real quotation producer required"));
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static T Change<T>(T value, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(value))!.AsObject();edit(node);
        return JsonSerializer.Deserialize<T>(node.ToJsonString())!;
    }
    private sealed class Fixture
    {
        internal readonly string Actor = Guid.NewGuid().ToString("D");
        internal readonly DateTimeOffset At = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        internal readonly SourcingEvent Event;
        internal readonly SourcingInvitation Invitation;
        internal readonly SourcingPublicationOpening Opening;
        internal readonly SourcingPublicationDecision Decision;
        internal readonly SourcingPublicationReceipt Receipt;
        internal readonly IReadOnlyList<SourcingLine> Sources;
        internal Fixture()
        {
            Event = new() { TenantId = "m7-factory", LegalEntityId = Guid.NewGuid(), BranchRef = Guid.NewGuid(),
                ExecutionVersion = 2, Status = "Published", Version = 3, CurrencyCode = "ZMW", OwnerSubjectId = Actor };
            Invitation = new() { TenantId = Event.TenantId, LegalEntityId = Event.LegalEntityId,
                SourcingEventId = Event.Id, SupplierId = Guid.NewGuid(), Status = "ReadyForManualDelivery" };
            Opening = new() { TenantId = Event.TenantId, LegalEntityId = Event.LegalEntityId,
                SourcingEventId = Event.Id, EventVersionAtOpening = 2, CoreApprovalCaseId = Guid.NewGuid(),
                MakerPrincipalId = Actor, OpenedAt = At.AddDays(-1) };
            Decision = new() { TenantId = Event.TenantId, LegalEntityId = Event.LegalEntityId,
                SourcingEventId = Event.Id, OpeningId = Opening.Id, CoreApprovalCaseId = Opening.CoreApprovalCaseId,
                CoreApprovalDecisionId = Guid.NewGuid(), CoreAssignmentId = Guid.NewGuid(), Action = "Approve",
                CaseState = "Approved", ActorPrincipalId = Guid.NewGuid().ToString("D"), EventVersionAtDecision = 3,
                DecidedAt = At.AddDays(-1) };
            Receipt = new() { TenantId = Event.TenantId, LegalEntityId = Event.LegalEntityId,
                SourcingEventId = Event.Id, OpeningId = Opening.Id, DecisionId = Decision.Id, Operation = "Decide",
                ActorPrincipalId = Decision.ActorPrincipalId, CommandKey = Guid.NewGuid(), ResultVersion = 3,
                ResponseStatusCode = 200, CompletedAt = Decision.DecidedAt };
            Receipt.ResponseJson = JsonSerializer.Serialize(new { eventId = Event.Id, status = "Published", version = 3,
                publicationReviewId = Opening.Id, coreCaseId = Opening.CoreApprovalCaseId, coreCaseVersion = 1,
                supplierDeliveryState = "ReadyForManualDelivery", financeReservationChanged = false,
                commandKey = Receipt.CommandKey, coreDecisionId = Decision.CoreApprovalDecisionId, caseState = "Approved" });
            Receipt.ResponseSha256 = ProcurementEvidenceEncoding.Digest(Receipt.ResponseJson);
            Sources = new[] { new SourcingLine { TenantId = Event.TenantId, LegalEntityId = Event.LegalEntityId,
                SourcingEventId = Event.Id, Status = "Active", Quantity = 3m, UnitCode = "each", Description = "Item" } };
        }
        internal SourcingManualContact Contact(DateTimeOffset? reported = null) => PrincipalManualQuotationEvidence.Contact(
            Event, Invitation, Opening, Decision, Receipt, Actor, null, "Physical", "Quotation delivered externally", reported ?? At, At, Guid.NewGuid());
        internal PrincipalManualQuotationEvidence.DraftProjection Draft(SourcingManualContact contact)
        {
            Invitation.Status = "ManualContactRecorded";
            return PrincipalManualQuotationEvidence.Draft(Event, Invitation, contact, Sources,
                new[] { new PrincipalManualQuotationEvidence.LineInput(Sources[0].Id, 2m, 1.25m, 0m, 0m) }, Actor);
        }
        internal SourcingManualQuoteVersion Version(SourcingManualContact contact, PrincipalManualQuotationEvidence.DraftProjection draft) =>
            PrincipalManualQuotationEvidence.Quotation(Event, Invitation, contact, draft.Bid, draft.Lines,
                Sources, Actor, null, Guid.NewGuid(), At, "Create");
        internal ProcurementDbContext Context() => new(new DbContextOptionsBuilder<ProcurementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Scope(Event.TenantId, Event.LegalEntityId, Actor));
    }
    private sealed record Scope(string TenantId, Guid LegalEntityId, string SubjectId) : IProcurementScope
    {
        public Guid? WorkLocationId => null; public Guid? OrgUnitId => null;
        public bool IsConfined => false; public string? CorrelationId => "m7-factory";
    }
}
