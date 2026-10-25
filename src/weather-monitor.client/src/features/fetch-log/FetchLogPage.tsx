import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import type { FetchLogEntry, FetchLogFilter, FetchStatus } from '../../api/types';
import { useWeatherApi } from '../../api/WeatherApiContext';
import { ErrorNotice } from '../../components/ErrorNotice';
import { PageHeader } from '../../components/PageHeader';
import { Pagination } from '../../components/Pagination';
import { useCities } from '../../hooks/useCities';
import { formatDateTimeWithSeconds } from '../../lib/format';
import { AttemptDetailsDialog } from './AttemptDetailsDialog';
import { FetchLogTable } from './FetchLogTable';

const PAGE_SIZES = [25, 50, 100] as const;
const REFRESH_MS = 30_000;

export function FetchLogPage() {
  const api = useWeatherApi();
  const [filter, setFilter] = useState<FetchLogFilter>({ page: 1, pageSize: PAGE_SIZES[0] });
  const [selected, setSelected] = useState<FetchLogEntry | null>(null);

  const cities = useCities();
  const log = useQuery({
    queryKey: ['fetch-log', filter],
    queryFn: ({ signal }) => api.getFetchLog(filter, signal),
    placeholderData: keepPreviousData,
    refetchInterval: REFRESH_MS,
  });

  // Changing a filter returns to the first page; paging keeps the filters.
  const updateFilter = (changes: Partial<FetchLogFilter>) =>
    setFilter((current) => ({ ...current, page: 1, ...changes }));

  return (
    <>
      <PageHeader
        title="Fetch log"
        description="Every attempt to fetch the weather, one per city per minute, newest first."
        aside={
          log.dataUpdatedAt > 0 && (
            <span className="muted">Refreshed {formatDateTimeWithSeconds(log.dataUpdatedAt)}</span>
          )
        }
      />

      <div className="filter-bar" role="group" aria-label="Fetch log filters">
        <label className="field">
          <span className="field__label">City</span>
          <select value={filter.city ?? ''} onChange={(event) => updateFilter({ city: event.target.value || undefined })}>
            <option value="">All cities</option>
            {cities.data?.map((city) => (
              <option key={city} value={city}>
                {city}
              </option>
            ))}
          </select>
        </label>

        <label className="field">
          <span className="field__label">Result</span>
          <select
            value={filter.status ?? ''}
            onChange={(event) => updateFilter({ status: (event.target.value || undefined) as FetchStatus | undefined })}
          >
            <option value="">All results</option>
            <option value="Success">Success</option>
            <option value="Failure">Failed</option>
          </select>
        </label>

        <label className="field">
          <span className="field__label">Rows</span>
          <select value={filter.pageSize} onChange={(event) => updateFilter({ pageSize: Number(event.target.value) })}>
            {PAGE_SIZES.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
      </div>

      {log.isError && <ErrorNotice error={log.error} onRetry={() => void log.refetch()} />}
      {log.isPending && <p className="muted">Loading fetch log…</p>}

      {log.data && (
        <section className={`card${log.isPlaceholderData ? ' is-refreshing' : ''}`} aria-label="Fetch attempts">
          <FetchLogTable entries={log.data.items} onShowDetails={setSelected} />
          <Pagination
            page={log.data.page}
            totalPages={log.data.totalPages}
            totalCount={log.data.totalCount}
            onPageChange={(page) => setFilter((current) => ({ ...current, page }))}
            label="Fetch log pages"
            itemsLabel="attempts"
            beyondLimitHint="filter by city or result to reach older entries"
          />
        </section>
      )}

      <AttemptDetailsDialog entry={selected} onClose={() => setSelected(null)} />
    </>
  );
}
