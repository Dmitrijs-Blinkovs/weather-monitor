import { vi } from 'vitest';
import type { WeatherApi } from '../api/weatherApi';
import type { CityTemperatureSeries, FetchLogEntry, PagedResult, WeatherHistory } from '../api/types';

export function series(overrides: Partial<CityTemperatureSeries> = {}): CityTemperatureSeries {
  return {
    city: 'Riga',
    country: 'LV',
    latestTemperature: 7.4,
    minTemperature: 3.1,
    maxTemperature: 9.6,
    lastUpdatedUtc: '2026-10-19T11:50:00Z',
    points: [
      { observedAtUtc: '2026-10-19T11:30:00Z', temperature: 6.9, temperatureMin: 6, temperatureMax: 7.5 },
      { observedAtUtc: '2026-10-19T11:50:00Z', temperature: 7.4, temperatureMin: 6.8, temperatureMax: 8 },
    ],
    ...overrides,
  };
}

export function history(cities: CityTemperatureSeries[]): WeatherHistory {
  return { fromUtc: '2026-10-18T12:00:00Z', toUtc: '2026-10-19T12:00:00Z', cities };
}

export function logEntry(overrides: Partial<FetchLogEntry> = {}): FetchLogEntry {
  return {
    id: 1,
    city: 'London',
    attemptedAtUtc: '2026-10-19T11:59:00Z',
    isSuccess: true,
    httpStatusCode: 200,
    errorMessage: null,
    durationMs: 142,
    ...overrides,
  };
}

export function page<T>(items: T[], totalCount = items.length, pageNumber = 1, pageSize = 25): PagedResult<T> {
  return { items, page: pageNumber, pageSize, totalCount, totalPages: Math.ceil(totalCount / pageSize) };
}

/** A fake API whose methods are spies; tests override the responses they care about. */
export function fakeApi(overrides: Partial<WeatherApi> = {}): WeatherApi {
  return {
    getHistory: vi.fn().mockResolvedValue(history([])),
    getFetchLog: vi.fn().mockResolvedValue(page([])),
    getCities: vi.fn().mockResolvedValue(['London', 'Riga', 'Rome']),
    ...overrides,
  };
}
