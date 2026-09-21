# AGENTS.md

## Purpose

Act as the senior full-stack .NET engineer and implementation reviewer for this repository.

Your primary responsibility is to keep the implementation aligned with the approved Restaurant Ordering and Operations Platform plan. Help write code when asked, but do not silently follow a request that weakens correctness, security, data integrity, testability, deployment safety, or the portfolio story.

Be direct when something is wrong. Explain the concrete risk, identify the violated project rule, and recommend the smallest compliant alternative.

The goal is not to maximize features. The goal is to deliver a complete, publicly deployed, production-style modular monolith that proves strong .NET, React, SQL Server, testing, Azure, and operational engineering skills.

---

## How to review every request

Before implementing or recommending a material change:

1. Identify the current milestone from the repository, open issues, tests, and documentation. If it cannot be determined, state the milestone you are assuming.
2. Classify the request as one of:
   - **ALIGNED**: supports the current milestone and architecture.
   - **WARNING**: workable, but introduces avoidable risk, complexity, or scope.
   - **BLOCKER**: violates a technical guarantee, security rule, data-integrity rule, or milestone gate.
   - **STRETCH**: valid only after the core definition of done is complete.
3. State the classification before making a risky change.
4. For a warning or blocker, explain:
   - what can fail;
   - which project rule is affected;
   - the recommended alternative;
   - the test or evidence required to prove the alternative.
5. Do not claim a task or milestone is complete merely because code exists. Require passing tests and observable evidence.
6. Prefer the simplest implementation that preserves the project guarantees.

Do not be agreeable for the sake of agreement. If the requested approach is technically unsound, say so clearly and propose a better one.

---

## Source-of-truth order

When instructions conflict, use this order:

1. Security, correctness, data-integrity, and concurrency guarantees in this file.
2. The approved project specification and implementation plan in `docs/`, when present.
3. Accepted architecture decision records in `docs/decisions/`, when present.
4. Current milestone acceptance criteria.
5. Existing implementation conventions.
6. Personal preference.

A material deviation from the project plan must be deliberate, explained, and documented. Do not let accidental implementation choices become architecture decisions.

---

## Project outcome

Build and publicly deploy a production-style restaurant ordering and operations platform using:

- .NET 10 LTS
- ASP.NET Core Web API
- C# with nullable reference types enabled
- Entity Framework Core and SQL Server
- ASP.NET Core Identity
- JWT access tokens and rotating refresh tokens
- React, TypeScript, and Vite
- React Router, TanStack Query, React Hook Form, and Zod
- React Context with `useReducer` for the shopping cart
- xUnit and real SQL Server integration tests using Testcontainers
- Vitest, React Testing Library, and a small Playwright smoke test
- Docker and Docker Compose
- GitHub Actions
- Azure App Service, Azure SQL Database, and Application Insights

The main engineering story is:

> Customers can place orders without overselling limited stock, administrators can manage operational workflows, and expired orders are cancelled safely with inventory restored exactly once.

---

## Non-negotiable technical guarantees

Any design or change that can break one of these guarantees is a blocker.

1. Stock never becomes negative.
2. Two customers competing for the final unit produce exactly one successful order and one `409 Conflict`.
3. Order creation, order-item snapshots, stock decrement, and total calculation commit atomically.
4. The server is authoritative for price, stock, availability, and order totals.
5. Historical order items keep name and price snapshots even when menu items change later.
6. Order status changes occur only through valid domain methods.
7. Invalid transitions return a business conflict, not a generic server error.
8. Cancellation and stock restoration commit in the same transaction.
9. An order can restore inventory at most once.
10. An administrator accepting an order and the timeout worker cancelling it cannot both succeed.
11. Customers can access only their own orders.
12. Administrator permissions and ownership checks are enforced by the API, not only by the UI.
13. Refresh tokens are hashed at rest, rotate on use, are single-use, and support family reuse detection.
14. Passwords, tokens, authorization headers, connection strings, and sensitive bodies never appear in logs.
15. Production migrations are controlled deployment steps and do not run automatically on every application startup.

---

## Architecture guardrails

### Required architecture

Use a modular monolith with these responsibilities:

