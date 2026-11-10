using System.Collections.Concurrent;
using Shared.Contracts;
using Shared.Infrastructure;
namespace Booking.Api;
public sealed class PermissionCache(TimeProvider clock) : IEventHandler
{
    private sealed record Entry(string[] Roles,long Version,DateTimeOffset Expires);
    private readonly ConcurrentDictionary<Guid,Entry> entries=new();
    private readonly ConcurrentDictionary<Guid,SemaphoreSlim> locks=new();
    public async Task<string[]> RolesFor(Guid id,Func<Task<string[]>> load)
    {
        if(entries.TryGetValue(id,out var cached)&&cached.Expires>clock.GetUtcNow()) return cached.Roles;
        var gate=locks.GetOrAdd(id,_=>new SemaphoreSlim(1,1));
        await gate.WaitAsync();
        try
        {
            if(entries.TryGetValue(id,out cached)&&cached.Expires>clock.GetUtcNow()) return cached.Roles;
            var observed=cached;
            var roles=await load();
            entries.AddOrUpdate(id,_=>new(roles,-1,clock.GetUtcNow().AddMinutes(10)),(_,existing)=>ReferenceEquals(existing,observed)?new(roles,existing.Version,clock.GetUtcNow().AddMinutes(10)):existing);
            return entries[id].Roles;
        }
        finally { gate.Release(); }
    }
    public Task Handle(EventEnvelope envelope)
    {
        if(envelope.Type!=EventTypes.UserRoleChanged) throw new System.Text.Json.JsonException("Unsupported permission event");
        var change=envelope.Read<UserRoleChanged>();
        entries.AddOrUpdate(change.UserId,_=>new(change.Roles,change.Version,clock.GetUtcNow().AddMinutes(10)),(_,old)=>change.Version>old.Version?new(change.Roles,change.Version,clock.GetUtcNow().AddMinutes(10)):old);
        return Task.CompletedTask;
    }
}
