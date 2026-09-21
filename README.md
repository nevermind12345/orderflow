# OrderFlow

OrderFlow is a production-style restaurant ordering and operations platform
being built as a modular .NET monolith. The current repository is in **Milestone
1 — Foundation**. It does not yet implement customer, administrator, menu, cart,
authentication, or order workflows.

Milestone 1 is not complete. Local build, SQL Server migration, container startup,
health, OpenAPI, Problem Details, and integration-test behavior have evidence,
but deterministic development seed data, a green GitHub Actions run, and
clean-checkout reproducibility remain outstanding. An initial pull-request CI
workflow exists, but it has only been validated locally so far.

## Start here

- [Local development setup](docs/local-development.md) — safe Windows PowerShell
  instructions for configuration, SQL Server, explicit migrations, API startup,
  verification, tests, shutdown, and troubleshooting.
- [Foundation testing evidence](docs/foundation-testing.md) — what the current
  tests and local verification prove, and what they do not prove.
- [Development conventions](docs/development-conventions.md) — repository names,
  ports, paths, and implementation conventions.
- [Environment-variable inventory](docs/environment-variables.md) — configuration
  ownership and secret-handling rules.
- [Approved scope](docs/scope.md) — core product boundary and non-negotiable
  engineering guarantees.

## Current Foundation behavior

- .NET SDK selection is controlled by `global.json`.
- EF CLI `10.0.11` is pinned in the repository-local `dotnet-tools.json`.
- SQL Server and the API can run through Docker Compose.
- EF Core migrations are applied explicitly; application startup does not migrate
  the database.
- `/health/live` reports process liveness.
- `/health/ready` verifies SQL Server connectivity.
- Development OpenAPI is available at `/openapi/v1.json`.
- Unknown routes return Problem Details with a trace identifier.
- SQL integration tests use disposable SQL Server Testcontainers rather than the
  persistent local Compose database.
- The initial CI workflow restores and audits dependencies, builds the solution,
  runs the real-SQL integration tests, and publishes TRX results.

Do not interpret this list as completion of the Foundation milestone. The exit
gate requires a reproducible clean checkout, clean-database migration, working
health endpoints, successful Compose startup, and green CI, together with the
remaining milestone deliverables.

## Architecture decisions

Accepted decisions are recorded under [`docs/adr`](docs/adr):

- Modular monolith.
- SQL Server as the authoritative store.
- Optimistic concurrency with SQL Server `rowversion`.
- Polling before SignalR.
- Simulated payment for the version-one portfolio scope.
