# 0018. Opt-in tests on a real SQL Server

Amends [0009](0009-test-strategy.md).

## Context

In development both hosts apply the migrations on start. On a missing database they both create it, and the host that
loses gets SQL error 1801 ("Database already exists"); EF Core's migration lock lives inside the database, so it cannot
cover creating it. A plain retry is not enough: the losing host's connection pool keeps replaying the earlier "cannot
open database" for a few seconds (SqlClient pool blocking period), so the retry sees no database again.
`DatabaseMigrationService` therefore clears the pool after 1801 and migrates again.

SQLite has no such race, so the tests of [0009](0009-test-strategy.md) cannot prove the fix.

## Decision

- Tests that need SQL Server carry `[Trait("Category", "SqlServer")]` and run only when `WEATHERMONITOR_TEST_SQLSERVER`
  holds a server connection string; otherwise they are skipped (`Assert.SkipWhen`). Each test creates and drops its own
  `WeatherMonitorTest_<guid>` database.
- CI runs them in the "Migrations on SQL Server" job, against the SQL Server 2022 container it already starts.
- The race is made deterministic: an EF Core command interceptor creates the database on its own connection right
  before the host's `CREATE DATABASE`. Several hosts in one test process cannot show it, because EF Core clears every
  connection pool of the process after creating a database and breaks the other hosts' commands; Web and Functions
  are separate processes.

## Consequences

- The default `dotnet test` still needs no SQL Server, Azurite, network or API key (the SQL Server tests are skipped).
- A host that loses the race logs EF Core's `Failed executing DbCommand` for the `CREATE DATABASE`, then
  `Another host created the database at the same time; applying the migrations again`, and starts normally.
- Locally the SQL Server tests run on LocalDB (`docs/runbooks/verify-changes.md`).

## Alternatives considered

- Only one host migrates in development: removes the race, but the Web host alone (without Functions) would no longer
  create its database, and Azure already relies on the function migrating.
- Testcontainers with SQL Server: rejected in 0009 (Docker on every machine); the opt-in variable reuses CI's container.
- Faking `SqlException` 1801 in a unit test: needs reflection over SqlClient internals and would not prove the pool
  behaviour, which was the actual reason a plain retry failed.
