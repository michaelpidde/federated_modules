// Exposed to PiddeCorp.Portal.Shell as the "./NavItems" remote module (see
// vite.config.ts). Plain data, not a rendered component - the shell renders
// this module's nav items inside its OWN <BrowserRouter> context (its
// <aside>), not this app's own routing. A rendered component exposed here
// would generate wrong hrefs relying on a base that doesn't exist in the
// shell's router.
//
// Paths are relative to this module's own base - do NOT include
// "/taskmanager" here, the shell prepends its base when it renders these.
//
// This interface is intentionally a local, structural duplicate of the
// shell's own NavItem (see PiddeCorp.Portal.Shell/src/modules.ts), not an
// import from it - these are separate npm packages, and there's no
// shared-types wiring across the Module Federation boundary (see this
// project's README).
export interface NavItem {
    path: string;
    label: string;
}

export const navItems: NavItem[] = [
    { path: '/', label: 'Home' },
    { path: '/tasks', label: 'Tasks' },
];
