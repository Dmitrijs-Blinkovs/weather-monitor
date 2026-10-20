import { describe, expect, it } from 'vitest';
import { formatAxisTick, formatDateTime, formatDateTimeWithSeconds, formatInteger, formatTemperature } from './format';

// Noon UTC on the 19th stays on the 19th in every time zone from UTC-11 to UTC+11.
const noonUtc = '2026-10-19T12:00:00Z';

describe('format', () => {
  it('formats dates in English with a 24-hour clock, whatever the browser language', () => {
    expect(formatDateTime(noonUtc)).toMatch(/^19 Oct 2026, \d{2}:\d{2}$/);
    expect(formatDateTimeWithSeconds(noonUtc)).toMatch(/^19 Oct 2026, \d{2}:\d{2}:00$/);
  });

  it('adds the day to axis ticks only for long ranges', () => {
    const time = Date.parse(noonUtc);

    expect(formatAxisTick(time, 60 * 60 * 1000)).toMatch(/^\d{2}:\d{2}$/);
    expect(formatAxisTick(time, 7 * 24 * 60 * 60 * 1000)).toMatch(/^19 Oct, \d{2}:\d{2}$/);
  });

  it('formats temperatures with one decimal and a dot', () => {
    expect(formatTemperature(7.36)).toBe('7.4 °C');
    expect(formatTemperature(-0.04)).toBe('-0.0 °C');
  });

  it('groups thousands the English way', () => {
    expect(formatInteger(12345)).toBe('12,345');
  });
});
