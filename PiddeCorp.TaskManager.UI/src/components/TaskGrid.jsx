import { useEffect, useRef , useState} from 'preact/hooks';
import { TabulatorFull as Tabulator } from 'tabulator-tables';
import 'tabulator-tables/dist/css/tabulator_site.min.css';
import { API_BASE } from '../api/tasks';

export function TaskGrid({ data }) {
    const tableRef = useRef(null);
    const tabulatorRef = useRef(null);
    const [isBuilt, setIsBuilt] = useState(false);

    useEffect(() => {
        tabulatorRef.current = new Tabulator(tableRef.current, {
            ajaxUrl: `${API_BASE}/tasks`,
            // This route requires a Gateway session (RequireGatewaySession),
            // and the cookie won't ride along on this cross-origin request
            // without it - see src/api/tasks.js.
            ajaxConfig: { credentials: 'include' },
            pagination: true,
            paginationMode: 'remote',
            paginationSize: 50,
            paginationInitialPage: 5,
            dataSendParams: {
                "page":"pageNo", //change page request parameter to "pageNo"
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