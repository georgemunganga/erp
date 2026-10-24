using System.Text.Json;
using System.Text.Json.Nodes;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Synthetic pure transition inputs only; actual retained Core/policy/
/// document proof and finite PostgreSQL acceptance remain adapter gates.</summary>
public sealed class PrincipalRfqOfferOpeningTests
{
    private sealed class Fixture
    {
        internal readonly string Officer = Guid.NewGuid().ToString("D"), Reader = Guid.NewGuid().ToString("D"),
            Closer = Guid.NewGuid().ToString("D"), Reviewer = Guid.NewGuid().ToString("D"), Maker = Guid.NewGuid().ToString("D");
        internal readonly Guid Company = Guid.NewGuid(), Branch = Guid.NewGuid(), Worker = Guid.NewGuid();
        internal readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        internal readonly PolicyVersion Policy;
        internal readonly PrincipalRfqPublishedRoundProof Round;
        internal readonly PrincipalRfqCommercialRoundSealRecord Seal;
        internal readonly string Snapshot;
        internal readonly RfqPrincipalActor Actor;
        internal readonly Dictionary<string, Guid?> Aliases;
        internal Fixture()
        {
            Policy = new() { TenantId = "fixture", LegalEntityId = Company,
                PolicyKey = "SOURCING_COMMERCIAL_ROUND", PolicyVersionNumber = 1,
                State = "Published", CreatedBy = Maker, PublishedBy = Reviewer,
                PublishedAt = Now.AddDays(-1), EffectiveFrom = Now.AddDays(-1),
                PublicationReason = "Explicit independent controls",
                RulesJson = PrincipalRfqOfferOpeningPolicyTests.Rules(Officer, Reader, Closer, Reviewer) };
            var rules = PrincipalRfqCommercialRoundPolicyRules.Parse(Policy.RulesJson);
            var roundId = Guid.NewGuid(); var eventId = Guid.NewGuid(); var planId = Guid.NewGuid();
            Snapshot = JsonSerializer.Serialize(new { coreCaseId = roundId, eventId,
                preparedPlanId = planId, preparedPlanSha256 = new string('a', 64),
                policyVersionId = Policy.Id, policyRulesSha256 = rules.RulesSha256,
                tenantId = "fixture", companyId = Company, branchId = Branch,
                offerOpening = rules.OfferOpening });
            Round = new(roundId, eventId, planId, new string('a', 64), roundId, Guid.NewGuid(),
                ProcurementEvidenceEncoding.Digest(Snapshot), "fixture", Company, Branch, "ZMW", 4,
                Now.AddHours(-2), Now.AddHours(-1), Closer, null, "Accepting", [Guid.NewGuid()]);
            Seal = new() { TenantId = "fixture", LegalEntityId = Company, OpeningId = roundId,
                SourcingEventId = eventId, BranchId = Branch, ApprovedDecisionId = Round.ApprovedDecisionId,
                OfficerPrincipalId = Closer, ClosedAt = Now.AddHours(-1), SubmissionCount = 0,
                SubmissionSetSha256 = ProcurementEvidenceEncoding.Digest("[]") };
            Actor = new(Officer, Worker, "fixture", Company, Branch, new HashSet<Guid> { Branch },
                false, new HashSet<string> { PrincipalRfqOfferOpeningControls.OpenAction });
            Aliases = new() { [Officer] = Worker, [Maker] = null, [Closer] = null, [Reader] = null };
        }
        internal PrincipalRfqOpenedOffersProof Open(string? snapshot = null,
            PrincipalRfqPublishedRoundProof? round = null, RfqPrincipalActor? actor = null,
            IReadOnlyList<RfqCommercialConflictParticipant>? conflicts = null)
            => PrincipalRfqOfferOpening.Open(round ?? Round, snapshot ?? Snapshot, Seal, Policy,
                [], actor ?? Actor, conflicts ?? [new(Maker, null)], Aliases, Now, "", "");
    }

    [Fact]
    public void Explicit_named_opening_retains_exact_seal_policy_and_zero_false_choices()
    {
        var f = new Fixture(); var opened = f.Open();
        Assert.Equal(f.Seal.Id, opened.SealId); Assert.Equal(f.Policy.Id, opened.PolicyVersionId);
        Assert.Equal(f.Seal.SubmissionSetSha256, opened.OfferSetSha256);
        Assert.Equal(0, opened.OfferCount); Assert.Equal("", opened.Reason);
        Assert.Equal(ProcurementEvidenceEncoding.Digest(opened.SnapshotJson), opened.SnapshotSha256);
    }

    [Fact]
    public void Approved_snapshot_and_exact_pinned_policy_cannot_be_rebound()
    {
        var f = new Fixture(); var node = JsonNode.Parse(f.Snapshot)!;
        node["policyVersionId"] = Guid.NewGuid();
        var changed = node.ToJsonString();
        Assert.Throws<InvalidOperationException>(() => f.Open(changed,
            f.Round with { ApprovalEvidenceSha256 = ProcurementEvidenceEncoding.Digest(changed) }));
        Assert.Throws<InvalidOperationException>(() => f.Open(round: f.Round with { BranchId = Guid.NewGuid() }));
        f.Policy.EffectiveTo = f.Now;
        Assert.Throws<InvalidOperationException>(() => f.Open());
    }

    [Fact]
    public void Named_opener_needs_current_distinct_action_and_branch_not_closer_authority()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Open(actor: f.Actor with { PrincipalId = f.Closer }));
        Assert.Throws<InvalidOperationException>(() => f.Open(actor: f.Actor with { Permissions = new HashSet<string> { PrincipalRfqSealedManualOffers.CloseAction } }));
        Assert.Throws<InvalidOperationException>(() => f.Open(actor: f.Actor with { SelectedBranchId = Guid.NewGuid() }));
    }

    [Fact]
    public void Both_retained_and_newly_linked_same_worker_conflicts_deny()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Open(conflicts: [new(f.Maker, f.Worker)]));
        f.Aliases[f.Maker] = f.Worker;
        Assert.Throws<InvalidOperationException>(() => f.Open());
        f.Aliases.Remove(f.Maker);
        Assert.Throws<InvalidOperationException>(() => f.Open());
    }

    [Fact]
    public void Open_does_not_grant_read_and_ordinary_read_does_not_grant_purpose_access()
    {
        var f = new Fixture(); var controls = PrincipalRfqCommercialRoundPolicyRules.Parse(f.Policy.RulesJson).OfferOpening!;
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqOfferOpening.RequireActor(f.Round, controls,
            f.Actor, f.Aliases, PrincipalRfqOfferOpeningControls.ReadAction, false));
        var reader = f.Actor with { PrincipalId = f.Reader, LinkedWorkerId = null,
            Permissions = new HashSet<string> { PrincipalRfqOfferOpeningControls.ReadAction } };
        PrincipalRfqOfferOpening.RequireActor(f.Round, controls, reader, f.Aliases,
            PrincipalRfqOfferOpeningControls.ReadAction, false);
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqOfferOpening.RequireActor(f.Round, controls,
            reader with { Permissions = new HashSet<string> { "procurement-sourcing-read" } },
            f.Aliases, PrincipalRfqOfferOpeningControls.ReadAction, false));
    }
}
