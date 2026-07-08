import { useEffect, useState } from 'preact/hooks'
import type { ComponentType } from 'preact'

type ModuleState =
    | { status: 'loading' }
    | { status: 'loaded'; Module: ComponentType }
    | { status: 'unavailable' }

export function Module2Module() {
    const [state, setState] = useState<ModuleState>({ status: 'loading' })

    useEffect(() => {
        let cancelled = false
        
        import('module2/App')
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
            return <p>Loading Module2 module...</p>
        case 'unavailable':
            return (
                <div className="module-placeholder">
                    <p>Module2 module not available.</p>
                    <p>
                        This is expected until PiddeCorp.Module2.UI is wired up as a Module Federation
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
