import {Route, Routes} from 'react-router-dom'
import {useAuthMe} from './auth/useAuthMe'
import {TaskManagerModule} from './components/TaskManagerModule'
import {Module2Module} from './components/Module2Module'
import {SideNav} from './components/SideNav'

// Defaults to LegacySite's container port mapping (see build.bat's
// `podman run ... -p 8083:8080 ... legacysite`). Aspire's AppHost overrides this
// via VITE_LEGACY_SITE_URL, since LegacySite runs on a different port there -
// see PiddeCorp.AppHost/AppHost.cs.
const legacySiteUrl = import.meta.env.VITE_LEGACY_SITE_URL || 'http://localhost:8083';

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
                    <p className="user">You're not signed in. Launch the portal from <a href={legacySiteUrl}>LegacySite</a> to start a session.</p>
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
                <Routes>
                    {/* Each of these is a "descendant routes" splat route: the
              trailing `/*` matches both the bare prefix ("/taskmanager")
              and everything nested under it ("/taskmanager/tasks", etc),
              and hands the whole remainder of the URL off to whatever
              <Routes> the matched element renders further down. TaskManager.UI's
              own App (see PiddeCorp.TaskManager.UI/src/app.tsx) renders a
              bare <Routes> with no <BrowserRouter> of its own - it's a true
              descendant of THIS <BrowserRouter> (see src/main.tsx), reading
              this router's ambient context rather than owning a separate
              one. That's what lets its own "/" and "/tasks" route
              definitions resolve correctly relative to "/taskmanager"
              without either side needing to know about a "base"/prefix at
              all - no equivalent of wouter's nested-Router-with-base dance
              needed here.

              This does mean react-router-dom itself has to be a true
              Module Federation singleton between this shell and every
              remote (see vite.config.ts's `shared` block) - if it weren't,
              the remote's <Routes> would be calling useContext against a
              *different* RouterContext object than the one this
              <BrowserRouter> provides, and would fail the same way a
              duplicated preact/hooks copy used to (see this project's
              README). Verified against a real running shell + remote pair,
              not just reasoned about - by loading /taskmanager,
              /taskmanager/tasks, /module2, and /module2/garbage-pit through
              the shell and confirming each remote resolves the right page
              with no console errors. */}
                    <Route path="/taskmanager/*" element={<TaskManagerModule/>}/>
                    <Route path="/module2/*" element={<Module2Module/>}/>
                    <Route path="*" element={<p>Select a module.</p>}/>
                </Routes>
            </main>
        </div>
    )
}
