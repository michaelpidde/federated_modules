# PiddeCorp.TaskManager.UI

The TaskManager module UI. Preact + Vite. Runs standalone (its own dev
server, own routes) but is also set up as a Module Federation **remote**,
consumed by PiddeCorp.Portal.Shell (the host).

## Running standalone

```bash
npm install
npm run dev:standalone
```

Serves on port 5174 (fixed in `vite.config.js` as `remoteDevPort` - not
actually the Vite default of 5173, despite that being the more common
convention), with `/taskmanager/*` and `/dev/login` proxied to
`https://localhost:7020` (PiddeCorp.Gateway's local `dotnet run` dev port).
This is the normal edit-and-reload workflow for working on this app's own
components/pages and does not exercise Module Federation at all - real,
authenticated API calls still go through the Gateway, exactly as they do in
production.

This requires:

1. **The Gateway running with `ASPNETCORE_ENVIRONMENT=Development`.**
   `/dev/login` only exists on the Gateway when it's running in that
   environment - see its `Program.cs`.
2. **A one-time visit to establish a session**, through this project's own
   dev server, not the Gateway's origin directly:

   ```
   http://localhost:5174/dev/login?user=someUser&returnTo=/
   ```

   This has to go through the proxy configured in `vite.config.js` (i.e.
   through `localhost:5174`, this app's own origin) rather than hitting
   `https://localhost:7020/dev/login` directly. Visiting the Gateway's own
   origin would scope the resulting session cookie to the Gateway's origin,
   not this app's - so the browser would never send it back on this app's
   own (proxied, but still browser-perceived-as-same-origin) API calls.
   Once visited, the session cookie is scoped correctly and every
   `/taskmanager/api/*` call this app makes will be authenticated, no
   further action needed until the cookie expires.

## Running as a Module Federation remote (for the shell to consume)

**Module Federation for Vite only gives the HOST a live dev server with
HMR.** There is no way to run this project's `vite dev` and have the shell
pick up live changes - that's a known limitation of the ecosystem, not
something to work around here. Instead, the remote has to be built to
static files, which the shell's browser then fetches directly.

Local dev workflow, two terminals:

```bash
# terminal 1 - rebuilds dist/ on every change
npm run build:watch

# terminal 2 - serves dist/ at the fixed port the shell expects
npm run preview
```

This serves the built app (including `remoteEntry.js`) on
**http://localhost:5174**, which is what `PiddeCorp.Portal.Shell`'s
`vite.config.ts` has hardcoded as the `taskmanager` remote's entry URL.

**After editing this app, `build:watch` will produce a new build automatically,
but the shell will not hot-reload - you need to manually refresh the
shell's browser tab to see the change.**

## What's exposed

```js
// vite.config.js
exposes: {
  './App': './src/app.jsx',
}
```

The whole app (`App`, from `src/app.jsx`), not a bare leaf component. `App`
owns its own routing (`<Router base="/taskmanager">` wrapping the existing
`/` and `/tasks` routes via `wouter-preact`) so that everything under
`/taskmanager` - current and future sub-routes alike - works without the
shell needing to know what routes exist. `App` is exported both as a named
export (used by this project's own `src/main.jsx`) and as `default` (what
the shell's federation consumer reads off the loaded module).

## Routing: wouter-preact, not preact-router

This app switched from `preact-router` to `wouter-preact`. Reason:
`preact-router` has no native base-path support (unresolved upstream -
see preactjs/preact-router issues #212, #263, #279), and this app needs one
now that it's mounted under `/taskmanager` by the shell. `wouter-preact`
supports this natively via the `base` prop on `<Router>`, which is what
lets `<Link href="/tasks">` correctly resolve to `/taskmanager/tasks`
whether this app is running standalone or embedded in the shell.

## `shared` must use the object form with `singleton: true`

```js
// vite.config.js
shared: {
  preact: { singleton: true },
  'preact/hooks': { singleton: true },
  'preact/compat': { singleton: true }
}
```

This has to be identical to `PiddeCorp.Portal.Shell`'s `vite.config.ts`
`shared` block. The shorthand array form (`shared: ['preact', ...]`) looks
equivalent but isn't - it leaves `singleton` at its default of `false`.
Verified the hard way: with the array form, this remote and the shell (host)
each ended up with a *separate* copy of `preact/hooks` at runtime, and
calling a hook from this app's copy (`wouter-preact`'s `useContext`, used
internally by its `Router`) threw `Cannot read properties of undefined
(reading 'context')` - because that copy's internal "currently rendering
component" state was never populated (that happens on whichever copy
actually renders, which was the host's). This is the exact "hooks break in
confusing ways" failure the `shared` config exists to prevent. The object
form with `singleton: true` on both sides is required, not just tidier.
