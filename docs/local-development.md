# Local Development Setup

This guide explains how to run the current OrderFlow Milestone 1 foundation
locally from Windows PowerShell.

The current database migration is an empty foundation migration because the
application has no business entities yet. Database migrations are applied
explicitly; application startup and `docker compose up` do not automatically
create or migrate the application database.

## Prerequisites

Install or provide:

- Windows PowerShell.
- Git.
- Docker Desktop configured to use Linux containers.
- The .NET SDK selected by `global.json`.
- The repository-local .NET tools restored from `dotnet-tools.json`.

The repository currently requests .NET SDK `10.0.302` with `latestPatch`
roll-forward. A later compatible patch in the same feature band, such as
`10.0.303`, is acceptable.

Verify the local tools:

```powershell
dotnet --version
dotnet tool restore
dotnet tool run dotnet-ef --version
docker version
docker compose version
```

Docker must report both a client and a Linux server. The repository pins EF CLI
`10.0.11`, matching its EF Core runtime packages. Use the repository-local tool
command shown above instead of relying on an independently installed global
version.

Do not place passwords or full connection strings in commands, documentation,
screenshots, issues, or source-controlled files.

## Configure the local Compose environment

Docker Compose reads local values from `.env`. The committed `.env.example` file
contains placeholders only; `.env` is ignored by Git and must never be committed.

Create `.env` only when it does not already exist:

```powershell
if (-not (Test-Path .\.env)) {
    Copy-Item .\.env.example .\.env
}
```

Open `.env` in a local editor. Do not print or paste its contents. For the current
Foundation Compose stack:

- Replace `SQL_SA_PASSWORD` with a unique local password that satisfies SQL
  Server password complexity requirements.
- Replace the password inside `ConnectionStrings__DefaultConnection` with the
  same value.
- Keep `Server=sql,1433` because the API runs inside the Compose network.
- Keep `Database=RestaurantOrdering`.
- Do not use these local values in CI or production.

The remaining authentication and seed placeholders are not consumed by the
current Foundation API. Replace and validate them when their corresponding
features are implemented; do not mistake their presence for implemented
authentication or seed-data support.

Confirm that Git ignores the file:

```powershell
git check-ignore .env
```

The expected output is `.env`. Validate Compose substitution without printing
the resolved configuration:

```powershell
docker compose config --quiet
```

A successful validation returns to the prompt without output.

## Start SQL Server

Start only the SQL Server service:

```powershell
docker compose up -d sql
```

Check its status:

```powershell
docker compose ps sql
```

Wait until the service reports `healthy` before applying migrations or starting
the API.

SQL Server is available:

- To other Compose services at `sql,1433`.
- To Windows-hosted tools at `tcp:127.0.0.1,1433`.

Starting the container does not apply Entity Framework Core migrations. The
named volume `orderflow_sql-data` preserves the database when the containers are
stopped or recreated normally.

Do not use `docker compose down --volumes` as a routine shutdown command. It
deletes the named SQL Server volume and its local database data.

## Apply database migrations explicitly

Starting SQL Server does not create or migrate the `RestaurantOrdering`
application database. Apply migrations as a separate, controlled command before
starting the API or expecting readiness to succeed.

When running EF Core from Windows, connect through `tcp:127.0.0.1,1433`. Prompt
for the existing local SQL Server password rather than placing it in command
history:

