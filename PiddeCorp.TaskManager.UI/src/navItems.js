// Exposed to PiddeCorp.Portal.Shell as the "./NavItems" remote module (see
// vite.config.js). Plain data, not a rendered component - the shell renders
// this module's nav items inside its OWN <Router> context (its <aside>),
// not this app's nested <Router base="/taskmanager">. A rendered component
// exposed here would generate wrong hrefs relying on a base that doesn't
// exist in the shell's router.
//
// Paths are relative to this module's own base - do NOT include
// "/taskmanager" here, the shell prepends its base when it renders these.
export const navItems = [
    { path: '/', label: 'Home' },
    { path: '/tasks', label: 'Tasks' },
];
