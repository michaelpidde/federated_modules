import { useEffect, useState } from 'preact/hooks'

export interface AuthMeResponse {
  authenticated: boolean
  userId?: string
  name?: string
}

export interface AuthMeState {
  status: 'loading' | 'ready'
  data: AuthMeResponse
}

const UNAUTHENTICATED: AuthMeResponse = { authenticated: false }

// Calls the Gateway's GET /auth/me (see PiddeCorp.Gateway/Program.cs) via
// the shell's own origin - the Vite dev proxy forwards /auth/* to the
// Gateway locally, and in production the Gateway serves this route itself.
export function useAuthMe(): AuthMeState {
  const [state, setState] = useState<AuthMeState>({ status: 'loading', data: UNAUTHENTICATED })

  useEffect(() => {
    let cancelled = false

    fetch('/auth/me', { credentials: 'include' })
      .then(response => (response.ok ? (response.json() as Promise<AuthMeResponse>) : UNAUTHENTICATED))
      .catch(() => UNAUTHENTICATED)
      .then(data => {
        if (!cancelled) {
          setState({ status: 'ready', data })
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  return state
}
