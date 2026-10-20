import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { MAX_LOG_PAGE, Pagination } from './Pagination';

describe('Pagination', () => {
  it('moves between pages', async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={2} totalPages={3} totalCount={60} onPageChange={onPageChange} />);

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await userEvent.click(screen.getByRole('button', { name: 'Previous' }));

    expect(onPageChange.mock.calls).toEqual([[3], [1]]);
  });

  it('stops at the deepest page the API serves and explains how to reach older entries', () => {
    render(<Pagination page={MAX_LOG_PAGE} totalPages={4_000} totalCount={100_000} onPageChange={vi.fn()} />);

    expect(screen.getByText(/Page 1,000 of 1,000 · 100,000 attempts/)).toHaveTextContent(
      'filter by city or result to reach older entries',
    );
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });
});
