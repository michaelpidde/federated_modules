using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using PiddeCorp.EnterpriseLogging;
using PiddeCorp.Gateway.Auth;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;

// ---------------------------------------------------------------------------
// PiddeCorp.Gateway
//
// This is the ONLY component in the portal architecture that should ever be
// publicly reachable. It does two jobs for this pilot:
//   1. Accept a one-time relay token POSTed from PiddeCorp.LegacySite,
//      validate it, and immediately throw it away.
//   2. Mint its own session (cookie) once that validation succeeds. Nothing
//      downstream ever sees the original relay token - see ADR-003.
// ---------------------------------------------------------------------------

const string LegacySiteIssuer = "legacysite";
const string GatewayAudience = "piddecorp-gateway";
const string PublicKeyRelativePath = "keys/PiddeCorp.LegacySite.Public.pem";
const string PrivateKeyRelativePath = "keys/PiddeCorp.Gateway.Private.pem";
const string LocalDevCorsPolicy = "LocalDevOrigins";

TimeSpan replayGuardTtl = TimeSpan.FromMinutes(5);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddEnterpriseLogging(o => {
        o.ServiceName = "PiddeCorp.Gateway";
        o.ForwardToProviders = true; // so Aspire ServiceDefaults' OTel logging provider gets events
    }
);
builder.AddServiceDefaults();

RSA rsa = RSA.Create();
rsa.ImportFromPem(File.ReadAllText(Path.Combine(builder.Environment.ContentRootPath, PublicKeyRelativePath)));
RsaSecurityKey legacySiteSigningKey = new(rsa);

RSA gatewayRsa = RSA.Create();
gatewayRsa.ImportFromPem(File.ReadAllText(Path.Combine(builder.Environment.ContentRootPath, PrivateKeyRelativePath)));
RsaSecurityKey gatewaySigningKey = new(gatewayRsa);
InternalTokenIssuer internalTokenIssuer = new(gatewaySigningKey);

string shellRedirectUrl = builder.Configuration["Gateway:ShellRedirectUrl"] ?? "http://localhost:5175";

builder.Services
       .AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
       .AddTransforms(transformBuilderContext => {
               // Metadata:InternalAudience is required on every route (enforced by
               // the startup check below), so this is safe to read as non-null here.
               string internalAudience = transformBuilderContext.Route.Metadata!["InternalAudience"];
               
               transformBuilderContext.AddRequestTransform(transformContext => {
                       ClaimsPrincipal user = transformContext.HttpContext.User;
                       string userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                       string name = user.FindFirstValue(ClaimTypes.Name) ?? userId;
                       
                       // The browser's Gateway session cookie never reaches the module -
                       // it gets swapped here for a short-lived, module-scoped token so
                       // each module only ever sees a credential it can validate itself.
                       string internalToken = internalTokenIssuer.IssueToken(userId, name, internalAudience);
                       transformContext.ProxyRequest.Headers.Remove("Authorization");
                       transformContext.ProxyRequest.Headers.Add("Authorization", $"Bearer {internalToken}");
                       
                       return ValueTask.CompletedTask;
                   }
               );
           }
       );

// Fail fast rather than at first-request time: InternalTokenIssuer needs
// InternalAudience for every route to know which module a token is minted
// for, so a route missing it is a config bug, not a runtime one.
List<string> routesMissingInternalAudience = builder.Configuration
                                                    .GetSection("ReverseProxy:Routes")
                                                    .GetChildren()
                                                    .Where(route => string.IsNullOrWhiteSpace(
                                                               route["Metadata:InternalAudience"]
                                                           )
                                                    )
                                                    .Select(route => route.Key)
                                                    .ToList();

if(routesMissingInternalAudience.Count > 0) {
    throw new InvalidOperationException(
        $"ReverseProxy route(s) missing required Metadata:InternalAudience: {string.Join(", ", routesMissingInternalAudience)}"
    );
}

// Redis:Enabled defaults to true - unset it explicitly (Aspire's local/AVD profile
// does this) to fall back to process-memory session storage and replay guarding
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
    builder.Services.AddSingleton<IReplayGuard, RedisReplayGuard>();
} else {
    builder.Services.AddSingleton<ITicketStore, InMemoryTicketStore>();
    builder.Services.AddSingleton<IReplayGuard, InMemoryReplayGuard>();
}

