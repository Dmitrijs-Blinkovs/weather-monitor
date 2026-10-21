// Mirrors the DTOs returned by the ASP.NET Core API (camelCase JSON, UTC ISO-8601 timestamps).

export type FetchStatus = 'Success' | 'Failure';

export interface FetchLogEntry {
  id: number;
  city: string;
  attemptedAtUtc: string;
  isSuccess: boolean;
  httpStatusCode: number | null;
  durationMs: number;
}

/** One attempt with its error text and the provider's full response body, loaded on demand. */
export interface FetchAttemptDetails extends FetchLogEntry {
  errorMessage: string | null;
  /** Full payload of a success, body of a failure (API key redacted); null when no response arrived. */
  responseBody: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PageRequest {
  page: number;
  pageSize: number;
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

/** The weather one successful fetch returned: one per city per minute, also when nothing changed. */
export interface WeatherReading {
  /** Id of the fetch attempt, as in the fetch log. */
  id: number;
  /** When the function received the response. */
  fetchedAtUtc: string;
  city: string;
  country: string;
  temperature: number;
  temperatureMin: number;
  temperatureMax: number;
  /** The provider's "last update" time; it repeats until the provider refreshes its observation. */
  observedAtUtc: string;
}
