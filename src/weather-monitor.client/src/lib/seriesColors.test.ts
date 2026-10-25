import { describe, expect, it } from 'vitest';
import { colorForCity, knownCities } from './seriesColors';

describe('colorForCity', () => {
  const allCities = ['London', 'Riga', 'Rome'];

  it('assigns slots in the fixed order of the known cities', () => {
    expect(allCities.map((city) => colorForCity(city, allCities))).toEqual([
      'var(--series-1)',
      'var(--series-2)',
      'var(--series-3)',
    ]);
  });

  it('keeps a city on its colour regardless of which other cities are in the result', () => {
    expect(colorForCity('Rome', allCities)).toBe(colorForCity('rome', allCities));
    expect(colorForCity('Rome', allCities)).toBe('var(--series-3)');
  });

  it('uses the neutral slot for an unknown city instead of inventing a hue', () => {
    expect(colorForCity('Paris', allCities)).toBe('var(--series-other)');
  });
});

describe('knownCities', () => {
  it('takes the cities of the current result while the list of known cities is still empty', () => {
    expect(knownCities([], ['Rome', 'London', 'Riga'])).toEqual(['London', 'Riga', 'Rome']);
  });

  it('merges both lists alphabetically without duplicates, whatever the case', () => {
    expect(knownCities(['London', 'Rome'], ['riga', 'rome'])).toEqual(['London', 'riga', 'Rome']);
  });
});
