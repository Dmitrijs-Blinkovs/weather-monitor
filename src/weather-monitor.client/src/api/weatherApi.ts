import type { DateRange } from '../lib/dateRange';
import { getJson } from './http';
import type {
  FetchAttemptDetails,
  FetchLogEntry,
  FetchLogFilter,
  PagedResult,
  PageRequest,
  WeatherHistory,
  WeatherReading,
} from './types';

export const weatherApi = {
  getHistory(range: DateRange, signal?: AbortSignal): Promise<WeatherHistory> {
    return getJson<WeatherHistory>(
      '/api/weather/history',
      { from: range.from.toISOString(), to: range.to.toISOString() },
      signal,
    );
  },

  getReadings(range: DateRange, paging: PageRequest, signal?: AbortSignal): Promise<PagedResult<WeatherReading>> {
    return getJson<PagedResult<WeatherReading>>(
      '/api/weather/readings',
      { from: range.from.toISOString(), to: range.to.toISOString(), page: paging.page, pageSize: paging.pageSize },
      signal,
    );
  },

  getFetchLog(filter: FetchLogFilter, signal?: AbortSignal): Promise<PagedResult<FetchLogEntry>> {
    return getJson<PagedResult<FetchLogEntry>>(
      '/api/logs',
      { page: filter.page, pageSize: filter.pageSize, city: filter.city, status: filter.status },
      signal,
    );
  },

  getFetchAttempt(id: number, signal?: AbortSignal): Promise<FetchAttemptDetails> {
    return getJson<FetchAttemptDetails>(`/api/logs/${id}`, undefined, signal);
  },

  getCities(signal?: AbortSignal): Promise<string[]> {
    return getJson<string[]>('/api/cities', undefined, signal);
  },
};

export type WeatherApi = typeof weatherApi;
