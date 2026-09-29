// ---------------------------------------------------------------------------
// PiddeCorp.AppHost
//
// Local, container-free orchestration for the whole pilot - one `dotnet run`
// starts every service and UI together, which is what the dev team needs on
// an Azure AVD host pool where Docker/Podman isn't available. This is
// deliberately NOT the same topology as the k8s deployment:
//   - No Redis resource: Gateway falls back to a process-memory session
//     store and replay guard (Redis:Enabled=false - see Auth/InMemoryTicketStore.cs
//     and Auth/InMemoryReplayGuard.cs). Sessions won't survive a restart and
//     aren't shared across instances, which is fine for one local dev process.
//   - Every port below is pinned to match the values already hardcoded
//     throughout this repo (CORS allow-lists, YARP proxy destinations in
//     appsettings.Development.json, Module Federation remote URLs in the
//     UI projects' vite.config files) rather than left to Aspire's default
//     dynamic port assignment - changing any of them here means updating
//     those other places too.
// ---------------------------------------------------------------------------

IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ProjectResource> gateway = builder
    .AddProject<Projects.PiddeCorp_Gateway>("gateway", launchProfileName: "PiddeCorp.Gateway")
    // No Redis resource in this graph - see the file banner above.
    .WithEnvironment("Redis__Enabled", "false");

IResourceBuilder<ProjectResource> legacySite = builder
    .AddProject<Projects.PiddeCorp_LegacySite>("legacysite", launchProfileName: "https")
    // LegacySite's login-tile POST is a top-level browser form submission straight to
    // Gateway (see PortalController.cs), not a server-to-server call - it has to target
    // wherever Gateway is actually listening. The container/k8s default (port 8082) has
    // nothing running on it here, so point it at Gateway's "PiddeCorp.Gateway" launch
    // profile instead. Must be the https endpoint, not http: Gateway's session cookie is
    // CookieSecurePolicy.Always, so a browser silently discards it (login "succeeds" but
    // no session sticks) if this response comes back over plain HTTP - confirmed by
    // testing both against a running instance.
    .WithEnvironment("Gateway__ExternalLoginUrl", "https://localhost:7020/auth/external-login")
    .WaitFor(gateway);

// Pinned to the "http" profile specifically - Gateway's appsettings.Development.json
// proxies to these two APIs' http ports (5000, 5012), not their https ports.
IResourceBuilder<ProjectResource> taskManagerApi = builder
    .AddProject<Projects.PiddeCorp_TaskManager_API>("taskmanager-api", launchProfileName: "http");

IResourceBuilder<ProjectResource> module2Api = builder
    .AddProject<Projects.PiddeCorp_Module2_API>("module2-api", launchProfileName: "http");

// The three Vite UIs bind their own fixed ports directly (see each project's
// vite.config.*), so these are declared with isProxied:false rather than letting
// Aspire pick a port and reverse-proxy to it.
builder.AddNpmApp("portal-shell", "../PiddeCorp.Portal.Shell", "dev")
       .WithHttpEndpoint(port: 5175, isProxied: false)
       .WithExternalHttpEndpoints()
       // vite.config.ts's /auth proxy defaults to Gateway's container port (8082),
       // which nothing listens on here - this is a Node-to-Node hop (not
       // browser-facing), so the plain http endpoint avoids needing to deal with
       // the dev cert's trust at all.
       .WithEnvironment("GATEWAY_URL", "http://localhost:7021")
       // app.tsx's "Launch the portal from LegacySite" link defaults to LegacySite's
       // container port (8083), which nothing listens on here - this one IS
       // browser-facing (a plain <a href>), so point it at LegacySite's actual
       // endpoint via a live reference rather than a second hardcoded port.
       .WithEnvironment("VITE_LEGACY_SITE_URL", legacySite.GetEndpoint("https"))
       .WaitFor(gateway);

// TaskManager.UI and Module2.UI are Module Federation REMOTES, not the host.
// Module Federation for Vite only gives the HOST a live dev server with HMR -
// there's no way to run a remote's own `vite dev` and have the shell pick up
// live changes (see each remote's own README). So these run as a one-shot
// build, then a preview server for the built output - not "dev".
IResourceBuilder<NodeAppResource> taskManagerUiBuild = builder
    .AddNpmApp("taskmanager-ui-build", "../PiddeCorp.TaskManager.UI", "build:remote");

builder.AddNpmApp("taskmanager-ui", "../PiddeCorp.TaskManager.UI", "preview")
       .WithHttpEndpoint(port: 5174, isProxied: false)
       .WaitForCompletion(taskManagerUiBuild)
       .WaitFor(taskManagerApi);

IResourceBuilder<NodeAppResource> module2UiBuild = builder
    .AddNpmApp("module2-ui-build", "../PiddeCorp.Module2.UI", "build:remote");

builder.AddNpmApp("module2-ui", "../PiddeCorp.Module2.UI", "preview")
       .WithHttpEndpoint(port: 5176, isProxied: false)
       .WaitForCompletion(module2UiBuild)
       .WaitFor(module2Api);

builder.Build().Run();