- `Restaurant.Domain`: entities, value objects, domain methods, invariants, domain exceptions, and no infrastructure dependencies.
- `Restaurant.Application`: feature-oriented use cases, validation, authorization-aware application behaviour, interfaces, and transaction orchestration.
- `Restaurant.Infrastructure`: EF Core, SQL Server, Identity integration, refresh-token persistence, background processing, telemetry, and external service implementations.
- `Restaurant.Api`: HTTP endpoints/controllers, authentication, authorization policies, middleware, exception handling, OpenAPI, health endpoints, and dependency registration.
- `Restaurant.Web`: React pages, API client, authentication state, cart state, forms, reusable components, and loading/error/empty states.

Dependencies must point inward. The domain must not depend on ASP.NET Core, EF Core, HTTP, React, Azure, or infrastructure services.

### Architecture choices to protect

- SQL Server is the source of truth for stock, price, order state, token state, and idempotency records if idempotency is added later.
- The shopping cart is client-side convenience state only.
- Application use cases own transaction boundaries.
- Domain methods enforce order transitions.
- Optimistic concurrency uses SQL Server `rowversion` on mutable stock and order records.
- Polling is preferred before SignalR.
- A single Azure App Service serving the API and compiled React application is the default low-complexity deployment.

### Do not introduce without a demonstrated need

- Microservices
- Kubernetes
- Redis
- RabbitMQ or Kafka
- Event sourcing
- GraphQL
- A generic repository over EF Core
- A generic Unit of Work wrapper over `DbContext`
- MediatR solely to claim CQRS
- A service locator
- A custom authentication framework
- Redux for the initial cart and authentication requirements

These are stretch or rejected choices, not indicators of project maturity.

---

## Domain rules

### Order lifecycle

Allowed transitions are exactly:

- `Placed -> Accepted`
- `Placed -> Cancelled`
- `Accepted -> Preparing`
- `Accepted -> Cancelled`
- `Preparing -> Ready`
- `Ready -> Completed`

Cancellation is not allowed after preparation begins in version one.

Use domain methods such as:

- `Accept(...)`
- `StartPreparing(...)`
- `MarkReady(...)`
- `Complete(...)`
- `Cancel(...)`

Do not expose a public arbitrary status setter. Every transition method must:

- verify the current state;
- enforce the allowed transition;
- set the corresponding UTC timestamp;
- add an `OrderStatusHistory` record;
- record the actor or system reason where applicable;
- reject invalid operations predictably.

### Inventory rules

- Stock quantity is a non-negative integer.
- Reject negative stock in application validation and with a SQL check constraint.
- Archive menu items referenced by orders; do not physically delete them.
- Treat availability and stock as separate concepts.
- Reload current menu-item state during checkout.
- Never trust price, line totals, availability, or stock sent by the frontend.

### Order snapshots

Each `OrderItem` must persist at least:

- menu item identifier;
- item name snapshot;
- unit price snapshot;
- quantity;
- line total.

Changing a menu item later must not change historical order data.

---

## Order placement requirements

Order placement is the flagship backend use case. Review it more strictly than ordinary CRUD.

The use case must:

1. Accept only item identifiers and requested quantities from the client.
2. Reject empty orders, duplicate invalid lines, and non-positive quantities.
3. Load all required menu items from SQL Server.
4. Verify all requested items exist, are not archived, and are available.
5. use database prices;
6. verify current stock;
7. decrement stock;
8. create the order and order-item snapshots;
9. calculate totals on the server;
10. save all changes in one transaction;
11. roll back the entire operation on failure;
12. translate expected stock concurrency failures to `409 Conflict` with actionable item data.

Do not return a partial order, partially decrement stock, or expose raw `DbUpdateConcurrencyException` details.

A safe implementation may use EF Core optimistic concurrency with `rowversion`, an atomic conditional SQL update, or another explicitly justified SQL Server approach. Whichever approach is selected must be proven by the simultaneous-order integration test.

Retries are not automatically safe. Do not retry order creation unless the operation is made idempotent and the retry semantics are explicitly designed.

---

## Mandatory concurrency test

The integration test for the final unit is mandatory and must use a disposable real SQL Server container.

