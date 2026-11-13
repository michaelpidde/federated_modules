namespace PiddeCorp.Gateway.Auth;

public record OktaLoginResult(string UserId, string Name, string AccessToken, DateTimeOffset ExpiresUtc);

// Seam for the real Okta integration. Nothing here talks to Okta yet - FakeOktaClient
// is the only implementation. When real Okta lands (OIDC code flow, token exchange,
// refresh), replace the implementation, not the callers: the Gateway stores whatever
// access token comes back in the Redis-backed session and forwards it to modules.
public interface IOktaClient {
    Task<OktaLoginResult> LoginAsync(string userHint);
}
