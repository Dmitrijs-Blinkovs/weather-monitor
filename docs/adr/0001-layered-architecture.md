# 0001. Clean/onion layering with ports in Application

## Context

Two hosts (an Azure Function that writes, a web app that reads) share the same model and database. The assignment asks
for SOLID and unit tests, so business logic must be testable without Azure, HTTP or SQL Server.

## Decision

Four libraries plus two hosts: `Domain` (entities, invariants) <- `Application` (use cases and the interfaces they need)
<- `Infrastructure` (EF Core, OpenWeatherMap client) <- hosts (`Functions`, `Web`) that only compose. Each layer
exposes `AddXxx` extension methods; hosts call only the ones they need (the web app never registers the weather
client). Reads and writes use separate interfaces (`*Reader` vs `*Repository`).

## Consequences

- Use cases are unit-tested with substitutes; infrastructure is tested against SQLite and a stub HTTP handler.
- Adding a provider or a store means implementing a port, not editing use cases.
- More projects than a minimal solution would need; the boundaries are kept honest by project references.

## Alternatives considered

- Single project per host with services inside: less ceremony, but logic would be duplicated or coupled to hosting.
- Generic repository over EF Core: adds an abstraction that hides EF features without adding testability; readers
  project straight to DTOs instead.
