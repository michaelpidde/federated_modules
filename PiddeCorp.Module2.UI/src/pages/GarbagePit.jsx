import { useState, useEffect } from 'preact/hooks';
import { getGarbage } from '../api/garbage';

export default function GarbagePit() {
    const [garbage, setGarbage] = useState([]);
    const [error, setError] = useState(null);

    useEffect(() => {
        getGarbage()
            .then(setGarbage)
            .catch(setError);
    }, []);

    if (error) return <div>Failed to load garbage. That's probably for the best.</div>;

    return (
        <div>
            <h1>Garbage</h1>
            <pre>{garbage}</pre>
        </div>
    );
}