builder.Services
       .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
       .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => {
               options.Cookie.Name = "piddecorp_portal_session";
               options.Cookie.HttpOnly = true;
               options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
               // Lax is fine here: the browser only ever needs to SEND this cookie on
               // same-origin requests from the shell/module UIs. It does NOT need to
               // be sent cross-site, because LegacySite never sees this cookie at all -
               // it only ever POSTs a one-time token TO us, it never reads our cookie.
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

// Shared by /auth/external-login and (in Development only) /dev/login -
// this is the only place that actually mints a Gateway session.
async Task EstablishGatewaySessionAsync(HttpContext ctx, string userId, string name) {
    List<Claim> sessionClaims = [new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Name, name)];
    ClaimsIdentity sessionIdentity = new(sessionClaims, CookieAuthenticationDefaults.AuthenticationScheme);
    ClaimsPrincipal sessionPrincipal = new(sessionIdentity);

    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, sessionPrincipal);
}

app.MapPost("/auth/external-login", async (HttpContext ctx, IReplayGuard replayGuard) => {
        IFormCollection form = await ctx.Request.ReadFormAsync();
        string token = form["token"].ToString();
        
        if(string.IsNullOrWhiteSpace(token)) {
            app.Logger.LogWarning("external-login called with no token in the request body");
            return Results.BadRequest("Missing token.");
        }
        
        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal relayPrincipal;
        
        try {
            relayPrincipal = handler.ValidateToken(token, new TokenValidationParameters {
                    ValidateIssuer = true,
                    ValidIssuer = LegacySiteIssuer,
                    ValidateAudience = true,
                    ValidAudience = GatewayAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = legacySiteSigningKey,
                    // Deliberately tight: this token should be seconds old, not minutes.
                    ClockSkew = TimeSpan.FromSeconds(5),
                }, out _
            );
        } catch(SecurityTokenException ex) {
            app.Logger.LogWarning(ex, "Relay token failed validation");
            return Results.Unauthorized();
        }
        
        string? jti = relayPrincipal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if(string.IsNullOrEmpty(jti)) {
            app.Logger.LogWarning("Relay token had no jti claim - rejecting, replay protection needs it");
            return Results.Unauthorized();
        }
        
        bool wasFirstUse = await replayGuard.TryConsumeAsync(jti, replayGuardTtl);

        if(!wasFirstUse) {
            app.Logger.LogWarning("Relay token jti {Jti} was already consumed - rejecting possible replay", jti);
            return Results.Unauthorized();
        }
        
        // The relay token's job ends here. We never store it, forward it, or hand
        // it back to the browser - only the claims we care about survive past this point.
        string userId = relayPrincipal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "unknown";
        string name = relayPrincipal.FindFirstValue("name") ?? userId;

        await EstablishGatewaySessionAsync(ctx, userId, name);

        app.Logger.LogInformation("Gateway session established for {UserId} via LegacySite relay", userId);
        
        return Results.Redirect(shellRedirectUrl);
    }
);

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
        "Development-only endpoint registered: GET /dev/login - establishes a Gateway " +
        "session without validating anything. Do not enable this outside Development."
    );

    // SECURITY: this endpoint is a real authentication bypass, not a simulated one.
    // It skips JWT/relay-token validation entirely and signs the caller in as
    // whatever `user` they pass on the query string. The only thing standing
    // between this and a production auth bypass is the IsDevelopment() check
    // above - the same guarantee that already protects things like detailed
    // error pages and the local HTTPS dev cert: that ASPNETCORE_ENVIRONMENT is
    // never set to Development anywhere reachable outside a dev machine. Do not
    // relax this gate to something broader "for convenience" later.
    app.MapGet("/dev/login", async (HttpContext ctx, string? user, string? returnTo) => {
            string userId = user ?? "dev-user";

            app.Logger.LogWarning("/dev/login hit - establishing Gateway session for {UserId} with no auth check", userId);

            await EstablishGatewaySessionAsync(ctx, userId, userId);

            // Relative-only, never an arbitrary URL - this endpoint is dev-only, but an
            // open redirect target is still a bad habit not worth forming even here.
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
