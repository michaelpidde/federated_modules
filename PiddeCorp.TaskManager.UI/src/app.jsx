import { Router, Switch, Route } from 'wouter-preact';
import Home from './pages/Home';
import Tasks from './pages/Tasks';

// This app is mounted under /taskmanager by PiddeCorp.Portal.Shell (see the
// "taskmanager" remote's exposes map in vite.config.js), so the base path
// has to live here rather than in something the shell owns - that's what
// lets /taskmanager/tasks (and future sub-routes) resolve correctly when
// embedded in the shell. Standalone (npm run dev:standalone), there's no
// /taskmanager prefix in the URL, so the base has to be empty there instead
// - hardcoding "/taskmanager" broke standalone routing entirely (wouter had
// nothing to match). VITE_ROUTER_BASE is set to "/taskmanager" only for the
// federation-remote build (build:watch/preview, via the "remote" Vite mode
// - see vite.config.js and .env.remote), left unset for dev/dev:standalone.
const routerBase = import.meta.env.VITE_ROUTER_BASE || '';

export function App() {
  return (
      <Router base={routerBase}>
        <Switch>
          <Route path="/" component={Home} />
          <Route path="/tasks" component={Tasks} />
        </Switch>
      </Router>
  );
}

// Also exported as default: PiddeCorp.Portal.Shell's TaskManagerModule
// component reads `mod.default` off this exposed module.
export default App;