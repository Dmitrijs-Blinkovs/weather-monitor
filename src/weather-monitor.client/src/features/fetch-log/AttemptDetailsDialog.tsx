import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import type { FetchLogEntry } from '../../api/types';
import { useWeatherApi } from '../../api/WeatherApiContext';
import { ErrorNotice } from '../../components/ErrorNotice';
import { StatusBadge } from '../../components/StatusBadge';
import { formatDateTimeWithSeconds } from '../../lib/format';

interface AttemptDetailsDialogProps {
  /** The attempt to show; null keeps the dialog closed. */
  entry: FetchLogEntry | null;
  onClose: () => void;
}

/**
 * Everything stored for one attempt: the provider's full payload of a success, or the full error and the body of a
 * failure. The body is requested only when the dialog opens, so the log itself never transfers payloads.
 */
export function AttemptDetailsDialog({ entry, onClose }: AttemptDetailsDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (entry && dialog && !dialog.open) {
      dialog.showModal();
    }
  }, [entry]);

  // The native dialog handles focus, Esc and the backdrop; "close" fires however it was closed.
  return (
    <dialog ref={dialogRef} className="dialog" aria-labelledby="attempt-details-title" onClose={onClose}>
      {entry && (
        <>
          <header className="dialog__header">
            <h2 id="attempt-details-title" className="dialog__title">
              {entry.isSuccess ? 'Payload' : 'Error details'} · {entry.city},{' '}
              {formatDateTimeWithSeconds(entry.attemptedAtUtc)}
            </h2>
            <button type="button" className="button" onClick={() => dialogRef.current?.close()}>
              Close
            </button>
          </header>
          <AttemptDetails entry={entry} />
        </>
      )}
    </dialog>
  );
}

function AttemptDetails({ entry }: { entry: FetchLogEntry }) {
  const api = useWeatherApi();
  const details = useQuery({
    queryKey: ['fetch-attempt', entry.id],
    queryFn: ({ signal }) => api.getFetchAttempt(entry.id, signal),
    // A stored attempt never changes.
    staleTime: Infinity,
  });

  return (
    <>
      <dl className="dialog__facts">
        <dt>Result</dt>
        <dd>
          <StatusBadge success={entry.isSuccess} />
        </dd>
        <dt>HTTP status</dt>
        <dd>{entry.httpStatusCode ?? 'none (no response)'}</dd>
        <dt>Duration</dt>
        <dd>{entry.durationMs} ms</dd>
        <dt>Attempt id</dt>
        <dd>{entry.id}</dd>
      </dl>

      {!entry.isSuccess && (
        <section>
          <h3 className="dialog__section-title">
            Error
          </h3>
          {details.data && (
            <p className="dialog__error">{details.data.errorMessage ?? 'No error text was stored.'}</p>
          )}
        </section>
      )}

      <section>
        <h3 className="dialog__section-title">
          Response body
        </h3>
        {details.isPending && <p className="muted">Loading the response body…</p>}
        {details.isError && <ErrorNotice error={details.error} onRetry={() => void details.refetch()} />}
        {details.data &&
          (details.data.responseBody === null ? (
            <p className="muted">No response body was received (for example a timeout or a network error).</p>
          ) : (
            <ResponseBody body={details.data.responseBody} />
          ))}
      </section>
    </>
  );
}

function ResponseBody({ body }: { body: string }) {
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'failed'>('idle');

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(body);
      setCopyState('copied');
    } catch {
      setCopyState('failed');
    }
  };

  return (
    <>
      <div className="dialog__toolbar">
        <span className="muted">Formatted for reading; Copy takes the body exactly as stored.</span>
        <span role="status" className="muted">
          {copyState === 'copied' && 'Copied.'}
          {copyState === 'failed' && 'The browser did not allow copying.'}
        </span>
        <button type="button" className="button button--small" onClick={() => void copy()}>
          Copy
        </button>
      </div>
      {/* A scrolling region must be reachable by keyboard, so it is focusable and named. */}
      <pre className="code-block" tabIndex={0} aria-label="Response body">
        {formatBody(body)}
      </pre>
    </>
  );
}

/** Indents JSON for reading; anything else (an HTML error page, plain text) is shown as it is. Rendered as text only. */
function formatBody(body: string): string {
  try {
    return JSON.stringify(JSON.parse(body), null, 2);
  } catch {
    return body;
  }
}
