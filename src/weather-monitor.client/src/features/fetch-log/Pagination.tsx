import { formatInteger } from '../../lib/format';

/** Deepest page the API serves (FetchLogQuery.MaxPage on the server); older entries are reached by filtering. */
export const MAX_LOG_PAGE = 1000;

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, totalCount, onPageChange }: PaginationProps) {
  const lastPage = Math.max(Math.min(totalPages, MAX_LOG_PAGE), 1);

  return (
    <nav className="pagination" aria-label="Fetch log pages">
      <button type="button" className="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        Previous
      </button>
      <span className="pagination__status">
        Page {formatInteger(page)} of {formatInteger(lastPage)} · {formatInteger(totalCount)} attempts
        {totalPages > MAX_LOG_PAGE && ' · filter by city or result to reach older entries'}
      </span>
      <button type="button" className="button" disabled={page >= lastPage} onClick={() => onPageChange(page + 1)}>
        Next
      </button>
    </nav>
  );
}
