import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { modules, type ModuleRegistryEntry, type NavItem } from '../modules'

type NavState =
  | { status: 'loading' }
  | { status: 'loaded'; items: NavItem[] }
  | { status: 'unavailable' }

function useActiveModule(): ModuleRegistryEntry | undefined {
  const { pathname } = useLocation()
  return modules.find(m => pathname === m.pathPrefix || pathname.startsWith(m.pathPrefix + '/'))
}

// A module's own nav items, fetched from its federated "./NavItems" export
// (plain data - see PiddeCorp.TaskManager.UI/src/navItems.js). Rendered as
// real <Link>s here, in the shell's own <Router> context, with the module's
// pathPrefix prepended - a module never renders this itself, since it would
// end up nested inside its own <Router base> and produce the wrong hrefs.
function ModuleNav({ module }: { module: ModuleRegistryEntry }) {
  const [state, setState] = useState<NavState>({ status: 'loading' })
  const { pathname } = useLocation()

  useEffect(() => {
    let cancelled = false
    setState({ status: 'loading' })

    module
      .loadNavItems()
      .then(mod => {
        if (!cancelled) setState({ status: 'loaded', items: mod.navItems })
      })
      .catch(() => {
        // Consistent with TaskManagerModule.tsx's own fallback - a remote
        // failing to load shows a small note instead of crashing the shell.
        if (!cancelled) setState({ status: 'unavailable' })
      })

    return () => {
      cancelled = true
    }
  }, [module.pathPrefix])

  return (
    <div className="nav-section">
      <p className="nav-section-title">{module.label}</p>
      {state.status === 'unavailable' && <p className="nav-unavailable">Navigation unavailable.</p>}
      {state.status === 'loaded' && (
        <ul className="nav-list">
          {state.items.map(item => {
            const href = module.pathPrefix + item.path
            const isCurrent = pathname === href || pathname.startsWith(href + '/')
            return (
              <li key={item.path}>
                <Link to={href} className={isCurrent ? 'is-current' : undefined}>
                  {item.label}
                </Link>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

// The "haven't clicked into a module yet" state - one link per registered
// module, straight to its pathPrefix.
function ModuleDirectory() {
  return (
    <div className="nav-section">
      <p className="nav-section-title">Modules</p>
      <ul className="nav-list">
        {modules.map(module => (
          <li key={module.pathPrefix}>
            <Link to={module.pathPrefix + '/'}>{module.label}</Link>
          </li>
        ))}
      </ul>
    </div>
  )
}

export function SideNav() {
  const activeModule = useActiveModule()
  return activeModule ? <><ModuleDirectory /><ModuleNav module={activeModule} /></> : <ModuleDirectory />
}
