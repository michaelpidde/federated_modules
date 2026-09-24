using StackExchange.Redis;

namespace PiddeCorp.Gateway.Auth;

public class RedisReplayGuard(IConnectionMultiplexer redis) : IReplayGuard {
    private const string KeyPrefix = "gateway:relay-jti:";

    public async Task<bool> TryConsumeAsync(string key, TimeSpan ttl) {
        // SET key value EX ttl NX - atomic check-and-set in a single round trip, and
        // Redis expires the key on its own, so there's no cleanup job needed either.
        IDatabase db = redis.GetDatabase();
        return await db.StringSetAsync(KeyPrefix + key, value: "1", expiry: ttl, when: When.NotExists);
    }
}
