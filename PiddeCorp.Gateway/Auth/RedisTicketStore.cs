using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using StackExchange.Redis;

namespace PiddeCorp.Gateway.Auth;

// Without this, ASP.NET Core's cookie auth serializes the whole claims
// ticket INTO the cookie itself (encrypted, but still round-tripped to the
// browser on every request). This swaps that for the real BFF pattern: the
// cookie holds only an opaque session key, and the actual session data lives
// server-side in Redis - which is also what makes "kill this session right
// now" and "see who's logged in" both trivial operations later, instead of
// requiring the cookie itself to be invalidated some other way.
public class RedisTicketStore(IConnectionMultiplexer redis) : ITicketStore {
    private const string KeyPrefix = "portal:session:";
    private static readonly TicketSerializer Serializer = TicketSerializer.Default;
    
    public async Task<string> StoreAsync(AuthenticationTicket ticket) {
        string key = Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }
    
    public async Task RenewAsync(string key, AuthenticationTicket ticket) {
        IDatabase db = redis.GetDatabase();
        byte[] bytes = Serializer.Serialize(ticket);
        
        DateTimeOffset expiresUtc = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(8);
        TimeSpan ttl = expiresUtc - DateTimeOffset.UtcNow;
        if(ttl <= TimeSpan.Zero) {
            ttl = TimeSpan.FromMinutes(1);
        }
        
        await db.StringSetAsync(KeyPrefix + key, bytes, ttl);
    }
    
    public async Task<AuthenticationTicket?> RetrieveAsync(string key) {
        IDatabase db = redis.GetDatabase();
        RedisValue bytes = await db.StringGetAsync(KeyPrefix + key);
        
        return bytes.IsNullOrEmpty ? null : Serializer.Deserialize(bytes!);
    }
    
    public async Task RemoveAsync(string key) {
        IDatabase db = redis.GetDatabase();
        await db.KeyDeleteAsync(KeyPrefix + key);
    }
}
