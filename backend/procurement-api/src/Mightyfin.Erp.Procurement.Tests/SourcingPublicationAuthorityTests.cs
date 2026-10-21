using System.Reflection;
using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

/// <summary>Pure proposed prerequisite tests; no persisted grant/policy or publication authority.</summary>
public sealed class SourcingPublicationAuthorityTests
{
    [Fact]
    public void Configured_publication_tuple_is_exact_without_changing_PR_PO_contracts()
    {
        var tuple = ProcurementSpendingActions.SourcingPublication;
        Assert.Equal("SourcingEvent.Publish", tuple.Action); Assert.Equal("procurement-sourcing-publish", tuple.Permission);
        Assert.Equal("ApprovedLineBase", tuple.AmountBasis);
        Assert.Same(tuple, ProcurementSpendingActions.Resolve(tuple.Action));
        Assert.True(ProcurementSpendingActions.Matches(tuple.Action, tuple.Permission, tuple.AmountBasis));
        Assert.Same(ProcurementSpendingActions.PurchaseRequest, ProcurementSpendingActions.Resolve("PurchaseRequest.Approve"));
        Assert.Same(ProcurementSpendingActions.PurchaseOrder, ProcurementSpendingActions.Resolve("PurchaseOrder.Approve"));
    }

    [Fact]
    public void Exact_boundary_covers_selected_original_hold_not_whole_PR_or_later_remaining_quantity()
    {
        var f = new Fixture(); var grant = f.Grant(20);
        f.Source.Line.AllocatedQuantity = 9; // Current other holds do not redefine this original selected event.
        var original = f.Draft.Fingerprint;
        var result = f.Check([grant]);
        Assert.True(result.AuthorityEligible); Assert.False(result.CanPublish);
        Assert.Equal(20m, result.Spend!.Amount);
        Assert.True(ProcurementSpendingAuthority.Evaluate(result.Spend, f.Reviewer.SubjectId,
            "procurement-sourcing-publish", "SourcingEvent.Publish", f.Source.Now, [grant]).Eligible);
        Assert.Equal(100m, f.Source.Source.Submission.EstimatedAmount);
        Assert.Equal(20m, Assert.Single(Assert.Single(result.Stages).Reviewers).Authority.TargetAmount);
        Assert.Contains("SourcingPublicationPolicyNotVerified", result.Blockers);
        Assert.Contains("PublicationPersistenceUnavailable", result.Blockers);
        Assert.Equal(original, f.Draft.Fingerprint); Assert.Equal("Draft", f.Draft.State);
        Assert.False(f.Check([f.Grant(19.9999m)]).AuthorityEligible);
        Assert.False(f.Check([f.Grant(12), f.Grant(12)]).AuthorityEligible);
    }

    [Theory]
    [InlineData("currency")][InlineData("basis")][InlineData("po-tuple")][InlineData("permission")]
    [InlineData("tenant")][InlineData("company")][InlineData("principal")][InlineData("branch")]
    [InlineData("department")][InlineData("category")][InlineData("revoked")][InlineData("expired")]
    public void Wrong_grant_tuple_scope_or_effective_authority_never_falls_back(string change)
    {
        var f = new Fixture(); var grant = f.Grant(20);
        switch (change)
        {
            case "currency": grant.CurrencyCode = "USD"; break;
            case "basis": grant.AmountBasis = "EstimatedLineBase"; break;
            case "po-tuple": grant.Action = "PurchaseOrder.Approve"; grant.Permission = "procurement-po-approve"; break;
            case "permission": grant.Permission = "procurement-approval-decide"; break;
            case "tenant": grant.TenantId = "foreign"; break;
            case "company": grant.LegalEntityId = Guid.NewGuid(); break;
            case "principal": grant.PrincipalId = "other"; break;
            case "branch": grant.AllBranches = false; grant.BranchRef = Guid.NewGuid(); break;
            case "department": grant.AllDepartments = false; grant.DepartmentRef = Guid.NewGuid(); break;
            case "category": grant.AllCategories = false; AddCategory(grant, "OTHER"); break;
            case "revoked": grant.State = "Revoked"; break;
            case "expired": grant.EffectiveUntil = f.Source.Now; break;
        }
        Assert.False(f.Check([grant]).AuthorityEligible);
        Assert.False(f.Check([]).AuthorityEligible);
    }

