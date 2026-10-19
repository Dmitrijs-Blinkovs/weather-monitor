interface ErrorNoticeProps {
  error: unknown;
  onRetry?: () => void;
}

export function ErrorNotice({ error, onRetry }: ErrorNoticeProps) {
  const message = error instanceof Error ? error.message : 'Something went wrong.';

  return (
    <div className="notice notice--error" role="alert">
      <span className="notice__icon" aria-hidden="true">
        !
      </span>
      <p className="notice__text">
        <strong>Could not load data.</strong> {message}
      </p>
      {onRetry && (
        <button type="button" className="button" onClick={onRetry}>
          Retry
        </button>
      )}
    </div>
  );
}
