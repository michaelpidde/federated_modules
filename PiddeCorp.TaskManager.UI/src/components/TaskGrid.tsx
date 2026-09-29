import { useEffect, useRef, useState } from 'react';
import { TabulatorFull as Tabulator } from 'tabulator-tables';
import 'tabulator-tables/dist/css/tabulator_site.min.css';
import { API_BASE, type OpenTask } from '../api/tasks';

export function TaskGrid({ data }: { data: OpenTask[] }) {
    const tableRef = useRef<HTMLDivElement>(null);
    const tabulatorRef = useRef<Tabulator | null>(null);
    const [isBuilt, setIsBuilt] = useState(false);

    useEffect(() => {
        tabulatorRef.current = new Tabulator(tableRef.current as HTMLDivElement, {
            // Note: `ajaxURL`, not `ajaxUrl` - Tabulator's real option name
            // (see node_modules/tabulator-tables/src/js/modules/Ajax/Ajax.js).
            // Plain JS never validated this key, so a prior `ajaxUrl` typo
            // here was silently ignored at runtime; TypeScript's `Options`
            // type is what caught it.
            ajaxURL: `${API_BASE}/tasks`,
            // This route requires a Gateway session (RequireGatewaySession),
            // and the cookie won't ride along on this cross-origin request
            // without it - see src/api/tasks.ts.
            ajaxConfig: { credentials: 'include' },
            pagination: true,
            paginationMode: 'remote',
            paginationSize: 50,
            paginationInitialPage: 5,
            dataSendParams: {
                "page": "pageNo", //change page request parameter to "pageNo"
            },
            // data: data,
            layout: 'fitColumns',
            columns: [
                { title: 'Id', field: 'id' },
                { title: 'Created', field: 'created' },
                { title: 'Type', field: 'typeName' },
                { title: 'Due', field: 'due' },
            ]
        });

        tabulatorRef.current.on('tableBuilt', () => {
            setIsBuilt(true);
        });

        return () => {
            tabulatorRef.current?.destroy();
            tabulatorRef.current = null;
            setIsBuilt(false);
        };
    }, []);

    // Update data when prop changes without reinitializing
    useEffect(() => {
        if (isBuilt && tabulatorRef.current && data) {
            tabulatorRef.current.setData(data);
        }
    }, [data, isBuilt]);

    return <div ref={tableRef} />;
}
