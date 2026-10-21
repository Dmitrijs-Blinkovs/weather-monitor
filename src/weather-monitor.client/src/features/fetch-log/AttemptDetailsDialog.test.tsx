import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/http';
import { attemptDetails, fakeApi, logEntry } from '../../test/fixtures';
import { renderWithProviders } from '../../test/renderWithProviders';
import { AttemptDetailsDialog } from './AttemptDetailsDialog';

const failure = logEntry({ id: 7, city: 'Rome', isSuccess: false, httpStatusCode: 401 });

describe('AttemptDetailsDialog', () => {
  it('stays closed and requests nothing without an attempt', () => {
    const api = fakeApi();
    renderWithProviders(<AttemptDetailsDialog entry={null} onClose={vi.fn()} />, api);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(api.getFetchAttempt).not.toHaveBeenCalled();
  });

  it('loads the payload of a success on opening and shows it formatted', async () => {
    const api = fakeApi();
    renderWithProviders(<AttemptDetailsDialog entry={logEntry({ id: 3 })} onClose={vi.fn()} />, api);

    const dialog = screen.getByRole('dialog', { name: /Payload · London/ });
    const body = await within(dialog).findByLabelText('Response body');

    expect(api.getFetchAttempt).toHaveBeenCalledWith(3, expect.anything());
    expect(body.textContent).toBe('{\n  "name": "London",\n  "main": {\n    "temp": 9.5\n  }\n}');
    expect(within(dialog).queryByText('Error')).not.toBeInTheDocument();
  });

  it('shows the full error and the body of a failure', async () => {
    const api = fakeApi({
      getFetchAttempt: vi.fn().mockResolvedValue(
        attemptDetails({ ...failure, errorMessage: 'Invalid API key. Please see the FAQ.', responseBody: '<html>401</html>' }),
      ),
    });
    renderWithProviders(<AttemptDetailsDialog entry={failure} onClose={vi.fn()} />, api);

    const dialog = screen.getByRole('dialog', { name: /Error details · Rome/ });

    expect(await within(dialog).findByText('Invalid API key. Please see the FAQ.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText('Response body')).toHaveTextContent('<html>401</html>');
    expect(within(dialog).getByText('401')).toBeInTheDocument();
  });

  it('says when no response arrived at all', async () => {
    const timeout = logEntry({ isSuccess: false, httpStatusCode: null });
    const api = fakeApi({
      getFetchAttempt: vi.fn().mockResolvedValue(attemptDetails({ ...timeout, errorMessage: 'Timed out', responseBody: null })),
    });
    renderWithProviders(<AttemptDetailsDialog entry={timeout} onClose={vi.fn()} />, api);

    expect(await screen.findByText(/No response body was received/)).toBeInTheDocument();
    expect(screen.getByText('none (no response)')).toBeInTheDocument();
  });

  it('copies the body exactly as stored', async () => {
    const user = userEvent.setup();
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue();
    renderWithProviders(<AttemptDetailsDialog entry={logEntry()} onClose={vi.fn()} />, fakeApi());

    await user.click(await screen.findByRole('button', { name: 'Copy' }));

    expect(writeText).toHaveBeenCalledWith('{"name":"London","main":{"temp":9.5}}');
    expect(screen.getByRole('status')).toHaveTextContent('Copied.');
  });

  it('explains when the attempt is gone', async () => {
    const api = fakeApi({ getFetchAttempt: vi.fn().mockRejectedValue(new ApiError(404, 'Not Found')) });
    renderWithProviders(<AttemptDetailsDialog entry={failure} onClose={vi.fn()} />, api);

    expect(await screen.findByRole('alert')).toHaveTextContent('Not Found');
  });

  it('reports closing', async () => {
    const onClose = vi.fn();
    renderWithProviders(<AttemptDetailsDialog entry={logEntry()} onClose={onClose} />, fakeApi());

    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    expect(onClose).toHaveBeenCalledOnce();
  });
});
