# ADR 0007: Use Same-Origin Refresh-Cookie Sessions

- **Status:** Accepted
- **Date:** 2026-09-22

## Context

OrderFlow requires short-lived JWT access tokens, rotating refresh tokens,
single-use enforcement, token-family reuse detection, and logout revocation.

Refresh-token plaintext must never be persisted or exposed to browser
JavaScript. The browser and API also need a deliberate cookie, CORS, and CSRF
boundary. Treating these as incidental implementation details could weaken the
authentication guarantee or make local behavior differ materially from the
production deployment.

The approved deployment topology uses one Azure App Service to serve the API
and compiled React application. Local development should preserve the same
browser-facing origin model where practical.

## Decision

OrderFlow uses the following authentication-session boundary:

- ASP.NET Core Identity manages users, passwords, and Customer/Admin roles.
- The API issues short-lived JWT access tokens after successful login and
  refresh.
- The frontend holds the access token in memory only. It does not store access
  or refresh tokens in `localStorage` or `sessionStorage`.
- The refresh-token plaintext is sent only in an HTTP-only cookie.
- SQL Server stores only a cryptographic hash of each refresh token.
- Every successful login creates a new refresh-token family.
- Every successful refresh consumes the current token and creates exactly one
  successor in the same family.
- Presenting a previously used token revokes the entire family.
- Logout revokes the active token family and clears the refresh cookie.
- Already-issued JWT access tokens remain valid until their short expiration
  after logout. Version one does not add an access-token denylist.
- Multiple browser sessions may have separate token families.
- Token consumption, successor creation, and family revocation use SQL Server
  transactions and concurrency protection.

The refresh cookie uses:

- `HttpOnly`;
- `Secure`;
- `SameSite=Strict`;
- no explicit `Domain`;
- a path restricted to `/api/v1/auth`;
- an expiry aligned with the persisted refresh-token expiry.

Production serves the frontend and API from the same HTTPS origin.

Local React development uses an HTTPS Vite development server with a
same-origin proxy for `/api` requests. The browser calls the Vite origin, and
Vite proxies API requests to the local ASP.NET Core API. This avoids requiring
`SameSite=None` refresh cookies or credentialed cross-origin authentication
requests solely for local development.

Direct cross-origin refresh-cookie requests are not supported in version one.
CORS remains restricted to explicitly configured origins for non-cookie API
development needs and must never combine wildcard origins with credentials.

The CSRF boundary is provided by the combination of:

- same-origin production and local browser requests;
- an `HttpOnly`, `Secure`, `SameSite=Strict`, host-only refresh cookie;
- a cookie path restricted to `/api/v1/auth`;
- expected HTTP methods and content types on authentication endpoints;
- no state-changing work through GET requests; and
- restricted CORS configuration.

Refresh, logout, and other authentication mutation endpoints should also
validate the `Origin` header when present as inexpensive defense-in-depth. A
separate anti-CSRF-token scheme is not introduced in version one because
credentialed cross-origin requests are not supported. Such a scheme requires a
new review if that deployment boundary changes.

## Token-family persistence model

A refresh-token family records:

- its identifier;
- the owning Identity user;
- creation time;
- optional revocation time and reason;
- a SQL Server `rowversion` concurrency token.

Each refresh-token record stores:

- its identifier;
- family identifier;
- fixed-length token hash protected by a UNIQUE database constraint or index;
- creation and expiry times;
- optional used and revoked times;
- optional successor-token identifier;
- a SQL Server `rowversion` concurrency token.

Plaintext refresh tokens are generated with a cryptographically secure random
number generator. Only the hash is retained after the response cookie is
created.

Used and revoked token records remain available for reuse detection. Cleanup
or retention automation is deferred until there is a demonstrated operational
need.

The database transaction uses the token `rowversion` to prevent two requests
from successfully consuming the same token. Application-level state checks are
required for clear behavior but are not sufficient concurrency protection on
their own.

