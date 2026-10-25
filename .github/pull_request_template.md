## What and why

<!-- One or two sentences: the change and the reason for it. Link the issue if there is one. -->

## How it was verified

<!-- Tests added or changed, and what you checked in the running app (screenshots for UI changes). -->

## Checklist (see "Definition of done" in AGENTS.md)

- [ ] `dotnet build` has 0 warnings and `dotnet test --solution WeatherMonitor.slnx` is green
- [ ] Front end: `npm audit --omit=dev --audit-level=high`, `npm run lint`, `npm test`, `npm run test:e2e` and `npm run build` are green
- [ ] New behaviour has tests; a bug fix has a test that fails without the fix
- [ ] Data model change comes with a migration; API change updates `src/api/types.ts` and the README API table
- [ ] Changed design decision has a new or superseding ADR in `docs/adr/`
- [ ] No secrets in code, config or logs; `docs/security.md` still true for new inputs, headers, outbound calls, identities or workflows
