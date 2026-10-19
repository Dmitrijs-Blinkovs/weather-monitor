// Mirrors the DTOs returned by the ASP.NET Core API (camelCase JSON, UTC ISO-8601 timestamps).

export type FetchStatus = 'Success' | 'Failure';

export interface FetchLogEntry {
  id: number;
  city: string;
  attemptedAtUtc: string;
  isSuccess: boolean;
  httpStatusCode: number | null;
  errorMessage: string | null;
  durationMs: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface FetchLogFilter {
  page: number;
  pageSize: number;
  city?: string;
  status?: FetchStatus;
}

export interface TemperaturePoint {
  observedAtUtc: string;
  temperature: number;
  temperatureMin: number;
  temperatureMax: number;
}

export interface CityTemperatureSeries {
  city: string;
  country: string;
  latestTemperature: number;
  /** Lowest temp_min in the requested period. */
  minTemperature: number;
  /** Highest temp_max in the requested period. */
  maxTemperature: number;
  lastUpdatedUtc: string;
  points: TemperaturePoint[];
}

export interface WeatherHistory {
  fromUtc: string;
  toUtc: string;
  cities: CityTemperatureSeries[];
}
