import type { Page } from '@playwright/test';

export const CITIES = [
  { city: 'London', country: 'GB', base: 11 },
  { city: 'Riga', country: 'LV', base: 4 },
  { city: 'Rome', country: 'IT', base: 18 },
] as const;

const TEN_MINUTES = 10 * 60 * 1000;

interface MockOptions {
  /** Status code to answer every /api/logs call with (e.g. 500); omitted = normal data. */
  logsStatus?: number;
}

/** The API URLs the page requested, in order, by endpoint. */
interface MockedApi {
  historyRequests(): URL[];
  readingRequests(): URL[];
  logRequests(): URL[];
  attemptRequests(): URL[];
}

/** Answers /api/* inside the browser with deterministic data shaped like the real API. */
export async function mockApi(page: Page, options: MockOptions = {}): Promise<MockedApi> {
  const requests: URL[] = [];

  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());
    requests.push(url);

    const attemptId = /^\/api\/logs\/(\d+)$/.exec(url.pathname)?.[1];
    if (attemptId) {
      const details = attemptDetails(Number(attemptId));
      return details
        ? route.fulfill({ json: details })
        : route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{"title":"Not Found","status":404}' });
    }

    switch (url.pathname) {
      case '/api/cities':
        return route.fulfill({ json: CITIES.map((c) => c.city) });
      case '/api/weather/history':
        return route.fulfill({ json: history(url) });
      case '/api/weather/readings':
        return route.fulfill({ json: readings(url) });
      case '/api/logs':
        if (options.logsStatus) {
          return route.fulfill({
            status: options.logsStatus,
            contentType: 'application/problem+json',
            body: JSON.stringify({ title: 'Service unavailable', status: options.logsStatus }),
          });
        }
        return route.fulfill({ json: logs(url) });
      default:
        return route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{"status":404}' });
    }
  });

  return {
    historyRequests: () => requests.filter((url) => url.pathname === '/api/weather/history'),
    readingRequests: () => requests.filter((url) => url.pathname === '/api/weather/readings'),
    logRequests: () => requests.filter((url) => url.pathname === '/api/logs'),
    attemptRequests: () => requests.filter((url) => /^\/api\/logs\/\d+$/.test(url.pathname)),
  };
}

/** One observation every 10 minutes for the last two hours of the requested range. */
function history(url: URL) {
  const from = Date.parse(url.searchParams.get('from') ?? '');
  const to = Date.parse(url.searchParams.get('to') ?? '');
  const start = Math.max(from, to - 2 * 60 * 60 * 1000);

  const cities = CITIES.map(({ city, country, base }, cityIndex) => {
    const points = [];
    for (let time = start, step = 0; time <= to; time += TEN_MINUTES, step++) {
      const temperature = round(base + Math.sin(step / 3 + cityIndex) * 1.5);
      points.push({
        observedAtUtc: new Date(time).toISOString(),
        temperature,
        temperatureMin: round(temperature - 1),
        temperatureMax: round(temperature + 1),
      });
    }
    const latest = points[points.length - 1]!;
    return {
      city,
      country,
      latestTemperature: latest.temperature,
      minTemperature: Math.min(...points.map((p) => p.temperatureMin)),
      maxTemperature: Math.max(...points.map((p) => p.temperatureMax)),
      lastUpdatedUtc: latest.observedAtUtc,
      points,
    };
  });

  return { fromUtc: new Date(from).toISOString(), toUtc: new Date(to).toISOString(), cities };
}

/**
 * One reading per city per minute for the last two hours of the requested range, newest first. The observation (and
 * so the temperature) changes only every 10 minutes, as with the real provider.
 */
function readings(url: URL) {
  const from = Date.parse(url.searchParams.get('from') ?? '');
  const to = Date.parse(url.searchParams.get('to') ?? '');
  const page = Number(url.searchParams.get('page') ?? '1');
  const pageSize = Number(url.searchParams.get('pageSize') ?? '50');
  const newest = Math.floor(to / 60_000) * 60_000 - 60_000;
  const oldest = Math.max(from, to - 2 * 60 * 60 * 1000);

  const all = [];
  for (let fetchedAt = newest, minute = 0; fetchedAt >= oldest; fetchedAt -= 60_000, minute++) {
    for (const [cityIndex, { city, country, base }] of CITIES.entries()) {
      const observedAt = Math.floor(fetchedAt / TEN_MINUTES) * TEN_MINUTES;
      const temperature = round(base + Math.sin(observedAt / TEN_MINUTES / 3 + cityIndex) * 1.5);
      all.push({
        id: 1_000_000 - (minute * CITIES.length + cityIndex),
        fetchedAtUtc: new Date(fetchedAt).toISOString(),
        city,
        country,
        temperature,
        temperatureMin: round(temperature - 1),
        temperatureMax: round(temperature + 1),
        observedAtUtc: new Date(observedAt).toISOString(),
      });
    }
  }

  return {
    items: all.slice((page - 1) * pageSize, page * pageSize),
    page,
    pageSize,
    totalCount: all.length,
    totalPages: Math.ceil(all.length / pageSize),
  };
}

/** One page of the log, filtered by city and result like the API. */
function logs(url: URL) {
  const page = Number(url.searchParams.get('page') ?? '1');
  const pageSize = Number(url.searchParams.get('pageSize') ?? '50');
  const city = url.searchParams.get('city');
  const status = url.searchParams.get('status');

  const filtered = logEntries().filter(
    (entry) =>
      (!city || entry.city === city) &&
      (!status || (status === 'Success') === entry.isSuccess),
  );

  return {
    items: filtered.slice((page - 1) * pageSize, page * pageSize),
    page,
    pageSize,
    totalCount: filtered.length,
    totalPages: Math.ceil(filtered.length / pageSize),
  };
}

/** One logged attempt with its error text and response body: the payload of a success, the error body of a failure. */
function attemptDetails(id: number) {
  const entry = logEntries().find((candidate) => candidate.id === id);
  if (!entry) {
    return null;
  }
  const errorMessage = entry.isSuccess
    ? null
    : 'Invalid API key. Please see https://openweathermap.org/faq#error401 for more info.';
  const responseBody = entry.isSuccess
    ? JSON.stringify({ name: entry.city, main: { temp: 11.5, temp_min: 10.5, temp_max: 12.5 }, dt: 1_760_875_200 })
    : JSON.stringify({ cod: 401, message: errorMessage });
  return { ...entry, errorMessage, responseBody };
}

/** 20 runs x 3 cities, newest first; Rome failed with 401 in the two newest runs. */
function logEntries() {
  const newestRun = Date.parse('2026-10-19T12:00:00Z');

  return Array.from({ length: 20 }, (_, run) =>
    CITIES.map(({ city: name }, cityIndex) => {
      const failed = name === 'Rome' && run < 2;
      return {
        id: 1000 - (run * 3 + cityIndex),
        city: name,
        attemptedAtUtc: new Date(newestRun - run * 60_000).toISOString(),
        isSuccess: !failed,
        httpStatusCode: failed ? 401 : 200,
        durationMs: 100 + run + cityIndex,
      };
    }),
  ).flat();
}

const round = (value: number) => Math.round(value * 100) / 100;
