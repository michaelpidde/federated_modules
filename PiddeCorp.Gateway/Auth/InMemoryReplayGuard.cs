using System.Collections.Concurrent;

namespace PiddeCorp.Gateway.Auth;

// Process-memory stand-in for RedisReplayGuard, for single-instance local dev only.
// ConcurrentDictionary.TryAdd is itself atomic, which is what gives this the same
// "first caller wins" guarantee Redis's SET NX gives - it just doesn't survive a
// restart or hold across multiple instances the way Redis does.
public class InMemoryReplayGuard : IReplayGuard {
    private readonly ConcurrentDictionary<string, DateTimeOffset> _consumed = new();

    public Task<bool> TryConsumeAsync(string key, TimeSpan ttl) {
        PruneExpired();

        bool firstUse = _consumed.TryAdd(key, DateTimeOffset.UtcNow + ttl);
        return Task.FromResult(firstUse);
    }

    private void PruneExpired() {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach(KeyValuePair<string, DateTimeOffset> entry in _consumed) {
            if(entry.Value <= now) {
                _consumed.TryRemove(entry.Key, out _);
            }
        }
    }
}
