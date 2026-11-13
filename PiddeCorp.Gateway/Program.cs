using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using PiddeCorp.EnterpriseLogging;
using PiddeCorp.Gateway.Auth;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;

// ---------------------------------------------------------------------------
// PiddeCorp.Gateway
//
// This is the ONLY component in the portal architecture that should ever be
// publicly reachable. Sessions are cookie-keyed and stored server-side
// (Redis, or process memory when Redis:Enabled=false). The session holds the
// identity provider's access token, which the Gateway forwards to modules.
//
// Okta is NOT wired up yet: IOktaClient is the seam, FakeOktaClient (Development
// only) is the stub, and there is deliberately no login endpoint outside
// Development until the real integration exists.
// ---------------------------------------------------------------------------

const string LocalDevCorsPolicy = "LocalDevOrigins";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddEnterpriseLogging(o => {
        o.ServiceName = "PiddeCorp.Gateway";
        o.ForwardToProviders = true; // so Aspire ServiceDefaults' OTel logging provider gets events
    }
);
builder.AddServiceDefaults();

string shellRedirectUrl = builder.Configuration["Gateway:ShellRedirectUrl"] ?? "http://localhost:5175";

builder.Services
       .AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
       .AddTransforms(transformBuilderContext => {
               transformBuilderContext.AddRequestTransform(async transformContext => {
                       // The browser's Gateway session cookie never reaches the module -
                       // it gets swapped here for the access token held in the session.
                       string? accessToken = await transformContext.HttpContext.GetTokenAsync("access_token");
                       transformContext.ProxyRequest.Headers.Remove("Authorization");
                       if(!string.IsNullOrEmpty(accessToken)) {
                           transformContext.ProxyRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                       }
                   }
               );
           }
       );

// Redis:Enabled defaults to true - unset it explicitly (Aspire's local/AVD profile
// does this) to fall back to process-memory session storage
// instead. That's a deliberate downgrade for environments that can't run a Redis
// container: no persistence across restarts, no sharing across instances. Never
// disable this for anything that isn't a single-instance local dev process.
bool redisEnabled = builder.Configuration.GetValue("Redis:Enabled", true);

if(redisEnabled) {
    // Explicit config key, explicit fallback - no magic connection-string discovery.
    string redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    IConnectionMultiplexer redisMultiplexer = ConnectionMultiplexer.Connect(redisConnectionString);

    builder.Services.AddSingleton(redisMultiplexer);
    builder.Services.AddSingleton<ITicketStore, RedisTicketStore>();
} else {
    builder.Services.AddSingleton<ITicketStore, InMemoryTicketStore>();
}

if(builder.Environment.IsDevelopment()) {
    builder.Services.AddSingleton<IOktaClient, FakeOktaClient>();
}

builder.Services
       .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
       .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => {
               options.Cookie.Name = "piddecorp_portal_session";
               options.Cookie.HttpOnly = true;
               options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
               // Lax is fine here: the browser only needs to SEND this cookie on
               // same-origin requests from the shell/module UIs.
               options.Cookie.SameSite = SameSiteMode.Lax;
               options.ExpireTimeSpan = TimeSpan.FromHours(1);
               options.SlidingExpiration = true;
               // options.SessionStore is deliberately NOT set here - it needs the
               // ITicketStore from DI, which isn't available yet at this
               // point in startup. Wired up just below instead.

               // Cookie auth defaults to challenging with a browser redirect to a
               // login page, which is wrong here - nothing on this Gateway is a page
               // a browser navigates to directly, it's all JSON APIs and a reverse
               // proxy, consumed by fetch() calls from the shell and modules.
               options.Events = new CookieAuthenticationEvents {
                   OnRedirectToLogin = context => {
                       context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                       return Task.CompletedTask;
                   },
                   OnRedirectToAccessDenied = context => {
                       context.Response.StatusCode = StatusCodes.Status403Forbidden;
                       return Task.CompletedTask;
                   }
               };
           }
       );

// This is what actually swaps "claims live inside the cookie" for "cookie holds a
// session key, claims live server-side" - see Auth/RedisTicketStore.cs and
// Auth/InMemoryTicketStore.cs, whichever got registered as ITicketStore above.
builder.Services
       .AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
       .Configure<ITicketStore>((options, store) => { options.SessionStore = store; });

