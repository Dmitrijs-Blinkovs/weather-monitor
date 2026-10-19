import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { RangeSelection } from '../../lib/dateRange';
import { DateRangeFilter } from './DateRangeFilter';

const last24h: RangeSelection = { kind: 'preset', presetId: '24h' };

describe('DateRangeFilter', () => {
  it('marks the active preset and switches presets', async () => {
    const onChange = vi.fn();
    render(<DateRangeFilter selection={last24h} onChange={onChange} />);

    expect(screen.getByRole('button', { name: 'Last 24 hours' })).toHaveAttribute('aria-pressed', 'true');

    await userEvent.click(screen.getByRole('button', { name: 'Last 7 days' }));

    expect(onChange).toHaveBeenCalledWith({ kind: 'preset', presetId: '7d' });
  });

  it('applies a valid custom date and time range', async () => {
    const onChange = vi.fn();
    render(<DateRangeFilter selection={last24h} onChange={onChange} />);

    await setInput('From', '2026-10-18T08:00');
    await setInput('To', '2026-10-19T08:30');
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));

    expect(onChange).toHaveBeenCalledWith({
      kind: 'custom',
      range: { from: new Date(2026, 9, 18, 8, 0), to: new Date(2026, 9, 19, 8, 30) },
    });
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('explains an invalid range instead of applying it', async () => {
    const onChange = vi.fn();
    render(<DateRangeFilter selection={last24h} onChange={onChange} />);

    await setInput('From', '2026-10-19T10:00');
    await setInput('To', '2026-10-19T09:00');
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));

    expect(screen.getByRole('alert')).toHaveTextContent('"From" must be earlier than "To".');
    expect(onChange).not.toHaveBeenCalled();
  });
});

async function setInput(label: string, value: string) {
  const input = screen.getByLabelText(label);
  await userEvent.clear(input);
  await userEvent.type(input, value);
}
