# CanonFlow Execution & Collaboration Plan

Based on the review of `direction_ai/MASTERPLAN.md`, the deep dives into `Symphony` and `Helios`, and integrating the split between `SqlHydra` and `NoSQLHydra`, here is our comprehensive master plan to tackle this massive Apache 2.0 undertaking. You mentioned we have **"all approval all clear can proceed of the gates"**, which fulfills the Phase 0 IP Clearance gate. We are officially cleared for liftoff.

## 1. The Core Architecture & Invariant

We will uphold the CanonFlow Constitutional Invariant:
`introspect (emit domain) ≅ domain` (with classified loss)

The foundation relies on:
- **State-based DDD**, not event sourcing.
- **Generated Code over Type Providers (ADR-009):** `SqlHydra` and `NoSQLHydra` will be built as explicit code generators emitting clean, diffable `.fs` files instead of opaque Type Providers.

## 2. The Great Split: SqlHydra & NoSQLHydra

To achieve the `introspect` and `emit` capabilities across both relational and document stores, we will split the driver responsibilities:

### A. SqlHydra (The Relational Wing)
- **Role:** Handles `Canon.Introspect` for brownfield RDBMS (Postgres, DuckDB, SQLite).
- **Action:** Read `information_schema` / `pg_constraint` and emit F# `Refined<'T,'P>` types (e.g., `CHECK (price > 0)` becomes `Refined<decimal, Positive>`).
- **Test:** Use the existing `docker/` container definitions (Postgres, MSSQL, MySQL) to run the schema introspection tests and validate the generated proofs.

### B. NoSQLHydra (The Document / Search Wing)
- **Role:** Handles Elasticsearch/OpenSearch mappings and the document projection side.
- **Reference:** Will heavily pull from the **Helios Deep Dive** (Elastic F# Query DSL). We need strong field kind information (`Keyword`, `Text`, `Nested`, `Date`) to make invalid Elasticsearch queries unconstructible in F#.
- **Action:** Convert the Symphony `Bridge.Folds` (which compiles strict ES mappings and bulk chunks) into the core of NoSQLHydra.

## 3. The Symphony Bridge & Helios DSL Integration

- **Symphony Bridge.Spec:** We will use the typed spec and pure folds pattern. Lineage (`Exact`, `Declared`, `Opaque`) and the expression algebra (`RCol`, `RConcat`, `RApply`) will form the backbone of our data mapping.
- **Helios Query DSL:** We will finalize the provider-independent query intent layer. The goal is a DSL where constraints become structured F# witnesses, eliminating runtime `failwith` scenarios.

## 4. Fable for DSL Transpilation (Phase 3)

We will use **Fable** as our bridge to the JavaScript/TypeScript ecosystem.
- **Goal:** Point CanonFlow at a Postgres DB, get `Domain.fs`, and then transpile the exact same validation predicates into TypeScript.
- **Inspiration:** Use Fable's AST transformations to ensure that a TypeScript client (`npm test`) rejects exactly the same invalid values that the F# server rejects (validated via FsCheck cross-runtime agreement tests).

## 5. Execution Steps & Docker Setup

To kick things off practically:

1. **Test the Base:** Spin up the `SqlHydra` docker containers (e.g., `docker-compose.yml` for Postgres) and run the current tests to ensure the base `SqlHydra` code is functioning.
2. **Refactor SqlHydra:** Pivot the codebase towards the explicit code-generation architecture defined in `ADR-009`, moving away from any legacy string-oriented constraint parsing.
3. **Stand up NoSQLHydra:** Build the Elasticsearch mapping and validation shell inspired by Helios and Symphony's `Bridge.Folds`.
4. **Wire the DSL:** Introduce the closed lattice algebra (`True/False/Leaf/Not/And/Or`) and Refined types.

---
### Let's Collaborate
This is indeed a massive, beautiful undertaking. Since we have passed the IP gates and are aligned on the Apache 2.0 BDFL-steward model:

**Where would you like me to start coding first?**
- Should I start by refactoring the `SqlHydra` generation engine?
- Or should I spin up the `docker-compose` tests to verify the baseline?
- Alternatively, do you want to start defining the core Refined types (`Refined<'T,'P>`) and the Canon algebra?
