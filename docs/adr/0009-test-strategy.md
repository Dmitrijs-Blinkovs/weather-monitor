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
- One test project per layer or host (domain tests live in Application.Tests); test names are sentences.

## Consequences

- No SQL Server, Azurite, network or API key needed to run tests.
- SQL Server-specific behaviour: CI applies, rolls back and re-applies every migration on SQL Server 2022, and
  `MigrationTests` keep the model and the migrations in sync; collation and query plans are checked by running locally.

## Alternatives considered

- Testcontainers with SQL Server: higher fidelity, but requires Docker on every machine.
- EF Core in-memory provider: does not translate queries, so it hides real bugs.
