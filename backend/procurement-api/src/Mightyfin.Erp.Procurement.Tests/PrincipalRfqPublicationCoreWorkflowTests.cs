using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqPublicationCoreWorkflowTests
{
    private static (RfqPrincipalDraft Draft, PolicyVersion Policy, SourcingPolicyIdentityEvidence Created,
        SourcingPolicyIdentityEvidence Submitted, SourcingPolicyIdentityEvidence Published,
        RfqPublicationPreparation Shortlist, RfqPrincipalActor Reviewer, PolicyScenario Scenario, DateTimeOffset Now) Fixture()
    {
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var tenant = "tenant"; var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        var maker = Guid.NewGuid().ToString("D"); var requester = Guid.NewGuid().ToString("D");
        var reviewerId = Guid.NewGuid().ToString("D");
        var line = new RfqPrincipalDemandPin(Guid.NewGuid(), Guid.NewGuid(), 3, Guid.NewGuid(), 2,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), new string('b', 64),
            new string('c', 64), Guid.NewGuid(), new string('d', 64), Guid.NewGuid(), 1, "RFQ", 1,
            maker, requester, null, null, new string('e', 64), Guid.NewGuid(), Guid.NewGuid(),
            2m, 10m, 20m, "EA", "OFFICE", null);
        var draft = GovernedPrincipalRfqDraft.NewDraft(Guid.NewGuid(), tenant, company, branch,
            "ZMW", "Office equipment", maker, null, "Draft", [line]);
        var policy = new PolicyVersion
        {
            TenantId = tenant, LegalEntityId = company, CreatedBy = maker,
            PolicyKey = "SOURCING", PolicyVersionNumber = 1,
            EffectiveFrom = now.AddDays(-1), RulesJson = $$"""
            {"schemaVersion":1,"policyKey":"SOURCING","transactionType":"SourcingPublication",
             "action":"SourcingEvent.Publish","permission":"procurement-sourcing-publish",
             "currencyCode":"ZMW","amountBasis":"ApprovedLineBase","rules":[
              {"code":"ALL","priority":0,
               "when":{"minimumAmountInclusive":null,"maximumAmountExclusive":null,"categoryCodes":[],"purchaseTypes":[],"branchIds":[]},
               "result":{"publication":{"minimumQuoteCount":1,"minimumInvitedSuppliers":1,
                "requirementsPackRequired":false,"evaluationCriteriaRequired":false,"closingDateRequired":false},
                "approvalStages":[{"code":"PROCUREMENT","assignee":"ProcurementManager","mode":"One",
                  "principalIds":["{{reviewerId}}"]}]}}]}
            """,
        };
        var created = SourcingPolicyIdentity.Capture(policy, tenant, company, "Created", maker, null, 1,
            now.AddDays(-1), schemaVersion: 2);
        policy.Version = 2; policy.State = "PendingReview"; policy.ReviewRequestedBy = maker;
        policy.ReviewRequestedAt = now.AddHours(-1);
        var submitted = SourcingPolicyIdentity.Capture(policy, tenant, company, "Submitted", maker,
            null, 2, policy.ReviewRequestedAt.Value, created, schemaVersion: 2);
        policy.Version = 3; policy.State = "Published"; policy.PublishedBy = reviewerId;
        policy.PublishedAt = now.AddMinutes(-1);
        var published = SourcingPolicyIdentity.Capture(policy, tenant, company, "Published", reviewerId,
            null, 3, policy.PublishedAt.Value, created, submitted, 2);
        var supplier = new Supplier { TenantId = tenant, LegalEntityId = company, Status = "Active", IsOrderable = true };
        var shortlist = PrincipalRfqPublicationPreparation.Prepare(draft,
            new SourcingPublicationControls(1, 1, false, false, false), [supplier], now);
        var reviewer = new RfqPrincipalActor(reviewerId, null, tenant, company, branch,
            new HashSet<Guid> { branch }, false, new HashSet<string> { PrincipalRfqPublicationCoreWorkflow.Permission });
        var scenario = new PolicyScenario("ZMW", 20m, "OFFICE", "GOODS", branch);
        return (draft, policy, created, submitted, published, shortlist, reviewer, scenario, now);
    }

    [Fact]
    public void Opens_only_the_named_independent_core_case_and_retains_original_source_snapshot()
    {
        var f = Fixture();
        var opened = PrincipalRfqPublicationCoreWorkflow.Open(f.Draft, 1, f.Policy, f.Created,
            f.Submitted, f.Published, [f.Scenario], [f.Reviewer], f.Shortlist, f.Now);
        Assert.Equal("sourcing_publication", opened.Case.SourceType);
        Assert.Equal(f.Draft.Id, opened.Case.SourceId);
        Assert.Equal(f.Policy.Id, opened.Case.PolicyVersionId);
        var stage = Assert.Single(opened.Case.Stages);
        Assert.Equal("PROCUREMENT", stage.Code);
        Assert.Equal(f.Reviewer.PrincipalId, Assert.Single(stage.Assignments).AssigneeSubjectId);
        Assert.Equal(64, opened.SnapshotSha256.Length);
        Assert.Contains(f.Draft.OriginalFingerprint, opened.SnapshotJson);
        Assert.False(opened.Shortlist.CanPublish);
    }

    [Fact]
    public void Denies_unpublished_policy_wrong_reviewers_and_changed_source_classification()
    {
        var f = Fixture();
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqPublicationCoreWorkflow.Open(f.Draft, 1,
            f.Policy, f.Created, f.Submitted, f.Published, [f.Scenario],
            [f.Reviewer with { PrincipalId = f.Draft.MakerPrincipalId }], f.Shortlist, f.Now));
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqPublicationCoreWorkflow.Open(f.Draft, 1,
            f.Policy, f.Created, f.Submitted, f.Published, [f.Scenario with { LineBaseAmount = 19m }],
            [f.Reviewer], f.Shortlist, f.Now));
        f.Policy.State = "Draft";
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqPublicationCoreWorkflow.Open(f.Draft, 1,
            f.Policy, f.Created, f.Submitted, f.Published, [f.Scenario], [f.Reviewer], f.Shortlist, f.Now));
    }
}
