import { describe, expect, it } from 'vitest';
import { fromLocalInputValue, resolveRange, toLocalInputValue, validateRange } from './dateRange';

const now = new Date('2026-10-19T12:00:00Z');

describe('resolveRange', () => {
  it('resolves a rolling preset against the given time', () => {
    const range = resolveRange({ kind: 'preset', presetId: '6h' }, now);

    expect(range.to).toEqual(now);
    expect(now.getTime() - range.from.getTime()).toBe(6 * 60 * 60 * 1000);
  });

  it('returns a custom range unchanged', () => {
    const custom = { from: new Date('2026-10-01T00:00:00Z'), to: new Date('2026-10-02T00:00:00Z') };

    expect(resolveRange({ kind: 'custom', range: custom }, now)).toBe(custom);
  });
});

describe('validateRange', () => {
  it('accepts a normal range', () => {
    expect(validateRange({ from: new Date('2026-10-18T12:00:00Z'), to: now })).toBeNull();
  });

  it('rejects a start that is not before the end', () => {
    expect(validateRange({ from: now, to: now })).toMatch(/earlier/);
  });

  it('rejects a range longer than 31 days', () => {
    expect(validateRange({ from: new Date('2026-09-01T00:00:00Z'), to: now })).toMatch(/31 days/);
  });
});

describe('datetime-local conversion', () => {
  it('round-trips a local date to the input format with minute precision', () => {
    const local = new Date(2026, 9, 19, 8, 5, 42);

    const value = toLocalInputValue(local);

    expect(value).toBe('2026-10-19T08:05');
    expect(fromLocalInputValue(value)).toEqual(new Date(2026, 9, 19, 8, 5));
  });

  it('returns null for an empty or invalid value', () => {
    expect(fromLocalInputValue('')).toBeNull();
    expect(fromLocalInputValue('not a date')).toBeNull();
  });
});
