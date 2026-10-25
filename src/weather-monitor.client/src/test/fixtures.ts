import { vi } from 'vitest';
import type { WeatherApi } from '../api/weatherApi';
import type {
  CityTemperatureSeries,
  FetchAttemptDetails,
  FetchLogEntry,
  PagedResult,
  WeatherHistory,
  WeatherReading,
} from '../api/types';

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
    durationMs: 142,
    ...overrides,
  };
}

export function attemptDetails(overrides: Partial<FetchAttemptDetails> = {}): FetchAttemptDetails {
  return { ...logEntry(), errorMessage: null, responseBody: '{"name":"London","main":{"temp":9.5}}', ...overrides };
}

export function reading(overrides: Partial<WeatherReading> = {}): WeatherReading {
  return {
    id: 1,
    fetchedAtUtc: '2026-10-19T11:59:00Z',
    city: 'Riga',
    country: 'LV',
    temperature: 7.4,
    temperatureMin: 6.8,
    temperatureMax: 8,
    observedAtUtc: '2026-10-19T11:50:00Z',
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
    getReadings: vi.fn().mockResolvedValue(page([])),
    getFetchLog: vi.fn().mockResolvedValue(page([])),
    getFetchAttempt: vi.fn().mockResolvedValue(attemptDetails()),
    getCities: vi.fn().mockResolvedValue(['London', 'Riga', 'Rome']),
    ...overrides,
  };
}
