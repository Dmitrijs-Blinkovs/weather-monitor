# 0013. Browser tests with Playwright against the production build and a mocked API

- Status: Accepted
- Date: 2026-10-19

## Context

Component tests (Vitest + jsdom) cannot render the chart (no layout engine), run the real router and bundle, or check
accessibility in a real browser. Full end-to-end tests against the backend would need SQL Server, Azurite and a live
OpenWeatherMap key, which makes them slow and flaky in CI.

## Decision

- Playwright (`src/weather-monitor.client/e2e`) runs Chromium against `vite preview` of the production build.
- The API is mocked in the browser with `page.route` (`e2e/support/mockApi.ts`) using data shaped like the real DTOs;
  tests also assert on the requests the UI makes (date range, filters, paging).
- The browser runs with a Latvian locale and the Europe/Riga time zone to prove that the UI stays English and that
  custom ranges are converted to UTC correctly.
- axe-core checks WCAG 2.0 A/AA (`wcag2a`, `wcag2aa`) on both pages in light and dark mode.
- The live end-to-end path (timer -> database -> API -> UI) is verified with the `run-locally` procedure.

## Consequences

- Deterministic, fast (~10 s) browser tests in CI; real chart rendering, tooltips, filters and accessibility covered.
- A change to an API contract must be reflected in `mockApi.ts` as well as in `src/api/types.ts`.

## Alternatives considered

- Full-stack E2E in CI with SQL Server and Azurite containers and a stubbed weather provider: higher fidelity, much
  slower and more fragile; worth adding if the backend contract changes often.
- Cypress: equivalent capability; Playwright has first-class multi-browser support and trace viewer.
