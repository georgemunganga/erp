using Mightyfin.Erp.Hrm.Application;
using Mightyfin.Erp.Hrm.Application.Time;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class MobileWorkdayRulesTests
{
    private static MobileWorkdayCommand Command(string kind,string reason="")=>new(Guid.NewGuid(),Guid.NewGuid(),0,kind,reason);
    [Fact]
    public void Multiple_breaks_and_departure_return_do_not_close_the_workday()
    {
        var state="working";
        foreach(var kind in new[]{"break-start","break-end","break-start","break-end","departure","return"})
            state=MobileWorkdayRules.Apply(state,Command(kind,kind=="departure"?"Customer visit":""),false);
        Assert.Equal("working",state);
    }
    [Theory]
    [InlineData("working","return")][InlineData("working","break-end")]
    [InlineData("on_break","break-start")][InlineData("on_break","departure")]
    [InlineData("away","break-start")][InlineData("away","departure")]
    public void Invalid_transitions_are_rejected(string state,string kind)
        =>Assert.Throws<DomainException>(()=>MobileWorkdayRules.Apply(state,Command(kind,"Reason supplied"),false));
    [Theory]
    [InlineData("break-start")][InlineData("break-end")][InlineData("departure")][InlineData("return")]
    public void Completed_attendance_rejects_presence_changes(string kind)
        =>Assert.Throws<DomainException>(()=>MobileWorkdayRules.Apply("working",Command(kind,"Work visit"),true));
    [Fact]
    public void Reports_can_follow_checkout_without_reopening_or_creating_time()
    {
        var c=Command("report") with{Summary="Prepared the supplier documentation",Blockers="Waiting for confirmation"};
        Assert.Equal("working",MobileWorkdayRules.Apply("working",c,true));
    }
    [Fact]
    public void Missing_departure_reason_or_unknown_and_mixed_payloads_are_rejected()
    {
        foreach(var c in new[]{Command("departure"),Command("auto-checkout"),Command("report"),
            Command("break-start") with{Summary="Hidden report"},Command("report") with{Summary=new string('x',2001)},
            Command("report") with{Summary="Work\0done"}})
            Assert.Throws<DomainException>(()=>MobileWorkdayRules.Validate(c));
    }
}
