# PiddeCorp.Portal.Shell

The host (shell) application for the micro-frontend portal. Preact + Vite +
TypeScript, using Module Federation (`@module-federation/vite`) to load
module UIs - starting with PiddeCorp.TaskManager.UI - as independently
built/deployed remotes.

This is a pilot proving out the shell/module split, not a production build.
Config favors explicit, hardcoded local-dev values over configurability, to
match the rest of this codebase (PiddeCorp.Gateway, PiddeCorp.LegacySite).

## Running the dev server

```bash
npm install
npm run dev
```

The shell serves on **http://localhost:5175**. It expects:

- **PiddeCorp.Gateway** reachable at `http://localhost:8082` - the shell's
  Vite dev server proxies `/auth/*` to it (see "Dev proxy vs. CORS" below).
  That's the Gateway's container port (see `build.bat`'s
  `podman run ... -p 8082:8080 ... gateway`), the same one
  `PiddeCorp.LegacySite`'s `PortalController` already hardcodes - not the
  `https://localhost:7020` port `dotnet run`/launchSettings.json uses when
  running the Gateway directly outside a container.
- **PiddeCorp.TaskManager.UI** built and served as the `taskmanager` remote
  on `http://localhost:5174` (see its own README for the two-terminal
  `build:watch` + `preview` workflow - Module Federation for Vite only gives
  the *host* live HMR, not the remote).

There is no login UI here and none is planned. If `/auth/me` reports
`authenticated: false`, the shell just tells the user to launch the portal
from LegacySite instead of rendering a login form.

`npm run typecheck` runs `tsc --noEmit` if you want to check types without
starting the dev server.

## Routing: the shell owns `/`, the remote owns everything under `/taskmanager`

`src/main.tsx` wraps the app root in a plain `<Router>` (no `base` - the
shell owns the root namespace). `src/app.tsx` then does the handoff:

```tsx
<Switch>
  <Route path="/taskmanager/*?">
    <TaskManagerModule />
  </Route>
  <Route>
    <p>Select a module.</p>
  </Route>
</Switch>
```

Two things about that route are easy to get wrong and worth spelling out:

**No `nest` prop.** TaskManager.UI's own `App` already wraps itself in
`<Router base="/taskmanager">` (see its `src/app.jsx`) - it owns its base
path internally so it works standalone or embedded. wouter's `base` stacks
across nested `<Router>`s (parent base + own base), and `nest` on a `<Route>`
auto-wraps its match in an implicit `<Router base={matchedPrefix}>`. Add
`nest` here too and the remote ends up scoped to `/taskmanager/taskmanager`,
silently breaking every path one level deep or more. Handing off via the
path pattern alone, with no `nest`, is correct - the remote's own `Router`
is the only place `base` gets applied.

**The pattern is `/taskmanager/*?`, not `/taskmanager*`.** The obvious-looking
`/taskmanager*` (star glued directly onto the literal) does not do what it
looks like it does: `regexparam` only special-cases a `*` that occupies its
own path segment. Glued onto a literal like that, it's parsed as a plain
regex quantifier on the preceding character, and outside of `nest`/loose
mode the match is still anchored to end-of-string - so it only ever matches
the bare `/taskmanager`, never `/taskmanager/tasks`. `/taskmanager/*?` (an
*optional* wildcard segment - the same shape as wouter's own `/orders/*?`
cheatsheet example) is what actually matches both the bare path and
everything nested under it.

Both of the above were verified against a real, running shell + remote pair
(not just reasoned about) - by watching `useLocation()` inside the actual
remote resolve to `/`, `/tasks`, and `/tasks/123` for `/taskmanager`,
`/taskmanager/tasks`, and `/taskmanager/tasks/123` respectively, with no
double-stripping and no crash.

## `shared` must use the object form with `singleton: true`

```ts
// vite.config.ts
shared: {
  preact: { singleton: true },
  'preact/hooks': { singleton: true },
  'preact/compat': { singleton: true },
},
```

The shorthand array form (`shared: ['preact', 'preact/hooks', 'preact/compat']`)
looks equivalent but isn't - it leaves `singleton` at its default of `false`.
Verified the hard way: with the array form, the shell (host) and
TaskManager.UI (remote) each ended up with a *separate* copy of
`preact/hooks` at runtime, and calling a hook from the remote's copy (e.g.
wouter-preact's `useContext`, used internally by its `Router`) threw
`Cannot read properties of undefined (reading 'context')`, because that
copy's internal "currently rendering component" state was never populated -
that populating happens on whichever copy actually renders, which was the
host's. This is the exact "hooks break in confusing ways" failure mode the
`shared` config exists to prevent in the first place. The object form with
`singleton: true` on both sides is required, not just tidier. Must match
`PiddeCorp.TaskManager.UI`'s `vite.config.js` `shared` block exactly.

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
