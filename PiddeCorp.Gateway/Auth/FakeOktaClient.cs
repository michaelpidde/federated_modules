namespace PiddeCorp.Gateway.Auth;

// STUB. Pretends to be Okta: no network calls, no validation, hands back an opaque
// random "access token" for whoever is asked for. Registered only in Development
// (see Program.cs) - it is effectively an authentication bypass.
public class FakeOktaClient : IOktaClient {
    public Task<OktaLoginResult> LoginAsync(string userHint) {
        OktaLoginResult result = new(
            UserId: userHint,
            Name: userHint,
            AccessToken: $"fake-okta-{Guid.NewGuid():N}",
            ExpiresUtc: DateTimeOffset.UtcNow.AddHours(1)
        );

        return Task.FromResult(result);
    }
}
