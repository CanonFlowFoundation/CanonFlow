# CanonFlow Manifesto

> Schema → Proofs → Algebra → Contracts.
> A type-derivation framework where the database is the axiom set, refined types are the proofs, and every downstream contract is a theorem.

## The Constitutional Invariant

CanonFlow is governed by one law. Everything in the repository either serves it or is deleted.

`introspect (emit domain) ≅ domain` (with classified loss)

- **emit**: Domain types → storage schema (greenfield, DDD radiating outward)
- **introspect**: Storage schema → refined domain types (brownfield, DB radiating inward)
- **≅**: Equivalence up to `FieldClass` — every field is classified as `Lossless | Widened | Narrowed | Unrepresentable`, and the loss is *reported as data*, never silently absorbed.

## Non-Goals

1. **No migration engine**: CanonFlow emits deterministic, versioned, idempotent DDL (e.g., `V003__orders.sql`). Tools like Flyway/dbmate/Liquibase execute and evolve. Diffing is out of scope.
2. **No Event Sourcing**: The kernel is a schema↔types adjunction. A log-as-truth model gives the adjunction nothing to grab.
3. **No Type Providers**: We rely strictly on build-time code generation (ADR-009). Type Providers cause IDE fragility and opaque failures. We generate clean, Fantomas-formatted `.fs` files that are diffable in PRs.

## The Steward Posture

This is a steward-led open source project (Apache 2.0). It is not a product. If it helps one person, it has succeeded.
