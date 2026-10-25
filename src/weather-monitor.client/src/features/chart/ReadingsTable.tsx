import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useWeatherApi } from '../../api/WeatherApiContext';
import { ErrorNotice } from '../../components/ErrorNotice';
import { Pagination } from '../../components/Pagination';
import { resolveRange, type RangeSelection } from '../../lib/dateRange';
import { formatDateTime, formatDateTimeWithSeconds, formatTemperature } from '../../lib/format';

/** Three cities x 20 minutes, so a page keeps each minute's fetches together. */
export const READINGS_PAGE_SIZE = 60;

/** New readings arrive every minute; refreshing twice as often shows them soon after they are stored. */
const REFRESH_MS = 30_000;

interface ReadingsTableProps {
  selection: RangeSelection;
}

/**
 * Every response the function stored in the selected period, newest first: the accessible, colour-free view of the
 * chart's data, and the proof that a fetch arrived even when the provider's observation did not change.
 */
export function ReadingsTable({ selection }: ReadingsTableProps) {
  const api = useWeatherApi();
  // The page belongs to the period it was chosen in: another period starts again at page 1.
  const [paging, setPaging] = useState({ selection, page: 1 });
  const page = paging.selection === selection ? paging.page : 1;

  const readings = useQuery({
    queryKey: ['weather-readings', selection, page],
    queryFn: ({ signal }) =>
      api.getReadings(resolveRange(selection, new Date()), { page, pageSize: READINGS_PAGE_SIZE }, signal),
    placeholderData: keepPreviousData,
    refetchInterval: selection.kind === 'preset' ? REFRESH_MS : false,
  });

  return (
    <>
      <p className="table-note">
        One row per city for every fetch (once a minute), newest first. The provider refreshes its observation only
        every 10-20 minutes, so consecutive rows of a city often repeat the same values and the same last update.
      </p>

      {readings.isError && <ErrorNotice error={readings.error} onRetry={() => void readings.refetch()} />}
      {readings.isPending && <p className="muted">Loading readings…</p>}

      {readings.data && readings.data.totalCount === 0 && <p className="empty-state">No readings in this period.</p>}

      {readings.data && readings.data.totalCount > 0 && (
        <div className={readings.isPlaceholderData ? 'is-refreshing' : undefined}>
          {/* A scrolling region must be reachable by keyboard, so it is focusable and named. */}
          <div className="table-wrap table-wrap--scroll" role="region" aria-label="Weather readings" tabIndex={0}>
            <table className="data-table">
              <caption className="visually-hidden">Weather readings, one per city and fetch, newest first</caption>
              <thead>
                <tr>
                  <th scope="col">Fetched at</th>
                  <th scope="col">City</th>
                  <th scope="col">Country</th>
                  <th scope="col" className="numeric">
                    Temperature
                  </th>
                  <th scope="col" className="numeric">
                    Min
                  </th>
                  <th scope="col" className="numeric">
                    Max
                  </th>
                  <th scope="col">Last update</th>
                </tr>
              </thead>
              <tbody>
                {readings.data.items.map((reading) => (
                  <tr key={reading.id}>
                    <td>
                      <time dateTime={reading.fetchedAtUtc}>{formatDateTimeWithSeconds(reading.fetchedAtUtc)}</time>
                    </td>
                    <td>{reading.city}</td>
                    <td>{reading.country}</td>
                    <td className="numeric">{formatTemperature(reading.temperature)}</td>
                    <td className="numeric">{formatTemperature(reading.temperatureMin)}</td>
                    <td className="numeric">{formatTemperature(reading.temperatureMax)}</td>
                    <td>
                      <time dateTime={reading.observedAtUtc}>{formatDateTime(reading.observedAtUtc)}</time>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={readings.data.page}
            totalPages={readings.data.totalPages}
            totalCount={readings.data.totalCount}
            onPageChange={(next) => setPaging({ selection, page: next })}
            label="Reading pages"
            itemsLabel="readings"
            beyondLimitHint="choose a shorter period to reach older readings"
          />
        </div>
      )}
    </>
  );
}
