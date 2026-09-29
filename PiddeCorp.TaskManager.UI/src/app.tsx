import { Routes, Route } from 'react-router-dom';
import Home from './pages/Home';
import Tasks from './pages/Tasks';

// This app is exposed to PiddeCorp.Portal.Shell as the "taskmanager"
// remote's "./App" (see vite.config.ts's exposes map). It renders a bare
// <Routes>, with no <BrowserRouter> of its own - when the shell mounts it
// under its own <Route path="/taskmanager/*">, this <Routes> is a true
// descendant of the shell's <BrowserRouter> and resolves "/" and "/tasks"
// relative to "/taskmanager" automatically (react-router's "descendant
// routes" pattern). Standalone (npm run dev:standalone), main.tsx supplies
// its own top-level <BrowserRouter> instead, so the exact same routes
// resolve at the app's own root with no prefix.
export function App() {
    return (
        <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/tasks" element={<Tasks />} />
        </Routes>
    );
}

// Also exported as default: PiddeCorp.Portal.Shell's TaskManagerModule
// component reads `mod.default` off this exposed module.
export default App;
