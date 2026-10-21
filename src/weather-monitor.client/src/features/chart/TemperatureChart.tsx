import { useMemo } from 'react';
import { Area, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { WeatherHistory } from '../../api/types';
import { formatAxisTick } from '../../lib/format';
import { colorForCity } from '../../lib/seriesColors';
import { lastRowIndexFor, rangeColumn, temperatureColumn, timeTicks, toChartRows } from './chartData';
import { downsample } from './downsample';
import { WeatherTooltip } from './WeatherTooltip';

/** About one point per two pixels of a wide chart; long ranges are thinned to this, the tooltip still uses all data. */
const MAX_POINTS_PER_CITY = 600;

interface TemperatureChartProps {
  history: WeatherHistory;
  /** Every known city, used to keep each city's colour stable across ranges. */
  allCities: readonly string[];
  showMinMax: boolean;
}

const axisTick = { fill: 'var(--text-muted)', fontSize: 12 };

export function TemperatureChart({ history, allCities, showMinMax }: TemperatureChartProps) {
  const rows = useMemo(
    () =>
      toChartRows(
        history.cities.map((city) => ({ ...city, points: downsample(city.points, MAX_POINTS_PER_CITY) })),
      ),
    [history.cities],
  );
  const colors = history.cities.map((city) => colorForCity(city.city, allCities));
  const from = Date.parse(history.fromUtc);
  const to = Date.parse(history.toUtc);

  return (
    <ResponsiveContainer width="100%" height={420}>
      <ComposedChart data={rows} margin={{ top: 12, right: 72, bottom: 4, left: 4 }}>
        <CartesianGrid vertical={false} stroke="var(--grid)" />
        <XAxis
          dataKey="time"
          type="number"
          scale="time"
          domain={[from, to]}
          ticks={timeTicks(from, to)}
          tickFormatter={(value: number) => formatAxisTick(value, to - from)}
          tick={axisTick}
          stroke="var(--axis)"
          tickLine={false}
          minTickGap={32}
        />
        <YAxis
          unit=" °C"
          width={64}
          domain={['auto', 'auto']}
          allowDecimals={false}
          tick={axisTick}
          axisLine={false}
          tickLine={false}
        />
        <Tooltip
          cursor={{ stroke: 'var(--axis)', strokeWidth: 1 }}
          isAnimationActive={false}
          content={({ active, label }) => (
            <WeatherTooltip active={active} time={Number(label)} cities={history.cities} colors={colors} />
          )}
        />
        <Legend
          verticalAlign="top"
          align="left"
          height={32}
          formatter={(value: string) => <span className="chart-legend__label">{value}</span>}
        />

        {showMinMax &&
          history.cities.map((city, index) => (
            <Area
              key={`range-${city.city}`}
              type="monotone"
              dataKey={rangeColumn(index)}
              name={`${city.city} min–max`}
              stroke="none"
              fill={colors[index]}
              fillOpacity={0.14}
              connectNulls
              activeDot={false}
              legendType="none"
              tooltipType="none"
              isAnimationActive={false}
            />
          ))}

        {history.cities.map((city, index) => {
          const lastIndex = lastRowIndexFor(rows, index);
          return (
            <Line
              key={`temperature-${city.city}`}
              type="monotone"
              dataKey={temperatureColumn(index)}
              name={`${city.city}, ${city.country}`}
              stroke={colors[index]}
              strokeWidth={2}
              // A line needs two observations; until the second arrives, the first is shown as a point.
              dot={city.points.length === 1 ? { r: 4, fill: colors[index], stroke: 'var(--surface)', strokeWidth: 2 } : false}
              activeDot={{ r: 4, strokeWidth: 2, stroke: 'var(--surface)' }}
              connectNulls
              isAnimationActive={false}
              label={({ x, y, index: rowIndex }) =>
                rowIndex === lastIndex ? (
                  <text x={Number(x) + 8} y={Number(y)} dy={4} className="chart-direct-label">
                    {city.city}
                  </text>
                ) : (
                  <g />
                )
              }
            />
          );
        })}
      </ComposedChart>
    </ResponsiveContainer>
  );
}
