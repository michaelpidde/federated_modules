import { defineConfig } from 'vite'
import preact from '@preact/preset-vite'
import { federation } from '@module-federation/vite'

// Keep this distinct from the Gateway (7020/7021), the fake LegacySite
// (7010/7011), and TaskManager.UI's current dev port (5173, the Vite
// default - see its vite.config.js). 5174 is reserved as TaskManager.UI's
// future Module Federation remote port (see the placeholder entry below).
const shellDevPort = 5175

export default defineConfig({
  server: {
    port: shellDevPort,
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
        // Matches the Gateway's container port mapping (see build.bat's
        // `podman run ... -p 8082:8080 ... gateway`), the same port
        // PiddeCorp.LegacySite's PortalController already hardcodes for
        // reaching the Gateway.
        target: 'http://localhost:8082',
        changeOrigin: true,
      },
      // TODO: once the TaskManager module's API calls are routed through
      // the Gateway at /taskmanager/*, proxy that here the same way.
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
    preact(),
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
      // (`shared: ['preact', ...]`) leaves `singleton` at its default of
      // `false`, which lets the runtime load a second, separate copy of
      // preact/hooks instead of actually sharing one. That second copy has
      // its own internal "currently rendering" state, so hooks called from
      // it (e.g. wouter-preact's useContext) blow up with "Cannot read
      // properties of undefined (reading 'context')" - the exact failure
      // mode this config exists to prevent. Must match TaskManager.UI's
      // vite.config.js shared block exactly.
      shared: {
        preact: { singleton: true },
        'preact/hooks': { singleton: true },
        'preact/compat': { singleton: true },
      },
    }),
  ],
})
