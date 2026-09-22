# ADR 0001: Use a Modular Monolith

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

OrderFlow must demonstrate clear domain boundaries, testability, and production-style engineering without allowing distributed-system infrastructure to delay the core release. The first release serves one restaurant and has one development and deployment team. Its most important risks are transactional correctness, concurrency, security, and deployment evidence rather than independent service scaling.

## Decision

Build OrderFlow as a modular monolith with these projects:

- `Restaurant.Domain`: entities, value objects, domain methods, invariants, and domain exceptions.
- `Restaurant.Application`: feature-oriented use cases, validation, authorization-aware behaviour, interfaces, and transaction orchestration.
- `Restaurant.Infrastructure`: EF Core, SQL Server, Identity integration, token persistence, background processing, and telemetry implementations.
- `Restaurant.Api`: HTTP endpoints, authentication, authorization, middleware, Problem Details, health checks, OpenAPI, and dependency registration.
- `Restaurant.Web`: the React and TypeScript frontend.

Dependencies point inward. `Restaurant.Domain` has no dependency on ASP.NET Core, EF Core, SQL Server, React, Azure, or infrastructure code. Application use cases own transaction boundaries, while domain methods protect business invariants and order transitions.

The initial Azure deployment uses one App Service for the API and compiled frontend unless measured evidence justifies a different topology.

## Alternatives considered

- **Microservices:** rejected because distributed transactions, messaging, service discovery, deployment coordination, and observability would add risk without a demonstrated scaling or ownership need.
- **Traditional layered application with weak boundaries:** rejected because business rules could drift into controllers or persistence code.
- **CQRS/MediatR as an architectural goal:** rejected because adding a mediator solely to claim CQRS does not improve the required guarantees.

## Consequences

- The application has one primary deployment unit and can use local SQL transactions for multi-record business operations.
- Project-reference tests and code review must enforce the dependency direction.
- Features should be organised by use case inside the Application project rather than by generic repository or service layers.
- Modules cannot be deployed or scaled independently in version 1.
- Microservices remain out of scope until the core release is complete and a concrete need is demonstrated.
