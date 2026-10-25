import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { series } from '../../test/fixtures';
import { WeatherTooltip } from './WeatherTooltip';

const cities = [
  series({ city: 'Riga', country: 'LV' }),
  series({
    city: 'Rome',
    country: 'IT',
    points: [{ observedAtUtc: '2026-10-19T11:40:00Z', temperature: 18.5, temperatureMin: 17, temperatureMax: 19.5 }],
  }),
];
const colors = ['var(--series-1)', 'var(--series-2)'];

describe('WeatherTooltip', () => {
  it('lists every city with the observation current at the hovered time', () => {
    render(<WeatherTooltip active time={Date.parse('2026-10-19T11:45:00Z')} cities={cities} colors={colors} />);

    expect(screen.getByText('Riga, LV')).toBeInTheDocument();
    expect(screen.getByText('6.9 °C')).toBeInTheDocument();
    expect(screen.getByText('Rome, IT')).toBeInTheDocument();
    expect(screen.getByText('18.5 °C')).toBeInTheDocument();
    expect(screen.getByText(/min 17\.0 °C · max 19\.5 °C/)).toBeInTheDocument();
  });

  it('keeps the alphabetical order of the cities, whatever their temperature', () => {
    const london = series({
      city: 'London',
      country: 'GB',
      points: [{ observedAtUtc: '2026-10-19T11:40:00Z', temperature: 9.6, temperatureMin: 8, temperatureMax: 10 }],
    });
    render(
      <WeatherTooltip
        active
        time={Date.parse('2026-10-19T11:45:00Z')}
        cities={[london, ...cities]}
        colors={[...colors, 'var(--series-3)']}
      />,
    );

    const names = screen.getAllByRole('listitem').map((row) => row.querySelector('.chart-tooltip__name')?.textContent);
    expect(names).toEqual(['London, GB', 'Riga, LV', 'Rome, IT']);
  });

  it('leaves out cities that had no observation yet', () => {
    render(<WeatherTooltip active time={Date.parse('2026-10-19T11:35:00Z')} cities={cities} colors={colors} />);

    expect(screen.getByText('Riga, LV')).toBeInTheDocument();
    expect(screen.queryByText('Rome, IT')).not.toBeInTheDocument();
  });

  it('renders nothing when inactive', () => {
    const { container } = render(<WeatherTooltip active={false} time={0} cities={cities} colors={colors} />);

    expect(container).toBeEmptyDOMElement();
  });
});
