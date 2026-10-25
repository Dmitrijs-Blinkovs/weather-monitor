export interface DateRange {
  from: Date;
  to: Date;
}

const HOUR = 60 * 60 * 1000;
const DAY = 24 * HOUR;

/** Must match WeatherHistoryRules.MaxRange on the server. */
const MAX_RANGE_DAYS = 31;

export const RANGE_PRESETS = [
  { id: '1h', label: 'Last hour', durationMs: HOUR },
  { id: '6h', label: 'Last 6 hours', durationMs: 6 * HOUR },
  { id: '24h', label: 'Last 24 hours', durationMs: DAY },
  { id: '7d', label: 'Last 7 days', durationMs: 7 * DAY },
] as const;

export type RangePresetId = (typeof RANGE_PRESETS)[number]['id'];

/** The range the chart opens with: the most recent readings, a few points per city. */
export const DEFAULT_PRESET_ID: RangePresetId = '1h';

/** Either a rolling preset (re-evaluated against "now" on every refresh) or a fixed custom range. */
export type RangeSelection = { kind: 'preset'; presetId: RangePresetId } | { kind: 'custom'; range: DateRange };

const MINUTE = 60 * 1000;

/**
 * A rolling preset ends at the start of the next minute: every request within the same minute is identical (so the
 * server can answer it from its cache) and still covers everything observed up to now.
 */
export function resolveRange(selection: RangeSelection, now: Date): DateRange {
  if (selection.kind === 'custom') {
    return selection.range;
  }
  const preset =
    RANGE_PRESETS.find((candidate) => candidate.id === selection.presetId) ??
    RANGE_PRESETS.find((candidate) => candidate.id === DEFAULT_PRESET_ID)!;
  const to = Math.floor(now.getTime() / MINUTE) * MINUTE + MINUTE;
  return { from: new Date(to - preset.durationMs), to: new Date(to) };
}

export function validateRange(range: DateRange): string | null {
  if (range.from.getTime() >= range.to.getTime()) {
    return '"From" must be earlier than "To".';
  }
  if (range.to.getTime() - range.from.getTime() > MAX_RANGE_DAYS * DAY) {
    return `The range must not exceed ${MAX_RANGE_DAYS} days.`;
  }
  return null;
}

const pad = (value: number) => String(value).padStart(2, '0');

/** Formats a date for <input type="datetime-local">, which works in local time without a zone. */
export function toLocalInputValue(date: Date): string {
  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
    `T${pad(date.getHours())}:${pad(date.getMinutes())}`
  );
}

/** Parses a datetime-local value ("2026-10-19T14:05") as local time; returns null when empty or invalid. */
export function fromLocalInputValue(value: string): Date | null {
  if (!value) {
    return null;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}
