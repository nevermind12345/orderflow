# ADR 0003: Use Optimistic Concurrency with SQL Server Rowversion

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

Concurrent customers may attempt to purchase the final unit of stock. An administrator may also accept an order while the timeout worker attempts to cancel it and restore inventory. Simple read-check-write logic can oversell stock or produce an accepted order whose inventory was restored.

Contention is expected to be occasional for one restaurant, so the system should detect conflicting writes without holding long-lived locks during ordinary request processing.

## Decision

Use SQL Server `rowversion` concurrency tokens on mutable `MenuItem` and `Order` records.

Application use cases own explicit transaction boundaries. Order placement reloads current items, validates them, decrements stock, creates snapshots, and saves the complete order atomically. Cancellation changes order state and restores all affected inventory within the same transaction.

Expected EF Core concurrency failures are translated into stable `409 Conflict` Problem Details responses. Raw database exceptions and internal values are not exposed. Stock conflicts include enough safe item information for the frontend to refresh availability and repair affected cart quantities.

No automatic retry is applied to order creation. A retry is permitted only after idempotency semantics are deliberately designed.

## Alternatives considered

- **No concurrency token:** rejected because separate reads and writes can overwrite each other and oversell inventory.
- **Application-process locks:** rejected because they fail across multiple processes or App Service instances.
- **Serializable transactions or pessimistic locking everywhere:** rejected as the default because broader, longer locks can reduce throughput and increase deadlock risk.
- **Atomic conditional SQL updates:** viable for a measured hotspot, but not selected as the initial general strategy because `rowversion` provides consistent stale-write handling across inventory and orders.

## Consequences

- API contracts and the frontend must treat stale writes as expected business conflicts.
- The acceptance-versus-cancellation race must protect both the order and affected inventory records.
- Concurrency tokens must be configured correctly in EF Core migrations and never accepted blindly from an untrusted client as authority.
- Tests must use controlled coordination, not sleeps or random timing.
- A mandatory real-SQL-Server integration test must prove that two simultaneous final-unit requests yield exactly one `201 Created`, one `409 Conflict`, stock `0`, and one persisted order.
- A separate race test must prove that acceptance and timeout cancellation have exactly one valid winner and inventory is restored at most once.
