import { defineConfig } from 'vite'
import preact from '@preact/preset-vite'
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
      // not its container port (8082, see src/api/tasks.js) or
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
    preact(),
    ...federation({
      name: 'module2',
      filename: 'remoteEntry.js',
      exposes: {
        // The whole app, not a bare leaf component - App owns routing
        // (see src/app.jsx) for everything under /module2 once the
        // shell hands off to it.
        './App': './src/app.jsx',
        // Plain data (see src/navItems.js) - the shell renders this
        // module's nav items itself, inside its own <Router>/<aside>,
        // rather than this exposing a rendered component.
        './NavItems': './src/navItems.js'
      },
      // `singleton: true` is required here, not optional - verified against
      // a real host+remote integration test. The shorthand array form
      // (`shared: ['preact', ...]`) leaves `singleton` at its default of
      // `false`, which let the runtime load a second, separate copy of
      // preact/hooks instead of actually sharing the host's one. That
      // second copy has its own internal "currently rendering" state, so
      // hooks called from it (wouter-preact's useContext, in this app's own
      // Router) blew up with "Cannot read properties of undefined (reading
      // 'context')". Must match PiddeCorp.Portal.Shell's vite.config.ts
      // shared block exactly.
      shared: {
        preact: { singleton: true },
        'preact/hooks': { singleton: true },
        'preact/compat': { singleton: true }
      },
      // This project is plain JS - there's no tsconfig.json for the dts
      // plugin to generate exposed-module types from, so turn it off
      // rather than let it fail on every build.
      dts: false
    })
  ]
})