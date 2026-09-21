# OrderFlow Project Scope

**Status:** Approved baseline for Week 0
**Product:** Restaurant Ordering and Operations Platform
**Release:** Version 1 core portfolio release

## Purpose

OrderFlow is a production-style ordering and operations platform for one restaurant. It demonstrates full-stack .NET engineering across domain modelling, implementation, automated testing, cloud deployment, observability, and operational support.

The project is intentionally more than CRUD. Its main engineering story is:

> Customers can place orders without overselling limited stock, administrators can manage operational workflows, and expired orders are cancelled safely with inventory restored exactly once.

## Product boundary and users

Version 1 supports one restaurant, one inventory pool, and two roles:

- **Customer:** register, log in, browse the menu, manage a cart, place an order, receive stock-conflict feedback, and view only their own orders.
- **Administrator:** manage the menu, availability and stock, view active orders, and progress eligible orders through the approved lifecycle.

Payment is simulated; the application does not collect card details or imitate a real card-entry workflow. Menu images use URLs or bundled samples, category ordering is numeric, and the administrator board polls every five to ten seconds.

## Must-have scope

The core release includes:

- A modular monolith with `Restaurant.Domain`, `Restaurant.Application`, `Restaurant.Infrastructure`, `Restaurant.Api`, and `Restaurant.Web`.
- ASP.NET Core Identity with Customer and Admin roles, short-lived JWT access tokens, hashed rotating refresh tokens, reuse detection, family revocation, and logout.
- Menu and inventory administration with non-negative stock and archival of referenced items.
- A React and TypeScript customer journey and administrator active-order board.
- Server-authoritative, transactional checkout with historical item snapshots and explicit `409 Conflict` responses.
- The order lifecycle `Placed -> Accepted -> Preparing -> Ready -> Completed`, with cancellation allowed only from `Placed` or `Accepted`.
- A timeout worker that cancels expired orders and restores inventory transactionally, at most once.
- SQL Server persistence, migrations, constraints, indexes, and `rowversion` concurrency protection.
- Problem Details, validation, API authorization and ownership checks, secure configuration, structured logging, health checks, and an operational runbook.
- Domain, real-SQL-Server integration, frontend, concurrency, race-condition, and small end-to-end tests.
- Docker-based local development, GitHub Actions CI/CD, and public deployment to Azure over HTTPS.

## Non-negotiable guarantees

The implementation is not acceptable unless evidence proves that:

1. Stock never becomes negative.
2. Two customers competing for the final unit produce exactly one `201 Created` and one `409 Conflict`.
3. Order creation, order-item snapshots, stock decrement, and total calculation commit atomically.
4. The server is authoritative for price, stock, availability, and totals.
5. Historical order items retain name and price snapshots after menu changes.
6. Order status changes occur only through valid domain methods.
7. Cancellation and inventory restoration commit together, and inventory is restored at most once.
8. An administrator accepting an order and the timeout worker cancelling it cannot both succeed.
9. Customers can access only their own orders, and administrator permissions are enforced by the API.
10. Refresh tokens are hashed at rest, rotate on use, are single-use, and support token-family reuse detection.
11. Secrets, credentials, tokens, authorization headers, connection strings, and sensitive bodies never appear in source control or logs.
12. Production migrations are controlled deployment steps rather than automatic application-startup behaviour.

## Deferred until after the core release

Stretch features must not enter the critical path. The preferred order after the core definition of done is:

1. Idempotency keys for order placement.
2. SignalR live updates.
3. Redis for a concrete, measured need.
4. Kubernetes as a separate infrastructure exercise.
5. Optional LLM-assisted menu search with a non-LLM fallback.

## Explicit exclusions

Version 1 excludes:

- Real payments, card-data storage, refunds, promotions, vouchers, or loyalty points.
- Multiple restaurants or branches, delivery-driver tracking, and table reservations.
- Customer reviews and ratings.
- Email confirmation, password recovery, MFA, and social login.
- Microservices, GraphQL, event sourcing, RabbitMQ, Kafka, and Redis as a source of truth.
- Kubernetes as the primary deployment and LLM dependencies in core workflows.
- Generic repository, generic Unit of Work, service-locator, or event-bus abstractions without demonstrated need.

## Definition of done

The core project is complete only when:

- The customer and administrator journeys work end to end on a public HTTPS deployment.
- All guarantees are proven by deterministic tests using disposable real SQL Server where database behaviour matters.
- GitHub Actions passes the required build, test, security, migration, readiness, and deployed smoke-test gates.
- Migrations apply cleanly through a documented, controlled process.
- Logs and telemetry support investigation without exposing secrets.
- Another developer can run the application from a clean checkout using the README.
- The repository contains ADRs, diagrams, trade-offs, performance evidence, a runbook, screenshots, and a short demonstration.

Optional features do not compensate for a missing core guarantee. The team completes and proves the smallest compliant core system before expanding scope.
