import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { logEntry } from '../../test/fixtures';
import { FetchLogTable } from './FetchLogTable';

describe('FetchLogTable', () => {
  it('shows one row per attempt with its result, HTTP status and duration, without the error text', () => {
    render(
      <FetchLogTable
        entries={[
          logEntry(),
          logEntry({ id: 2, city: 'Rome', isSuccess: false, httpStatusCode: 401, durationMs: 95 }),
          logEntry({ id: 3, city: 'Riga', isSuccess: false, httpStatusCode: null }),
        ]}
        onShowDetails={vi.fn()}
      />,
    );

    const [, london, rome, riga] = screen.getAllByRole('row');

    expect(within(london!).getByText('London')).toBeInTheDocument();
    expect(within(london!).getByText('Success')).toBeInTheDocument();
    expect(within(london!).getByText('200')).toBeInTheDocument();
    expect(within(london!).getByText('142 ms')).toBeInTheDocument();

    expect(within(rome!).getByText('Failed')).toBeInTheDocument();
    expect(within(rome!).getByText('401')).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Error' })).not.toBeInTheDocument();

    expect(within(riga!).getByText('—')).toBeInTheDocument();
  });

  it('offers the payload of a success and the details of a failure', async () => {
    const onShowDetails = vi.fn();
    const failure = logEntry({ id: 2, isSuccess: false, httpStatusCode: 401 });
    render(<FetchLogTable entries={[logEntry(), failure]} onShowDetails={onShowDetails} />);

    await userEvent.click(screen.getByRole('button', { name: 'Details' }));

    expect(screen.getByRole('button', { name: 'Payload' })).toBeInTheDocument();
    expect(onShowDetails).toHaveBeenCalledExactlyOnceWith(failure);
  });

  it('explains an empty result', () => {
    render(<FetchLogTable entries={[]} onShowDetails={vi.fn()} />);

    expect(screen.getByText(/No fetch attempts match/)).toBeInTheDocument();
  });
});
