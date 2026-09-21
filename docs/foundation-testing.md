# Foundation integration tests

Current milestone: Milestone 1. These tests establish the SQL Server test harness;
they do not complete the Foundation exit gate.

## Prerequisites and ordered verification

1. Install the SDK selected by `global.json` and start Docker Desktop using Linux containers.
2. Run `docker version`. Both the client and Linux server must be available.
3. Run `dotnet build RestaurantOrdering.slnx --configuration Release`.
4. Run `dotnet test RestaurantOrdering.slnx --configuration Release --no-build`.

The first SQL test run downloads the pinned SQL Server 2022 CU14 Ubuntu 22.04
image and Testcontainers resource-reaper image. It requires registry access and
enough Docker memory/disk for three isolated SQL Server containers. SQL startup
has a five-minute bound. Docker failures are test failures, never silent skips.
No local SQL Server, Compose stack, user secrets, or developer database is required.

To run only the HTTP checks without Docker:

```powershell
dotnet test tests/Restaurant.Api.IntegrationTests --configuration Release --filter FullyQualifiedName~FoundationEndpointTests
```

## What the tests prove

- `FoundationEndpointTests`: liveness works without database setup; Development
  OpenAPI and the unknown-route Problem Details contract remain available.
- `SqlServerReadinessTests`: the real SQL health check returns 200 / Healthy
  against a migrated test database.
- `SqlServerOutageTests`: readiness first returns 200, then returns 503 / Unhealthy
  after its own SQL container has stopped; liveness continues to return 200.
  Awaited container shutdown supplies coordination; there are no timing sleeps.
- `DatabaseMigrationTests`: a uniquely named database is absent before migration;
  the nonempty migration list applies, its history matches the assembly, no
  migrations/model changes remain pending, and a second application does not
  duplicate migration history.

Each SQL test class owns its own `RestaurantApiFactory`. xUnit v2 calls
`InitializeAsync` before the test uses the factory. Testcontainers waits for SQL
startup; the factory injects its mapped-port connection string before API startup
reads configuration. Test-only setup migrates `RestaurantOrderingTests` through
the API service provider. The separate migration test uses another new database
on its disposable server, so setup cannot pre-apply that test's migrations.

The factory disposes the API host before the container, including initialization
failure cleanup. The resource reaper provides additional cleanup after an aborted
run. The outage test cannot stop another class's database. Test connections use
short connection timeouts, no connection retry, and no pooling so stale pooled
connections do not obscure the outage. Container passwords are generated per
fixture and connection strings are not written to logs or repository files.

## Initial migration scope

`InitialFoundation` intentionally has empty Up/Down methods because the current
DbContext has no entities. EF creates the database and migration history. This
proves the migration mechanism against real SQL Server, not business-table,
constraint, index, inventory, or concurrency correctness. Add schema and its
assertions alongside the relevant domain implementation.

