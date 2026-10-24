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
}
