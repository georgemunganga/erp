using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;
using Xunit;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqAwardHeldQuantityTests
{
    private const string Tenant = "held-candidate";
    private const string Maker = "33333333-3333-4333-8333-333333333333";
    private const string Reviewer = "44444444-4444-4444-8444-444444444444";
    private static readonly Guid Company = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Branch = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 5, 0, 0, TimeSpan.Zero);
    private static string Rules => "{\"schemaVersion\":1,\"transactionType\":\"SupplierAward\",\"allocationMode\":\"FullOfferLineOnly\",\"reviewBranchId\":\""
        + Branch.ToString("D") + "\",\"currencyCode\":\"ZMW\",\"maximumAmountExact\":\"100\",\"stages\":[{\"code\":\"REVIEW\",\"mode\":\"All\",\"principalIds\":[\""
        + Reviewer + "\"]}]}";

    private static PrincipalRfqAwardHeldQuantity.OriginalHold Hold(Guid eventId, Guid lineId,
        string quantity = "1")
    {
        var request = Guid.NewGuid();
        var requestLine = Guid.NewGuid();
        var allocation = new PurchaseRequestAllocation
        {
            TenantId = Tenant, LegalEntityId = Company, PurchaseRequestId = request,
            PurchaseRequestLineId = requestLine, TargetType = "SourcingEvent", TargetId = eventId,
            Status = "Reserved", Quantity = decimal.Parse(quantity,
                System.Globalization.CultureInfo.InvariantCulture),
        };
        return new(new SourcingLine
        {
            Id = lineId, TenantId = Tenant, LegalEntityId = Company,
            SourcingEventId = eventId, PurchaseRequestLineId = requestLine,
            Status = "Active", Quantity = allocation.Quantity,
        }, new SourcingDemandBinding
        {
            TenantId = Tenant, LegalEntityId = Company,
            SourcingEventId = eventId, SourcingLineId = lineId,
            PurchaseRequestId = request, PurchaseRequestLineId = requestLine,
            AllocationId = allocation.Id, TargetType = "SourcingEvent",
            Quantity = allocation.Quantity,
        }, allocation);
    }

    private static PrincipalRfqAwardHeldQuantity.Claim Claim(Guid eventId, Guid lineId,
        string selectedQuantity = "0.6")
    {
        var head = new PrincipalRfqRecommendationDraftHead
        {
            TenantId = Tenant, LegalEntityId = Company, RoundId = Guid.NewGuid(),
            UnsealingId = Guid.NewGuid(), SealId = Guid.NewGuid(),
            SourcingEventId = eventId, BranchId = Branch,
            PreparedPlanId = Guid.NewGuid(), EvaluationPlanRevisionId = Guid.NewGuid(),
            ApprovedRoundSha256 = new string('a', 64), OpeningSha256 = new string('b', 64),
            OfferSetSha256 = new string('c', 64), EvaluationDefinitionSha256 = new string('d', 64),
            MakerPrincipalId = Maker,
        };
        var revision = new PrincipalRfqRecommendationDraftRevision
        {
            TenantId = Tenant, LegalEntityId = Company, DraftId = head.Id,
            RoundId = head.RoundId, UnsealingId = head.UnsealingId,
            SourcingEventId = eventId, BranchId = Branch, Revision = 1,
            ActorPrincipalId = Maker, RecordedAt = Now,
            CommandKey = Guid.NewGuid(), RequestSha256 = new string('e', 64),
        };
        var offer = Guid.NewGuid();
        var supplier = Guid.NewGuid();
        var offerSha = new string('f', 64);
        var opened = new[] { new PrincipalRfqRecommendationDraft.OfferLine(
            offer, offerSha, supplier, lineId, selectedQuantity, "ZMW") };
        var approved = new[] { new PrincipalRfqRecommendationDraft.SourceLine(lineId, "1") };
        var proposed = PrincipalRfqRecommendationDraft.Validate(
            [new PrincipalRfqRecommendationDraft.Selection(offer, lineId,
                selectedQuantity, "5.1234", "Human comparison")], opened, approved, "ZMW");
        revision.SnapshotJson = PrincipalRfqRecommendationDraft.Snapshot(head, revision, proposed);
        revision.SnapshotSha256 = ProcurementEvidenceEncoding.Digest(revision.SnapshotJson);
        head.CurrentRevision = 1;
        head.CurrentRevisionId = revision.Id;
        head.CurrentSnapshotSha256 = revision.SnapshotSha256;
        var currentLine = new PrincipalRfqRecommendationDraftLine
        {
            TenantId = Tenant, LegalEntityId = Company, DraftId = head.Id,
            RevisionId = revision.Id, RoundId = head.RoundId,
            UnsealingId = head.UnsealingId, SourcingEventId = eventId,
            BranchId = Branch, OfferId = offer, OfferSha256 = offerSha,
            SupplierId = supplier, SourcingLineId = lineId,
            QuantityExact = selectedQuantity, ProposedValueExact = "5.1234",
            CurrencyCode = "ZMW", Rationale = "Human comparison",
        };
        var policy = new PolicyVersion
        {
            TenantId = Tenant, LegalEntityId = Company,
            PolicyKey = PrincipalRfqAwardFoundation.PolicyKey,
            PolicyVersionNumber = 1, RulesJson = Rules,
            State = "Published", EffectiveFrom = Now.AddDays(-1),
        };
        var maker = new PrincipalRfqAwardFoundation.CurrentPerson(Maker, null, false,
            new HashSet<Guid> { Branch }, new HashSet<string> {
                "procurement-access", PrincipalRfqAwardFoundation.SubmitAction });
        var reviewer = new PrincipalRfqAwardFoundation.CurrentPerson(Reviewer, null, false,
            new HashSet<Guid> { Branch }, new HashSet<string> {
                "procurement-access", PrincipalRfqAwardFoundation.ReviewAction });
        var raw = new PrincipalRfqAwardReviewSourceGraph([revision], [currentLine], opened,
            approved, [new PrincipalRfqAwardFoundation.RetainedLine(offer, offerSha,
                supplier, lineId, selectedQuantity, "1", "ZMW", "5.1234")], maker, []);
        var verified = PrincipalRfqAwardSourceQualification.VerifyCurrent(head,
            revision, [revision], [currentLine], opened, approved, "ZMW");
        var body = PrincipalRfqAwardFoundation.CommandBody(verified, policy.Id, Branch);
        var candidate = PrincipalRfqAwardFoundation.Qualify(verified, raw.RetainedLines,
            policy, maker, [reviewer], [], Guid.NewGuid(), Tenant, Company, Branch,
            Guid.NewGuid(), body, ProcurementEvidenceEncoding.Digest(body), 1, Now);
        var opening = PrincipalRfqAwardReviewWorkflow.Prepare(candidate, raw, head,
            revision, policy, [reviewer], null, null, null, null, Now);
        return new(opening.FrozenRecommendation, opening.Submission,
            opening.Lines, opening.Receipt, null, opening.CoreCase,
            policy, Array.Empty<ApprovalDecision>());
    }

    private static PrincipalRfqAwardHeldQuantity.Balance Evaluate(
        PrincipalRfqAwardHeldQuantity.OriginalHold hold,
        params PrincipalRfqAwardHeldQuantity.Claim[] claims)
        => PrincipalRfqAwardHeldQuantity.Evaluate(hold, claims, [], [], []);

    [Fact]
    public void Shared_Core_replay_admits_pending_but_terminal_API_still_requires_terminal()
    {
        var claim = Claim(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(PrincipalRfqAwardCoreHistory.VerifyRetained(claim.Review,
            claim.CoreCase, claim.Policy, claim.Decisions));
        Assert.Equal("RfqAwardCoreTerminalSourceChanged", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqAwardCoreHistory.VerifyTerminal(claim.Review,
                claim.CoreCase, claim.Policy, claim.Decisions)).Message);
        var stage = Assert.Single(claim.CoreCase.Stages);
        stage.Code = "TAMPERED";
        Assert.Equal("RfqAwardAssignmentPinsChanged", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqAwardCoreHistory.VerifyRetained(claim.Review,
                claim.CoreCase, claim.Policy, claim.Decisions)).Message);
    }

    [Fact]
    public void Pending_claims_across_rounds_consume_the_same_original_source_hold()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line);
        var one = Claim(source, line, "0.6");
        var two = Claim(source, line, "0.4");
        var balance = Evaluate(hold, one, two);
        Assert.Equal("1", balance.ClaimedQuantityExact);
        Assert.Equal("0", balance.RemainingQuantityExact);
        Assert.Equal("RfqAwardHeldCapacityExceeded", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, one, Claim(source, line, "0.5"))).Message);
    }

    [Fact]
    public void Only_real_verified_return_or_rejection_releases_a_claim()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line); var claim = Claim(source, line);
        var stage = Assert.Single(claim.CoreCase.Stages);
        var task = Assert.Single(stage.Assignments);
        var decisions = new List<ApprovalDecision>();
        ApprovalEngine.Decide(claim.CoreCase, task,
            new ApprovalActor(Reviewer, Tenant, Company,
                new HashSet<string> { PrincipalRfqAwardFoundation.ReviewAction }),
            "Return", Guid.NewGuid().ToString("D"), "Correct recommendation",
            decisions, Now.AddMinutes(1));
        var terminal = claim with { Decisions = decisions };
        Assert.Equal("0.6", Evaluate(hold, terminal).ClaimedQuantityExact);
        Assert.Equal("Returned", PrincipalRfqAwardCoreHistory.VerifyRetained(
            claim.Review, claim.CoreCase, claim.Policy, decisions)!.CoreState);
        var verified = PrincipalRfqAwardCoreHistory.VerifyTerminal(claim.Review,
            claim.CoreCase, claim.Policy, decisions);
        Assert.Equal("1", Evaluate(hold, terminal with { Outcome = verified }).RemainingQuantityExact);
        Assert.Equal("RfqAwardCoreOutcomeChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, terminal with { Outcome = new PrincipalRfqAwardReviewOutcome
            {
                TenantId = Tenant, LegalEntityId = Company, SubmissionId = claim.Review.Id,
                CoreCaseId = claim.CoreCase.Id, CoreState = "Returned",
                SourceOutcome = "Returned", TerminalDecisionId = Guid.NewGuid(),
                RecordedAt = verified.RecordedAt,
            } })).Message);
    }

    [Fact]
    public void Forged_terminal_or_orphan_and_duplicate_claims_deny()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line); var claim = Claim(source, line);
        Assert.Equal("RfqAwardCoreOutcomeChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, claim with { Outcome = new PrincipalRfqAwardReviewOutcome
            {
                TenantId = Tenant, LegalEntityId = Company, SubmissionId = claim.Review.Id,
                CoreCaseId = claim.CoreCase.Id, CoreState = "Returned",
                SourceOutcome = "Returned", TerminalDecisionId = Guid.NewGuid(),
            } })).Message);
        Assert.Equal("RfqAwardClaimDuplicateOrOrphan", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, claim, claim)).Message);
        Assert.Equal("RfqAwardClaimGraphChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, claim with { Receipt = new PrincipalRfqAwardReviewReceipt
            {
                TenantId = Tenant, LegalEntityId = Company,
            } })).Message);
    }

    [Fact]
    public void Wrong_scope_source_or_original_hold_never_disappears_from_the_reducer()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line);
        Assert.Equal("RfqAwardClaimGraphChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, Claim(Guid.NewGuid(), line))).Message);
        var alien = Hold(source, line);
        alien.Line.LegalEntityId = Guid.NewGuid();
        Assert.Equal("RfqAwardOriginalHoldChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(alien, Claim(source, line))).Message);
        hold.Allocation.Status = "Released";
        Assert.Equal("RfqAwardOriginalHoldChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, Claim(source, line))).Message);
    }

    [Fact]
    public void Legacy_award_or_award_backed_order_requires_explicit_handoff()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line);
        Assert.Equal("RfqAwardLegacyHandoffRequired", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqAwardHeldQuantity.Evaluate(hold, [],
                [new SourcingAward { TenantId = Tenant, LegalEntityId = Company,
                    SourcingEventId = source }], [], [])).Message);
        Assert.Equal("RfqAwardLegacyHandoffRequired", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqAwardHeldQuantity.Evaluate(hold, [], [], [],
                [new PurchaseOrder { TenantId = Tenant, LegalEntityId = Company,
                SourcingAwardId = Guid.NewGuid() }])).Message);
        Assert.Equal("1", PrincipalRfqAwardHeldQuantity.Evaluate(hold, [], [], [],
            [new PurchaseOrder { TenantId = Tenant, LegalEntityId = Company,
                SourcingAwardId = null }]).RemainingQuantityExact);
    }

    [Fact]
    public void Empty_owned_ids_and_noncanonical_actor_or_request_pins_deny_pending_claim()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line);
        var noLineId = Claim(source, line);
        noLineId.Lines[0].Id = Guid.Empty;
        Assert.Equal("RfqAwardClaimDuplicateOrOrphan", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, noLineId)).Message);
        var noReceiptId = Claim(source, line);
        noReceiptId.Receipt.Id = Guid.Empty;
        Assert.Equal("RfqAwardClaimDuplicateOrOrphan", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, noReceiptId)).Message);
        var noReviewId = Claim(source, line);
        noReviewId.Review.Id = Guid.Empty;
        Assert.Equal("RfqAwardClaimDuplicateOrOrphan", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, noReviewId)).Message);
        var noKey = Claim(source, line);
        typeof(PrincipalRfqAwardReviewSubmission).GetProperty("CommandKey")!
            .SetValue(noKey.Review, Guid.Empty);
        Assert.Equal("RfqAwardClaimGraphChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, noKey)).Message);
        var badActor = Claim(source, line);
        typeof(PrincipalRfqAwardReviewSubmission).GetProperty("MakerPrincipalId")!
            .SetValue(badActor.Review, "not-a-canonical-principal");
        Assert.Equal("RfqAwardClaimGraphChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, badActor)).Message);
        var badHash = Claim(source, line);
        typeof(PrincipalRfqAwardReviewSubmission).GetProperty("RequestSha256")!
            .SetValue(badHash.Review, new string('Z', 64));
        Assert.Equal("RfqAwardClaimGraphChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, badHash)).Message);
    }

    [Fact]
    public void Out_of_scope_legacy_input_denies_even_without_an_award_reference()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line);
        Assert.Equal("RfqAwardLegacyScopeChanged", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqAwardHeldQuantity.Evaluate(hold, [], [], [],
                [new PurchaseOrder { TenantId = "other", LegalEntityId = Company,
                    SourcingAwardId = null }])).Message);
        Assert.Equal("RfqAwardLegacyScopeChanged", Assert.Throws<InvalidOperationException>(
            () => PrincipalRfqAwardHeldQuantity.Evaluate(hold, [],
                [new SourcingAward { TenantId = Tenant, LegalEntityId = Company,
                    SourcingEventId = Guid.NewGuid() }], [], [])).Message);
    }

    [Fact]
    public void Exact_four_decimal_boundary_is_conserved_without_rounding()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line, "0.0001");
        var claim = Claim(source, line, "0.0001");
        Assert.Equal("0", Evaluate(hold, claim).RemainingQuantityExact);
        var other = Claim(source, line, "0.0001");
        Assert.Equal("RfqAwardHeldCapacityExceeded", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, claim, other)).Message);
        hold.Line.Quantity = 0.00011m;
        Assert.Equal("RfqAwardExactQuantityInvalid", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, claim)).Message);
    }
    [Fact]
    public void Core_approved_pending_business_award_still_consumes_hold()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line); var claim = Claim(source, line);
        var stage = Assert.Single(claim.CoreCase.Stages);
        var task = Assert.Single(stage.Assignments);
        var decisions = new List<ApprovalDecision>();
        ApprovalEngine.Decide(claim.CoreCase, task,
            new ApprovalActor(Reviewer, Tenant, Company,
                new HashSet<string> { PrincipalRfqAwardFoundation.ReviewAction }),
            "Approve", Guid.NewGuid().ToString("D"), null,
            decisions, Now.AddMinutes(1));
        var terminal = PrincipalRfqAwardCoreHistory.VerifyTerminal(claim.Review,
            claim.CoreCase, claim.Policy, decisions);
        Assert.Equal("CoreApprovedPendingAward", terminal.SourceOutcome);
        Assert.Equal("0.6", Evaluate(hold, claim with
            { Decisions = decisions, Outcome = terminal }).ClaimedQuantityExact);
    }

    [Fact]
    public void Pending_history_with_wrong_actor_or_scope_is_not_silently_counted()
    {
        var source = Guid.NewGuid(); var line = Guid.NewGuid();
        var hold = Hold(source, line); var claim = Claim(source, line);
        var stage = Assert.Single(claim.CoreCase.Stages);
        var task = Assert.Single(stage.Assignments);
        var bad = new ApprovalDecision
        {
            TenantId = Tenant, LegalEntityId = Company, CaseId = claim.CoreCase.Id,
            StageId = stage.Id, AssignmentId = task.Id, ActorSubjectId = Maker,
            Action = "Approve", IdempotencyKey = Guid.NewGuid().ToString("D"),
            DecidedAt = Now.AddMinutes(1),
        };
        Assert.Equal("RfqAwardCoreVoteActorChanged", Assert.Throws<InvalidOperationException>(
            () => Evaluate(hold, claim with { Decisions = [bad] })).Message);
    }
}