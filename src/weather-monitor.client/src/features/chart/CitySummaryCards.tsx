import type { CityTemperatureSeries } from '../../api/types';
import { formatDateTime, formatTemperature } from '../../lib/format';
import { colorForCity } from '../../lib/seriesColors';

interface CitySummaryCardsProps {
  cities: readonly CityTemperatureSeries[];
  allCities: readonly string[];
}

/** One card per city in the API's order (alphabetical), the same order as the legend and the tooltip rows. */
export function CitySummaryCards({ cities, allCities }: CitySummaryCardsProps) {
  return (
    <ul className="city-cards" aria-label="Cities in the selected period">
      {cities.map((city) => (
        <li key={city.city} className="city-card">
          <div className="city-card__heading">
            <span
              className="series-key series-key--line"
              style={{ background: colorForCity(city.city, allCities) }}
              aria-hidden="true"
            />
            <h3 className="city-card__city">{city.city}</h3>
            <span className="city-card__country" title="Country">
              {city.country}
            </span>
          </div>
          <p className="city-card__temperature" aria-label={`Temperature ${formatTemperature(city.latestTemperature)}`}>
            {formatTemperature(city.latestTemperature)}
          </p>
          <dl className="city-card__facts">
            <div>
              <dt>Min</dt>
              <dd>{formatTemperature(city.minTemperature)}</dd>
            </div>
            <div>
              <dt>Max</dt>
              <dd>{formatTemperature(city.maxTemperature)}</dd>
            </div>
            <div>
              <dt>Last update</dt>
              <dd>
                <time dateTime={city.lastUpdatedUtc}>{formatDateTime(city.lastUpdatedUtc)}</time>
              </dd>
            </div>
          </dl>
        </li>
      ))}
    </ul>
  );
}
