using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PiddeCorp.Gateway.Auth;

// Same server-side-session shape as RedisTicketStore, backed by process memory instead
// of Redis - for local dev environments (Aspire on AVD hosts) that can't run containers.
// Sessions don't survive a restart and aren't shared across instances, which is fine for
// a single local dev process but is NOT a substitute for RedisTicketStore anywhere real
// multi-instance or persistence-across-restarts matters.
public class InMemoryTicketStore : ITicketStore {
    private readonly ConcurrentDictionary<string, (AuthenticationTicket Ticket, DateTimeOffset ExpiresUtc)> _sessions = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket) {
        string key = Guid.NewGuid().ToString("N");
        return RenewAsync(key, ticket).ContinueWith(_ => key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) {
        PruneExpired();

        DateTimeOffset expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(8);
        _sessions[key] = (ticket, expiresUtc);

        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) {
        if(_sessions.TryGetValue(key, out (AuthenticationTicket Ticket, DateTimeOffset ExpiresUtc) entry)
           && entry.ExpiresUtc > DateTimeOffset.UtcNow) {
            return Task.FromResult<AuthenticationTicket?>(entry.Ticket);
        }

        return Task.FromResult<AuthenticationTicket?>(null);
    }

    public Task RemoveAsync(string key) {
        _sessions.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private void PruneExpired() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach(KeyValuePair<string, (AuthenticationTicket Ticket, DateTimeOffset ExpiresUtc)> entry in _sessions) {
            if(entry.Value.ExpiresUtc <= now) {
                _sessions.TryRemove(entry.Key, out _);
            }
        }
    }
}
