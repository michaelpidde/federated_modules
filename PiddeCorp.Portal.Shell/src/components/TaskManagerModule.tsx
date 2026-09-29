import { useEffect, useState } from 'react'
import type { ComponentType } from 'react'

type ModuleState =
  | { status: 'loading' }
  | { status: 'loaded'; Module: ComponentType }
  | { status: 'unavailable' }

export function TaskManagerModule() {
  const [state, setState] = useState<ModuleState>({ status: 'loading' })

  useEffect(() => {
    let cancelled = false

    // This has to stay a literal string, not a variable - the federation plugin
    // statically rewrites import specifiers shaped like "remoteName/thing"
    // at build time, so it can't recognize an indirected one.
    // @ts-expect-error - "taskmanager" is a federated remote, not a real
    // package on disk, so TS can't resolve this until the remote generates
    // real types (see the dts option in @module-federation/vite).
    import('taskmanager/App')
      .then(mod => {
        if (!cancelled) {
          setState({ status: 'loaded', Module: mod.default })
        }
      })
      .catch(() => {
        // Expected for now - the placeholder remote URL in vite.config.ts
        // doesn't resolve to anything real yet.
        if (!cancelled) {
          setState({ status: 'unavailable' })
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  switch (state.status) {
    case 'loading':
      return <p>Loading TaskManager module...</p>
    case 'unavailable':
      return (
        <div className="module-placeholder">
          <p>TaskManager module not available.</p>
          <p>
            This is expected until PiddeCorp.TaskManager.UI is wired up as a Module Federation
            remote - see this project's README.
          </p>
        </div>
      )
    case 'loaded': {
      const { Module } = state
      return <Module />
    }
  }
}
