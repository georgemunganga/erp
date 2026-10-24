using Mightyfin.Erp.Procurement.Application;
using Mightyfin.Erp.Procurement.Domain;

namespace Mightyfin.Erp.Procurement.Tests;

public sealed class PrincipalRfqAssessmentCoverageTests
{
    private static readonly Guid Offer = Guid.Parse("9d31350e-16e4-4fac-bfdc-7dc82f82908f");
    private const string Alice = "d4b22a7e-3c95-4f3c-a9ac-4ed6cefec7a1";
    private const string Bob = "26104afd-b110-49a5-94d0-03f750427c92";
    private static readonly PrincipalRfqEvaluationPlanDefinition.Criterion Criterion =
        new("TECHNICAL", "Technical", "Scored", true, 10m, 1m,
            Array.AsReadOnly(new[] { Alice, Bob }));

    private static PrincipalRfqCriterionAssessmentRecord Row(string person, int revision,
        Guid? prior = null, int? priorRevision = null, string criterion = "TECHNICAL",
        bool? passed = null) => new()
    {
        Id = Guid.NewGuid(), OfferId = Offer, CriterionCode = criterion,
        EvaluatorPrincipalId = person, Revision = revision,
        PreviousAssessmentId = prior, PreviousRevision = priorRevision,
        Passed = passed,
        SnapshotSha256 = new('a', 64),
    };

    [Fact]
    public void Missing_and_current_observations_are_presence_only_without_scores_or_winner()
    {
        var first = Row(Alice, 1);
        first.Score = 0m;
        var projection = PrincipalRfqAssessmentCoverage.Build(Offer, Criterion, new[] { first });
        Assert.Equal(2, projection.RequiredObservations);
        Assert.Equal(1, projection.ObservedObservations);
        Assert.Equal(new[] { Alice, Bob }, projection.Slots.Select(x => x.EvaluatorPrincipalId));
        Assert.Equal<Guid?>(first.Id, projection.Slots[0].AssessmentId);
        Assert.Equal<int?>(1, projection.Slots[0].Revision);
        Assert.True(projection.Slots[0].Observed);
        Assert.False(projection.Slots[1].Observed);
        Assert.Null(projection.Slots[1].AssessmentId);
        var visible = typeof(PrincipalRfqAssessmentCoverage.Slot).GetProperties()
            .Select(x => x.Name).ToArray();
        Assert.DoesNotContain("Score", visible);
        Assert.DoesNotContain("Passed", visible);
        Assert.DoesNotContain("Rationale", visible);
        Assert.DoesNotContain("Winner", visible);
    }

    [Fact]
    public void Correction_returns_latest_revision_but_requires_the_exact_original_chain()
    {
        var first = Row(Alice, 1);
        var second = Row(Alice, 2, first.Id, 1);
        var coverage = PrincipalRfqAssessmentCoverage.Build(Offer, Criterion, new[] { second, first });
        Assert.Equal<Guid?>(second.Id, coverage.Slots[0].AssessmentId);
        Assert.Equal<int?>(2, coverage.Slots[0].Revision);
        var rebound = Row(Alice, 2, Guid.NewGuid(), 1);
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqAssessmentCoverage.Build(
            Offer, Criterion, new[] { first, rebound }));
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqAssessmentCoverage.Build(
            Offer, Criterion, new[] { first, Row(Alice, 3, first.Id, 2) }));
    }

    [Fact]
    public void A_failed_pass_fail_observation_is_present_without_implying_a_pass()
    {
        var criterion = new PrincipalRfqEvaluationPlanDefinition.Criterion("COMPLIANCE",
            "Technical", "PassFail", true, null, null, Array.AsReadOnly(new[] { Bob }));
        var failed = Row(Bob, 1, criterion: "COMPLIANCE", passed: false);
        var coverage = PrincipalRfqAssessmentCoverage.Build(Offer, criterion, new[] { failed });
        Assert.Equal(1, coverage.ObservedObservations);
        Assert.True(coverage.Slots[0].Observed);
        Assert.DoesNotContain("Passed", typeof(PrincipalRfqAssessmentCoverage.Slot)
            .GetProperties().Select(x => x.Name));
    }

    [Fact]
    public void Unknown_evaluator_offer_criterion_and_duplicate_revision_fail_closed()
    {
        var first = Row(Alice, 1);
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqAssessmentCoverage.Build(
            Guid.NewGuid(), Criterion, new[] { first }));
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqAssessmentCoverage.Build(
            Offer, Criterion, new[] { Row(Guid.NewGuid().ToString("D"), 1) }));
        var wrongCriterion = Row(Alice, 1, criterion: "PRICE");
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqAssessmentCoverage.Build(
            Offer, Criterion, new[] { wrongCriterion }));
        Assert.Throws<InvalidOperationException>(() => PrincipalRfqAssessmentCoverage.Build(
            Offer, Criterion, new[] { first, Row(Alice, 1) }));
    }
}
