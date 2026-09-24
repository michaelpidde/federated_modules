using PiddeCorp.EnterpriseLogging;
using PiddeCorp.LegacySite.Auth;

// ---------------------------------------------------------------------------
// PiddeCorp.LegacySite
//
// A deliberately contrived stand-in for a legacy site. Its only
// job here is to give you something that (a) has its own throwaway login,
// and (b) signs a one-time relay token server-side when the "portal tile"
// is clicked, then POSTs it to the Gateway - see ADR-003.
//
// The username/password check is NOT real. Any non-empty username "succeeds".
// That's intentional - the point of this pilot is the relay mechanism, not
// implementing actual login mechanisms against a database.
// ---------------------------------------------------------------------------

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddEnterpriseLogging(o => {
        o.ServiceName = "PiddeCorp.LegacySite";
        o.ForwardToProviders = true; // so Aspire ServiceDefaults' OTel logging provider gets events
    }
);
builder.AddServiceDefaults();

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
        options.Cookie.Name = "legacysite_fake_session";
        options.IdleTimeout = TimeSpan.FromMinutes(30);
    }
);
builder.Services.AddSingleton<RelayTokenIssuer>();

WebApplication app = builder.Build();

app.UseEnterpriseLogging();
app.MapDefaultEndpoints();

app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}"
);

app.Run();
