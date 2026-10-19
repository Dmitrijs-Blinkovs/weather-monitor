import { useState, type FormEvent } from 'react';
import {
  RANGE_PRESETS,
  fromLocalInputValue,
  resolveRange,
  toLocalInputValue,
  validateRange,
  type RangePresetId,
  type RangeSelection,
} from '../../lib/dateRange';

interface DateRangeFilterProps {
  selection: RangeSelection;
  onChange: (selection: RangeSelection) => void;
}

/** Quick presets first, then a custom date and time range. */
export function DateRangeFilter({ selection, onChange }: DateRangeFilterProps) {
  const initial = resolveRange(selection, new Date());
  const [fromValue, setFromValue] = useState(() => toLocalInputValue(initial.from));
  const [toValue, setToValue] = useState(() => toLocalInputValue(initial.to));
  const [error, setError] = useState<string | null>(null);

  function selectPreset(presetId: RangePresetId) {
    const range = resolveRange({ kind: 'preset', presetId }, new Date());
    setFromValue(toLocalInputValue(range.from));
    setToValue(toLocalInputValue(range.to));
    setError(null);
    onChange({ kind: 'preset', presetId });
  }

  function applyCustomRange(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const from = fromLocalInputValue(fromValue);
    const to = fromLocalInputValue(toValue);
    if (!from || !to) {
      setError('Enter both "From" and "To".');
      return;
    }

    const range = { from, to };
    const validationError = validateRange(range);
    setError(validationError);
    if (!validationError) {
      onChange({ kind: 'custom', range });
    }
  }

  return (
    <form className="filter-bar" onSubmit={applyCustomRange} aria-label="Date and time range" noValidate>
      <div className="segmented" role="group" aria-label="Quick ranges">
        {RANGE_PRESETS.map((preset) => (
          <button
            key={preset.id}
            type="button"
            className="segmented__option"
            aria-pressed={selection.kind === 'preset' && selection.presetId === preset.id}
            onClick={() => selectPreset(preset.id)}
          >
            {preset.label}
          </button>
        ))}
      </div>

      <label className="field">
        <span className="field__label">From</span>
        <input type="datetime-local" value={fromValue} onChange={(event) => setFromValue(event.target.value)} />
      </label>
      <label className="field">
        <span className="field__label">To</span>
        <input type="datetime-local" value={toValue} onChange={(event) => setToValue(event.target.value)} />
      </label>
      <button type="submit" className="button button--primary">
        Apply
      </button>

      {error && (
        <p className="filter-bar__error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}
