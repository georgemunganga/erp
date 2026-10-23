using Mightyfin.Erp.Hrm.Api;

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
}
