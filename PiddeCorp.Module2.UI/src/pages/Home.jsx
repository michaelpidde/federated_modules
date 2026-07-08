import { Link } from 'wouter-preact';
import dragon from './dragon.txt?raw';

export default function Home() {
    return (
        <div>
            <p>Module2 main page. Isn't it pretty?</p>
            <pre>{dragon}</pre>
        </div>
    );
};