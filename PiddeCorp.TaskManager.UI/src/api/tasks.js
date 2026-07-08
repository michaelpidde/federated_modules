// The Gateway is the only public origin in this architecture (see
// PiddeCorp.Gateway) - this app never talks to TaskManager.API directly.
// /taskmanager is the path prefix PiddeCorp.Gateway's reverse-proxy route
// matches on and strips before forwarding to this API's own /api routes
// (see PiddeCorp.Gateway/appsettings.json's "taskmanager-api" route).
//
// Deliberately relative, not an absolute Gateway URL: in production the
// Gateway is the single origin serving everything, so a relative path just
// works. In standalone dev, this app's own vite.config.js proxies
// /taskmanager/* to the Gateway's local dev port, making it same-origin
// there too - matching the session cookie's scope (see README.md's
// "Running standalone" section). An absolute cross-origin URL here would
// skip that proxy and fall back to CORS, which won't see the same-origin
// session cookie established via /dev/login.
export const API_BASE = '/taskmanager/api';

export async function getOpenTasks() {
    // credentials: 'include' - this route requires a Gateway session
    // (RequireGatewaySession), and the cookie won't ride along on a
    // cross-origin request without it.
    const res = await fetch(`${API_BASE}/tasks`, { credentials: 'include' });
    if (!res.ok) throw new Error('Failed to fetch tasks');
    return res.json();
}