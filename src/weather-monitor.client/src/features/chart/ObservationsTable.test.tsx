import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { TemperaturePoint } from '../../api/types';
import { series } from '../../test/fixtures';
import { OBSERVATIONS_PAGE_SIZE, ObservationsTable } from './ObservationsTable';

function points(count: number): TemperaturePoint[] {
  const start = Date.parse('2026-10-01T00:00:00Z');
  return Array.from({ length: count }, (_, index) => ({
    observedAtUtc: new Date(start + index * 600_000).toISOString(),
    temperature: 5,
    temperatureMin: 4,
    temperatureMax: 6,
  }));
}

describe('ObservationsTable', () => {
  it('lists observations newest first', () => {
    render(<ObservationsTable cities={[series()]} />);

    const rows = within(screen.getByRole('table')).getAllByRole('row');
    expect(rows).toHaveLength(3);
    expect(rows[1]).toHaveTextContent('7.4 °C');
    expect(screen.queryByRole('button', { name: 'Show more' })).not.toBeInTheDocument();
  });

  it('renders long periods in pages and shows more on request', async () => {
    render(<ObservationsTable cities={[series({ points: points(1_200) })]} />);

    const table = screen.getByRole('table');
    expect(within(table).getAllByRole('row')).toHaveLength(OBSERVATIONS_PAGE_SIZE + 1);
    expect(screen.getByText('Showing 500 of 1,200 observations')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Show more' }));

    expect(within(table).getAllByRole('row')).toHaveLength(2 * OBSERVATIONS_PAGE_SIZE + 1);
  });
});
