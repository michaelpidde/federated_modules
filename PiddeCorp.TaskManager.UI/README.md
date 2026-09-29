# PiddeCorp.TaskManager.UI

The TaskManager module UI. React + Vite + TypeScript. Runs standalone (its
own dev server, own routes) but is also set up as a Module Federation
**remote**, consumed by PiddeCorp.Portal.Shell (the host).

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

```ts
// vite.config.ts
exposes: {
  './App': './src/app.tsx',
}
```

The whole app (`App`, from `src/app.tsx`), not a bare leaf component. `App`
owns its own routing (a bare `<Routes>` wrapping the existing `/` and
`/tasks` routes via `react-router-dom`) so that everything under
`/taskmanager` - current and future sub-routes alike - works without the
shell needing to know what routes exist. `App` is exported both as a named
export (used by this project's own `src/main.tsx`) and as `default` (what
the shell's federation consumer reads off the loaded module).

## Routing: descendant `<Routes>`, not a `base` prop

This app's `App` renders a bare `<Routes>` with **no `<BrowserRouter>` of its
own**. When the shell mounts it under its own `<Route path="/taskmanager/*">`
(see `PiddeCorp.Portal.Shell/src/app.tsx`), this `<Routes>` becomes a true
*descendant* of the shell's `<BrowserRouter>`, reading its ambient router
context via `useContext` - react-router's built-in pattern for exactly this
shell/remote-under-a-path-prefix shape. That's what lets `/` and `/tasks`
resolve to `/taskmanager` and `/taskmanager/tasks` with no `base`/prefix
plumbing anywhere. Standalone (`npm run dev:standalone`), `src/main.tsx`
supplies its own top-level `<BrowserRouter>` instead, so the exact same
route definitions resolve at this app's own root with no prefix at all.

## `shared` must use the object form with `singleton: true`

```ts
// vite.config.ts
shared: {
  react: { singleton: true },
  'react-dom': { singleton: true },
  'react-router-dom': { singleton: true }
}
```

This has to be identical to `PiddeCorp.Portal.Shell`'s `vite.config.ts`
`shared` block. The shorthand array form (`shared: ['react', ...]`) looks
equivalent but isn't - it leaves `singleton` at its default of `false`,
which would let the runtime load a second, separate copy of `react-dom`, and
- specific to this app's routing - a second copy of `react-router-dom` too.
Because this app's `<Routes>` is a genuine descendant of the shell's
`<BrowserRouter>` (see "Routing" above), it reads the shell's router context
via `useContext` - if `react-router-dom` weren't a true singleton, that call
would hit a *different* `RouterContext` object than the one the shell's copy
provides, and fail the same class of "hooks/context break in confusing
ways" failure this repo already hit once with a duplicated `preact/hooks`
copy (`Cannot read properties of undefined (reading 'context')`). The object
form with `singleton: true` on all three is required, not just tidier.
