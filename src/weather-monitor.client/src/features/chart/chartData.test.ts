import { describe, expect, it } from 'vitest';
import { series } from '../../test/fixtures';
import { lastRowIndexFor, pointAt, rangeColumn, temperatureColumn, timeTicks, toChartRows } from './chartData';

const london = series({
  city: 'London',
  points: [
    { observedAtUtc: '2026-10-19T10:00:00Z', temperature: 10, temperatureMin: 9, temperatureMax: 11 },
    { observedAtUtc: '2026-10-19T10:20:00Z', temperature: 11, temperatureMin: 10, temperatureMax: 12 },
  ],
});
const riga = series({
  city: 'Riga',
  points: [{ observedAtUtc: '2026-10-19T10:10:00Z', temperature: 5, temperatureMin: 4, temperatureMax: 6 }],
});

describe('toChartRows', () => {
  it('merges the cities into one row per observation time, sorted by time', () => {
    const rows = toChartRows([london, riga]);

    expect(rows.map((row) => new Date(row.time).toISOString())).toEqual([
      '2026-10-19T10:00:00.000Z',
      '2026-10-19T10:10:00.000Z',
      '2026-10-19T10:20:00.000Z',
    ]);
    expect(rows[0]).toEqual({ time: Date.parse('2026-10-19T10:00:00Z'), temperature_0: 10, range_0: [9, 11] });
    expect(rows[1]?.[temperatureColumn(1)]).toBe(5);
    expect(rows[1]?.[temperatureColumn(0)]).toBeUndefined();
    expect(rows[1]?.[rangeColumn(1)]).toEqual([4, 6]);
  });

  it('puts cities observed at the same moment into the same row', () => {
    const sameTime = series({ city: 'Rome', points: [{ ...london.points[0]!, temperature: 18 }] });

    const rows = toChartRows([london, sameTime]);

    expect(rows[0]).toMatchObject({ temperature_0: 10, temperature_1: 18 });
  });

  it('returns no rows for no data', () => {
    expect(toChartRows([])).toEqual([]);
  });
});

describe('pointAt', () => {
  it('returns the observation that was current at the given time', () => {
    expect(pointAt(london.points, Date.parse('2026-10-19T10:15:00Z'))?.temperature).toBe(10);
    expect(pointAt(london.points, Date.parse('2026-10-19T10:20:00Z'))?.temperature).toBe(11);
    expect(pointAt(london.points, Date.parse('2026-10-19T23:00:00Z'))?.temperature).toBe(11);
  });

  it('returns nothing before the first observation', () => {
    expect(pointAt(london.points, Date.parse('2026-10-19T09:59:00Z'))).toBeUndefined();
  });
});

describe('timeTicks', () => {
  it('spreads the ticks evenly over the whole range, rounded to minutes', () => {
    const from = Date.parse('2026-10-19T09:20:12Z');
    const to = Date.parse('2026-10-19T10:20:12Z');

    expect(timeTicks(from, to).map((tick) => new Date(tick).toISOString().slice(11, 16))).toEqual([
      '09:20',
      '09:32',
      '09:44',
      '09:56',
      '10:08',
      '10:20',
    ]);
  });

  it('returns no ticks for an empty range', () => {
    expect(timeTicks(1000, 1000)).toEqual([]);
  });
});

describe('lastRowIndexFor', () => {
  it('finds the last row that has a value for the city', () => {
    const rows = toChartRows([london, riga]);

    expect(lastRowIndexFor(rows, 0)).toBe(2);
    expect(lastRowIndexFor(rows, 1)).toBe(1);
    expect(lastRowIndexFor(rows, 5)).toBe(-1);
  });
});