// Named to match the "AuthorizationPolicy" referenced by proxied routes in
// appsettings.json - YARP applies it per-route when it maps the endpoints.
builder.Services.AddAuthorization(options => {
        options.AddPolicy("RequireGatewaySession", policy => policy.RequireAuthenticatedUser());
    }
);

// The Gateway is the only public origin in production, so this only matters
// for local dev: module UIs (standalone or federated) run on their own Vite
// origins and call the Gateway's proxied routes directly rather than through
// a dev-server proxy, so those requests are genuinely cross-origin and need
// the session cookie to ride along. Explicit origins (not AllowAnyOrigin) +
// AllowCredentials, since credentialed requests can't use a wildcard origin.
builder.Services.AddCors(options => {
        options.AddPolicy(LocalDevCorsPolicy, policy => {
                policy.WithOrigins(
                          "http://localhost:5173", // PiddeCorp.TaskManager.UI, standalone dev
                          "http://localhost:5174", // PiddeCorp.TaskManager.UI, as the "taskmanager" MF remote (build:watch + preview)
                          "http://localhost:5175", // PiddeCorp.Portal.Shell
                          "http://localhost:5176"  // PiddeCorp.Module2.UI
                      )
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        );
    }
);

WebApplication app = builder.Build();

app.UseEnterpriseLogging();
app.MapDefaultEndpoints();

// Before auth/authorization: CORS has to run early enough to handle
// preflight OPTIONS requests before anything else in the pipeline would
// otherwise reject them.
app.UseCors(LocalDevCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

// The only place that actually establishes a Gateway session. The access token
// from the identity provider rides along in the ticket's AuthenticationProperties,
// so it ends up in the server-side session store (Redis), never in the cookie.
async Task EstablishGatewaySessionAsync(HttpContext ctx, OktaLoginResult login) {
    List<Claim> sessionClaims = [new(ClaimTypes.NameIdentifier, login.UserId), new(ClaimTypes.Name, login.Name)];
    ClaimsIdentity sessionIdentity = new(sessionClaims, CookieAuthenticationDefaults.AuthenticationScheme);
    ClaimsPrincipal sessionPrincipal = new(sessionIdentity);

    AuthenticationProperties properties = new() { ExpiresUtc = login.ExpiresUtc };
    properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = login.AccessToken }]);

    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, sessionPrincipal, properties);
}

app.MapGet("/auth/me", (HttpContext ctx) => {
        if(ctx.User.Identity?.IsAuthenticated != true) {
            return Results.Json(new { authenticated = false });
        }
        
        return Results.Json(new {
                authenticated = true,
                userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier),
                name = ctx.User.FindFirstValue(ClaimTypes.Name),
            }
        );
    }
);

app.MapPost("/auth/logout", async (HttpContext ctx) => {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new { loggedOut = true });
    }
);

if(app.Environment.IsDevelopment()) {
    app.Logger.LogWarning(
        "Development-only endpoint registered: GET /auth/login - signs in via the FAKE Okta " +
        "stub without validating anything. Do not enable this outside Development."
    );

    // SECURITY: a real authentication bypass. Signs the caller in as whatever `user`
    // they pass on the query string. The only gate is IsDevelopment(). Replace with
    // the real Okta flow before this runs anywhere else; do not widen the gate.
    app.MapGet("/auth/login", async (HttpContext ctx, IOktaClient okta, string? user, string? returnTo) => {
            OktaLoginResult login = await okta.LoginAsync(user ?? "dev-user");

            app.Logger.LogWarning("/auth/login hit - fake Okta session for {UserId}", login.UserId);

            await EstablishGatewaySessionAsync(ctx, login);

            // Relative-only, never an arbitrary URL.
            bool returnToIsSafeRelativePath = returnTo is not null
                                               && returnTo.StartsWith('/')
                                               && !returnTo.StartsWith("//")
                                               && !returnTo.Contains("://");

            return Results.Redirect(returnToIsSafeRelativePath ? returnTo! : shellRedirectUrl);
        }
    );
}

// Everything else - i.e. every route configured under ReverseProxy:Routes -
// is proxied through to its module BFF. None of it lives under /auth, so
// there's no overlap with the endpoints mapped above.
app.MapReverseProxy();

app.Run();
