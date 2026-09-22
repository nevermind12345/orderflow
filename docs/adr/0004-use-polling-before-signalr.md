# ADR 0004: Use Polling Before SignalR

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

Administrators need reasonably fresh active-order information, and customers need to observe order-status changes. The first release serves one restaurant, so sub-second updates are not required. Real-time connections would add connection management, reconnection behaviour, scaling considerations, and more end-to-end failure modes before the core order guarantees are complete.

## Decision

Use HTTP polling for changing order views in version 1.

The administrator active-order board polls every five to ten seconds using TanStack Query. Customer order details may use the same approach while an order is active. Mutations invalidate or refresh affected queries after success and after a `409 Conflict`.

The UI must represent loading, empty, stale-data, conflict, unauthorized, forbidden, and unexpected-error states. Backend authorization and ownership remain authoritative regardless of what the polling UI displays.

SignalR is considered only after all core milestones pass and polling has a demonstrated usability or load limitation.

## Alternatives considered

- **SignalR from the beginning:** deferred because it adds infrastructure and client lifecycle complexity without being necessary for the initial freshness requirement.
- **Manual refresh only:** rejected because it creates a weak operational workflow and can leave active-order information stale for too long.
- **Aggressive one-second polling:** rejected because it creates unnecessary API and database load.
- **Messaging infrastructure for UI updates:** rejected because RabbitMQ, Kafka, and similar infrastructure are outside the core scope.

## Consequences

- Users may see updates up to one polling interval after they occur.
- API queries must be efficient, paginated in SQL, cancellable, and safe to execute repeatedly.
- Polling pauses or reduces activity when the relevant view is not active where supported by the client library.
- Stale administrator actions remain possible and must return `409 Conflict`; the UI then refreshes authoritative state.
- SignalR can be evaluated later using measured request volume, latency, and user-experience evidence.