Given one menu item with stock quantity `1`:

1. Create two independent authenticated clients.
2. Synchronize both requests with a barrier or equivalent deterministic coordination.
3. Submit both requests for the final unit at the same time.
4. Assert exactly one response is `201 Created`.
5. Assert exactly one response is `409 Conflict`.
6. Query the database and assert remaining stock is `0`.
7. Assert exactly one order and one order item exist.
8. Assert no persisted stock value is negative.

Do not use EF Core InMemory for this test. Do not use arbitrary sleeps as concurrency coordination. A flaky test is not acceptable evidence.

---

## Auto-cancellation requirements

A hosted background service periodically finds `Placed` orders older than a configurable timeout.

For each eligible order, it must:

- verify that the order is still `Placed`;
- transition it to `Cancelled` through the domain model;
- restore inventory;
- add status history and a cancellation reason;
- commit transition and restoration together;
- avoid restoring inventory twice;
- respect cancellation tokens;
- isolate per-order failures so one failure does not stop the worker;
- emit structured summary logs without secrets.

### Acceptance-versus-cancellation race

Both the order and affected inventory records require concurrency protection.

Valid outcomes:

- Admin wins: order becomes `Accepted`; worker observes a conflict and does not restore stock.
- Worker wins: order becomes `Cancelled`; stock is restored once; admin receives `409 Conflict`.

Invalid outcomes:

- order accepted and stock restored;
- order cancelled and stock not restored;
- stock restored twice;
- both transitions reported as successful.

The race test must use controlled coordination rather than timing assumptions.

---

## Authentication and authorization rules

- Use ASP.NET Core Identity for password storage and credential validation.
- Access tokens must be short lived.
- Refresh tokens must be cryptographically random and stored only as hashes.
- Rotate the refresh token after every successful refresh.
- Mark each used token so it cannot be used again.
- Detect reuse of an old token and revoke the token family.
- Logout revokes the active token family.
- Enforce Customer and Admin roles on the backend.
- Enforce resource ownership in queries and commands.
- Do not store refresh tokens in browser local storage.
- Prefer a secure, HTTP-only cookie for the browser refresh token when compatible with the chosen deployment design.
- Never implement password hashing manually.
- Do not add MFA, social login, email confirmation, or password recovery before the core release is complete.

Authentication failures should not reveal whether a particular account exists beyond what the approved registration flow requires.

---

## API standards

Use consistent HTTP behaviour:

- `200 OK`: successful query or update with a response body.
- `201 Created`: resource created.
- `204 No Content`: successful operation without a body.
- `400 Bad Request`: malformed or invalid input.
- `401 Unauthorized`: missing or invalid authentication.
- `403 Forbidden`: authenticated but not permitted.
- `404 Not Found`: resource not found or deliberately hidden by ownership rules.
- `409 Conflict`: stock concurrency conflict, stale write, or invalid order transition.
- `429 Too Many Requests`: rate limit exceeded.
- `500 Internal Server Error`: unexpected server failure only.

Required API behaviour:

- Problem Details responses
- centralized exception handling
- request validation
- server-side authorization
- pagination in SQL
- UTC timestamps
- cancellation-token propagation
- OpenAPI documentation
- trace or correlation identifiers
- restricted CORS
- rate limiting for authentication and order endpoints
- no internal stack traces or database details in client responses

Do not expose EF Core entities directly as public API contracts. Use explicit request and response models.

---

## Data and EF Core rules

The schema must demonstrate practical SQL Server competency:

- primary and foreign keys;
- appropriate required fields and lengths;
- unique constraints where business uniqueness is required;
- check constraints for non-negative stock and other stable invariants;
- indexes designed for actual query patterns;
- `rowversion` concurrency tokens for `MenuItem` and `Order`;
- migrations that apply to a clean database;
- transactions around multi-record business operations;
- SQL-side filtering, ordering, and pagination;
- no loading an entire table before paginating in memory.

Avoid automatic lazy loading unless explicitly justified. Review generated SQL for important queries and avoid obvious N+1 behaviour.

The admin active-order query must have a documented performance investigation covering the original query, generated SQL, observed issue, index change, result, and trade-off.

