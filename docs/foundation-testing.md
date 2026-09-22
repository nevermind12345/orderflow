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

Clean-checkout verification for commit `21891f5` and successful GitHub-hosted CI
runs are recorded below. ADR 0006 assigns deterministic development seed data to
the milestones that introduce the corresponding real entities instead of adding
fake Foundation schema. The plan correction passed on GitHub at commit
`5bd2732`. Every Milestone 1 exit gate now has observable evidence, so Foundation
is complete on `main` at merge commit `e361748`.

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
startup, and HTTP behavior for commit `21891f5`. It did not by itself prove a
green GitHub Actions run; the subsequent hosted runs are recorded below. Under
ADR 0006, seed behavior is proved in Milestones 2 and 3 when its real entities
exist. The final gate assessment appears in the closeout section.

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

The run uploaded a non-expired `test-results` artifact. This was the first actual
GitHub-hosted execution of the pull-request workflow and closed the previously
unproven CI evidence gap for commit `811bc60`. The later plan-correction run and
final gate assessment are recorded below.

## Seed-data milestone correction — 2026-09-21

The original plan assigned deterministic development seed data to Milestone 1
while the Foundation `RestaurantDbContext` intentionally had no entities. Adding
a fake table or a no-op seeder would have produced misleading evidence, while
pulling authentication or menu entities forward would have violated the
milestone sequence.

ADR 0006 corrects ownership without weakening seed requirements. Milestone 2
must implement and test deterministic, idempotent, Development-only roles and
demo accounts alongside Identity. Milestone 3 must extend that behavior with
deterministic categories, menu items, availability, and stock alongside the
catalog schema. Seed behavior must be blocked outside `Development`, and reruns
must not create duplicates.

Milestone 1 instead documents and enforces this boundary. It does not claim seed
behavior for nonexistent entities.

The correction was committed as `5bd2732` and validated by
[GitHub Actions run 35584531101](https://github.com/nevermind12345/orderflow/actions/runs/35584531101).
The `.NET build and test` job completed successfully for exact head SHA
`5bd27324773d7ae2b884789b036b4bed5d946c55`. Tool restore, dependency restore
and audit, Release build, Docker verification, tests, and test-result upload all
passed. The run uploaded a non-expired `test-results` artifact.

## Milestone 1 closeout — 2026-09-22

Milestone 1 is complete on `main`.
[Pull request #1](https://github.com/nevermind12345/orderflow/pull/1) merged the
validated Foundation branch as commit
`e3617485f2e0720945bee23fe2ad995641e78bb4` on 2026-09-22.

Exit-gate evidence:

- **Clean checkout builds:** the detached clean worktree at commit `21891f5`
  restored repository tools and audited dependencies, built all seven projects
  in Release, and passed all eight tests with 0 failed and 0 skipped.
- **Compose starts required services:** the isolated
  `orderflow-clean-21891f5` project started SQL Server healthy and the API on the
  documented ports using a separate temporary SQL volume.
- **Migrations apply to a clean database:** repository-local EF CLI 10.0.11
  created `RestaurantOrdering` and explicitly applied
  `20260914083626_InitialFoundation` to the new volume.
- **Health endpoints work:** clean-checkout liveness and SQL-backed readiness
  both returned 200 `Healthy`; OpenAPI 3.1.1 and 404 Problem Details with a
  nonempty `traceId` also passed.
- **CI is green:** the closeout head passed in run `35699873340`, and
  [post-merge main run 35700277856](https://github.com/nevermind12345/orderflow/actions/runs/35700277856)
  completed successfully for merge commit `e3617485`. The main run covered
  tool restore, dependency restore and audit, Release build, Docker verification,
  real-SQL integration tests, and test-result publication.

The placeholder Domain/Application tests accurately reflect that those projects
contain no business behavior yet. The missing API container health check does
not replace or invalidate the proven HTTP health contracts and is not a stated
Foundation exit gate. Seed behavior remains mandatory in Milestones 2 and 3
under ADR 0006. No Milestone 2 behavior is claimed by this closeout.

The post-merge run uploaded a non-expired `test-results` artifact. This confirms
that the same Foundation gate remains green after integration into `main`, not
only on the feature branch.
