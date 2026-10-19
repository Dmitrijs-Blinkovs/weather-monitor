import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { logEntry } from '../../test/fixtures';
import { FetchLogTable } from './FetchLogTable';

describe('FetchLogTable', () => {
  it('shows one row per attempt with its result, HTTP status, duration and error', () => {
    render(
      <FetchLogTable
        entries={[
          logEntry(),
          logEntry({ id: 2, city: 'Rome', isSuccess: false, httpStatusCode: 401, errorMessage: 'Invalid API key', durationMs: 95 }),
          logEntry({ id: 3, city: 'Riga', isSuccess: false, httpStatusCode: null, errorMessage: 'Request timed out' }),
        ]}
      />,
    );

    const [, london, rome, riga] = screen.getAllByRole('row');

    expect(within(london!).getByText('London')).toBeInTheDocument();
    expect(within(london!).getByText('Success')).toBeInTheDocument();
    expect(within(london!).getByText('200')).toBeInTheDocument();
    expect(within(london!).getByText('142 ms')).toBeInTheDocument();

    expect(within(rome!).getByText('Failed')).toBeInTheDocument();
    expect(within(rome!).getByText('401')).toBeInTheDocument();
    expect(within(rome!).getByText('Invalid API key')).toBeInTheDocument();

    expect(within(riga!).getByText('—')).toBeInTheDocument();
  });

  it('explains an empty result', () => {
    render(<FetchLogTable entries={[]} />);

    expect(screen.getByText(/No fetch attempts match/)).toBeInTheDocument();
  });
});
