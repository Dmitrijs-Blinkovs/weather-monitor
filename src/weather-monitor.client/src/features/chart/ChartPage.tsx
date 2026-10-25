import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { ErrorNotice } from '../../components/ErrorNotice';
import { PageHeader } from '../../components/PageHeader';
import { useCities } from '../../hooks/useCities';
import { DEFAULT_PRESET_ID, resolveRange, type RangeSelection } from '../../lib/dateRange';
import { formatDateTime } from '../../lib/format';
import { knownCities } from '../../lib/seriesColors';
import { useWeatherApi } from '../../api/WeatherApiContext';
import { CitySummaryCards } from './CitySummaryCards';
import { DateRangeFilter } from './DateRangeFilter';
import { ReadingsTable } from './ReadingsTable';
import { TemperatureChart } from './TemperatureChart';

/** Rolling presets are re-evaluated against "now" on every refresh, so the chart keeps moving. */
const PRESET_REFRESH_MS = 60_000;

export function ChartPage() {
  const api = useWeatherApi();
  const [selection, setSelection] = useState<RangeSelection>({ kind: 'preset', presetId: DEFAULT_PRESET_ID });
  const [showMinMax, setShowMinMax] = useState(true);
  const [showTable, setShowTable] = useState(false);

  const cities = useCities();
  const history = useQuery({
    queryKey: ['weather-history', selection],
    queryFn: ({ signal }) => api.getHistory(resolveRange(selection, new Date()), signal),
    placeholderData: keepPreviousData,
    refetchInterval: selection.kind === 'preset' ? PRESET_REFRESH_MS : false,
  });

  const allCities = knownCities(cities.data ?? [], history.data?.cities.map((city) => city.city) ?? []);
  const data = history.data;

  return (
    <>
      <PageHeader
        title="Temperature"
        description="Temperature of every city over time, with the reported min/max band, country and last update time."
        aside={
          history.dataUpdatedAt > 0 && (
            <span className="muted">Refreshed {formatDateTime(history.dataUpdatedAt)}</span>
          )
        }
      />

      <DateRangeFilter selection={selection} onChange={setSelection} />

      {history.isError && <ErrorNotice error={history.error} onRetry={() => void history.refetch()} />}

      {history.isPending && <p className="muted">Loading temperatures…</p>}

      {data && data.cities.length === 0 && (
        <p className="empty-state">
          No weather data in this period yet. The function stores a new reading for every city once a minute.
        </p>
      )}

      {data && data.cities.length > 0 && (
        <div className={history.isPlaceholderData || history.isFetching ? 'is-refreshing' : undefined}>
          <CitySummaryCards cities={data.cities} allCities={allCities} />

          <section className="card" aria-labelledby="chart-title">
            <div className="card__header">
              <h2 id="chart-title" className="card__title">
                Temperature by city
              </h2>
              <div className="card__actions">
                <label className="toggle">
                  <input type="checkbox" checked={showMinMax} onChange={(event) => setShowMinMax(event.target.checked)} />
                  Min/max band
                </label>
                <label className="toggle">
                  <input type="checkbox" checked={showTable} onChange={(event) => setShowTable(event.target.checked)} />
                  Show as table
                </label>
              </div>
            </div>

            {showTable ? (
              <ReadingsTable selection={selection} />
            ) : (
              <figure className="chart" aria-label="Line chart of temperature by city over the selected period">
                <TemperatureChart history={data} allCities={allCities} showMinMax={showMinMax} />
                <figcaption className="chart__caption">
                  Lines show the reported temperature. The shaded band spans the provider's current min and max
                  temperature within each city area (not daily extremes).
                </figcaption>
              </figure>
            )}
          </section>
        </div>
      )}
    </>
  );
}
