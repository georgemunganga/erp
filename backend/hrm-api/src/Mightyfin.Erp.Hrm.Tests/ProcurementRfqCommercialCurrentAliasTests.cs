using Mightyfin.Erp.Hrm.Api;
using Mightyfin.Erp.Procurement.Application;

namespace Mightyfin.Erp.Hrm.Tests;

/// <summary>Pure denial-only Core rule, not a live account/branch/provider test.</summary>
public sealed class ProcurementRfqCommercialCurrentAliasTests
{
    [Fact]
    public void Null_historical_workers_do_not_hide_current_same_worker_accounts()
    {
        var maker=Guid.NewGuid();var evaluator=Guid.NewGuid();var worker=Guid.NewGuid();
        var current=new Dictionary<Guid,Guid?>{{maker,worker},{evaluator,worker}};
        Assert.False(ProcurementPrincipalPeople.IndependentWithCurrentAliases(maker.ToString("D"),null,evaluator.ToString("D"),null,current));
    }
    [Fact]
    public void Either_original_or_current_alias_can_deny_cross_account_self_evaluation()
    {
        var first=Guid.NewGuid();var second=Guid.NewGuid();var historical=Guid.NewGuid();var current=Guid.NewGuid();
        Assert.False(ProcurementPrincipalPeople.IndependentWithCurrentAliases(first.ToString("D"),historical,second.ToString("D"),null,
            new Dictionary<Guid,Guid?>{{first,current},{second,historical}}));
        Assert.False(ProcurementPrincipalPeople.IndependentWithCurrentAliases(first.ToString("D"),null,second.ToString("D"),current,
            new Dictionary<Guid,Guid?>{{first,current},{second,null}}));
    }
    [Fact]
    public void Distinct_current_workerless_principals_remain_independent_without_employees()
    {
        var first=Guid.NewGuid();var second=Guid.NewGuid();
        Assert.True(ProcurementPrincipalPeople.IndependentWithCurrentAliases(first.ToString("D"),null,second.ToString("D"),null,
            new Dictionary<Guid,Guid?>{{first,null},{second,null}}));
        Assert.False(ProcurementPrincipalPeople.IndependentWithCurrentAliases(first.ToString("D"),null,first.ToString("D"),null,new Dictionary<Guid,Guid?>()));
    }

    [Fact]
    public void Opening_and_later_actions_deny_new_alias_link_from_original_maker_to_named_officer()
    {
        var maker = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var closer = Guid.NewGuid();
        var worker = Guid.NewGuid();
        RfqPrincipalActor Officer(Guid id, Guid? alias) => new(id.ToString("D"), alias,
            "fixture", Guid.NewGuid(), null, new HashSet<Guid>(), true,
            new HashSet<string>());
        var original = new (string PrincipalId, Guid? WorkerAlias)[]
            { (maker.ToString("D"), null) };
        var reviewers = new[] { Officer(reviewer, worker), Officer(closer, null) };
        Assert.True(ProcurementSourcingExecutionRoutes.CommercialParticipantsIndependentNow(
            original, reviewers, new Dictionary<Guid, Guid?>
                { [maker] = null, [reviewer] = worker, [closer] = null }));
        Assert.False(ProcurementSourcingExecutionRoutes.CommercialParticipantsIndependentNow(
            original, reviewers, new Dictionary<Guid, Guid?>
                { [maker] = worker, [reviewer] = worker, [closer] = null }));
        Assert.False(ProcurementSourcingExecutionRoutes.CommercialParticipantsIndependentNow(
            original, [Officer(closer, worker)], new Dictionary<Guid, Guid?>
                { [maker] = worker, [closer] = worker }));
        Assert.False(ProcurementSourcingExecutionRoutes.CommercialParticipantsIndependentNow(
            original, [Officer(reviewer, null), Officer(closer, null)],
            new Dictionary<Guid, Guid?>
                { [maker] = null, [reviewer] = worker, [closer] = worker }));
    }

    [Fact]
    public void Later_votes_and_close_deny_current_or_retained_alias_of_other_named_officers()
    {
        var acting = Guid.NewGuid();
        var otherReviewer = Guid.NewGuid();
        var closer = Guid.NewGuid();
        var worker = Guid.NewGuid();
        var actor = new RfqPrincipalActor(acting.ToString("D"), worker,
            "fixture", Guid.NewGuid(), null, new HashSet<Guid>(), true,
            new HashSet<string>());
        var others = new (string PrincipalId, Guid? OpeningAlias, Guid? VoteAlias)[]
        {
            (otherReviewer.ToString("D"), null, null),
            (closer.ToString("D"), null, null),
        };
        Assert.True(ProcurementSourcingExecutionRoutes.CommercialActingOfficerIndependentNow(
            actor, others, new Dictionary<Guid, Guid?>
                { [acting] = worker, [otherReviewer] = null, [closer] = null }));
        Assert.False(ProcurementSourcingExecutionRoutes.CommercialActingOfficerIndependentNow(
            actor, others, new Dictionary<Guid, Guid?>
                { [acting] = worker, [otherReviewer] = worker, [closer] = null }));
        others[0] = (otherReviewer.ToString("D"), null, worker);
        Assert.False(ProcurementSourcingExecutionRoutes.CommercialActingOfficerIndependentNow(
            actor, others, new Dictionary<Guid, Guid?>
                { [acting] = worker, [otherReviewer] = null, [closer] = null }));
        others[0] = (otherReviewer.ToString("D"), null, null);
        Assert.False(ProcurementSourcingExecutionRoutes.CommercialActingOfficerIndependentNow(
            actor, others, new Dictionary<Guid, Guid?>
                { [acting] = worker, [otherReviewer] = null, [closer] = worker }));
    }
}
