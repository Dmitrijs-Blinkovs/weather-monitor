import { describe, expect, it } from 'vitest';
import type { TemperaturePoint } from '../../api/types';
import { downsample } from './downsample';

function series(count: number, temperatureAt: (index: number) => number): TemperaturePoint[] {
  const start = Date.parse('2026-10-01T00:00:00Z');
  return Array.from({ length: count }, (_, index) => {
    const temperature = temperatureAt(index);
    return {
      observedAtUtc: new Date(start + index * 10 * 60 * 1000).toISOString(),
      temperature,
      temperatureMin: temperature - 1,
      temperatureMax: temperature + 1,
    };
  });
}

describe('downsample', () => {
  it('leaves short series untouched', () => {
    const points = series(50, (i) => i);

    expect(downsample(points, 100)).toEqual(points);
  });

  it('reduces a long series to the threshold and keeps the first and last point', () => {
    const points = series(4_464, (i) => Math.sin(i / 50) * 10); // 31 days of 10-minute observations

    const sampled = downsample(points, 500);

    expect(sampled).toHaveLength(500);
    expect(sampled[0]).toBe(points[0]);
    expect(sampled.at(-1)).toBe(points.at(-1));
  });

  it('keeps a single sharp peak that plain stride sampling would drop', () => {
    const points = series(1_000, (i) => (i === 517 ? 35 : 10));

    const sampled = downsample(points, 50);

    expect(sampled.some((point) => point.temperature === 35)).toBe(true);
  });

  it('keeps the extremes of the min/max band even when the temperature line does not move', () => {
    const points = series(1_200, () => 10).map((point, index) =>
      index === 777 ? { ...point, temperatureMax: 40 } : index === 321 ? { ...point, temperatureMin: -20 } : point,
    );

    const sampled = downsample(points, 600);

    expect(sampled).toHaveLength(600);
    expect(Math.max(...sampled.map((point) => point.temperatureMax))).toBe(40);
    expect(Math.min(...sampled.map((point) => point.temperatureMin))).toBe(-20);
    expect(sampled.every((point) => point.temperature === 10)).toBe(true);
  });

  it('keeps points in time order', () => {
    const sampled = downsample(series(2_000, (i) => (i % 7) - 3), 300);

    const times = sampled.map((point) => Date.parse(point.observedAtUtc));
    expect(times).toEqual([...times].sort((left, right) => left - right));
  });
});
