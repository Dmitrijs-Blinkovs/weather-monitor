import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeApi, logEntry, page } from '../../test/fixtures';
import { renderWithProviders } from '../../test/renderWithProviders';
import { FetchLogPage } from './FetchLogPage';

describe('FetchLogPage', () => {
  it('loads the first page of the fetch log', async () => {
    const api = fakeApi({ getFetchLog: vi.fn().mockResolvedValue(page([logEntry()], 60)) });

    renderWithProviders(<FetchLogPage />, api);

    expect(await screen.findByText('London', { selector: 'td' })).toBeInTheDocument();
    expect(screen.getByText(/Page 1 of 3/)).toBeInTheDocument();
    expect(api.getFetchLog).toHaveBeenCalledWith({ page: 1, pageSize: 25 }, expect.any(AbortSignal));
  });

  it('filters by city and result and resets to the first page', async () => {
    const api = fakeApi({ getFetchLog: vi.fn().mockResolvedValue(page([logEntry()], 60)) });
    renderWithProviders(<FetchLogPage />, api);
    await screen.findByText(/Page 1 of 3/);

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() =>
      expect(api.getFetchLog).toHaveBeenLastCalledWith({ page: 2, pageSize: 25 }, expect.any(AbortSignal)),
    );

    await userEvent.selectOptions(await screen.findByLabelText('City'), 'Riga');
    await userEvent.selectOptions(screen.getByLabelText('Result'), 'Failure');

    await waitFor(() =>
      expect(api.getFetchLog).toHaveBeenLastCalledWith(
        { page: 1, pageSize: 25, city: 'Riga', status: 'Failure' },
        expect.any(AbortSignal),
      ),
    );
  });

  it('shows an error with a retry button when loading fails', async () => {
    const api = fakeApi({ getFetchLog: vi.fn().mockRejectedValue(new Error('Service unavailable')) });

    renderWithProviders(<FetchLogPage />, api);

    expect(await screen.findByRole('alert')).toHaveTextContent('Service unavailable');
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });
});
