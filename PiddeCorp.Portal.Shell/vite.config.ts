import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { federation } from '@module-federation/vite'

// Keep this distinct from the Gateway (7020/7021), and TaskManager.UI's current dev port (5173, the Vite
// default - see its vite.config.js). 5174 is reserved as TaskManager.UI's
// future Module Federation remote port (see the placeholder entry below).
const shellDevPort = 5175

// Defaults to the Gateway's container port mapping (see build.bat's
// `podman run ... -p 8082:8080 ... gateway`). Aspire's AppHost overrides this
// via the GATEWAY_URL env var, since nothing listens on 8082 there - see
// PiddeCorp.AppHost/AppHost.cs.
const gatewayProxyTarget = process.env.GATEWAY_URL ?? 'http://localhost:8082'

export default defineConfig({
  server: {
    port: shellDevPort,
    // Set by Aspire's AppHost so running it lands on the shell; off for plain `npm run dev`.
    open: process.env.OPEN_BROWSER === 'true',
    // Module Federation needs the host to know its own origin so remote
    // entries resolve correctly against it.
    origin: `http://localhost:${shellDevPort}`,
    proxy: {
      // In production, the Gateway is the only public origin, reached via
      // path-based reverse-proxy routes (see PiddeCorp.Gateway/Program.cs).
      // Locally, the shell's own Vite dev server runs on a different port
      // than the Gateway, so calls to /auth/* are cross-origin in dev even
      // though they won't be in production.
      //
      // This is deliberately solved with a dev-server proxy instead of CORS
      // on the Gateway: proxying makes the browser see everything as
      // same-origin in dev too, matching the real production topology.
      // Enabling CORS instead would paper over that difference rather than
      // reproduce it - don't "simplify" this back to CORS later.
      '/auth': {
        target: gatewayProxyTarget,
        changeOrigin: true,
      },
      // Module BFF calls (see each module's src/api/*.js - they fetch relative
      // paths like /module2/api/garbage expecting whatever serves the shell to
      // be same-origin with Gateway) - same reasoning as /auth above.
      //
      // Scoped to /module2/api and /taskmanager/api specifically, NOT the
      // broader /module2 and /taskmanager prefixes: Gateway's own reverse-proxy
      // routes match /module2/{**catch-all} and /taskmanager/{**catch-all} (see
      // PiddeCorp.Gateway/appsettings.json), so a wider rule here would also
      // swallow direct navigation/refresh on module page routes like
      // /module2/garbage-pit and forward them to Gateway/Module2.API (which has
      // no page route for it) instead of letting the shell's own client-side
      // routing handle it. Matches the same scoping each module's own
      // dev:standalone vite.config.js already uses, for the same reason.
      '/module2/api': {
        target: gatewayProxyTarget,
        changeOrigin: true,
      },
      '/taskmanager/api': {
        target: gatewayProxyTarget,
        changeOrigin: true,
      },
    },
  },
  base: `http://localhost:${shellDevPort}`,
  build: {
    // Module Federation's runtime relies on native ESM dynamic import and
    // top-level await, which need Chrome 89+ (or an extra polyfill plugin
    // this pilot doesn't need).
    target: 'chrome89',
  },
  plugins: [
    react(),
    ...federation({
      name: 'shell',
      remotes: {
        taskmanager: {
          type: 'module',
          name: 'taskmanager',
          entry: 'http://localhost:5174/remoteEntry.js',
        },
        module2: {
          type: 'module',
          name: 'module2',
          entry: 'http://localhost:5176/remoteEntry.js',
        },
      },
      // `singleton: true` is required here, not optional - verified against
      // a real host+remote integration test. The shorthand array form
      // (`shared: ['react', ...]`) leaves `singleton` at its default of
      // `false`, which lets the runtime load a second, separate copy of
      // react-dom (breaking rendering entirely - two ReactDOM instances
      // can't share one root) and, just as badly for this app specifically,
      // a second copy of react-router-dom. Each remote's App renders a bare
      // <Routes> with no <BrowserRouter> of its own (see app.tsx) - it's a
      // true descendant of this shell's <BrowserRouter>, reading this
      // router's ambient context via useContext. If react-router-dom
      // weren't a singleton, the remote's copy would be calling useContext
      // against a *different* RouterContext object than the one this
      // shell's copy provides, and would fail the same way a duplicated
      // preact/hooks copy used to (see this project's README) - "Cannot
      // read properties of undefined (reading 'context')" or equivalent.
      // Must match each remote's vite.config.ts shared block exactly.
      shared: {
        react: { singleton: true },
        'react-dom': { singleton: true },
        'react-router-dom': { singleton: true },
      },
    }),
  ],
})