Migrations reside in `src/Restaurant.Infrastructure/Persistence/Migrations`.
The API references EF Design as a private development dependency for tooling.
Restore and use the repository-local EF CLI 10.0.11 to match the runtime:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> --project src/Restaurant.Infrastructure --startup-project src/Restaurant.Api --output-dir Persistence/Migrations --configuration Release
```

Production application startup does not apply migrations. Deployment must use a
separate controlled migration step. Do not substitute `EnsureCreated` for migrations.

## Remaining Foundation evidence

Docker Compose startup has been verified locally with SQL Server and the API.
The initial migration applied successfully to the Compose database. Both API
health endpoints returned 200 Healthy, Development OpenAPI returned 200, and
an unknown endpoint returned 404 with an application/problem+json response.

Clean-checkout verification for commit `21891f5` and a successful GitHub-hosted
CI run for commit `811bc60` are recorded below. Deterministic development seed
data remains outstanding. The Domain and Application tests are still
placeholders and do not prove business behavior. The API container has no Docker
health check, and the initial migration contains no business tables. Milestone 1
remains incomplete.

## Local verification — 2026-09-14

- Before changes: Release build succeeded with 0 warnings and 0 errors. Full
  solution tests: 5 passed, 1 failed (API: 3 passed, 1 failed; Domain/Application:
  one placeholder pass each). Readiness returned 503 as SQL was unavailable.
- After changes: `dotnet build RestaurantOrdering.slnx --configuration Release`
  succeeded with 0 warnings and 0 errors.
- `dotnet test RestaurantOrdering.slnx --configuration Release --no-build`
  succeeded: API 6 passed, Domain 1 passed, Application 1 passed; total 8 passed,
  0 failed, 0 skipped. API test duration: 1 minute 13 seconds.
- Post-run `docker ps --all` showed no SQL test or resource-reaper containers.
- Initial migration generation succeeded using the installed EF CLI 10.0.5,
  which emitted a tooling-version warning against EF runtime 10.0.11. Generated
  migration metadata uses 10.0.11. Tooling alignment remains a setup improvement.
- These are local results, not clean-checkout, Compose, or GitHub Actions evidence.

## Compose and regression verification — 2026-09-14

Verified locally through user-run PowerShell commands and their supplied output:

- Docker Compose configuration validation passed.
- SQL Server started healthy, published on 127.0.0.1:1433.
- EF created RestaurantOrdering and applied
  20260914083626_InitialFoundation.
- The API image built successfully using SDK 10.0.302.
- Docker Compose started the API, published on 127.0.0.1:8080.
- GET /health/live returned 200 Healthy.
- GET /health/ready returned 200 Healthy.
- GET /openapi/v1.json returned 200 and OpenAPI version 3.1.1.
- An unknown endpoint returned 404, application/problem+json,
  and a traceId.
- Release solution build succeeded in 4.8 seconds with no warnings
  or errors reported.
- Full solution tests: 8 passed, 0 failed, 0 skipped; duration 20.0 seconds.
  Two passing tests remain Domain/Application placeholders.

The Unhealthy SQL Server log entry during the outage test is expected: the test
stops its isolated SQL container and verifies readiness returns 503. The final
suite summary confirms that the test passed.

Setup corrections established during verification:

- The Docker build SDK must match global.json. The floating sdk:10.0
  image supplied 10.0.401, which the latestPatch policy did not accept.
  The Dockerfile now uses sdk:10.0.302.
- Windows-hosted migration commands succeeded using tcp:127.0.0.1,1433.
  Connections between Compose services use sql,1433.
- Windows PowerShell connection-string builders use indexer keys such
  as ['Data Source'], ['Initial Catalog'], and ['User ID'].
- EF CLI 10.0.5 emitted a version warning against runtime 10.0.11;
  migration application nevertheless succeeded.

This evidence covers the current local workspace. It does not establish
clean-checkout reproducibility, green CI, or completion of Milestone 1.

## Documentation and local verification — 2026-09-19

The reusable Windows PowerShell setup guide was added at
`docs/local-development.md`, and the root `README.md` now links to the setup,
evidence, conventions, environment-variable inventory, scope, and ADRs.

The following behavior was reverified while preparing the guide:

- `git check-ignore .env` returned `.env`.
- `docker compose config --quiet` completed without output.
- SQL Server started and reached `healthy` on 127.0.0.1:1433.
- The controlled host-side EF command connected through
  tcp:127.0.0.1,1433 and reported that the database was already up to date.
- The EF command built successfully and completed without exposing a password or
  connection string. It still reported the expected EF CLI 10.0.5 versus runtime
  10.0.11 warning.
- `dotnet build RestaurantOrdering.slnx --configuration Release` succeeded in
  10.9 seconds with no warnings or errors reported.
- `docker compose up -d --build api` completed using the pinned SDK 10.0.302
  build image; unchanged application layers were served from the Docker cache.
- Compose reported the API running and SQL Server healthy.
- `/health/live` returned 200 Healthy.
- `/health/ready` returned 200 Healthy.
- `/openapi/v1.json` returned 200, JSON content, and OpenAPI version 3.1.1.
- An unknown endpoint returned 404, `application/problem+json`, Problem Details
  status 404, and a nonempty trace ID.

The full automated test suite was not rerun because only Markdown documentation
changed. The latest full-suite evidence remains the 2026-09-14 result of eight
passing tests. The current workspace still does not prove clean-checkout
reproducibility or green GitHub Actions, and Milestone 1 remains incomplete.

## EF tooling alignment — 2026-09-21

A repository-local `dotnet-tools.json` now pins `dotnet-ef` version 10.0.11 with
roll-forward disabled. `dotnet tool run dotnet-ef --version` reported 10.0.11,
matching the EF Core runtime packages. Local setup and migration instructions now
restore and invoke the repository-scoped tool explicitly rather than depending
on the older global EF CLI 10.0.5.

This resolves the local tooling-version mismatch. The historical 2026-09-14 and
2026-09-19 warning records above remain accurate for those earlier runs. It does
not resolve clean-checkout reproducibility or CI because the manifest and the
rest of the project are not yet committed and validated from a fresh checkout.

## Initial CI workflow and local command verification — 2026-09-21

`.github/workflows/ci.yml` now defines a least-privilege backend job for pull
requests and pushes to `main`. It restores the repository-local tool manifest,
restores and audits direct and transitive NuGet dependencies, builds in Release,
checks Docker availability, runs the complete test suite against disposable SQL
Server containers, and uploads TRX test results.

The workflow's substantive commands were run locally in the same order:

- `dotnet tool restore` restored `dotnet-ef` 10.0.11.
- Restore with `NuGetAudit=true` and `NuGetAuditMode=all` completed without
  vulnerability warnings.
- The Release build with `--no-restore` succeeded for all seven projects in 3.4
  seconds, with no warnings or errors reported.
- The Release test run with `--no-build` passed: 8 succeeded, 0 failed, 0
  skipped, in 24.8 seconds; the overall command completed in 25.2 seconds.
- Six API integration tests used disposable SQL Server containers. The expected
  unhealthy readiness log appeared during the deliberate outage test.
- Three TRX files were written under `TestResults`, matching the workflow's test
  artifact intent. `TestResults` remains ignored by Git.

This is local workflow-command evidence, not a successful GitHub Actions run.
The workflow must be committed and executed by GitHub before initial PR CI or
the related Foundation gate can be marked complete.

## Clean-checkout verification — 2026-09-21

Commit `21891f5498264b486ef356276a25f60f87a8ea44` was verified from a detached
temporary worktree outside the repository. The worktree contained 48 tracked
files and was clean before generated build and test outputs were created. Its
ignored `.env` was copied from the main worktree without displaying its
contents.

Before the Compose check, repository-local tool restore, dependency restore with
NuGet audit, the Release build, and the complete test suite had already passed
from this worktree. The test result was 8 succeeded, 0 failed, and 0 skipped;
six API integration tests used disposable SQL Server Testcontainers. The
expected `Unhealthy` log appeared during the deliberate SQL outage test.

The clean Compose verification used project name `orderflow-clean-21891f5`.
The existing `orderflow` services were stopped without deleting their
`orderflow_sql-data` volume. `docker compose config --quiet` passed before any
clean-project resources existed. Starting only SQL created the distinct
`orderflow-clean-21891f5_sql-data` volume, and SQL reached `healthy` on
`127.0.0.1:1433`.

Using the repository-local EF CLI 10.0.11 and a masked PowerShell password
prompt, EF created the new `RestaurantOrdering` database and explicitly applied
`20260914083626_InitialFoundation`. The clean API image then built successfully
from the worktree, and Compose started it on `127.0.0.1:8080`. Runtime checks
returned:

- `/health/live`: 200 `Healthy`.
- `/health/ready`: 200 `Healthy`.
- `/openapi/v1.json`: 200 with OpenAPI 3.1.1.
- An unknown endpoint: 404 `application/problem+json`, Problem Details status
  404, and a nonempty `traceId`.

After explicit approval, the project-scoped cleanup removed only the clean API
and SQL containers, clean network, and
`orderflow-clean-21891f5_sql-data` volume. The original `orderflow_sql-data`
volume remained present. Restarting the original Compose project returned SQL
to `healthy`; its API liveness and readiness checks both returned 200 `Healthy`.

This proves local clean-checkout build, test, clean-database migration, Compose
startup, and HTTP behavior for commit `21891f5`. It does not by itself prove a
green GitHub Actions run; the subsequent hosted run is recorded below. It also
does not prove deterministic development seed data, business behavior in the
placeholder Domain/Application tests, or an API container health check.
Milestone 1 remains incomplete.

## GitHub Actions verification — 2026-09-21

[Pull request #1](https://github.com/nevermind12345/orderflow/pull/1) triggered
[GitHub Actions run 35582399048](https://github.com/nevermind12345/orderflow/actions/runs/35582399048)
for head commit `811bc60fe893a88a5c843a073e3677d7a484fc3d`. The PR targeted
`main` from `feature/foundation`, and the hosted `CI` workflow completed with a
`success` conclusion.

The `.NET build and test` job completed successfully. Its substantive steps all
passed:

- Restore local tools.
- Restore and audit dependencies.
- Build solution.
- Verify Docker.
- Run tests.
- Upload test results.

The run uploaded a non-expired `test-results` artifact. This is the first actual
GitHub-hosted execution of the pull-request workflow and closes the previously
unproven CI evidence gap for commit `811bc60`. It does not resolve deterministic
development seed data, placeholder Domain/Application tests, or the missing API
container health check. Milestone 1 remains incomplete.
