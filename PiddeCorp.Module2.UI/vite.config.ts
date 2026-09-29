import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { federation } from '@module-federation/vite'

// Fixed, not dynamic - PiddeCorp.Portal.Shell's vite.config.ts has this
// exact URL hardcoded as its "taskmanager" remote entry point.
const remoteDevPort = 5176

export default defineConfig({
  server: {
    port: remoteDevPort,
    // Module Federation needs this app to know its own origin so the
    // shell can resolve remoteEntry.js and its exposed chunks correctly.
    origin: `http://localhost:${remoteDevPort}`,
    // The shell's browser fetches remoteEntry.js and exposed chunks
    // directly from this origin during dev - without CORS that fetch
    // fails silently. This is unrelated to any cookie/session concern;
    // no credentials cross this boundary.
    cors: true,
    proxy: {
      // Standalone dev (`npm run dev:standalone`) needs real, authenticated
      // calls through the Gateway - not straight to Module2.API - so this
      // proxies to the Gateway's local `dotnet run` dev port (7020, HTTPS),
      // not its container port (8082, see src/api/garbage.ts) or
      // Module2.API directly. Matches the same-origin-proxy pattern
      // PiddeCorp.Portal.Shell's vite.config.ts already uses for /auth/*,
      // so the session cookie set via /dev/login below is scoped to this
      // app's own dev origin rather than the Gateway's.
      //
      // Scoped to /module2/api specifically, NOT the broader
      // /module2 prefix: the Gateway's own reverse-proxy route matches
      // /module2/{**catch-all} (see PiddeCorp.Gateway/appsettings.json),
      // so a wider rule here also swallows plain page navigation like
      // /module2/tasks and forwards it to the Gateway/backend chain
      // (which has no page route for it - it 401s/404s there) instead of
      // letting this app's own SPA routing handle it client-side.
      '/module2/api': {
        target: 'https://localhost:7020',
        changeOrigin: true,
        // The Gateway's local HTTPS dev cert is the ASP.NET Core dev
        // certificate, which Node's proxy client won't trust by default.
        secure: false
      },
      // The Gateway only registers this route when it itself is running
      // with ASPNETCORE_ENVIRONMENT=Development - see PiddeCorp.Gateway's
      // /dev/login handler in Program.cs. Proxied for the same reason as
      // /module2 above: visiting it through this origin (not the
      // Gateway's) is what scopes the resulting session cookie correctly.
      '/dev/login': {
        target: 'https://localhost:7020',
        changeOrigin: true,
        secure: false
      }
    }
  },
  preview: {
    port: remoteDevPort,
    cors: true
  },
  build: {
    // Module Federation's runtime relies on native ESM dynamic import and
    // top-level await, which need Chrome 89+.
    target: 'chrome89'
  },
  plugins: [
    react(),
    ...federation({
      name: 'module2',
      filename: 'remoteEntry.js',
      exposes: {
        // The whole app, not a bare leaf component - App owns routing
        // (see src/app.tsx) for everything under /module2 once the
        // shell hands off to it.
        './App': './src/app.tsx',
        // Plain data (see src/navItems.ts) - the shell renders this
        // module's nav items itself, inside its own <BrowserRouter>/<aside>,
        // rather than this exposing a rendered component.
        './NavItems': './src/navItems.ts'
      },
      // `singleton: true` is required here, not optional - verified against
      // a real host+remote integration test. The shorthand array form
      // (`shared: ['react', ...]`) leaves `singleton` at its default of
      // `false`, which would let the runtime load a second, separate copy
      // of react-dom, and - specific to this app - a second copy of
      // react-router-dom. This app's own App (src/app.tsx) renders a bare
      // <Routes> with no <BrowserRouter> of its own; when the shell mounts
      // it, that <Routes> is a true descendant of the shell's
      // <BrowserRouter>, reading its ambient context via useContext. If
      // react-router-dom weren't a singleton, that useContext call would
      // hit a *different* RouterContext object than the one the shell's
      // copy provides and blow up the same way a duplicated preact/hooks
      // copy used to (see this project's README). Must match
      // PiddeCorp.Portal.Shell's vite.config.ts shared block exactly.
      shared: {
        react: { singleton: true },
        'react-dom': { singleton: true },
        'react-router-dom': { singleton: true }
      },
      // Wiring up automatic .d.ts generation/consumption for a dynamically
      // loaded federated remote (type-serving across origins, a
      // `dts: { consumeTypes: true }` block on the shell's remote entry, a
      // dev workflow where this app has to be built before the shell can
      // resolve its types) is a separate, heavier piece of config than just
      // having a tsconfig.json - not wired up here, so turn generation off
      // rather than let it fail on every build. The federation boundary
      // stays a deliberate, documented trust boundary (see the shell's
      // ts-expect-error comments on its dynamic remote imports).
      dts: false
    })
  ]
})