Strict single-use semantics apply to concurrent refresh attempts. When two
requests present the same token concurrently, one request may rotate it
successfully while the other observes that the token has been consumed and is
treated as reuse. Reuse revokes the family, including any successor already
issued by the winning request. SQL Server must never allow both requests to
consume the token successfully or leave two valid successors.

The frontend serializes refresh operations so only one refresh request is in
flight at a time. Callers share that operation rather than initiating parallel
refreshes, and a failed refresh is not retried blindly.

## Error behavior

Login failures do not reveal whether an email address exists.

Missing, unknown, expired, revoked, or reused refresh tokens return the same
safe authentication-failure contract. Internal token state, hashes, family
identifiers, and database details are not returned to clients or written to
logs.

Anonymous requests to protected endpoints return `401 Unauthorized`.
Authenticated users without the required role return `403 Forbidden`.

Identity role and demo-account seed behavior remains governed separately by
[ADR 0006](0006-seed-data-with-owning-milestones.md). It is not part of the
refresh-cookie session design.

## Implementation requirements

Authentication implementation is divided into small, reviewable slices that
collectively include:

- Identity user, refresh-token-family, and refresh-token entities;
- EF Core mappings, constraints, indexes, relationships, and `rowversion`
  configuration;
- cryptographically secure refresh-token generation and one-way hashing;
- login and short-lived JWT issuance;
- transactional refresh-token rotation;
- token-reuse detection and family-wide revocation;
- logout family revocation and cookie clearing;
- frontend in-memory access-token handling and refresh serialization;
- backend Customer/Admin role authorization;
- unit tests for behavior that does not depend on database semantics;
- real SQL Server integration tests for rotation, reuse detection, logout, and
  concurrent consumption;
- HTTPS Vite development-server and `/api` proxy configuration; and
- production same-origin configuration.

The real SQL Server tests must prove that concurrent requests cannot both
consume one refresh token successfully and cannot create two valid successors.
Any implementation proposal that contradicts this ADR must be flagged for
review before code is generated or the decision is changed.

## Alternatives considered

### Store refresh tokens in browser storage

Rejected because browser JavaScript and an XSS vulnerability could read the
long-lived credential.

### Persist refresh-token plaintext

Rejected because a database disclosure would immediately expose active
sessions.

### Use `SameSite=None` and credentialed cross-origin requests

Rejected for version one because it expands the CSRF and CORS surface without a
production requirement. It would require additional anti-CSRF controls and
careful credentialed-CORS configuration.

### Use one refresh token per user

Rejected because logging out one browser would prevent independent session
management and would make token-family reuse detection ambiguous.

### Use an external identity provider

Rejected for the core release because the approved scope specifically requires
ASP.NET Core Identity and does not require social login, OpenID Connect, or a
separate identity service.

### Add an access-token denylist

Rejected for version one because access tokens are deliberately short lived
and a denylist would add state, operational complexity, and a request-time
lookup to every protected API call. Logout revokes the refresh-token family but
does not invalidate already-issued access tokens.

### Add distributed session infrastructure or refresh grace periods

Rejected because SQL Server is already the authoritative session store and the
core requirements do not justify Redis, distributed locks, device
fingerprinting, or refresh-token grace periods.

## Consequences

- Local frontend development requires HTTPS and a Vite `/api` proxy.
- Frontend API calls use relative `/api/v1/...` URLs in the browser.
- Refresh-token rotation and logout can rely on a same-origin HTTP-only cookie.
- SQL Server remains authoritative for session and token-family state.
- Concurrent refresh attempts require deterministic real-SQL-Server
  integration tests.
- The frontend must serialize refresh attempts and must not blindly retry a
  failed refresh operation.
- Logout prevents future refreshes for the family but does not immediately
  invalidate already-issued access tokens.
- Production migration remains an explicit deployment step.
- A later cross-origin deployment would require a new security review and an
  ADR update covering `SameSite=None`, credentialed CORS, and CSRF protection.
