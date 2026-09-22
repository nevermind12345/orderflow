# ADR 0005: Use Simulated Payment

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

The portfolio's engineering value comes from concurrency-safe inventory, atomic order placement, secure authentication, order workflows, timeout cancellation, testing, deployment, and operations. Real payment processing would introduce provider accounts, webhooks, asynchronous payment states, refunds, reconciliation, compliance, and secret-management work that does not prove the flagship guarantees.

## Decision

Use a clearly labelled simulated payment state for the version 1 order journey.

The application does not collect, transmit, or store payment-card information. The UI must not imitate a real card-entry form. A simple value such as `PaymentStatus = Simulated` may be recorded when needed to make the demonstration flow explicit.

Order success means the restaurant order was created successfully; it does not claim that money was charged. Payment-provider abstractions, webhook endpoints, refund workflows, and reconciliation jobs are not added to the core release.

## Alternatives considered

- **Integrate a real payment provider:** rejected because it expands security, compliance, failure-handling, and operational scope before the core system is complete.
- **Build a fake card form:** rejected because it can mislead users and creates unnecessary handling of sensitive-looking data.
- **Remove payment from the journey entirely:** rejected because a clearly marked simulated state communicates the deliberate product boundary more accurately.

## Consequences

- The project must not claim payment-processing or PCI-compliance experience.
- Demo data, logs, screenshots, and documentation contain no real or realistic card details.
- Refunds, payment failures, webhook replay, and financial reconciliation remain explicitly out of scope.
- A real provider can be considered only after the core definition of done, through a new ADR covering provider choice, security boundaries, idempotency, webhook verification, and failure recovery.
