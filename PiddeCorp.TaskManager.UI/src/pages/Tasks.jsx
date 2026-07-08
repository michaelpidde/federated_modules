import { useState, useEffect } from 'preact/hooks';
import { TaskGrid } from '../components/TaskGrid';
import { getOpenTasks } from '../api/tasks';

export default function Tasks() {
    const [tasks, setTasks] = useState([]);
    const [error, setError] = useState(null);

    useEffect(() => {
        getOpenTasks()
            .then(setTasks)
            .catch(setError);
    }, []);

    if (error) return <div>Failed to load tasks.</div>;

    return (
        <div>
            <h1>Open Tasks</h1>
            <TaskGrid data={tasks} />
        </div>
    );
}