```powershell
$previousConnectionString = $env:ConnectionStrings__DefaultConnection
$sqlPassword = Read-Host 'Enter the local SQL Server sa password' -AsSecureString
$credential = [System.Management.Automation.PSCredential]::new(
    'sa',
    $sqlPassword
)

$sqlConnection = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$sqlConnection['Data Source'] = 'tcp:127.0.0.1,1433'
$sqlConnection['Initial Catalog'] = 'RestaurantOrdering'
$sqlConnection['User ID'] = 'sa'
$sqlConnection['Password'] = $credential.GetNetworkCredential().Password
$sqlConnection['Encrypt'] = $true
$sqlConnection['TrustServerCertificate'] = $true

try {
    $env:ConnectionStrings__DefaultConnection =
        $sqlConnection.ConnectionString

    dotnet tool run dotnet-ef database update `
        --project .\src\Restaurant.Infrastructure `
        --startup-project .\src\Restaurant.Api `
        --configuration Release
}
finally {
    $env:ConnectionStrings__DefaultConnection =
        $previousConnectionString

    $sqlConnection['Password'] = ''
    Remove-Variable -Name sqlPassword, credential, sqlConnection, previousConnectionString -ErrorAction SilentlyContinue
}
```

A successful command reports that the migrations were applied or that the
database is already up to date. Do not replace this controlled step with
automatic migration during production application startup.

## Build and start the API

Build the complete solution in Release configuration:

```powershell
dotnet build .\RestaurantOrdering.slnx --configuration Release
```

The build must succeed without warnings or errors. This command restores missing
NuGet packages automatically.

Build the API container image and start the API:

```powershell
docker compose up -d --build api
```

The API waits for the SQL Server container health check before starting. Inspect
both services:

```powershell
docker compose ps
```

SQL Server must report `healthy`, and the API must remain `Up`. The API currently
has no Docker health check, so its container state does not replace the HTTP
health checks in the next section.

## Verify application health

Verify that the API process is responding:

```powershell
Invoke-WebRequest `
    -UseBasicParsing `
    -Uri 'http://127.0.0.1:8080/health/live' |
    Select-Object StatusCode, Content
```

Liveness must return status `200` with content `Healthy`.

Verify that the API can reach SQL Server:

```powershell
Invoke-WebRequest `
    -UseBasicParsing `
    -Uri 'http://127.0.0.1:8080/health/ready' |
    Select-Object StatusCode, Content
```

Readiness must return status `200` with content `Healthy`. If liveness succeeds
but readiness returns `503`, investigate SQL Server state, migration state, and
the configured connection string without printing the connection string.

## Verify OpenAPI and error handling

OpenAPI is available when the API runs in the `Development` environment:

```powershell
$openApiResponse = Invoke-WebRequest `
    -UseBasicParsing `
    -Uri 'http://127.0.0.1:8080/openapi/v1.json'

[PSCustomObject]@{
    StatusCode = $openApiResponse.StatusCode
    ContentType = $openApiResponse.Headers.'Content-Type'
    OpenApiVersion = ($openApiResponse.Content | ConvertFrom-Json).openapi
}
```

The response must have status `200` and report OpenAPI version `3.1.1`.

Use `curl.exe` to inspect an expected non-success response because Windows
PowerShell treats HTTP `404` as an `Invoke-WebRequest` exception:

```powershell
curl.exe --silent --show-error --include `
    "http://127.0.0.1:8080/endpoint-that-does-not-exist"
```

The response must contain:

- `HTTP/1.1 404 Not Found`.
- `Content-Type: application/problem+json`.
- A Problem Details body with status `404`.
- A nonempty `traceId`.

## Run the automated tests

Keep Docker Desktop running with Linux containers. The SQL integration tests use
Testcontainers to create disposable SQL Server instances with generated
credentials and dynamically mapped ports. They do not use `.env`, the Compose
database, or the `orderflow_sql-data` volume.

After a successful Release build, run the complete test suite:

```powershell
dotnet test .\RestaurantOrdering.slnx `
    --configuration Release `
    --no-build
```

The first integration-test run may download the pinned SQL Server image and the
Testcontainers resource-reaper image. Docker must have sufficient memory and
disk space.

The current Foundation suite contains six API integration tests and two
Domain/Application placeholder tests. The placeholder tests do not prove
business behavior. An `Unhealthy` SQL Server health-check log during the outage
test is expected because that test deliberately stops its isolated SQL container
and verifies that readiness returns `503`.

To run only the HTTP foundation checks without starting SQL Testcontainers:

```powershell
dotnet test .\tests\Restaurant.Api.IntegrationTests `
    --configuration Release `
    --no-build `
    --filter FullyQualifiedName~FoundationEndpointTests
