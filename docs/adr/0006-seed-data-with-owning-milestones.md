# ADR 0006: Implement Development Seed Data with Its Owning Milestones

- **Status:** Accepted
- **Date:** 2026-09-21

## Context

The original milestone plan assigned deterministic development seed data to
Milestone 1, while the Foundation `RestaurantDbContext` intentionally contains
no business or Identity entities. Meaningful account seed data depends on
Milestone 2 Identity and role models. Meaningful category, menu-item,
availability, and stock seed data depends on the Milestone 3 catalog schema.

Adding a fake Foundation table or a no-op seeder would satisfy the plan only in
appearance. Pulling authentication or catalog entities into Foundation would
break the milestone sequence and expand scope before their validation and
authorization requirements are ready.

## Decision

Implement deterministic development seed data alongside the milestones that
introduce and own its real entities:

- Milestone 1 documents the configuration and ownership boundary. It does not
  create fake schema or claim seed behavior for nonexistent entities.
- Milestone 2 implements deterministic roles and demo administrator/customer
  accounts with Identity.
- Milestone 3 implements deterministic categories, menu items, availability,
  and stock quantities with the catalog schema.

All seed behavior must be explicitly enabled, restricted to the `Development`
environment, idempotent across repeated runs, and covered by tests against SQL
Server where persistence behavior matters. Seed configuration must use
placeholders in committed files. Real passwords and deployment credentials must
remain outside the repository and logs.

Production migrations remain controlled deployment steps. This decision does
not authorize production startup seeding or automatic production migration.

## Alternatives considered

- **Add a fake Foundation entity solely to seed it:** rejected because it adds
  schema with no product purpose and creates misleading portfolio evidence.
- **Add a no-op seeder in Foundation:** rejected because code existence does not
  prove deterministic seed behavior.
- **Pull Identity and catalog entities into Milestone 1:** rejected because it
  violates milestone ordering and brings later authorization, validation, and
  test obligations forward prematurely.
- **Remove deterministic seed data entirely:** rejected because repeatable local
  demo data remains valuable once the owning entities exist.

## Consequences

- Milestone 1 can prove its real Foundation guarantees without placeholder seed
  claims.
- Milestones 2 and 3 gain explicit seed deliverables and exit-gate evidence.
- Repeated development startup or seed execution must not create duplicate
  roles, accounts, categories, or menu items.
- Seed data cannot be used to bypass registration, authorization, stock, or
  other production rules.
- Documentation must distinguish inactive configuration placeholders from
  implemented seed behavior.