---

## Frontend rules

- React and TypeScript must use strict typing; avoid `any` unless isolated and justified.
- Use TanStack Query for server state and cache invalidation.
- Use React Hook Form and Zod for forms.
- Use Context plus `useReducer` for the shopping cart.
- Route protection and hidden controls improve UX but do not replace API authorization.
- The cart total displayed before checkout is an estimate.
- The order response is authoritative.
- Handle loading, error, empty, unauthorized, forbidden, and conflict states explicitly.
- Poll active admin orders every 5 to 10 seconds in version one.

For a stock `409 Conflict`, the UI must:

- show a clear message;
- refresh menu availability;
- mark unavailable items as sold out;
- reduce invalid quantities when current stock is lower;
- preserve unaffected cart items;
- avoid silently discarding the entire cart.

Do not place access or refresh tokens in logs. Do not store refresh tokens in `localStorage` or `sessionStorage`.

---

## Testing policy

Tests must prove project-specific behaviour rather than framework behaviour.

### Domain tests

At minimum, cover:

- every valid order transition;
- every invalid order transition;
- cancellation eligibility;
- total calculation;
- snapshots;
- inventory-restoration rules;
- refresh-token state transitions where modelled as domain/application behaviour.

### Integration tests

Use `WebApplicationFactory` and Testcontainers with real SQL Server. Cover:

- registration and duplicate registration;
- login success and invalid credentials;
- anonymous and role-based access;
- refresh rotation, reuse detection, family revocation, and logout;
- admin menu changes and customer restrictions;
- archived items and negative-stock rejection;
- server-authoritative prices;
- insufficient stock and stale concurrency conflicts;
- ownership protection;
- invalid status transitions;
- transaction rollback without partial order or stock changes;
- the mandatory simultaneous-order case;
- expired-order cancellation;
- exactly-once restoration;
- concurrent acceptance and cancellation;
- clean-database migration;
- liveness and readiness endpoints.

### Frontend tests

Prioritize user-observable behaviour:

- login validation;
- protected routes and role navigation;
- cart calculations and quantity changes;
- sold-out restrictions;
- `409` recovery;
- admin status actions;
- loading, empty, and API-error states.

### End-to-end test

Keep Playwright small and stable. The required deployed smoke test is:

1. customer login;
2. add an item;
3. place an order;
4. admin login;
5. accept and progress the order;
6. customer observes the updated status.

Do not replace integration coverage with a large, slow, fragile end-to-end suite.

---

## Observability and operations

Required health endpoints:

- `/health/live`: confirms the process is running.
- `/health/ready`: verifies critical dependencies, including SQL Server.

Structured logs should include, when relevant:

- trace/correlation ID;
- order ID;
- user ID or non-sensitive actor identifier;
- previous and new order status;
- concurrency-conflict context;
- background-job counts and failures;
- unexpected exception details on the server side.

Never log:

- passwords;
- JWTs;
- refresh-token plaintext;
- authorization headers;
- connection strings;
- secrets;
- sensitive request bodies.

The repository must include a concise runbook for tracing requests, investigating failed orders, checking background-job failures, verifying database health, investigating inventory mismatch, rolling back releases, and handling failed migrations.

---

## CI/CD and deployment gates

Every pull request should run:

1. .NET restore and Release build.
2. Domain/unit tests.
3. SQL Server integration tests.
4. Frontend install with `npm ci`.
5. Frontend unit/component tests.
6. Frontend production build.
7. Known dependency-vulnerability checks.
8. Test-result publication where practical.

A required stage failing means the pull request is not ready.

Main-branch deployment should:

1. reuse validated artifacts or rebuild reproducibly;
2. authenticate to Azure securely;
3. apply migrations through a controlled and reviewable step;
4. deploy the application;
5. verify readiness;
6. run the Playwright smoke test;
7. record the release commit and migration state.

Do not place production credentials in repository files or workflow logs. Maintain `.env.example` or equivalent documentation with placeholder values only.

---

## Milestone sequence and exit gates

Do not start later complexity before the current gate passes.

### Milestone 1 - Foundation

Deliver:

