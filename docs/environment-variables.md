# Environment-Variable Inventory

- **Status:** Accepted baseline for Week 0
- **Last reviewed:** 2026-07-29

This inventory defines OrderFlow configuration names and where their values belong. It contains no real credentials. Environment variables use `__` to represent nested ASP.NET Core configuration keys.

## Storage rules

- Commit `.env.example`; never commit `.env`.
- Use .NET user secrets or process-level environment variables for local API secrets.
- Use an ignored `.env` only for local Docker Compose substitution.
- Store CI secrets in GitHub Actions secrets or protected environments.
- Store production values in Azure App Service application settings or another approved secret store.
- Never put passwords, connection strings, JWT signing keys, tokens, or authorization headers in frontend variables, logs, screenshots, issues, or pull requests.
- Treat every `VITE_` variable as public because Vite embeds it in browser-delivered JavaScript.
- Production startup must fail clearly when a required secret is missing or still begins with `CHANGE_ME`.

## Runtime and database

| Variable | Secret | Required in | Safe example | Purpose |
| --- | --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | No | All | `Development` | Selects `Development`, `Testing`, or `Production` behaviour. |
| `ASPNETCORE_URLS` | No | Container | `http://+:8080` | Binds the API to the agreed container port. Local launch settings may bind ports `5000` and `5001`. |
| `SQL_SA_PASSWORD` | Yes | Local Compose/CI | `CHANGE_ME` | Initial SQL Server container administrator password. The deliberately invalid placeholder must be replaced before startup. It is not an application credential. |
| `ConnectionStrings__DefaultConnection` | Yes | API/CI/Production | See `.env.example` | Connects the API to SQL Server. The host is `sql,1433` from Compose and `localhost,1433` from a locally running API. |

`TrustServerCertificate=True` is allowed only for local disposable SQL Server. Production uses Azure SQL encryption and certificate validation without this development exception.

## Authentication and session

| Variable | Secret | Required in | Safe example | Purpose |
| --- | --- | --- | --- | --- |
| `Jwt__Issuer` | No | API | `Restaurant.Api` | Expected JWT issuer. |
| `Jwt__Audience` | No | API | `Restaurant.Web` | Expected JWT audience. |
| `Jwt__SigningKey` | Yes | API/CI/Production | `CHANGE_ME_AT_LEAST_32_RANDOM_CHARACTERS` | Signs access tokens. Production uses a cryptographically random value supplied outside source control. |
| `Jwt__AccessTokenLifetimeMinutes` | No | API | `15` | Configures the short-lived access-token lifetime. |
| `RefreshTokens__LifetimeDays` | No | API | `7` | Configures refresh-token expiry; rotation and reuse detection remain mandatory. |
| `RefreshTokens__CookieName` | No | API/Web | `orderflow.refresh` | Names the secure HTTP-only browser refresh-token cookie. |

Refresh-token plaintext is never persisted. The database stores only cryptographic hashes and token-family state. Production cookies must be `Secure`, `HttpOnly`, and use an explicitly reviewed `SameSite` policy; these security attributes are code-level guarantees, not user-controlled environment switches.

## Browser access and frontend

| Variable | Secret | Required in | Safe example | Purpose |
| --- | --- | --- | --- | --- |
| `Cors__AllowedOrigins__0` | No | API | `http://localhost:5173` | Allows the exact Vite development origin. Additional origins use sequential indexes. |
| `VITE_API_BASE_URL` | No | Web build | `https://localhost:5001` | Browser-visible API origin for local Vite development. |

Production must use its exact HTTPS origin and must not use wildcard CORS. If the production frontend and API are served from the same origin, the frontend should prefer same-origin requests rather than embedding an unnecessary external hostname.

## Order cancellation

| Variable | Secret | Required in | Safe example | Purpose |
| --- | --- | --- | --- | --- |
| `OrderCancellation__PlacedOrderTimeoutMinutes` | No | API/Worker | `15` | Age after which a still-`Placed` order becomes eligible for timeout cancellation. |
| `OrderCancellation__PollingIntervalSeconds` | No | API/Worker | `60` | Delay between worker scans. |

Both values require positive bounds validation at startup. Changing them does not weaken the transactional, concurrency, or exactly-once restoration guarantees.

## Development seed data

| Variable | Secret | Required in | Safe example | Purpose |
| --- | --- | --- | --- | --- |
| `DevelopmentSeed__Enabled` | No | Development only | `false` | Explicitly enables deterministic local seed accounts. |
| `DevelopmentSeed__AdminEmail` | No | Development only | `admin@example.invalid` | Local seeded administrator identifier. |
| `DevelopmentSeed__AdminPassword` | Yes | Development only | `CHANGE_ME_ADMIN_PASSWORD` | Local seeded administrator password. |
| `DevelopmentSeed__CustomerEmail` | No | Development only | `customer@example.invalid` | Local seeded customer identifier. |
| `DevelopmentSeed__CustomerPassword` | Yes | Development only | `CHANGE_ME_CUSTOMER_PASSWORD` | Local seeded customer password. |

Seed-account creation must be blocked outside `Development`. Public demo credentials, if deliberately provided later, must be low-privilege, documented separately, and supplied through deployment configuration rather than committed as production secrets.

## Telemetry

| Variable | Secret | Required in | Safe example | Purpose |
| --- | --- | --- | --- | --- |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Yes | Production | Empty locally | Connects production telemetry to Application Insights. It must never be written to logs or client configuration. |

## Local setup

1. Copy `.env.example` to `.env`.
2. Replace every `CHANGE_ME` value with a unique local value.
3. Keep `.env` untracked and confirm it does not appear in `git status`.
4. When running the API outside Compose, store the `localhost,1433` connection string and JWT signing key with .NET user secrets instead of committing local settings.
5. Before deployment, configure every production secret independently in Azure and GitHub; do not promote the local `.env` file.

When a new environment variable is introduced, update this inventory, `.env.example` when appropriate, validation, deployment configuration, and setup documentation in the same change.
