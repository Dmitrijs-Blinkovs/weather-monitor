import type { CityTemperatureSeries, TemperaturePoint } from '../../api/types';

/** One row per observation time; each city contributes its own temperature and min/max range columns. */
export interface ChartRow {
  time: number;
  [column: string]: number | [number, number] | undefined;
}

// Column names use the city's position rather than its name, so names with dots or spaces are safe.
export const temperatureColumn = (cityIndex: number) => `temperature_${cityIndex}`;
export const rangeColumn = (cityIndex: number) => `range_${cityIndex}`;

/**
 * Recharts draws every series from a single data array, so the per-city series are merged into
 * rows keyed by observation time. Cities are observed at different moments, so most rows hold
 * only some cities; the lines bridge those gaps with connectNulls.
 */
export function toChartRows(cities: readonly CityTemperatureSeries[]): ChartRow[] {
  const rows = new Map<number, ChartRow>();

  cities.forEach((city, cityIndex) => {
    for (const point of city.points) {
      const time = Date.parse(point.observedAtUtc);
      const row = rows.get(time) ?? { time };
      row[temperatureColumn(cityIndex)] = point.temperature;
      row[rangeColumn(cityIndex)] = [point.temperatureMin, point.temperatureMax];
      rows.set(time, row);
    }
  });

  return [...rows.values()].sort((left, right) => left.time - right.time);
}

/**
 * Returns the observation that was current at the given time: the latest point at or before it.
 * Points must be sorted by time ascending (the API guarantees this).
 */
export function pointAt(points: readonly TemperaturePoint[], time: number): TemperaturePoint | undefined {
  let low = 0;
  let high = points.length - 1;
  let found: TemperaturePoint | undefined;

  while (low <= high) {
    const middle = (low + high) >>> 1;
    const candidate = points[middle]!;
    if (Date.parse(candidate.observedAtUtc) <= time) {
      found = candidate;
      low = middle + 1;
    } else {
      high = middle - 1;
    }
  }

  return found;
}

/**
 * Evenly spaced ticks across the whole selected range, so the axis shows the period the user asked for
 * even where there is no data yet. Ticks are rounded to whole minutes.
 */
export function timeTicks(from: number, to: number, count = 6): number[] {
  if (!(to > from) || count < 2) {
    return [];
  }
  const minute = 60_000;
  const step = (to - from) / (count - 1);
  return Array.from({ length: count }, (_, index) => Math.round((from + index * step) / minute) * minute);
}

/** Index of the last row that holds a value for the given city; used to place its direct label. */
export function lastRowIndexFor(rows: readonly ChartRow[], cityIndex: number): number {
  const column = temperatureColumn(cityIndex);
  for (let index = rows.length - 1; index >= 0; index--) {
    if (rows[index]![column] !== undefined) {
      return index;
    }
  }
  return -1;
}
