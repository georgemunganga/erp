using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqSealedManualOffersTests
{
    private static readonly DateTimeOffset Opens = new(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Closes = Opens.AddHours(2);

    private sealed record Fixture(PrincipalRfqPublishedRoundProof Round,
        PrincipalRfqPreparedPlan Prepared, SourcingEvent Event,
        SourcingInvitation Invitation, SupplierBid Bid,
        SourcingManualQuoteVersion Version, SourcingLine SourceLine,
        SupplierBidLine BidLine, RfqPrincipalActor Recorder);

    private static Fixture New()
    {
        var tenant = "fixture";
        var company = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var bidId = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var principal = Guid.NewGuid().ToString("D");
        var prepared = new PrincipalRfqPreparedPlan
        {
            TenantId = tenant, LegalEntityId = company, SourcingEventId = eventId,
            EventVersion = 1, BranchId = branch, SnapshotSha256 = new string('a', 64),
        };
        var round = new PrincipalRfqPublishedRoundProof(Guid.NewGuid(), eventId,
            prepared.Id, prepared.SnapshotSha256, Guid.NewGuid(), Guid.NewGuid(),
            new string('b', 64), tenant, company, branch, "USD", 1,
            Opens, Closes, principal, null, "Accepting", [supplier]);
        var ev = new SourcingEvent
        {
            Id = eventId, TenantId = tenant, LegalEntityId = company,
            BranchRef = branch, ExecutionVersion = 2, Status = "Published",
            CurrencyCode = "USD",
        };
        var invitation = new SourcingInvitation
        {
            Id = invitationId, TenantId = tenant, LegalEntityId = company,
            SourcingEventId = eventId, SupplierId = supplier,
            Status = "ManualContactRecorded",
        };
        var bid = new SupplierBid
        {
            Id = bidId, TenantId = tenant, LegalEntityId = company,
            SourcingEventId = eventId, InvitationId = invitationId,
            SupplierId = supplier, SourceChannel = "Manual", Status = "Draft",
            CurrencyCode = "USD", SurrogateEnteredBy = principal,
            SubmissionVersion = 1, DraftRevision = 1,
        };
        var sourceLine = new SourcingLine
        {
            Id = lineId, TenantId = tenant, LegalEntityId = company,
            SourcingEventId = eventId, Status = "Active", Quantity = 1,
        };
        var bidLine = new SupplierBidLine
        {
            TenantId = tenant, LegalEntityId = company,
            SupplierBidId = bidId, SourcingLineId = lineId,
            Quantity = 1, UnitPrice = 100,
        };
        var snapshot = PrincipalManualQuotationSnapshot.Build(bid, [bidLine]);
        var version = new SourcingManualQuoteVersion
        {
            TenantId = tenant, LegalEntityId = company,
            SourcingEventId = eventId, SupplierBidId = bidId,
            Revision = 1, BidVersion = 1, Operation = "Create",
            ActorPrincipalId = principal, CommandKey = Guid.NewGuid(),
            CommandSha256 = new string('c', 64),
            SnapshotJson = snapshot,
            SnapshotSha256 = ProcurementEvidenceEncoding.Digest(snapshot),
            ResponseJson = "{}",
            ResponseSha256 = ProcurementEvidenceEncoding.Digest("{}"),
            RecordedAt = Opens.AddMinutes(-1),
        };
        var actor = new RfqPrincipalActor(principal, null, tenant, company,
            branch, new HashSet<Guid> { branch }, false,
            new HashSet<string> { PrincipalRfqSealedManualOffers.SubmitAction,
                PrincipalRfqSealedManualOffers.CloseAction });
        return new(round, prepared, ev, invitation, bid, version,
            sourceLine, bidLine, actor);
    }

    private static PrincipalRfqManualOfferSubmission Submit(Fixture f,
        DateTimeOffset observed, DateTimeOffset? reported = null)
        => PrincipalRfqSealedManualOffers.Submit(f.Round, f.Prepared, f.Event,
            f.Invitation, f.Bid, f.Version, [f.SourceLine], [f.BidLine],
            f.Recorder, Guid.NewGuid(), new string('d', 64),
            reported ?? observed, observed);

    [Fact]
    public void Manual_submission_freezes_original_draft_and_close_seals_exact_set()
    {
        var f = New();
        var submitted = Submit(f, Opens.AddMinutes(1));
        Assert.Equal(f.Version.SnapshotSha256, submitted.DraftSnapshotSha256);
        Assert.Equal(f.Bid.Id, submitted.BidId);
        Assert.Contains("\"quantityExact\":\"1\"", submitted.SnapshotJson);
        Assert.NotEqual(f.Version.SnapshotSha256, submitted.SnapshotSha256);
        var seal = PrincipalRfqSealedManualOffers.Close(f.Round, [submitted],
            f.Recorder, Closes);
        Assert.Equal(1, seal.SubmissionCount);
        Assert.Equal(64, seal.SubmissionSetSha256.Length);
        Assert.Equal("Draft", f.Bid.Status); // Submission never repurposes draft bytes.
    }

    [Fact]
    public void Reported_early_receipt_cannot_make_late_server_submission_eligible()
    {
        var f = New();
        Assert.Equal("RfqSealedOfferWindowClosed", Assert.Throws<InvalidOperationException>(
            () => Submit(f, Closes.AddSeconds(1), Opens.AddMinutes(1))).Message);
    }

    [Fact]
    public void Prepared_candidate_or_changed_draft_cannot_be_adopted_as_submitted()
    {
        var f = New();
        Assert.Equal("RfqSealedRoundPublicationRequired", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqSealedManualOffers.Submit(f.Round with { State = "PreparedCandidate" },
                f.Prepared, f.Event, f.Invitation, f.Bid, f.Version,
                [f.SourceLine], [f.BidLine], f.Recorder,
                Guid.NewGuid(), new string('d', 64), Opens, Opens)).Message);
        f.BidLine.UnitPrice = 101;
        Assert.Equal("RfqSealedOfferDraftChanged", Assert.Throws<InvalidOperationException>(
            () => Submit(f, Opens.AddMinutes(1))).Message);
    }

    [Fact]
    public void Scoped_recorder_and_named_closing_officer_are_required()
    {
        var f = New();
        var outsider = f.Recorder with { Branches = new HashSet<Guid> { Guid.NewGuid() },
            SelectedBranchId = null };
        Assert.Equal("RfqSealedOfferRecorderNotAuthorized", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqSealedManualOffers.Submit(f.Round, f.Prepared, f.Event,
                f.Invitation, f.Bid, f.Version, [f.SourceLine], [f.BidLine],
                outsider, Guid.NewGuid(), new string('d', 64), Opens, Opens)).Message);
        var submitted = Submit(f, Opens.AddMinutes(1));
        var otherOfficer = f.Recorder with { PrincipalId = Guid.NewGuid().ToString("D") };
        Assert.Equal("RfqSealedRoundOfficerNotAuthorized", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqSealedManualOffers.Close(f.Round, [submitted],
                otherOfficer, Closes)).Message);
    }

    [Fact]
    public void Close_rejects_duplicate_supplier_and_changed_submission_bytes()
    {
        var f = New();
        var submitted = Submit(f, Opens.AddMinutes(1));
        Assert.Equal("RfqSealedSubmissionSetChanged", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqSealedManualOffers.Close(f.Round,
                [submitted, submitted with { BidId = Guid.NewGuid() }],
                f.Recorder, Closes)).Message);
        Assert.Equal("RfqSealedSubmissionSetChanged", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqSealedManualOffers.Close(f.Round,
                [submitted with { SnapshotJson = "{}" }], f.Recorder, Closes)).Message);
        Assert.Equal("RfqSealedSubmissionSetChanged", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqSealedManualOffers.Close(f.Round,
                [submitted with { SupplierId = Guid.NewGuid() }], f.Recorder, Closes)).Message);
    }

    [Fact]
    public void Invitation_created_after_review_for_another_supplier_cannot_enter_round()
    {
        var f = New();
        Assert.Equal("RfqSealedRoundPublicationRequired", Assert.Throws<InvalidOperationException>(
            () => Submit(f with { Round = f.Round with { ApprovedSupplierIds = [Guid.NewGuid()] } },
                Opens.AddMinutes(1))).Message);
    }
}
