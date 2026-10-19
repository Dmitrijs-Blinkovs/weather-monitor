import type { CityTemperatureSeries } from '../../api/types';
import { formatDateTime, formatTemperature } from '../../lib/format';
import { pointAt } from './chartData';

interface WeatherTooltipProps {
  active?: boolean;
  /** Hovered position on the time axis (epoch milliseconds). */
  time: number;
  cities: readonly CityTemperatureSeries[];
  colors: readonly string[];
}

/** Lists every city's observation that was current at the hovered time, not only the cities observed exactly then. */
export function WeatherTooltip({ active, time, cities, colors }: WeatherTooltipProps) {
  if (!active || !Number.isFinite(time)) {
    return null;
  }

  const rows = cities.flatMap((city, index) => {
    const point = pointAt(city.points, time);
    return point ? [{ city, point, color: colors[index] }] : [];
  });

  if (rows.length === 0) {
    return null;
  }

  return (
    <div className="chart-tooltip">
      <p className="chart-tooltip__time">{formatDateTime(time)}</p>
      <ul className="chart-tooltip__rows">
        {rows.map(({ city, point, color }) => (
          <li key={city.city} className="chart-tooltip__row">
            <span className="series-key series-key--line" style={{ background: color }} aria-hidden="true" />
            <strong className="chart-tooltip__value">{formatTemperature(point.temperature)}</strong>
            <span className="chart-tooltip__name">
              {city.city}, {city.country}
            </span>
            <span className="chart-tooltip__meta">
              min {formatTemperature(point.temperatureMin)} · max {formatTemperature(point.temperatureMax)} · updated{' '}
              {formatDateTime(point.observedAtUtc)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
