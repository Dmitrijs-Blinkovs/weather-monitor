import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { MAX_PAGE, Pagination } from './Pagination';

const labels = { label: 'Fetch log pages', itemsLabel: 'attempts', beyondLimitHint: 'filter to reach older entries' };

describe('Pagination', () => {
  it('moves between pages', async () => {
    const onPageChange = vi.fn();
    render(<Pagination page={2} totalPages={3} totalCount={60} onPageChange={onPageChange} {...labels} />);

    await userEvent.click(screen.getByRole('button', { name: 'Next' }));
    await userEvent.click(screen.getByRole('button', { name: 'Previous' }));

    expect(onPageChange.mock.calls).toEqual([[3], [1]]);
    expect(screen.getByRole('navigation', { name: 'Fetch log pages' })).toHaveTextContent('Page 2 of 3 · 60 attempts');
  });

  it('stops at the deepest page the API serves and explains how to reach older rows', () => {
    render(<Pagination page={MAX_PAGE} totalPages={4_000} totalCount={100_000} onPageChange={vi.fn()} {...labels} />);

    expect(screen.getByText(/Page 1,000 of 1,000 · 100,000 attempts/)).toHaveTextContent('filter to reach older entries');
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });
});