    [Theory]
    [InlineData("event-worker-alias")][InlineData("source-maker")][InlineData("source-requester")]
    [InlineData("no-permission")][InlineData("foreign-company")][InlineData("branch-confined")]
    public void Canonical_worker_independence_and_current_actor_scope_are_required(string change)
    {
        var f = new Fixture(); var reviewer = f.Reviewer;
        reviewer = change switch
        {
            "event-worker-alias" => reviewer with { WorkerId = f.Maker.WorkerId },
            "source-maker" => reviewer with { WorkerId = f.Draft.Lines[0].MakerWorkerId },
            "source-requester" => reviewer with { WorkerId = f.Draft.Lines[0].RequesterWorkerId },
            "no-permission" => reviewer with { Permissions = new HashSet<string>() },
            "foreign-company" => reviewer with { CompanyId = Guid.NewGuid() },
            _ => reviewer with { OrganisationWide = false, Branches = new HashSet<Guid>() },
        };
        Assert.False(f.Check([f.Grant(20)], [new(new("PUBLISH", "ProcurementManager", "One"), [reviewer])]).AuthorityEligible);
    }

    [Theory]
    [InlineData("One", 1)][InlineData("All", 2)]
    public void Named_supported_quorum_is_only_a_proposal_not_a_Core_vote(string mode, int expected)
    {
        var f = new Fixture(); var second = f.Reviewer with { SubjectId = "reviewer-two", WorkerId = Guid.NewGuid() };
        var result = f.Check([f.Grant(20), f.Grant(20, second.SubjectId)],
            [new(new("PUBLISH", "ProcurementManager", mode), [f.Reviewer, second])]);
        Assert.True(result.AuthorityEligible); Assert.False(result.CanPublish);
        Assert.Equal(expected, Assert.Single(result.Stages).RequiredApprovals);
        Assert.Equal(2, Assert.Single(result.Stages).Reviewers.Count);
    }

    [Fact]
    public void No_implicit_reviewers_or_alias_quorum_and_no_unsupported_policy_modes()
    {
        var f = new Fixture(); var grant = f.Grant(100);
        foreach (var stage in new[] { new PolicyPreviewStage("PUBLISH", "ProcurementManager", "Any"),
            new("PUBLISH", "LineManager", "One"), new("PUBLISH", "ProcurementManager", "NoApproval") })
            Assert.False(f.Check([grant], [new(stage, [f.Reviewer])]).AuthorityEligible);
        Assert.False(f.Check([grant], []).AuthorityEligible);
        Assert.False(f.Check([grant], [new(new("PUBLISH", "ProcurementManager", "One"), [])]).AuthorityEligible);
        var alias = f.Reviewer with { SubjectId = "alias" };
        Assert.False(f.Check([grant, f.Grant(100, alias.SubjectId)],
            [new(new("PUBLISH", "ProcurementManager", "All"), [f.Reviewer, alias])]).AuthorityEligible);
    }

    [Fact]
    public void One_grant_must_cover_full_multi_category_department_event_not_each_separate_line()
    {
        var f = new Fixture(); var departmentA = Guid.NewGuid(); var departmentB = Guid.NewGuid();
        // Reconstructed immutable draft is a pure guard fixture only, not retained source approval.
        var first = f.Draft.Lines[0] with { DepartmentId = departmentA };
        var second = first with { LineId = Guid.NewGuid(), EventLineId = Guid.NewGuid(), AllocationId = Guid.NewGuid(),
            Quantity = 3, EstimatedValue = 30, CategoryCode = "SERVICES", DepartmentId = departmentB };
        var draft = Reconstruct(f.Draft, f.Maker, [first, second]);
        var result = f.Check([f.Grant(50)], draft: draft);
        Assert.True(result.AuthorityEligible); Assert.Equal(50m, result.Spend!.Amount);
        var categoryA = f.Grant(50); categoryA.AllCategories = false; AddCategory(categoryA, first.CategoryCode!);
        var categoryB = f.Grant(50); categoryB.AllCategories = false; AddCategory(categoryB, second.CategoryCode!);
        Assert.False(f.Check([categoryA, categoryB], draft: draft).AuthorityEligible);
        var oneDepartment = f.Grant(50); oneDepartment.AllDepartments = false; oneDepartment.DepartmentRef = departmentA;
        Assert.False(f.Check([oneDepartment], draft: draft).AuthorityEligible);
    }

