import { useMemo } from 'react';
import type { CityTemperatureSeries } from '../../api/types';
import { formatDateTime, formatTemperature } from '../../lib/format';

interface ObservationsTableProps {
  cities: readonly CityTemperatureSeries[];
}

/** The chart's data as a table: the accessible, colour-free view of the same observations. */
export function ObservationsTable({ cities }: ObservationsTableProps) {
  const rows = useMemo(
    () =>
      cities
        .flatMap((city) => city.points.map((point) => ({ city, point })))
        .sort((left, right) => right.point.observedAtUtc.localeCompare(left.point.observedAtUtc)),
    [cities],
  );

  return (
    <div className="table-wrap table-wrap--scroll">
      <table className="data-table">
        <caption className="visually-hidden">Weather observations, newest first</caption>
        <thead>
          <tr>
            <th scope="col">Observed at</th>
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
          </tr>
        </thead>
        <tbody>
          {rows.map(({ city, point }) => (
            <tr key={`${city.city}-${point.observedAtUtc}`}>
              <td>
                <time dateTime={point.observedAtUtc}>{formatDateTime(point.observedAtUtc)}</time>
              </td>
              <td>{city.city}</td>
              <td>{city.country}</td>
              <td className="numeric">{formatTemperature(point.temperature)}</td>
              <td className="numeric">{formatTemperature(point.temperatureMin)}</td>
              <td className="numeric">{formatTemperature(point.temperatureMax)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
