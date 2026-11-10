using Booking.Api;
using Booking.Domain;
using Shared.Contracts;
public class BookingTests
{
    [Theory]
    [InlineData(BookingStatus.PendingApproval,BookingStatus.Approved,true)]
    [InlineData(BookingStatus.PendingApproval,BookingStatus.Rejected,true)]
    [InlineData(BookingStatus.PendingApproval,BookingStatus.Assigned,false)]
    [InlineData(BookingStatus.Approved,BookingStatus.Assigned,true)]
    [InlineData(BookingStatus.Approved,BookingStatus.Completed,false)]
    [InlineData(BookingStatus.Assigned,BookingStatus.Completed,true)]
    [InlineData(BookingStatus.Assigned,BookingStatus.Cancelled,true)]
    [InlineData(BookingStatus.Rejected,BookingStatus.Assigned,false)]
    [InlineData(BookingStatus.Completed,BookingStatus.Cancelled,false)]
    [InlineData(BookingStatus.Cancelled,BookingStatus.Approved,false)]
    [InlineData(BookingStatus.PendingApproval,BookingStatus.Cancelled,true)]
    public void EnforcesLifecycle(BookingStatus from,BookingStatus to,bool expected)=>Assert.Equal(expected,BookingRules.CanTransition(from,to));
    [Fact] public void OnlyDirectManagerCanApprove(){var manager=Guid.NewGuid();Assert.True(BookingRules.CanApprove(manager,[Roles.Manager],manager));Assert.False(BookingRules.CanApprove(Guid.NewGuid(),[Roles.Manager],manager));Assert.False(BookingRules.CanApprove(manager,[Roles.Employee],manager));Assert.False(BookingRules.CanApprove(manager,[Roles.Manager],null));}
    [Fact] public void TerminalStatesHaveNoTransitions(){foreach(var current in new[]{BookingStatus.Rejected,BookingStatus.Cancelled,BookingStatus.Completed})foreach(var next in Enum.GetValues<BookingStatus>())Assert.False(BookingRules.CanTransition(current,next));}
    [Fact] public async Task RoleEventUpdatesCachedPermissionsAndIgnoresOlderEvents()
    {
        var id=Guid.NewGuid();var cache=new PermissionCache(TimeProvider.System);var loads=0;
        Task<string[]> Load(){loads++;return Task.FromResult(new[]{Roles.Manager});}
        Assert.Contains(Roles.Manager,await cache.RolesFor(id,Load));
        await cache.Handle(EventEnvelope.Create(EventTypes.UserRoleChanged,new UserRoleChanged(id,[Roles.Employee],2)));
        await cache.Handle(EventEnvelope.Create(EventTypes.UserRoleChanged,new UserRoleChanged(id,[Roles.Manager],1)));
        Assert.Equal(new[]{Roles.Employee},await cache.RolesFor(id,Load));Assert.Equal(1,loads);
    }
    [Fact] public async Task ConcurrentColdLoadsAreCoalesced()
    {
        var id=Guid.NewGuid();var cache=new PermissionCache(TimeProvider.System);var loads=0;
        async Task<string[]> Load(){Interlocked.Increment(ref loads);await Task.Delay(10);return [Roles.Manager];}
        await Task.WhenAll(Enumerable.Range(0,15).Select(_=>cache.RolesFor(id,Load)));Assert.Equal(1,loads);
    }
    [Theory][InlineData(BookingStatus.PendingApproval,false)][InlineData(BookingStatus.Approved,true)][InlineData(BookingStatus.Assigned,true)][InlineData(BookingStatus.Rejected,false)][InlineData(BookingStatus.Completed,false)][InlineData(BookingStatus.Cancelled,false)]
    public void ReportIncludesOnlyDispatchableCurrentStatuses(BookingStatus status,bool expected)=>Assert.Equal(expected,OperationalReports.InDispatchQueue(status));
}

public class PermissionExpiryTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    [Fact] public async Task EventPopulatedCacheRefreshesAfterTenMinutes()
    {
        var clock=new Clock();var cache=new PermissionCache(clock);var id=Guid.NewGuid();
        await cache.Handle(EventEnvelope.Create(EventTypes.UserRoleChanged,new UserRoleChanged(id,[Roles.Employee],1)));
        clock.Now=clock.Now.AddMinutes(11);
        var roles=await cache.RolesFor(id,()=>Task.FromResult(new[]{Roles.Manager}));
        Assert.Equal(new[]{Roles.Manager},roles);
    }
}