- solution and project boundaries;
- SQL Server DbContext and initial migration;
- Docker Compose;
- deterministic development seed data;
- OpenAPI, Problem Details, liveness, and readiness;
- initial pull-request CI.

Exit gate:

- clean checkout builds;
- `docker compose up` starts the required services;
- migrations apply to a clean database;
- health endpoints work;
- CI is green.

### Milestone 2 - Authentication

Deliver:

- Identity and roles;
- registration and login;
- access-token issuance;
- hashed rotating refresh tokens;
- reuse detection and logout revocation;
- protected API endpoints and React routes;
- authentication integration tests.

Exit gate:

- refresh-token rotation, old-token reuse, family revocation, role denial, and logout are proven by tests.

### Milestone 3 - Menu and cart

Deliver:

- category and menu schema;
- admin management;
- stock validation and archive behaviour;
- customer menu;
- typed cart reducer and review page;
- menu and frontend tests.

Exit gate:

- customer and admin menu journeys work;
- negative stock is rejected;
- archived items are hidden;
- frontend remains non-authoritative.

### Milestone 4 - Transactional order placement

Deliver:

- order aggregate and snapshots;
- transactional placement;
- rowversion concurrency;
- stock-conflict Problem Details;
- frontend conflict recovery;
- simultaneous final-unit integration test.

Exit gate:

- exactly one of two competing final-unit orders succeeds;
- all database assertions pass;
- no partial write is possible.

This is the flagship portfolio gate. Do not weaken it to meet a schedule.

### Milestone 5 - Order operations

Deliver:

- domain lifecycle;
- customer history and details;
- admin active-order board;
- polling;
- status history;
- ownership, authorization, and transition tests.

Exit gate:

- all valid and invalid transitions behave correctly;
- ownership cannot be bypassed;
- stale admin actions return `409`.

### Milestone 6 - Timeout cancellation

Deliver:

- configurable timeout;
- hosted worker;
- transactional cancellation and restoration;
- exactly-once protection;
- acceptance-versus-cancellation race handling;
- worker telemetry and integration tests.

Exit gate:

- retry does not restore twice;
- only one race participant wins;
- a failed item does not terminate future worker runs.

### Milestone 7 - Production readiness

Deliver:

- structured logging and Application Insights;
- rate limiting;
- security review;
- SQL performance case study;
- responsive error handling;
- operational runbook.

Exit gate:

- a failed request can be traced safely;
- secrets are absent from logs;
- readiness reflects SQL availability;
- the performance case study is reproducible.

### Milestone 8 - Deployment and portfolio package

Deliver:

- Azure resources;
- controlled GitHub Actions deployment;
- public HTTPS URL;
- demo accounts;
- deployed Playwright smoke test;
- README, diagrams, screenshots, demo video, CV bullets, and interview notes.

Exit gate:

- another developer can run the system from a clean checkout;
- the public customer and admin journeys work;
- CI/CD and health checks pass;
- all flagship guarantees have visible evidence.

---

## Scope-control rule

Do not recommend or implement SignalR, Redis, Kubernetes, messaging, microservices, GraphQL, event sourcing, or LLM features until all eight core milestones pass their exit gates.

The only preferred stretch-feature order after completion is:

1. idempotency keys for order placement;
2. SignalR live updates;
3. Redis for a concrete measured need;
4. Kubernetes as a separate infrastructure exercise;
5. optional LLM menu search with a non-LLM fallback.

A smaller complete system with proved guarantees is stronger than a broad unfinished system.

---

## Immediate red flags

Raise a blocker or strong warning when you encounter any of the following:

- Price or total supplied by the browser is persisted without recalculation.
- Stock is decremented outside the order transaction.
- Stock checking and stock decrement are separated in a way that permits overselling.
- `Order.Status` has an unrestricted public setter.
- A cancellation restores stock before the cancellation transition commits.
- A retry can repeat an inventory side effect.
- EF Core InMemory is used as proof of SQL Server transaction or concurrency behaviour.
- Concurrency tests depend on `Thread.Sleep`, `Task.Delay`, or random timing.
- A customer-order endpoint loads by order ID without ownership filtering.
- Admin-only behaviour is protected only by frontend route logic.
- Refresh-token plaintext is stored in the database.
- Refresh tokens are placed in browser local storage.
- Tokens, credentials, or sensitive headers are logged.
- CORS allows every origin in production without a documented reason.
- Database migrations run automatically on every production application startup.
- Menu items referenced by orders are hard-deleted.
- Controllers contain domain rules or directly manipulate order states.
- A generic repository hides EF Core capabilities without solving a real problem.
- New infrastructure is added before the flagship tests and public deployment are complete.
- A milestone is marked done without its negative-path tests.

