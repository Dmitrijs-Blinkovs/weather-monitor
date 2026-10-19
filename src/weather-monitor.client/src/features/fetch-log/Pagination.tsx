interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, totalCount, onPageChange }: PaginationProps) {
  const lastPage = Math.max(totalPages, 1);

  return (
    <nav className="pagination" aria-label="Fetch log pages">
      <button type="button" className="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        Previous
      </button>
      <span className="pagination__status">
        Page {page} of {lastPage} · {totalCount.toLocaleString()} attempts
      </span>
      <button type="button" className="button" disabled={page >= lastPage} onClick={() => onPageChange(page + 1)}>
        Next
      </button>
    </nav>
  );
}
