import {Route, Switch} from 'wouter-preact'
import {useAuthMe} from './auth/useAuthMe'
import {TaskManagerModule} from './components/TaskManagerModule'
import {Module2Module} from './components/Module2Module'
import {SideNav} from './components/SideNav'

export function App() {
    const {status, data} = useAuthMe();
    const title = <h1>PiddeCorp Portal</h1>;

    if (status === 'loading') {
        return <p>Checking session...</p>
    }

    if (!data.authenticated) {
        return (
            <div className="shell-layout">
                <header className="shell-header">
                    {title}
                    <p className="user">You're not signed in. Launch the portal from <a href="http://localhost:8083">LegacySite</a> to start a session.</p>
                </header>
            </div>
        )
    }

    return (
        <div className="shell-layout">
            <header className="shell-header">
                {title}
                <p className="user">Signed in as {data.name ?? data.userId}</p>
            </header>
            <aside className="shell-aside">
                <SideNav/>
            </aside>
            <main className="shell-main">
                <Switch>
                    {/* Deliberately no `nest` prop here. TaskManager.UI's own App
              already wraps itself in <Router base="/taskmanager"> (see
              PiddeCorp.TaskManager.UI/src/app.jsx) - it owns its base path
              internally so it works whether it's running standalone or
              embedded here. wouter's `base` stacks across nested <Router>s
              (parent base + own base - see the Router source), and `nest`
              on a <Route> auto-wraps its match in an implicit
              <Router base={matchedPrefix}>. If this route also used
              `nest`, the remote would end up nested inside a base of
              "/taskmanager" from here PLUS its own "/taskmanager", i.e.
              scoped to "/taskmanager/taskmanager" - breaking every deep
              link like /taskmanager/tasks. Handing off via the pattern
              alone (no `nest`) is sufficient, and doesn't touch base at
              all - the remote's own <Router base="/taskmanager"> is the
              only place base gets applied.

              Pattern note: "/taskmanager*" (no slash before the `*`) does
              NOT do what it looks like it does here - regexparam only
              special-cases a `*` that occupies its own path segment. Glued
              directly onto a literal like this, it's parsed as a plain
              regex quantifier on the preceding character, and outside
              `nest`/loose mode the match is anchored to end-of-string
              regardless - so "/taskmanager*" only matches "/taskmanager"
              itself, never "/taskmanager/tasks". "/taskmanager/*?" (an
              optional wildcard *segment*, per wouter's own "/orders/*?"
              cheatsheet example) is what actually matches both the bare
              path and everything nested under it. Verified against a real
              deep link - see PiddeCorp.Portal.Shell's README. */}
                    <Route path="/taskmanager/*?">
                        <TaskManagerModule/>
                    </Route>
                    <Route path="/module2/*?">
                        <Module2Module/>
                    </Route>
                    <Route>
                        <p>Select a module.</p>
                    </Route>
                </Switch>
            </main>
        </div>
    )
}
