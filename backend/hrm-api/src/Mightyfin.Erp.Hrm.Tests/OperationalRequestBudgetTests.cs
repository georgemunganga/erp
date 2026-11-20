using Mightyfin.Erp.Hrm.Api;
namespace Mightyfin.Erp.Hrm.Tests;
public sealed class OperationalRequestBudgetTests
{
    private sealed class Clock : TimeProvider { internal long Ticks; public override long TimestampFrequency => TimeSpan.TicksPerSecond; public override long GetTimestamp() => Ticks; }
    [Fact] public void Verified_sessions_have_independent_budgets_behind_the_same_proxy()
    {
        var clock = new Clock(); var budget = new OperationalRequestBudget(clock);
        for (var i=0;i<OperationalRequestBudget.PerSession;i++) Assert.True(budget.TryAuthenticated("server-verified-session-a"));
        Assert.False(budget.TryAuthenticated("server-verified-session-a"));
        Assert.True(budget.TryAuthenticated("server-verified-session-b"));
        clock.Ticks=TimeSpan.FromSeconds(59).Ticks; Assert.False(budget.TryAuthenticated("server-verified-session-a"));
        clock.Ticks=TimeSpan.FromMinutes(1).Ticks; Assert.True(budget.TryAuthenticated("server-verified-session-a"));
    }
    [Fact] public void Verified_session_rotation_cannot_exceed_the_global_resource_budget()
    {
        var budget=new OperationalRequestBudget();
        for(var i=0;i<OperationalRequestBudget.PerProcess;i++) Assert.True(budget.TryAuthenticated("verified-"+(i/OperationalRequestBudget.PerSession)));
        Assert.False(budget.TryAuthenticated("another-verified-session"));
    }
    [Fact] public async Task Parallel_requests_observe_one_atomic_session_limit()
    {
        var budget=new OperationalRequestBudget();
        var results=await Task.WhenAll(Enumerable.Range(0,1000).Select(_=>Task.Run(()=>budget.TryAuthenticated("one-verified-session"))));
        Assert.Equal(OperationalRequestBudget.PerSession,results.Count(x=>x));
    }
}
