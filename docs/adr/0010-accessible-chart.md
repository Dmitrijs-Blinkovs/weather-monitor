# 0010. Validated colour palette, colour per city, value-first tooltip

- Status: Accepted
- Date: 2026-10-19

## Context

All cities share one chart; readers must tell the cities apart (including with colour-vision deficiency, in light and
dark mode) and read exact values.

## Decision

- Categorical slots in a fixed order (`--series-1..8` in `index.css`), validated for CVD separation and contrast in
  both modes; dark mode has its own steps.
- Colour follows the city: `colorForCity` indexes the full alphabetical city list, so a filter never repaints a city.
- Identity is never colour alone: legend plus direct labels at the end of each line; a table view of the same data.
- Tooltip lists every city's observation current at the hovered time, value first, with min/max and update time.
- Text uses ink tokens, never series colours; status (success/failed) always pairs an icon with a label.

## Consequences

- Adding a ninth city falls back to a neutral colour; with that many series, consider small multiples instead.

## Alternatives considered

- Library default palette: not validated, and colours would shift with series order.
