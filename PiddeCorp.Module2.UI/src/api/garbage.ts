// The Gateway is the only public origin in this architecture (see
// PiddeCorp.Gateway) - this app never talks to Module2.API directly.
// /module2 is the path prefix PiddeCorp.Gateway's reverse-proxy route
// matches on and strips before forwarding to this API's own /api routes
// (see PiddeCorp.Gateway/appsettings.json's "module2-api" route).
//
// Deliberately relative, not an absolute Gateway URL: in production the
// Gateway is the single origin serving everything, so a relative path just
// works. In standalone dev, this app's own vite.config.ts proxies
// /module2/* to the Gateway's local dev port, making it same-origin
// there too - matching the session cookie's scope (see README.md's
// "Running standalone" section). An absolute cross-origin URL here would
// skip that proxy and fall back to CORS, which won't see the same-origin
// session cookie established via /dev/login.
export const API_BASE = '/module2/api';

// PiddeCorp.Module2.API's GET /api/garbage returns a bare JSON string (see
// Program.cs: `Results.Json(garbage)` where `garbage` is a plain C# string),
// not an array or object.
export async function getGarbage(): Promise<string> {
    // credentials: 'include' - this route requires a Gateway session
    // (RequireGatewaySession), and the cookie won't ride along on a
    // cross-origin request without it.
    const res = await fetch(`${API_BASE}/garbage`, { credentials: 'include' });
    if (!res.ok) throw new Error('Failed to fetch garbage');
    return res.json();
}
