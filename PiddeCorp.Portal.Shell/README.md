# PiddeCorp.Portal.Shell

The host (shell) application for the micro-frontend portal. React + Vite +
TypeScript, using Module Federation (`@module-federation/vite`) to load
module UIs - starting with PiddeCorp.TaskManager.UI - as independently
built/deployed remotes.

This is a pilot proving out the shell/module split, not a production build.
Config favors explicit, hardcoded local-dev values over configurability, to
match the rest of this codebase (PiddeCorp.Gateway).

## Running the dev server

```bash
npm install
npm run dev
```

The shell serves on **http://localhost:5175**. It expects:

- **PiddeCorp.Gateway** reachable at `http://localhost:8082` - the shell's
  Vite dev server proxies `/auth/*` to it (see "Dev proxy vs. CORS" below).
  That's the Gateway's container port (see `build.bat`'s
  `podman run ... -p 8082:8080 ... gateway`), not the
  `https://localhost:7020` port `dotnet run`/launchSettings.json uses when
  running the Gateway directly outside a container.
- **PiddeCorp.TaskManager.UI** built and served as the `taskmanager` remote
  on `http://localhost:5174` (see its own README for the two-terminal
  `build:watch` + `preview` workflow - Module Federation for Vite only gives
  the *host* live HMR, not the remote).

There is no login UI here. If `/auth/me` reports `authenticated: false`,
the shell navigates to the Gateway's `/auth/login` (a Development-only
fake-Okta stub until real Okta is integrated), which creates the session and
redirects back. If that doesn't yield a session, the shell stops after one
attempt and shows a retry link rather than looping.

`npm run typecheck` runs `tsc --noEmit` if you want to check types without
starting the dev server.

## Routing: descendant `<Routes>`, not a `base` prop

`src/main.tsx` wraps the app root in one real `<BrowserRouter>` (the shell
owns the root namespace). `src/app.tsx` then does the handoff:

```tsx
<Routes>
  <Route path="/taskmanager/*" element={<TaskManagerModule />} />
  <Route path="/module2/*" element={<Module2Module />} />
  <Route path="*" element={<p>Select a module.</p>} />
</Routes>
```

The trailing `/*` on each splat route matches both the bare prefix
(`/taskmanager`) and everything nested under it (`/taskmanager/tasks`, and
so on). TaskManager.UI's own `App` (see its `src/app.tsx`) renders a bare
`<Routes>` with **no `<BrowserRouter>` of its own** - it's a true descendant
of this shell's `<BrowserRouter>`, reading this router's ambient context via
`useContext` rather than owning a separate Router instance. That's
react-router's built-in "descendant routes" pattern, and it's what lets the
remote's own `/` and `/tasks` route definitions resolve correctly relative
to `/taskmanager` with no `base`/prefix plumbing anywhere - no equivalent of
a nested-Router-with-base dance is needed.

This was verified against a real, running shell + remote pair (not just
reasoned about) - by loading `/taskmanager`, `/taskmanager/tasks`,
`/module2`, and `/module2/garbage-pit` through the shell and confirming each
remote resolves the right page with no console errors.

## `shared` must use the object form with `singleton: true`

```ts
// vite.config.ts
shared: {
  react: { singleton: true },
  'react-dom': { singleton: true },
  'react-router-dom': { singleton: true },
},
```

The shorthand array form (`shared: ['react', 'react-dom', 'react-router-dom']`)
looks equivalent but isn't - it leaves `singleton` at its default of `false`,
which would let the runtime load a second, separate copy of `react-dom`
(breaking rendering outright) and, specific to this app's routing, a second
copy of `react-router-dom`. Because the remote's `<Routes>` is a genuine
**descendant** of this shell's `<BrowserRouter>` (see "Routing" above), it
reads this shell's router context via `useContext` - if `react-router-dom`
weren't a true singleton, that `useContext` call would be looking at a
*different* `RouterContext` object than the one this shell's copy provides,
and would fail the same class of "hooks/context break in confusing ways"
failure this repo hit once already with a duplicated `preact/hooks` copy
(`Cannot read properties of undefined (reading 'context')`). The object form
with `singleton: true` on all three is required, not just tidier. Must match
`PiddeCorp.TaskManager.UI`'s and `PiddeCorp.Module2.UI`'s `vite.config.ts`
`shared` blocks exactly.

## Dev proxy vs. CORS - why proxy, and don't change this to CORS

In production, PiddeCorp.Gateway is meant to be the **only** public origin.
Everything else - including module UIs - is reached through it via
path-based reverse-proxy routes (e.g. `/taskmanager/*`). The browser only
ever talks to the Gateway's origin.

Locally, the shell's Vite dev server (`:5175`) and the Gateway (`:8082`) run
on different ports, which makes calls from the shell to `/auth/*`
cross-origin *only in dev* - production never has this problem, because
there's only one origin.

The fix here is **`server.proxy` in `vite.config.ts`**, not CORS on the
Gateway:

```ts
proxy: {
  '/auth': {
    target: 'http://localhost:8082',
    changeOrigin: true,
  },
}
```

This makes the browser see `/auth/*` as same-origin in dev, the same way it
will be in production. Enabling CORS on the Gateway instead would make dev
*work*, but it would work for a different reason than production does -
it'd paper over a topology difference instead of reproducing it, and nothing
would ever force you to notice if some other same-origin assumption broke.

**Do not "simplify" this back to CORS.** If a future module needs its own
API routed through the Gateway locally (e.g. `/taskmanager/*`), add another
entry to this same `proxy` block instead.
