import type { FetchLogEntry } from '../../api/types';
import { StatusBadge } from '../../components/StatusBadge';
import { formatDateTimeWithSeconds } from '../../lib/format';

interface FetchLogTableProps {
  entries: readonly FetchLogEntry[];
}

export function FetchLogTable({ entries }: FetchLogTableProps) {
  if (entries.length === 0) {
    return <p className="empty-state">No fetch attempts match these filters yet.</p>;
  }

  return (
    <div className="table-wrap">
      <table className="data-table">
        <caption className="visually-hidden">Fetch attempts, newest first</caption>
        <thead>
          <tr>
            <th scope="col">Time</th>
            <th scope="col">City</th>
            <th scope="col">Result</th>
            <th scope="col" className="numeric">
              HTTP
            </th>
            <th scope="col" className="numeric">
              Duration
            </th>
            <th scope="col">Error</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => (
            <tr key={entry.id}>
              <td>
                <time dateTime={entry.attemptedAtUtc}>{formatDateTimeWithSeconds(entry.attemptedAtUtc)}</time>
              </td>
              <td>{entry.city}</td>
              <td>
                <StatusBadge success={entry.isSuccess} />
              </td>
              <td className="numeric">{entry.httpStatusCode ?? '—'}</td>
              <td className="numeric">{entry.durationMs} ms</td>
              <td className="data-table__error">{entry.errorMessage}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
