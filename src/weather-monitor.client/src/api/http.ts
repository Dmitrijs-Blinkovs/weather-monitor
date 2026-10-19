export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

type QueryParams = Record<string, string | number | undefined>;

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function buildUrl(path: string, params: QueryParams = {}): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') {
      query.set(key, String(value));
    }
  }
  const queryString = query.toString();
  return queryString ? `${path}?${queryString}` : path;
}

export async function getJson<T>(path: string, params?: QueryParams, signal?: AbortSignal): Promise<T> {
  const response = await fetch(buildUrl(path, params), {
    headers: { Accept: 'application/json' },
    signal,
  });

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorMessage(response));
  }

  return (await response.json()) as T;
}

/** Turns an RFC 7807 problem response into a readable message, preferring validation errors. */
async function readErrorMessage(response: Response): Promise<string> {
  const fallback = `Request failed with status ${response.status}`;
  try {
    const problem = (await response.json()) as ProblemDetails;
    const validationErrors = problem.errors ? Object.values(problem.errors).flat() : [];
    if (validationErrors.length > 0) {
      return validationErrors.join(' ');
    }
    return problem.detail ?? problem.title ?? fallback;
  } catch {
    return fallback;
  }
}