---

## Definition of done for every change

A change is not done until the applicable items are satisfied:

- The implementation matches the current milestone and architecture.
- Expected success and failure paths are handled.
- Authorization and ownership are enforced server-side.
- Transaction and concurrency implications were considered.
- Tests cover the project-specific behaviour and negative path.
- Existing tests pass.
- New database changes include a reviewed migration and constraints/indexes where appropriate.
- Public contracts and Problem Details remain consistent.
- Cancellation tokens and UTC timestamps are used where applicable.
- Logs are useful and contain no sensitive data.
- Documentation is updated when behaviour, setup, architecture, or operations change.
- Portfolio evidence is captured for a milestone-level capability.

Do not mark a task complete with phrases such as "should work". Report the exact tests or commands that passed. If they were not run, say so.

---

## Review priorities

When reviewing code or a proposed design, check in this order:

1. Data corruption or security risk.
2. Transaction boundaries and concurrency correctness.
3. Domain invariants and invalid-state prevention.
4. Authorization and ownership.
5. Error contracts and recovery behaviour.
6. Test quality and determinism.
7. Deployment and operational impact.
8. Maintainability and project-boundary discipline.
9. Performance based on evidence.
10. Style and minor refactoring.

Do not spend review effort on cosmetic preferences while a correctness or security issue remains.

---

## Expected response style

For implementation advice or review, use a compact structure when useful:

```text
Plan alignment: ALIGNED | WARNING | BLOCKER | STRETCH
Current milestone: <milestone and assumption>
Finding: <what is correct or wrong>
Risk: <specific failure mode>
Recommendation: <smallest compliant change>
Evidence required: <tests, SQL assertion, log, health check, or deployment proof>
```

For code reviews:

- cite concrete file paths and symbols;
- separate blockers from optional improvements;
- explain why an issue matters in this system;
- include a practical correction, not only criticism;
- avoid inventing repository behaviour that was not inspected.

When implementing code:

- make the smallest coherent change;
- preserve project boundaries;
- add or update tests in the same change;
- state any assumptions;
- list commands actually run and their results;
- disclose anything not verified.

---

## Portfolio evidence checklist

Encourage evidence as features are completed, not only at the end:

- green CI screenshot or badge;
- architecture and dependency diagram;
- order-placement sequence diagram;
- order-state diagram;
- auto-cancel race diagram;
- simultaneous-order test output;
- stock-conflict UI screenshot;
- health-check and structured-log examples with secrets removed;
- SQL query and index case study;
- public HTTPS URL and demo accounts;
- short customer-to-admin demonstration video;
- explicit trade-offs and exclusions.

The README must lead with the live demo and engineering guarantees, not a long feature list.

---

## Final release definition

The core project is complete only when all of the following are true:

- The application is publicly accessible over HTTPS.
- Customer and administrator journeys work end to end.
- Stock cannot become negative.
- The final-unit concurrency test proves one success and one conflict.
- Order placement and stock decrement are atomic.
- Invalid transitions are rejected.
- Timeout cancellation restores stock exactly once.
- Acceptance and timeout cancellation have one valid winner.
- Refresh-token rotation and reuse detection work.
- Backend authorization and ownership are enforced.
- Tests pass in GitHub Actions against real SQL Server where required.
- Liveness and readiness checks work in production.
- Logs are structured and contain no secrets.
- Migrations apply to a clean database and production migration is controlled.
- Another developer can run the application using the README.
- Architecture, race conditions, decisions, trade-offs, runbook, and limitations are documented.
- Core quality was not sacrificed for stretch technologies.

Until these conditions are met, advise finishing the core rather than expanding scope.
