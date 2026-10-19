# 0009. Test without external dependencies

- Status: Accepted
- Date: 2026-10-19

## Context

Tests must run anywhere (developer machines, CI on Linux) quickly and deterministically, and still exercise the parts
that usually break: SQL translation, HTTP mapping, ASP.NET Core binding and validation.

## Decision

- xUnit v3 on Microsoft.Testing.Platform (configured in `global.json`), Shouldly, NSubstitute, `FakeTimeProvider`.
- Persistence: in-memory SQLite with the real EF model (`EnsureCreated`), not the EF in-memory provider, so queries run
  as SQL.
- HTTP client: a stub `HttpMessageHandler` with recorded real responses.
- API: `WebApplicationFactory` in a `Testing` environment with substituted ports and a temporary web root.
- Front end: Vitest + Testing Library + jsdom, with the API injected through a React context.
- One test project per production project; test names are sentences.

## Consequences

- No SQL Server, Azurite, network or API key needed to run tests; the whole suite runs in seconds.
- SQL Server-specific behaviour (collation, `INCLUDE` columns, migrations) is verified by running the app locally.

## Alternatives considered

- Testcontainers with SQL Server: higher fidelity, but requires Docker on every machine.
- EF Core in-memory provider: does not translate queries, so it hides real bugs.
