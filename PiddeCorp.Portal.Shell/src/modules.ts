// Explicit registry, not dynamically discovered - adding a second module
// later means one more entry here, not new branching logic in SideNav.tsx
// or app.tsx. Mirrors how PiddeCorp.Gateway's own ReverseProxy:Routes config
// is one static entry per module rather than something dynamically probed.

export interface NavItem {
  path: string
  label: string
}

export interface ModuleRegistryEntry {
  // Used both to prefix a module's own relative nav item paths (see
  // SideNav.tsx) and to detect which module - if any - the current URL is
  // inside (see useActiveModule in SideNav.tsx).
  pathPrefix: string
  label: string
  loadNavItems: () => Promise<{ navItems: NavItem[] }>
}

export const modules: ModuleRegistryEntry[] = [
  {
    pathPrefix: '/taskmanager',
    label: 'Task Manager',
    loadNavItems: () =>
      // @ts-expect-error - "taskmanager" is a federated remote, not a real
      // package on disk, so TS can't resolve this until the remote generates
      // real types (see the dts option in @module-federation/vite) - same
      // reasoning as TaskManagerModule.tsx's import('taskmanager/App').
      import('taskmanager/NavItems'),
  },
  {
    pathPrefix: '/module2',
    label: 'Module 2',
    loadNavItems: () =>
        import('module2/NavItems'),
  },
]
