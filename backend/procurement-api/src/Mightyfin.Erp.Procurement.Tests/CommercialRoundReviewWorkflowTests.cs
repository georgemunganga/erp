using Mightyfin.Erp.Core.Approvals.Domain;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class CommercialRoundReviewWorkflowTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Maker = Guid.NewGuid();
    private static readonly Guid Reviewer = Guid.NewGuid();
    private static readonly Guid Method = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);
    private static string Rules => $$"""{"schemaVersion":1,"mode":"Any","requiredApprovals":1,"assigneePrincipalIds":["{{Reviewer:D}}"],"methodIds":["{{Method:D}}"],"categoryCodes":["*"],"currencyCodes":["ZMW"],"minimumCriteria":1,"requiredCriterionStages":["Technical"],"requiredParticipantDuties":["Planner"],"requireTechnicalRelease":false,"requireCommercialRelease":false,"documentsRequired":false}""";

    [Fact]
    public void Review_policy_needs_explicit_named_people_and_no_default_rule()
    {
        var definition = Assert.IsType<CommercialRoundReviewPolicyRules.Definition>(CommercialRoundReviewPolicyRules.Read(Rules));
        Assert.Equal(Reviewer, Assert.Single(definition.AssigneePrincipalIds));
        Assert.Equal(Method, Assert.Single(definition.MethodIds));
        Assert.Equal("*", Assert.Single(definition.CategoryCodes));
        Assert.Equal("ZMW", Assert.Single(definition.CurrencyCodes));
        Assert.Equal("Planner", Assert.Single(definition.RequiredParticipantDuties));
        Assert.Null(CommercialRoundReviewPolicyRules.Read(Rules.Replace(Reviewer.ToString("D"), Maker.ToString("D"))
            .Replace("\"minimumCriteria\":1,", "")));
        Assert.Null(CommercialRoundReviewPolicyRules.Read(Rules.Replace("\"requiredApprovals\":1", "\"requiredApprovals\":0")));
    }

    [Fact]
    public void Submit_return_resubmit_and_publish_keep_content_but_append_new_result_versions()
    {
        var source = new RoundPlanSource("round-review-tests", Company, Guid.NewGuid(), Branch,
            Guid.NewGuid(), 1, new string('a', 64), Maker.ToString("D"));
        var draft = new RoundPlanDraftInput("Goods", "Approved RFQ method", null, null,
            At.AddDays(1), At.AddDays(2), null, null,
            [new(Guid.NewGuid(), 1, "TECH", "Technical", "PassFail", null, null, null, null, null)],
            [new(Guid.NewGuid(), 1, Maker.ToString("D"), "Planner", null)]);
        var created = CommercialRoundPlanDraft.Create(source, new(Maker.ToString("D"), null), draft,
            "UTC", Guid.NewGuid(), new string('b', 64), At);
        created.Plan.Version = 1;
        var submitted = CommercialRoundPlanDraft.Transition(created.Plan, created.Revision,
            created.Criteria, created.Participants, new(Maker.ToString("D"), null),
            "Submit", "InReview", null, Guid.NewGuid(), new string('c', 64), At.AddMinutes(1),
            new(Guid.NewGuid(), Guid.NewGuid(), null, null));
        Assert.Equal(2, submitted.Revision.ResultVersion);
        Assert.Equal(created.Revision.ContentSha256, submitted.Revision.ContentSha256);
        CommercialRoundPlanDraft.VerifyReceipt(submitted.Receipt, submitted.Plan, submitted.Revision);

        created.Plan.Status = "InReview"; created.Plan.Version = 2;
        var returned = CommercialRoundPlanDraft.Transition(created.Plan, submitted.Revision,
            submitted.Criteria, submitted.Participants, new(Reviewer.ToString("D"), null),
            "Return", "Draft", "Need another quote", Guid.NewGuid(), new string('d', 64), At.AddMinutes(2),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(3, returned.Revision.ResultVersion);
        Assert.Equal(submitted.Revision.ContentSha256, returned.Revision.ContentSha256);
        CommercialRoundPlanDraft.VerifyReceipt(returned.Receipt, returned.Plan, returned.Revision);

        created.Plan.Status = "Draft"; created.Plan.Version = 3;
        var again = CommercialRoundPlanDraft.Transition(created.Plan, returned.Revision,
            returned.Criteria, returned.Participants, new(Maker.ToString("D"), null),
            "Submit", "InReview", null, Guid.NewGuid(), new string('e', 64), At.AddMinutes(3),
            new(Guid.NewGuid(), Guid.NewGuid(), null, null));
        created.Plan.Status = "InReview"; created.Plan.Version = 4;
        var published = CommercialRoundPlanDraft.Transition(created.Plan, again.Revision,
            again.Criteria, again.Participants, new(Reviewer.ToString("D"), null),
            "Approve", "Published", null, Guid.NewGuid(), new string('f', 64), At.AddMinutes(4),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(5, published.Revision.ResultVersion);
        Assert.Equal(created.Revision.ContentSha256, published.Revision.ContentSha256);
        CommercialRoundPlanDraft.VerifyReceipt(published.Receipt, published.Plan, published.Revision);
        Assert.Throws<InvalidOperationException>(() => CommercialRoundPlanDraft.Transition(created.Plan, published.Revision,
            published.Criteria, published.Participants, new(Reviewer.ToString("D"), null),
            "Approve", "Published", null, Guid.NewGuid(), new string('0', 64), At.AddMinutes(5),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public void Shared_Core_case_requires_independent_assigned_current_reviewer()
    {
        var source = new RoundPlanSource("round-review-tests", Company, Guid.NewGuid(), Branch,
            Guid.NewGuid(), 1, new string('a', 64), Maker.ToString("D"));
        var first = CommercialRoundPlanDraft.Create(source, new(Maker.ToString("D"), null),
            new RoundPlanDraftInput(null, null, null, null, null, null, null, null, [], []),
            "UTC", Guid.NewGuid(), new string('b', 64), At);
        first.Plan.Version = 1;
        var submitted = CommercialRoundPlanDraft.Transition(first.Plan, first.Revision,
            first.Criteria, first.Participants, new(Maker.ToString("D"), null),
            "Submit", "InReview", null, Guid.NewGuid(), new string('c', 64), At.AddMinutes(1),
            new(Guid.NewGuid(), Guid.NewGuid(), null, null));
        first.Plan.Status = "InReview";
        var policy = new PolicyVersion { TenantId = source.TenantId, LegalEntityId = Company,
            PolicyKey = CommercialRoundReviewPolicyRules.PolicyKey, PolicyVersionNumber = 1,
            State = "Published", RulesJson = Rules, EffectiveFrom = At.AddDays(-1) };
        var round = new CommercialRoundReviewSubmission { TenantId = source.TenantId,
            LegalEntityId = Company, SourcingEventId = source.EventId, BranchId = Branch,
            PlanId = first.Plan.Id, SubmittedRevisionId = submitted.Revision.Id,
            SubmittedPlanVersion = submitted.Revision.ResultVersion,
            SubmittedContentSha256 = submitted.Revision.ContentSha256,
            PolicyVersionId = policy.Id, PolicyVersionNumber = 1,
            PolicyRulesSha256 = ProcurementEvidenceEncoding.PolicyJsonDigest(Rules),
            CoreCaseId = Guid.NewGuid(), MakerPrincipalId = Maker.ToString("D"), SubmittedAt = At.AddMinutes(1) };
        var maker = new PlanReviewPrincipal(Maker, null, source.TenantId, Company, false,
            new HashSet<Guid> { Branch }, new HashSet<string> { "procurement-access", CommercialRoundReviewCoreWorkflow.SubmitPermission });
        var reviewer = new PlanReviewPrincipal(Reviewer, null, source.TenantId, Company, false,
            new HashSet<Guid> { Branch }, new HashSet<string> { "procurement-access", CommercialRoundReviewCoreWorkflow.ReviewPermission });
        var rules = CommercialRoundReviewPolicyRules.Read(Rules)!;
        var approvalCase = CommercialRoundReviewCoreWorkflow.Open(first.Plan, submitted.Revision,
            round, policy, rules, maker, [reviewer], At.AddMinutes(1));
        var assignment = Assert.Single(Assert.Single(approvalCase.Stages).Assignments);
        var decisions = new List<ApprovalDecision>();
        var result = CommercialRoundReviewCoreWorkflow.Decide(first.Plan, round, approvalCase,
            assignment, reviewer, "Approve", Guid.NewGuid().ToString("D"), null,
            decisions, At.AddMinutes(2));
        Assert.Equal("Published", result.ResultStatus);
        Assert.Single(decisions);
        Assert.Throws<InvalidOperationException>(() => CommercialRoundReviewCoreWorkflow.Decide(first.Plan,
            round, approvalCase, assignment, maker, "Approve", Guid.NewGuid().ToString("D"), null,
            decisions, At.AddMinutes(3)));
    }

    [Fact]
    public void Rejected_plan_can_be_cancelled_without_reopening_or_mutating_its_decision()
    {
        var source = new RoundPlanSource("round-review-tests", Company, Guid.NewGuid(), Branch,
            Guid.NewGuid(), 1, new string('a', 64), Maker.ToString("D"));
        var draft = new RoundPlanDraftInput(null, null, null, null, null, null, null, null, [], []);
        var first = CommercialRoundPlanDraft.Create(source, new(Maker.ToString("D"), null), draft,
            "UTC", Guid.NewGuid(), new string('b', 64), At);
        first.Plan.Status = "Rejected"; first.Plan.Version = 2;
        // A rejected result is immutable; cancellation adds another version with
        // the same content and a required explanation.
        var rejectedRevision = new CommercialRoundPlanRevision
        {
            TenantId = first.Revision.TenantId, LegalEntityId = first.Revision.LegalEntityId,
            PlanId = first.Plan.Id, SourcingEventId = source.EventId, BranchId = Branch,
            PreviousRevisionId = first.Revision.Id, ExpectedVersion = 1, ResultVersion = 2,
            Operation = "Reject", ResultStatus = "Rejected", ActorPrincipalId = Reviewer.ToString("D"),
            CompanyTimeZoneId = "UTC", ContentJson = first.Revision.ContentJson,
            ContentSha256 = first.Revision.ContentSha256, RecordedAt = At.AddMinutes(1),
        };
        var cancelled = CommercialRoundPlanDraft.Revise(first.Plan, rejectedRevision, 2,
            new(Maker.ToString("D"), null), draft, "UTC", "Cancel", "No further procurement",
            Guid.NewGuid(), new string('c', 64), At.AddMinutes(2));
        Assert.Equal("Cancelled", cancelled.Revision.ResultStatus);
        Assert.Equal(rejectedRevision.ContentSha256, cancelled.Revision.ContentSha256);
    }
}
