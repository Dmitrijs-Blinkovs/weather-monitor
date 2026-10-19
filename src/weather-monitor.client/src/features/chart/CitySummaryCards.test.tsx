import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { series } from '../../test/fixtures';
import { CitySummaryCards } from './CitySummaryCards';

describe('CitySummaryCards', () => {
  it('shows city, country, temperature, min/max and last update time for every city', () => {
    render(
      <CitySummaryCards
        cities={[series(), series({ city: 'Rome', country: 'IT', latestTemperature: 18.2 })]}
        allCities={['Riga', 'Rome']}
      />,
    );

    const cards = screen.getAllByRole('listitem');
    expect(cards).toHaveLength(2);

    const riga = within(cards[0]!);
    expect(riga.getByRole('heading', { name: 'Riga' })).toBeInTheDocument();
    expect(riga.getByText('LV')).toBeInTheDocument();
    expect(riga.getByText('7.4 °C')).toBeInTheDocument();
    expect(riga.getByText('3.1 °C')).toBeInTheDocument();
    expect(riga.getByText('9.6 °C')).toBeInTheDocument();
    expect(riga.getByText('Last update')).toBeInTheDocument();
    expect(cards[0]!.querySelector('time')).toHaveAttribute('dateTime', '2026-10-19T11:50:00Z');

    expect(within(cards[1]!).getByText('18.2 °C')).toBeInTheDocument();
  });
});
