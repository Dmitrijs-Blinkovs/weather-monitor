import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeApi, history, series } from '../../test/fixtures';
import { renderWithProviders } from '../../test/renderWithProviders';
import { ChartPage } from './ChartPage';

describe('ChartPage', () => {
  it('requests the last 24 hours by default and shows a card per city', async () => {
    const api = fakeApi({
      getHistory: vi.fn().mockResolvedValue(history([series(), series({ city: 'Rome', country: 'IT' })])),
    });

    renderWithProviders(<ChartPage />, api);

    const cards = await screen.findByRole('list', { name: 'Cities in the selected period' });
    expect(within(cards).getAllByRole('listitem')).toHaveLength(2);

    const [range] = vi.mocked(api.getHistory).mock.calls[0]!;
    expect(range.to.getTime() - range.from.getTime()).toBe(24 * 60 * 60 * 1000);
  });

  it('can show the same data as a table', async () => {
    const api = fakeApi({ getHistory: vi.fn().mockResolvedValue(history([series()])) });
    renderWithProviders(<ChartPage />, api);
    await screen.findByRole('list', { name: 'Cities in the selected period' });

    await userEvent.click(screen.getByLabelText('Show as table'));

    const table = screen.getByRole('table');
    expect(within(table).getAllByRole('row')).toHaveLength(3);
    expect(within(table).getByText('6.9 °C')).toBeInTheDocument();
  });

  it('explains when the period has no data yet', async () => {
    renderWithProviders(<ChartPage />, fakeApi());

    expect(await screen.findByText(/No weather data in this period yet/)).toBeInTheDocument();
  });
});
