import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import type { RangeSelection } from '../../lib/dateRange';
import { fakeApi, page, reading } from '../../test/fixtures';
import { renderWithProviders } from '../../test/renderWithProviders';
import { READINGS_PAGE_SIZE, ReadingsTable } from './ReadingsTable';

const lastHour: RangeSelection = { kind: 'preset', presetId: '1h' };

/** Lets a test change the period the way the date filter does. */
function WithPeriodSwitch() {
  const [selection, setSelection] = useState<RangeSelection>(lastHour);
  return (
    <>
      <button type="button" onClick={() => setSelection({ kind: 'preset', presetId: '6h' })}>
        Last 6 hours
      </button>
      <ReadingsTable selection={selection} />
    </>
  );
}

describe('ReadingsTable', () => {
  it('lists every fetch, also when the provider returned the same observation again', async () => {
    const api = fakeApi({
      getReadings: vi.fn().mockResolvedValue(
        page([
          reading({ id: 3, fetchedAtUtc: '2026-10-19T11:59:00Z' }),
          reading({ id: 2, fetchedAtUtc: '2026-10-19T11:58:00Z' }),
          reading({ id: 1, fetchedAtUtc: '2026-10-19T11:57:00Z' }),
        ]),
      ),
    });

    renderWithProviders(<ReadingsTable selection={lastHour} />, api);

    const table = await screen.findByRole('table');
    const [header, ...rows] = within(table).getAllByRole('row');
    expect(header).toHaveTextContent('Fetched atCityCountryTemperatureMinMaxLast update');
    expect(rows).toHaveLength(3);
    for (const row of rows) {
      expect(row).toHaveTextContent('RigaLV7.4 °C6.8 °C8.0 °C');
      expect(within(row).getAllByRole('time')[1]).toHaveAttribute('datetime', '2026-10-19T11:50:00Z');
    }
    expect(within(rows[0]!).getAllByRole('time')[0]).toHaveAttribute('datetime', '2026-10-19T11:59:00Z');
    expect(screen.getByText(/One row per city for every fetch/)).toBeInTheDocument();
  });

  it('requests the selected period page by page', async () => {
    const getReadings = vi.fn().mockResolvedValue(page([reading()], 150, 1, READINGS_PAGE_SIZE));
    renderWithProviders(<ReadingsTable selection={lastHour} />, fakeApi({ getReadings }));

    const pages = await screen.findByRole('navigation', { name: 'Reading pages' });
    expect(pages).toHaveTextContent('Page 1 of 3 · 150 readings');
    const [range, paging] = getReadings.mock.calls[0]!;
    expect(range.to.getTime() - range.from.getTime()).toBe(60 * 60 * 1000);
    expect(paging).toEqual({ page: 1, pageSize: READINGS_PAGE_SIZE });

    await userEvent.click(within(pages).getByRole('button', { name: 'Next' }));

    expect(getReadings).toHaveBeenLastCalledWith(
      expect.anything(),
      { page: 2, pageSize: READINGS_PAGE_SIZE },
      expect.anything(),
    );
  });

  it('starts again at the first page when the period changes', async () => {
    const getReadings = vi.fn().mockResolvedValue(page([reading()], 150, 1, READINGS_PAGE_SIZE));
    renderWithProviders(<WithPeriodSwitch />, fakeApi({ getReadings }));
    await userEvent.click(await screen.findByRole('button', { name: 'Next' }));
    await vi.waitFor(() => expect(getReadings).toHaveBeenCalledTimes(2));

    await userEvent.click(screen.getByRole('button', { name: 'Last 6 hours' }));

    await vi.waitFor(() => expect(getReadings).toHaveBeenCalledTimes(3));
    const [range, paging] = getReadings.mock.calls[2]!;
    expect(range.to.getTime() - range.from.getTime()).toBe(6 * 60 * 60 * 1000);
    expect(paging).toEqual({ page: 1, pageSize: READINGS_PAGE_SIZE });
  });

  it('says so when the period has no readings', async () => {
    renderWithProviders(<ReadingsTable selection={lastHour} />, fakeApi());

    expect(await screen.findByText('No readings in this period.')).toBeInTheDocument();
  });
});
