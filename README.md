# OrderFlow

OrderFlow is a production-style restaurant ordering and operations platform
being built as a modular .NET monolith. **Milestone 1 — Foundation is complete
on `main` at merge commit `e361748`.** Customer, administrator, menu, cart,
authentication, and order workflows are not yet implemented.

Foundation evidence covers the Release build and tests, isolated SQL Server
migration, Docker Compose startup, health, OpenAPI, Problem Details, clean
checkout, and GitHub-hosted CI. Deterministic development seed data is assigned
to the milestones that introduce its real entities: accounts and roles in
Milestone 2, then catalog and stock data in Milestone 3. The Domain and
Application tests remain placeholders because they contain no business behavior
yet, and the API container has no Docker health check; neither is a Foundation
exit-gate failure.

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
- The initial CI workflow passed on PR #1 and again on merged `main`: it restored
  and audited dependencies, built the solution, ran the real-SQL integration
  tests, and uploaded the `test-results` artifact.

Together, the clean-worktree evidence and successful pull-request workflow prove
every Milestone 1 exit-gate requirement. See the Foundation testing evidence for
the gate-by-gate closeout. This does not imply that any Milestone 2 product or
security behavior exists yet.

## Architecture decisions

Accepted decisions are recorded under [`docs/adr`](docs/adr):

- Modular monolith.
- SQL Server as the authoritative store.
- Optimistic concurrency with SQL Server `rowversion`.
- Polling before SignalR.
- Simulated payment for the version-one portfolio scope.
- [Development seed data follows its owning milestones](docs/adr/0006-seed-data-with-owning-milestones.md),
  never fake Foundation tables or a no-op seeder.
