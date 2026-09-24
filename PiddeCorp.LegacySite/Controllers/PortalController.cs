using Microsoft.AspNetCore.Mvc;
using PiddeCorp.LegacySite.Auth;

namespace PiddeCorp.LegacySite.Controllers;

public class PortalController(RelayTokenIssuer issuer, IConfiguration configuration) : Controller {
    // Default matches Program.cs's applicationUrl in the Gateway project's
    // launchSettings.json for the containerized/k8s topology (see build.bat's
    // `-p 8082:8080` mapping). Overridden via Gateway:ExternalLoginUrl when Gateway
    // isn't reachable there - e.g. the Aspire AppHost points this at Gateway's
    // actual local dev endpoint instead, since nothing is listening on 8082 there.
    private const string DefaultGatewayExternalLoginUrl = "http://localhost:8082/auth/external-login";

    private string GatewayExternalLoginUrl => configuration["Gateway:ExternalLoginUrl"] ?? DefaultGatewayExternalLoginUrl;

    // The "tile" page - only reachable once "logged in" to fake LegacySite.
    public IActionResult Tile() {
        string? username = HttpContext.Session.GetString("LegacyUser");
        if(string.IsNullOrEmpty(username)) {
            return RedirectToAction("Login", "Account");
        }
        
        ViewBag.Username = username;
        return View();
    }
    
    // Clicking the tile hits this. It signs a fresh relay token server-side,
    // then hands the browser a page that auto-submits a real HTML form POST
    // to the Gateway - a top-level navigation, not a fetch/XHR, so there's no
    // CORS to configure. See ADR-003, decision 3.
    [HttpPost]
    public IActionResult LaunchPortal() {
        string? username = HttpContext.Session.GetString("LegacyUser");
        if(string.IsNullOrEmpty(username)) {
            return RedirectToAction("Login", "Account");
        }
        
        string token = issuer.IssueRelayToken(userId: username, name: username);
        
        ViewBag.GatewayUrl = GatewayExternalLoginUrl;
        ViewBag.Token = token;
        
        /*
         * Intentionally serve a self-submitting form to the browser. The POST must happen from the browser.
         * If we POSTed here from the server, the Set-Cookie response would be returned here to the server.
         * Set-Cookie needs to be handled in the browser.
         */
        return View("RelayHandoff");
    }
}
