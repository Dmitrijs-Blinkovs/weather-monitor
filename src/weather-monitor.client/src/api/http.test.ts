import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, buildUrl, getJson } from './http';

describe('buildUrl', () => {
  it('adds only the parameters that have a value', () => {
    expect(buildUrl('/api/logs', { page: 2, city: 'Riga', status: undefined, empty: '' })).toBe(
      '/api/logs?page=2&city=Riga',
    );
  });

  it('encodes values', () => {
    expect(buildUrl('/api/weather/history', { from: '2026-10-19T10:00:00+03:00' })).toBe(
      '/api/weather/history?from=2026-10-19T10%3A00%3A00%2B03%3A00',
    );
  });

  it('leaves the path alone without parameters', () => {
    expect(buildUrl('/api/cities')).toBe('/api/cities');
  });
});

describe('getJson', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns the parsed body of a successful response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(['London'])));

    await expect(getJson<string[]>('/api/cities')).resolves.toEqual(['London']);
  });

  it('turns validation problems into a readable error', async () => {
    const problem = {
      title: 'One or more validation errors occurred.',
      errors: { From: ["'from' must be earlier than 'to'."] },
    };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(problem, { status: 400 })));

    const error = await getJson('/api/weather/history').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 400, message: "'from' must be earlier than 'to'." });
  });

  it('falls back to the status code when the error body is not JSON', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('oops', { status: 502 })));

    await expect(getJson('/api/logs')).rejects.toThrow('Request failed with status 502');
  });
});
