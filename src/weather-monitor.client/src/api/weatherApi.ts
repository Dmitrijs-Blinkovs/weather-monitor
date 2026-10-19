import type { DateRange } from '../lib/dateRange';
import { getJson } from './http';
import type { FetchLogEntry, FetchLogFilter, PagedResult, WeatherHistory } from './types';

export const weatherApi = {
  getHistory(range: DateRange, signal?: AbortSignal): Promise<WeatherHistory> {
    return getJson<WeatherHistory>(
      '/api/weather/history',
      { from: range.from.toISOString(), to: range.to.toISOString() },
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

  getCities(signal?: AbortSignal): Promise<string[]> {
    return getJson<string[]>('/api/cities', undefined, signal);
  },
};

export type WeatherApi = typeof weatherApi;
