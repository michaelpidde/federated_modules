using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PiddeCorp.Gateway.Auth;

// TEMPORARY HOME: this lives in the Gateway only for the POC. Ultimately it will live in
// a dedicated Authentication API, with a provided SDK that exposes the extension method
// (AddFakeOktaBearer today, the real Okta validation setup later) so each module API
// just calls it - instead of module APIs project-referencing the Gateway as they do now.
//
// POC STUB, Development only. Accepts any "Authorization: Bearer fake-okta-..." token
// that the Gateway's FakeOktaClient hands out - no signature, no expiry, no validation.
// Stands in for real Okta JWT validation until that integration exists.
public static class FakeOktaBearerExtensions {
    public const string SchemeName = "FakeOktaBearer";

    public static AuthenticationBuilder AddFakeOktaBearer(this AuthenticationBuilder builder) =>
        builder.AddScheme<AuthenticationSchemeOptions, FakeOktaBearerHandler>(SchemeName, _ => { });
}

public class FakeOktaBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    private const string Prefix = "Bearer fake-okta-";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        string header = Request.Headers.Authorization.ToString();
        if(!header.StartsWith(Prefix, StringComparison.Ordinal)) {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, "fake-okta-user")], FakeOktaBearerExtensions.SchemeName);
        AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), FakeOktaBearerExtensions.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