    [Fact]
    public void Exact_products_sums_and_retained_values_are_never_rounded_or_silently_recomputed()
    {
        var f = new Fixture(); var pin = f.Draft.Lines[0]; var original = f.Draft.Fingerprint;
        Assert.Equal(0.00000001m, SourcingPublicationAuthority.SelectedOriginalAmount([pin with { Quantity = 0.0001m, UnitEstimate = 0.0001m, EstimatedValue = 0.00000001m }]));
        Assert.Throws<InvalidOperationException>(() => SourcingPublicationAuthority.SelectedOriginalAmount([pin with { EstimatedValue = 19.9999m }]));
        Assert.Equal("ExactDecimalNotRepresentable", Assert.Throws<InvalidOperationException>(() =>
            SourcingPublicationAuthority.SelectedOriginalAmount([pin with { Quantity = 99999999999999.9999m, UnitEstimate = 99999999999999.9999m }])).Message);
        var operand = 99999999999999m; var product = ExactDecimalArithmetic.Multiply(operand, operand);
        var lines = Enumerable.Range(0, 9).Select(_ => pin with { LineId = Guid.NewGuid(), EventLineId = Guid.NewGuid(), AllocationId = Guid.NewGuid(),
            Quantity = operand, UnitEstimate = operand, EstimatedValue = product }).ToArray();
        Assert.Equal("ExactDecimalNotRepresentable", Assert.Throws<InvalidOperationException>(() => SourcingPublicationAuthority.SelectedOriginalAmount(lines)).Message);
        Assert.Equal(original, f.Draft.Fingerprint);
    }

    [Fact]
    public void Overlapping_narrowed_version_cannot_revive_older_broader_authority()
    {
        var f = new Fixture(); var old = f.Grant(100); var replacement = f.Grant(1);
        replacement.AuthorityReference = old.AuthorityReference; replacement.AuthorityVersion = 2;
        var result = f.Check([old, replacement]);
        Assert.False(result.AuthorityEligible); Assert.Contains("SpendingAuthorityEvidenceAmbiguous", result.Blockers);
    }

    private static void AddCategory(ProcurementSpendingAuthorityGrant grant, string category) => grant.Categories.Add(new()
    { GrantId = grant.Id, TenantId = grant.TenantId, LegalEntityId = grant.LegalEntityId, CategoryCode = category });
    private static GovernedRfqDraft.Draft Reconstruct(GovernedRfqDraft.Draft original, RfqCurrentActor maker, RfqDemandPin[] pins) =>
        (GovernedRfqDraft.Draft)typeof(GovernedRfqDraft.Draft).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [original.Id, maker, original.BranchId, original.CurrencyCode, original.Title, "Draft", pins])!;

    private sealed class Fixture
    {
        public PurchaseOrderSourceAllocationTests.Fixture Source { get; } = new(executionRoute: "CompetitiveSourcing", minimumQuoteCount: 0);
        public RfqCurrentActor Maker { get; }
        public RfqCurrentActor Reviewer { get; }
        public GovernedRfqDraft.Draft Draft { get; }
        public Fixture()
        {
            Maker = new("maker", Guid.NewGuid(), Source.Request.TenantId, Source.Company, null, new HashSet<Guid>(), true,
                new HashSet<string> { "procurement-sourcing-convert", "procurement-sourcing-submit" }, true, new HashSet<Guid>());
            Reviewer = Maker with { SubjectId = "reviewer", WorkerId = Guid.NewGuid(), Permissions = new HashSet<string> { "procurement-sourcing-publish" } };
            Draft = GovernedRfqDraft.Create(Maker, Guid.NewGuid(), Guid.NewGuid(), "Publication prerequisite",
                [new(Source.Source, Source.Request.Version, 2)], Source.Now.AddMinutes(2)).Result;
        }
        public ProcurementSpendingAuthorityGrant Grant(decimal limit, string principal = "reviewer") => new()
        { TenantId = Draft.TenantId, LegalEntityId = Draft.CompanyId, Version = 3, AuthorityReference = Guid.NewGuid().ToString("N"),
            AuthorityVersion = 1, PrincipalId = principal, Permission = "procurement-sourcing-publish", Action = "SourcingEvent.Publish",
            AmountBasis = "ApprovedLineBase", CurrencyCode = Draft.CurrencyCode, MaximumAmount = limit, State = "Active",
            EffectiveFrom = Source.Now.AddDays(-1), AllBranches = true, AllDepartments = true, AllCategories = true };
        public SourcingPublicationAuthorityProposal Check(ProcurementSpendingAuthorityGrant[] grants,
            SourcingPublicationNamedStage[]? stages = null, GovernedRfqDraft.Draft? draft = null) =>
            SourcingPublicationAuthority.Evaluate(draft ?? Draft, Maker,
                stages ?? [new(new("PUBLISH", "ProcurementManager", "One"), [Reviewer])], Source.Now, grants);
    }
}
