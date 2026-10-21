import { formatInteger } from '../lib/format';

/** Deepest page the API serves (PageLimits.MaxPage on the server); older rows are reached by narrowing the list. */
export const MAX_PAGE = 1000;

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  /** Accessible name of the navigation, e.g. "Fetch log pages". */
  label: string;
  /** What is counted, in the plural, e.g. "attempts". */
  itemsLabel: string;
  /** How to reach the rows beyond the deepest page, e.g. "filter by city or result to reach older entries". */
  beyondLimitHint: string;
}

export function Pagination({
  page,
  totalPages,
  totalCount,
  onPageChange,
  label,
  itemsLabel,
  beyondLimitHint,
}: PaginationProps) {
  const lastPage = Math.max(Math.min(totalPages, MAX_PAGE), 1);

  return (
    <nav className="pagination" aria-label={label}>
      <button type="button" className="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        Previous
      </button>
      <span className="pagination__status">
        Page {formatInteger(page)} of {formatInteger(lastPage)} · {formatInteger(totalCount)} {itemsLabel}
        {totalPages > MAX_PAGE && ` · ${beyondLimitHint}`}
      </span>
      <button type="button" className="button" disabled={page >= lastPage} onClick={() => onPageChange(page + 1)}>
        Next
      </button>
    </nav>
  );
}
