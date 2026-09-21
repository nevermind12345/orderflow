# Development Conventions

- **Status:** Accepted baseline for Week 0
- **Last reviewed:** 2026-07-29

This document fixes the initial names, URLs, ports, and repository conventions for OrderFlow. Consistency is more important than any individual choice. A material architectural change requires an ADR; a small convention change requires this document and affected setup instructions to be updated together.

## Product and repository names

| Purpose | Convention |
| --- | --- |
| Product/display name | `OrderFlow` |
| GitHub repository | `orderflow` |
| .NET solution | `RestaurantOrdering.slnx` |
| C# root namespace | `Restaurant` |
| Database | `RestaurantOrdering` |
| Docker Compose project | `orderflow` |

The repository name and product name do not determine the C# namespace. Backend projects use the `Restaurant.*` names required by the approved architecture.

## Repository layout

```text
orderflow/
|-- src/
|   |-- Restaurant.Domain/
|   |-- Restaurant.Application/
|   |-- Restaurant.Infrastructure/
|   |-- Restaurant.Api/
|   `-- Restaurant.Web/
|-- tests/
|   |-- Restaurant.Domain.Tests/
|   |-- Restaurant.Application.Tests/
|   `-- Restaurant.Api.IntegrationTests/
|-- docs/
|   `-- adr/
|-- .github/
|-- compose.yaml
|-- Directory.Build.props
`-- RestaurantOrdering.slnx
```

`Restaurant.Web` is a Vite React TypeScript application. It lives under `src/` but is not required to be a .NET project. Its Vitest and React Testing Library tests are colocated with the frontend source; the small Playwright smoke suite may live under `Restaurant.Web/e2e/`.

## Local URLs and ports

| Service | Local development address | Purpose |
| --- | --- | --- |
| React/Vite | `http://localhost:5173` | Browser development server |
| API HTTP | `http://localhost:5000` | Local API when HTTPS is unnecessary |
| API HTTPS | `https://localhost:5001` | Preferred browser-to-API development URL |
| API in Docker | `http://localhost:8080` | Host-mapped container endpoint |
| SQL Server | `localhost,1433` | Host connection for tools and locally run API |

Reserved API paths:

- API base path: `/api/v1`
- Liveness: `/health/live`
- Readiness: `/health/ready`
- OpenAPI document: `/openapi/v1.json`

Only the React development origin `http://localhost:5173` is allowed by development CORS initially. Production uses its exact deployed HTTPS origin and must not allow every origin.

## Docker network names

Compose service names provide internal DNS:

| Service | Compose service name | Container port | Suggested container name |
| --- | --- | --- | --- |
| SQL Server | `sql` | `1433` | `orderflow-sql` |
| API | `api` | `8080` | `orderflow-api` |

The address depends on where the caller runs:

- A locally running API connects to SQL Server at `localhost,1433`.
- The API container connects to SQL Server at `sql,1433`.
- A host browser calls the containerised API at `http://localhost:8080`.
- Browser code must never use the Docker-only hostname `api` or `sql`.

Credentials and full connection strings are configuration, not conventions. They belong in ignored local configuration, user secrets, CI secrets, or Azure settings. `.env.example` contains placeholders only.

## Environment names

Use the standard ASP.NET Core environment names:

- `Development`
- `Testing`
- `Production`

Use UTC for stored and exchanged timestamps. Timestamp property names end in `Utc`, for example `CreatedAtUtc`. Local-time conversion is a presentation concern.

## API and contract naming

- Public endpoints use lowercase plural nouns, for example `/api/v1/menu-items` and `/api/v1/orders`.
- Route segments use kebab-case.
- JSON property names use camelCase.
- C# request and response types use explicit names such as `PlaceOrderRequest` and `OrderResponse`.
- EF Core entities are not exposed as public API contracts.
- Asynchronous C# methods end in `Async`.
- `CancellationToken` parameters are named `cancellationToken` and are propagated through applicable calls.
- Expected stock, stale-write, and invalid-transition conflicts return `409 Conflict` using Problem Details.

## Source naming

### C#

- Types, methods, properties, enums, and public constants use `PascalCase`.
- Parameters and local variables use `camelCase`.
- Interfaces use the `I` prefix.
- Private fields use `_camelCase`.
- Nullable reference types remain enabled.
- Domain methods use business verbs such as `Accept`, `StartPreparing`, `MarkReady`, `Complete`, and `Cancel`.

### React and TypeScript

- Components and component filenames use `PascalCase`, for example `OrderDetailsPage.tsx`.
- Hooks begin with `use`, for example `useActiveOrders`.
- Variables, functions, and utility filenames use `camelCase`.
- Route folders and URL-oriented names use kebab-case.
- Avoid `any`; isolate and document it when an external boundary makes it unavoidable.

## Database naming

- EF Core migrations live in `Restaurant.Infrastructure` under `Persistence/Migrations`.
- Primary keys use `<EntityName>Id` in C#.
- Foreign-key properties use the referenced `<EntityName>Id`.
- SQL constraints and indexes receive explicit descriptive names when configured manually.
- Concurrency-token properties use `RowVersion`.
- Stable database invariants, including non-negative stock, are enforced with named check constraints.

Database naming decisions must remain compatible with SQL Server. Do not introduce a generic repository or Unit of Work wrapper over `DbContext`.

## Git conventions

`main` is the protected, releasable branch. Work uses short-lived branches created from an up-to-date `main`.

Branch names use lowercase kebab-case:

- `feature/<issue-number>-<summary>`
- `fix/<issue-number>-<summary>`
- `test/<issue-number>-<summary>`
- `docs/<issue-number>-<summary>`
- `chore/<issue-number>-<summary>`

If no issue exists yet, omit the issue number rather than inventing one.

Commit messages use a concise Conventional Commit-style prefix:

- `feat:`
- `fix:`
- `test:`
- `docs:`
- `refactor:`
- `chore:`
- `ci:`

Example: `docs: define week 0 architecture decisions`

Keep commits focused. Pull requests must state the observable outcome, tests actually run, risks, documentation changes, and any unverified items. A required CI stage failing means the pull request is not ready to merge.

## Changing these conventions

Do not silently change a published port, project name, route base, or Docker service name. Update this document, `.env.example`, Compose configuration, launch settings, frontend configuration, CI, and README together. Use an ADR when the change affects architecture, deployment topology, data authority, concurrency, or security guarantees.