```

Testcontainers should remove its SQL Server and resource-reaper containers after
the tests finish. A passing local run is not a substitute for clean-checkout or
GitHub Actions evidence.

## Stop and restart local services

To stop the containers while keeping them available for a quick restart:

```powershell
docker compose stop
```

Restart the existing services with:

```powershell
docker compose start
```

To stop and remove the Compose containers and network while preserving the named
SQL Server volume:

```powershell
docker compose down
```

A later `docker compose up -d` recreates the containers and reuses the preserved
`orderflow_sql-data` volume. Normal container removal does not reset the database.

Do not add `--volumes` unless you deliberately intend to delete the local
database and have first reviewed the data impact:

```text
docker compose down --volumes
```

The destructive form is shown only as a warning and is not a normal setup or
shutdown command.

## Troubleshooting

### SQL Server is still starting

Immediately after `docker compose up -d sql`, `docker compose ps sql` may report
`health: starting`. Do not apply migrations or diagnose readiness yet. Check the
status again and continue only after it reports `healthy`.

### A Windows-hosted command cannot connect to SQL Server

Use `tcp:127.0.0.1,1433` for EF Core and other tools running on Windows. Compose
publishes SQL Server on IPv4. `localhost` may first attempt IPv6 on some systems;
this is a troubleshooting possibility, not proof of the cause of every timeout.
Code running inside the API container must instead use `sql,1433`.

When constructing a connection string in Windows PowerShell, use builder
indexers such as `$sqlConnection['Data Source']`. Dot-property assignments can
be interpreted as unsupported connection-string keywords.

### EF Core reports a tooling-version warning

The repository pins EF CLI `10.0.11` in `dotnet-tools.json`, matching the EF Core
runtime. Restore and invoke that local tool explicitly:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef --version
```

An independently installed global EF CLI may still report an older version. If a
migration command emits a version warning, confirm that it uses `dotnet tool run
dotnet-ef` rather than the global tool.

### The Docker build cannot select the requested SDK

`global.json` requests SDK `10.0.302` with `latestPatch` roll-forward. The
Dockerfile intentionally pins `mcr.microsoft.com/dotnet/sdk:10.0.302`; a floating
`sdk:10.0` image previously selected a different feature band and failed the
build. Keep the Docker SDK tag aligned with `global.json`.

If Docker reports that it cannot find the Dockerfile, confirm that the file is
named exactly `Dockerfile`, not `Dockerfile.txt`.

### Compose configuration fails

Validate interpolation and YAML structure without printing resolved secrets:

```powershell
docker compose config --quiet
```

Check that `.env` exists locally, remains ignored, and contains matching SQL
password values. Do not print the file while troubleshooting. YAML service keys
such as `sql:` and `api:` must have consistent indentation.

### The outage integration test logs an unhealthy SQL Server

This log is expected when the final test summary passes. The outage test stops
its own isolated SQL container to prove that readiness returns `503` while
liveness remains healthy. Do not weaken readiness or change the test to expect a
successful readiness response during the simulated outage.

## Current Foundation limitations

- `RestaurantDbContext` has no entities, so `InitialFoundation` creates migration
  history but no business tables, constraints, or indexes.
- Deterministic development seed data is not implemented. The seed variables in
  `.env.example` are an inventory of future configuration, not working seed
  behavior. Seed accounts and menu data cannot be meaningfully added until the
  corresponding models exist.
- The Domain and Application test projects each contain a passing placeholder;
  they do not prove business rules.
- The API container has no Docker health check. Use the HTTP health endpoints.
- Commit `21891f5` has local clean-checkout evidence covering the Release build
  and tests, explicit migration to a new SQL volume, Compose startup, and the
  HTTP foundation checks. See `docs/foundation-testing.md` for the dated record.
- The initial pull-request CI workflow passed on GitHub for PR #1 at commit
  `811bc60`. See `docs/foundation-testing.md` for the run and artifact evidence.
- Production startup correctly does not apply migrations automatically; a
  controlled deployment migration step remains future work.

These limitations mean Milestone 1 is not complete even when all commands in
this guide succeed locally.
