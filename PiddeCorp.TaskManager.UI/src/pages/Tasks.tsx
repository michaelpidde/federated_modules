import { useState, useEffect } from 'react';
import { TaskGrid } from '../components/TaskGrid';
import { getOpenTasks, type OpenTask } from '../api/tasks';

export default function Tasks() {
    const [tasks, setTasks] = useState<OpenTask[]>([]);
    const [error, setError] = useState<Error | null>(null);

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
