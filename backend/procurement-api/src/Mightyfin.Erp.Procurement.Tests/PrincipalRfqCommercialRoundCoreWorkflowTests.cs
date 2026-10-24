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
        stage.State = "Approved";
        assignment.State = "Approved";
        opening.Case.State = "Approved";
        opening.Case.CompletedAt = at;
        var decision = new ApprovalDecision
        {
            TenantId = f.Plan.TenantId, LegalEntityId = f.Plan.LegalEntityId,
            CaseId = opening.Case.Id, StageId = stage.Id,
            AssignmentId = assignment.Id, ActorSubjectId = f.Reviewer.PrincipalId,
            Action = "Approve", DecidedAt = at,
        };
        var approved = PrincipalRfqCommercialRoundCoreWorkflow.Finish(
            opening, f.Plan, f.Event, f.Policy, opening.Case,
            decision, f.Reviewer, at);
        Assert.Equal("Approved", approved.State);
        Assert.Equal(f.Plan.Id, approved.AcceptingRound!.PreparedPlanId);
        Assert.Equal(opening.SnapshotSha256,
            approved.AcceptingRound.ApprovalEvidenceSha256);
        opening.Case.State = "Returned";
        stage.State = "Returned";
        assignment.State = "Returned";
        decision.Action = "Return";
        decision.Reason = "Correct controls";
        var returned = PrincipalRfqCommercialRoundCoreWorkflow.Finish(
            opening, f.Plan, f.Event, f.Policy, opening.Case,
            decision, f.Reviewer, at);
        Assert.Equal("Returned", returned.State);
        Assert.Null(returned.AcceptingRound);
    }
}
