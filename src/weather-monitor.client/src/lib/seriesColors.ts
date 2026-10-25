// Categorical slots in fixed order, validated for colour-vision deficiency in light and dark mode.
// The values live in index.css as --series-N so that dark mode can step them; here we only refer to roles.
const SERIES_SLOTS = [
  'var(--series-1)',
  'var(--series-2)',
  'var(--series-3)',
  'var(--series-4)',
  'var(--series-5)',
  'var(--series-6)',
  'var(--series-7)',
  'var(--series-8)',
] as const;

const OTHER_SLOT = 'var(--series-other)';

/**
 * Colour follows the city, never its position in the current result: the slot is taken from the
 * full, alphabetically sorted list of known cities, so narrowing the range never repaints a city.
 */
export function colorForCity(city: string, allCities: readonly string[]): string {
  const key = city.toLowerCase();
  const index = allCities.findIndex((candidate) => candidate.toLowerCase() === key);
  return SERIES_SLOTS[index] ?? OTHER_SLOT;
}

/**
 * The cities to assign colours from: the server's list of known cities plus the cities in the current result,
 * alphabetically. Right after the first fetch into an empty database that list is still empty (it is cached for
 * minutes), and every city would otherwise stay grey until it expires.
 */
export function knownCities(listed: readonly string[], shown: readonly string[]): string[] {
  const byKey = new Map<string, string>();
  for (const city of [...listed, ...shown]) {
    if (!byKey.has(city.toLowerCase())) {
      byKey.set(city.toLowerCase(), city);
    }
  }
  return [...byKey.values()].sort((a, b) => a.localeCompare(b, 'en', { sensitivity: 'base' }));
}
