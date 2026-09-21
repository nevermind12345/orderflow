# ADR 0002: Use SQL Server as the Authoritative Store

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

The browser displays menu data and maintains convenient client-side cart state, but that state can be stale or manipulated. OrderFlow must prevent overselling, preserve historical order data, enforce valid state transitions, and support secure refresh-token rotation. These guarantees require one authoritative persistence boundary.

## Decision

SQL Server is the source of truth for:

- menu-item prices, availability, archival state, and stock;
- order totals, item snapshots, status, and status history;
- refresh-token hashes, usage state, token families, and revocation;
- idempotency records if order-placement idempotency is added later.

Checkout accepts only menu-item identifiers and requested quantities. The application reloads current menu-item data from SQL Server, validates it, calculates prices and totals on the server, decrements stock, and creates the order and item snapshots in one transaction.

The React cart is convenience state only, and its displayed total is an estimate. Database constraints protect stable invariants such as non-negative stock. Important queries filter, order, and paginate in SQL.

## Alternatives considered

- **Trust browser-submitted prices or totals:** rejected because clients can be stale or malicious.
- **Use the cart as the inventory reservation system:** rejected because client state cannot provide transactional or concurrency guarantees.
- **Use Redis or another cache as the source of truth:** rejected because it adds consistency and operational complexity without a measured need.
- **Split authoritative state across services or databases:** rejected because it would complicate atomic order placement and cancellation.

## Consequences

- Checkout and order operations require database access and can return business conflicts when stored state has changed.
- Order items must store name and unit-price snapshots so later menu changes do not alter history.
- EF Core InMemory cannot prove transaction or SQL Server concurrency behaviour; applicable integration tests must use disposable real SQL Server.
- Caching may be added only as a non-authoritative optimisation with a clear invalidation strategy and measured benefit.
- Database migrations, constraints, indexes, and important generated SQL require review.
