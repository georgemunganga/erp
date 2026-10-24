using System.Text.Json;
using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed partial class PrincipalRfqPrepublicationPlanTests
{
    private static (PrincipalRfqPreparedPlan Plan,
        PrincipalRfqCommercialDraftHead Head,
        PrincipalRfqCommercialDraftRevision Revision,
        SourcingEvent Event, RfqPrincipalDraft Original,
        PolicyVersion Policy, RfqPrincipalActor Reviewer,
        RfqPrincipalActor Closer, DateTimeOffset At) CommercialRoundShape()
    {
        var shape = PreparedShape();
        var prepared = PrincipalRfqPreparedPlanEvidence.Create(shape.Candidate,
            shape.F.Policy, shape.Head, shape.Row, CommercialActor(shape.F),
            shape.F.At, 0, null, null);
        shape.F.Event.Status = "Published";
        var original = GovernedPrincipalRfqRestoration.RestoreVerified(
            shape.F.Evidence, shape.F.Event, [shape.F.Binding]);
        var reviewer = shape.F.Evaluator with
        {
            Permissions = new HashSet<string>
            {
                PrincipalRfqCommercialRoundPolicyRules.ApprovalAction,
            },
        };
        var closer = reviewer with
        {
            PrincipalId = Guid.NewGuid().ToString("D"),
            Permissions = new HashSet<string>
            {
                PrincipalRfqSealedManualOffers.CloseAction,
            },
        };
        var rules = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, identitySchemaVersion = 2,
            policyKey = PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
            transactionType = PrincipalRfqCommercialRoundPolicyRules.TransactionType,
            submissionMode = "ManualSurrogateOnly",
            requireOriginalOfferDocument = true,
            requireIndependentReview = true,
            closingOfficerPrincipalId = closer.PrincipalId,
            stages = new[] { new { code = "CommercialReview", mode = "All",
                principalIds = new[] { reviewer.PrincipalId } } },
        });
        var policy = new PolicyVersion
        {
            TenantId = prepared.TenantId, LegalEntityId = prepared.LegalEntityId,
            PolicyKey = PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
            PolicyVersionNumber = 1, State = "Published", RulesJson = rules,
            CreatedBy = prepared.ActorPrincipalId,
            PublishedBy = Guid.NewGuid().ToString("D"),
            PublicationReason = "Independent commercial controls",
            PublishedAt = shape.F.At.AddMinutes(-1),
            EffectiveFrom = shape.F.At.AddMinutes(-1),
        };
        return (prepared, shape.Head, shape.Row, shape.F.Event, original,
            policy, reviewer, closer, shape.F.At.AddMinutes(1));
    }

    [Fact]
    public void Actual_Core_approval_pins_exact_v2_opening_controls_and_rejects_later_policy_change()
    {
        var f = CommercialRoundShape();
        var document = System.Text.Json.Nodes.JsonNode.Parse(f.Policy.RulesJson)!.AsObject();
        document["schemaVersion"] = 2;
        document["offerOpening"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new
        {
            officerPrincipalId = Guid.NewGuid().ToString("D"),
            readerPrincipalIds = new[] { Guid.NewGuid().ToString("D") },
            allowClosingOfficer = false, delayAfterCloseSeconds = 0,
            reasonRequired = false, referenceRequired = false,
        }));
        f.Policy.RulesJson = document.ToJsonString();
        var opening = PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan,
            f.Head, f.Revision, f.Event, f.Original, f.Policy,
            [f.Reviewer], f.Closer, [Guid.NewGuid()], f.At);
        var rules = PrincipalRfqCommercialRoundPolicyRules.Parse(f.Policy.RulesJson);
        Assert.True(PrincipalRfqOfferOpeningControls.MatchesRoundSnapshot(opening.SnapshotJson, rules));
        var assignment = opening.Case.Stages.Single().Assignments.Single();
        var decisions = new List<ApprovalDecision>();
        var actor = new ApprovalActor(f.Reviewer.PrincipalId, f.Plan.TenantId,
            f.Plan.LegalEntityId, f.Reviewer.Permissions);
        var at = f.At.AddMinutes(1);
        ApprovalEngine.Decide(opening.Case, assignment, actor, "Approve",
            Guid.NewGuid().ToString("D"), null, decisions, at);
        Assert.Equal("Approved", PrincipalRfqCommercialRoundCoreWorkflow.Finish(
            opening, f.Plan, f.Event, f.Policy, opening.Case,
            decisions, decisions.Single(), f.Reviewer, at).State);
        document["offerOpening"]!["reasonRequired"] = true;
        f.Policy.RulesJson = document.ToJsonString();
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqCommercialRoundCoreWorkflow.Finish(
            opening, f.Plan, f.Event, f.Policy, opening.Case,
            decisions, decisions.Single(), f.Reviewer, at));
    }

    [Fact]
    public void Distinct_commercial_case_binds_plan_supplier_window_and_independent_core_review()
    {
        var f = CommercialRoundShape();
        var supplier = Guid.NewGuid();
        var opening = PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan,
            f.Head, f.Revision, f.Event, f.Original, f.Policy,
            [f.Reviewer], f.Closer, [supplier], f.At);
        Assert.Equal(PrincipalRfqCommercialRoundCoreWorkflow.SourceType,
            opening.Case.SourceType);
        Assert.Equal(f.Plan.Id, opening.Case.SourceId);
        Assert.Equal(f.Policy.Id, opening.Case.PolicyVersionId);
        Assert.Equal(f.Reviewer.PrincipalId,
            opening.Case.Stages.Single().Assignments.Single().AssigneeSubjectId);
        Assert.Contains(f.Plan.SnapshotSha256, opening.SnapshotJson);
        Assert.Equal([supplier], opening.SupplierIds);
    }

    [Fact]
    public void Current_alias_rebinding_and_wrong_supplier_set_block_opening()
    {
        var f = CommercialRoundShape();
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan, f.Head,
                f.Revision, f.Event, f.Original, f.Policy,
                [f.Reviewer], f.Closer, [Guid.Empty], f.At));
        var relinked = f.Reviewer with { LinkedWorkerId = f.Original.MakerWorkerAlias };
        // A null original worker link is not an alias conflict; a current
        // duplicate known worker between reviewer and closer is.
        var worker = Guid.NewGuid();
        relinked = relinked with { LinkedWorkerId = worker };
        var sameWorkerCloser = f.Closer with { LinkedWorkerId = worker };
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan, f.Head,
                f.Revision, f.Event, f.Original, f.Policy,
                [relinked], sameWorkerCloser, [Guid.NewGuid()], f.At));
    }

    [Fact]
    public void Approved_core_terminal_can_yield_round_but_return_cannot()
    {
        var f = CommercialRoundShape();
        var opening = PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan,
            f.Head, f.Revision, f.Event, f.Original, f.Policy,
            [f.Reviewer], f.Closer, [Guid.NewGuid()], f.At);
        var stage = opening.Case.Stages.Single();
        var assignment = stage.Assignments.Single();
        var at = f.At.AddMinutes(1);
        var decisions = new List<ApprovalDecision>();
        var actor = new ApprovalActor(f.Reviewer.PrincipalId, f.Plan.TenantId,
            f.Plan.LegalEntityId, f.Reviewer.Permissions);
        ApprovalEngine.Decide(opening.Case, assignment, actor, "Approve",
            Guid.NewGuid().ToString("D"), null, decisions, at);
        var decision = decisions.Single();
        var approved = PrincipalRfqCommercialRoundCoreWorkflow.Finish(
            opening, f.Plan, f.Event, f.Policy, opening.Case,
            decisions, decision, f.Reviewer, at);
        Assert.Equal("Approved", approved.State);
        Assert.Equal(f.Plan.Id, approved.AcceptingRound!.PreparedPlanId);
        Assert.Equal(opening.SnapshotSha256,
            approved.AcceptingRound.ApprovalEvidenceSha256);
        var retained = new PrincipalRfqCommercialRoundDecisionRecord
        {
            TenantId = f.Plan.TenantId, LegalEntityId = f.Plan.LegalEntityId,
            OpeningId = opening.Case.Id, CoreCaseId = opening.Case.Id,
            SourcingEventId = f.Event.Id, BranchId = f.Plan.BranchId,
            CoreDecisionId = decision.Id, CoreAssignmentId = assignment.Id,
            TerminalVoteId = Guid.NewGuid(),
            State = "Approved", ActorPrincipalId = decision.ActorSubjectId,
            ActorWorkerAliasId = f.Reviewer.LinkedWorkerId,
            OpeningSha256 = opening.SnapshotSha256,
            CoreOutcomeSha256 = approved.CoreOutcomeSha256,
            DecidedAt = ProcurementEvidenceEncoding.DatabaseTimestamp(decision.DecidedAt)!.Value,
        };
        var historical = PrincipalRfqCommercialRoundCoreWorkflow.VerifyRetainedApproved(
            opening, f.Plan, f.Event, f.Policy, opening.Case,
            decisions, decision, retained, at.AddMinutes(1));
        Assert.Equal(approved.AcceptingRound.RoundId, historical.RoundId);
        Assert.Equal(approved.AcceptingRound.ApprovedDecisionId, historical.ApprovedDecisionId);
        var poisoned = new PrincipalRfqCommercialRoundDecisionRecord
        {
            TenantId = retained.TenantId, LegalEntityId = retained.LegalEntityId,
            OpeningId = retained.OpeningId, CoreCaseId = retained.CoreCaseId,
            SourcingEventId = retained.SourcingEventId, BranchId = retained.BranchId,
            CoreDecisionId = retained.CoreDecisionId,
            CoreAssignmentId = retained.CoreAssignmentId,
            TerminalVoteId = retained.TerminalVoteId,
            State = retained.State, ActorPrincipalId = retained.ActorPrincipalId,
            ActorWorkerAliasId = retained.ActorWorkerAliasId,
            OpeningSha256 = retained.OpeningSha256,
            CoreOutcomeSha256 = new string('f', 64),
            DecidedAt = retained.DecidedAt,
        };
        Assert.Equal("RfqCommercialRetainedApprovalChanged", Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.VerifyRetainedApproved(
                opening, f.Plan, f.Event, f.Policy, opening.Case,
                decisions, decision, poisoned, at.AddMinutes(1))).Message);
        var returnedOpening = PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan,
            f.Head, f.Revision, f.Event, f.Original, f.Policy,
            [f.Reviewer], f.Closer, [Guid.NewGuid()], f.At);
        var returnedAssignment = returnedOpening.Case.Stages.Single().Assignments.Single();
        var returnedDecisions = new List<ApprovalDecision>();
        ApprovalEngine.Decide(returnedOpening.Case, returnedAssignment, actor,
            "Return", Guid.NewGuid().ToString("D"), "Correct controls", returnedDecisions, at);
        var returned = PrincipalRfqCommercialRoundCoreWorkflow.Finish(
            returnedOpening, f.Plan, f.Event, f.Policy, returnedOpening.Case,
            returnedDecisions, returnedDecisions.Single(), f.Reviewer, at);
        Assert.Equal("Returned", returned.State);
        Assert.Null(returned.AcceptingRound);
    }

    [Fact]
    public void Approved_label_without_all_core_votes_and_retained_closer_alias_denies()
    {
        var f = CommercialRoundShape();
        var opening = PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan,
            f.Head, f.Revision, f.Event, f.Original, f.Policy,
            [f.Reviewer], f.Closer, [Guid.NewGuid()], f.At);
        var stage = opening.Case.Stages.Single();
        var assignment = stage.Assignments.Single();
        var at = f.At.AddMinutes(1);
        opening.Case.State = "Approved";
        opening.Case.CompletedAt = at;
        opening.Case.CompletionKey = "completed";
        stage.State = "Approved";
        assignment.State = "Approved";
        var forged = new ApprovalDecision
        {
            TenantId = f.Plan.TenantId, LegalEntityId = f.Plan.LegalEntityId,
            CaseId = opening.Case.Id, StageId = stage.Id,
            AssignmentId = assignment.Id, ActorSubjectId = f.Reviewer.PrincipalId,
            Action = "Approve", IdempotencyKey = "completed", DecidedAt = at,
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Finish(opening, f.Plan,
                f.Event, f.Policy, opening.Case, [], forged, f.Reviewer, at));
        var wrongCloser = opening with { ClosingOfficerWorkerAliasId = Guid.NewGuid() };
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Finish(wrongCloser, f.Plan,
                f.Event, f.Policy, opening.Case, [forged], forged, f.Reviewer, at));
    }

    [Fact]
    public void Real_two_stage_one_then_all_core_votes_obey_exact_quorum_and_order()
    {
        var f = CommercialRoundShape();
        var people = Enumerable.Range(0, 4).Select(_ => f.Reviewer with
        {
            PrincipalId = Guid.NewGuid().ToString("D"),
        }).ToArray();
        f.Policy.RulesJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, identitySchemaVersion = 2,
            policyKey = PrincipalRfqCommercialRoundPolicyRules.PolicyKey,
            transactionType = PrincipalRfqCommercialRoundPolicyRules.TransactionType,
            submissionMode = "ManualSurrogateOnly",
            requireOriginalOfferDocument = true,
            requireIndependentReview = true,
            closingOfficerPrincipalId = f.Closer.PrincipalId,
            stages = new[]
            {
                new { code = "Technical", mode = "One",
                    principalIds = new[] { people[0].PrincipalId, people[1].PrincipalId } },
                new { code = "Commercial", mode = "All",
                    principalIds = new[] { people[2].PrincipalId, people[3].PrincipalId } },
            },
        });
        var opening = PrincipalRfqCommercialRoundCoreWorkflow.Open(f.Plan,
            f.Head, f.Revision, f.Event, f.Original, f.Policy,
            people, f.Closer, [Guid.NewGuid()], f.At);
        var stages = opening.Case.Stages.OrderBy(x => x.Sequence).ToArray();
        var history = new List<ApprovalDecision>();
        var at = f.At.AddMinutes(1);
        foreach (var person in new[] { people[0], people[2], people[3] })
        {
            var assignment = stages.SelectMany(x => x.Assignments)
                .Single(x => x.AssigneeSubjectId == person.PrincipalId);
            ApprovalEngine.Decide(opening.Case, assignment,
                new ApprovalActor(person.PrincipalId, f.Plan.TenantId,
                    f.Plan.LegalEntityId, person.Permissions),
                "Approve", Guid.NewGuid().ToString("D"), null, history, at);
        }
        var terminal = history.Last();
        var result = PrincipalRfqCommercialRoundCoreWorkflow.Finish(opening,
            f.Plan, f.Event, f.Policy, opening.Case, history, terminal,
            people[3], at);
        Assert.Equal("Approved", result.State);
        var foreignStageVote = new ApprovalDecision
        {
            TenantId = f.Plan.TenantId, LegalEntityId = f.Plan.LegalEntityId,
            CaseId = opening.Case.Id, StageId = Guid.NewGuid(),
            AssignmentId = Guid.NewGuid(), ActorSubjectId = people[1].PrincipalId,
            Action = "Approve", IdempotencyKey = Guid.NewGuid().ToString("D"),
            DecidedAt = at,
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Finish(opening,
                f.Plan, f.Event, f.Policy, opening.Case,
                history.Append(foreignStageVote).ToArray(), terminal, people[3], at));
        Assert.Equal("Superseded", stages[0].Assignments.Single(x =>
            x.AssigneeSubjectId == people[1].PrincipalId).State);
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Finish(opening,
                f.Plan, f.Event, f.Policy, opening.Case, history.Skip(1).ToArray(),
                terminal, people[3], at));
        var firstVote = history[0];
        firstVote.DecidedAt = at.AddSeconds(1);
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Finish(opening,
                f.Plan, f.Event, f.Policy, opening.Case, history, terminal,
                people[3], at));
        firstVote.DecidedAt = at;
        var other = stages[0].Assignments.Single(x =>
            x.AssigneeSubjectId == people[1].PrincipalId);
        other.State = "Approved";
        history.Add(new ApprovalDecision
        {
            TenantId = f.Plan.TenantId, LegalEntityId = f.Plan.LegalEntityId,
            CaseId = opening.Case.Id, StageId = stages[0].Id,
            AssignmentId = other.Id, ActorSubjectId = other.AssigneeSubjectId,
            Action = "Approve", IdempotencyKey = Guid.NewGuid().ToString("D"),
            DecidedAt = at,
        });
        Assert.Throws<InvalidOperationException>(() =>
            PrincipalRfqCommercialRoundCoreWorkflow.Finish(opening,
                f.Plan, f.Event, f.Policy, opening.Case, history, terminal,
                people[3], at));
    }
}
