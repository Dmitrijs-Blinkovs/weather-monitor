import type { TemperaturePoint } from '../../api/types';

/**
 * Reduces a series to at most `threshold` points with Largest-Triangle-Three-Buckets: it keeps the first and last
 * point and, per bucket, the point that best preserves the line's visual shape (peaks and dips survive).
 * A chart is only a few hundred pixels wide, so drawing 4,000+ points per city for a month adds work, not detail.
 */
export function downsample(points: readonly TemperaturePoint[], threshold: number): TemperaturePoint[] {
  if (threshold < 3 || points.length <= threshold) {
    return [...points];
  }

  const x = (point: TemperaturePoint) => Date.parse(point.observedAtUtc);
  const sampled: TemperaturePoint[] = [points[0]!];
  const bucketSize = (points.length - 2) / (threshold - 2);
  let previous = points[0]!;

  for (let bucket = 0; bucket < threshold - 2; bucket++) {
    const start = Math.floor(bucket * bucketSize) + 1;
    const end = Math.min(Math.floor((bucket + 1) * bucketSize) + 1, points.length - 1);

    // Average of the next bucket: the third corner of the triangle.
    const nextStart = end;
    const nextEnd = Math.min(Math.floor((bucket + 2) * bucketSize) + 1, points.length);
    let averageX = 0;
    let averageY = 0;
    for (let i = nextStart; i < nextEnd; i++) {
      averageX += x(points[i]!);
      averageY += points[i]!.temperature;
    }
    const count = Math.max(nextEnd - nextStart, 1);
    averageX /= count;
    averageY /= count;

    let best = points[start]!;
    let bestArea = -1;
    for (let i = start; i < end; i++) {
      const candidate = points[i]!;
      const area = Math.abs(
        (x(previous) - averageX) * (candidate.temperature - previous.temperature) -
          (x(previous) - x(candidate)) * (averageY - previous.temperature),
      );
      if (area > bestArea) {
        bestArea = area;
        best = candidate;
      }
    }

    sampled.push(best);
    previous = best;
  }

  sampled.push(points[points.length - 1]!);
  return sampled;
}
