# OrderFlow

OrderFlow is a production-style restaurant ordering and operations platform
being built as a modular .NET monolith. The current repository is in **Milestone
1 — Foundation**. It does not yet implement customer, administrator, menu, cart,
authentication, or order workflows.

Milestone 1 is not complete. Local and clean-checkout evidence now covers the
Release build and tests, isolated SQL Server migration, Docker Compose startup,
health, OpenAPI, and Problem Details behavior. Deterministic development seed
data is assigned to the milestones that introduce its real entities: accounts
and roles in Milestone 2, then catalog and stock data in Milestone 3. The Domain
and Application tests are still placeholders, and the API container still has
no Docker health check. The initial pull-request CI workflow passed on GitHub
for PR #1 at commit `811bc60`.

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
- Commit `21891f5` has been verified from a detached clean worktree: all eight
  tests passed, a separate Compose project started against a new SQL volume,
  `InitialFoundation` applied explicitly, and the HTTP foundation checks passed.
- The initial CI workflow passed on GitHub for PR #1: it restored and audited
  dependencies, built the solution, ran the real-SQL integration tests, and
  uploaded the `test-results` artifact.

Do not interpret this list as completion of the Foundation milestone. The exit
gate's clean-checkout, clean-database migration, health, and Compose behaviors
have local evidence, and pull-request CI has passed on GitHub. The remaining
milestone deliverables are not yet proven complete.

## Architecture decisions

Accepted decisions are recorded under [`docs/adr`](docs/adr):

- Modular monolith.
- SQL Server as the authoritative store.
- Optimistic concurrency with SQL Server `rowversion`.
- Polling before SignalR.
- Simulated payment for the version-one portfolio scope.
- [Development seed data follows its owning milestones](docs/adr/0006-seed-data-with-owning-milestones.md),
  never fake Foundation tables or a no-op seeder.
