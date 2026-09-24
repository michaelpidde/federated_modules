namespace PiddeCorp.Gateway.Auth;

// One-time-use check for relay token jtis: TryConsumeAsync must behave like an atomic
// "set if not exists" - the first caller for a given key gets true, every subsequent
// caller (including concurrent ones) gets false until the key expires.
public interface IReplayGuard {
    Task<bool> TryConsumeAsync(string key, TimeSpan ttl);
}
