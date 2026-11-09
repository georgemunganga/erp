using System.Net;
using Mightyfin.Erp.Hrm.Api;

namespace Mightyfin.Erp.Hrm.Tests;

public sealed class ProductInvitationGuestBudgetTests
{
    private sealed class Clock:TimeProvider
    {
        internal long Ticks;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>Ticks;
    }
    [Theory]
    [InlineData(0,60)]
    [InlineData(1,10)]
    [InlineData(2,20)]
    public void Address_limits_expire_only_on_monotonic_window_boundary(int operation,int limit)
    {
        var op=(ProductInvitationGuestBudget.Operation)operation;
        var clock=new Clock();var budget=new ProductInvitationGuestBudget(clock);var ip=IPAddress.Parse("192.0.2.7");
        for(var i=0;i<limit;i++)Assert.True(budget.TryAcquire(op,ip));
        Assert.False(budget.TryAcquire(op,ip));
        clock.Ticks=TimeSpan.FromSeconds(59).Ticks;Assert.False(budget.TryAcquire(op,ip));
        clock.Ticks=TimeSpan.FromMinutes(1).Ticks;Assert.True(budget.TryAcquire(op,ip));
    }
    [Fact]
    public void Rotating_addresses_cannot_exceed_process_prepare_budget()
    {
        var budget=new ProductInvitationGuestBudget(new Clock());var accepted=0;
        for(var i=0;i<5000;i++)if(budget.TryAcquire(ProductInvitationGuestBudget.Operation.Prepare,new IPAddress(new byte[]{192,0,(byte)(i/256),(byte)i})))accepted++;
        Assert.Equal(100,accepted);
    }
    [Fact]
    public void IPv4_mapped_address_and_IPv6_privacy_addresses_cannot_split_the_same_bucket()
    {
        var budget=new ProductInvitationGuestBudget(new Clock());var op=ProductInvitationGuestBudget.Operation.Prepare;
        for(var i=0;i<10;i++)Assert.True(budget.TryAcquire(op,IPAddress.Parse("192.0.2.7")));
        Assert.False(budget.TryAcquire(op,IPAddress.Parse("::ffff:192.0.2.7")));
        budget=new(new Clock());
        for(var i=0;i<10;i++)Assert.True(budget.TryAcquire(op,IPAddress.Parse("2001:db8:1:2::"+(i+1))));
        Assert.False(budget.TryAcquire(op,IPAddress.Parse("2001:db8:1:2:ffff:ffff:ffff:ffff")));
    }
    [Fact]
    public void Missing_connection_address_is_bounded_and_operation_budgets_are_independent()
    {
        var budget=new ProductInvitationGuestBudget(new Clock());
        for(var i=0;i<10;i++)Assert.True(budget.TryAcquire(ProductInvitationGuestBudget.Operation.Prepare,null));
        Assert.False(budget.TryAcquire(ProductInvitationGuestBudget.Operation.Prepare,null));
        Assert.True(budget.TryAcquire(ProductInvitationGuestBudget.Operation.Session,null));
        Assert.True(budget.TryAcquire(ProductInvitationGuestBudget.Operation.Start,null));
    }
    [Fact]
    public async Task Competing_requests_share_the_limit_atomically()
    {
        var budget=new ProductInvitationGuestBudget(new Clock());
        var result=await Task.WhenAll(Enumerable.Range(0,200).Select(_=>Task.Run(()=>budget.TryAcquire(ProductInvitationGuestBudget.Operation.Prepare,IPAddress.Loopback))));
        Assert.Equal(10,result.Count(x=>x));
    }
}
