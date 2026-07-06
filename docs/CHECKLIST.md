# CanonFlow Launch Checklist

This document consolidates the remaining high-level tasks required to elevate CanonFlow to a 10/10 Proof Engine, ready to unleash into sample apps and production environments.

## 🟢 Phase 1: The Sample App Proving Ground
*These tasks must be completed to successfully unleash CanonFlow into a real sample app without friction.*

- [x] **Live Postgres Brownfield Queries:** Replace the placeholder `PostgresSchemaProvider` data with actual `information_schema` / `pg_catalog` SQL queries that extract live PKs, FKs, Unique Indexes, and Default Values.
- [x] **The "Agreement Test" (Node.js vs F#):** Build a test where random FsCheck lattices are evaluated in native F#, transpiled to TypeScript, and executed in a hidden Node.js process. Prove byte-for-byte agreement.
- [x] **NNF Canonical Normalization:** Implement Negation Normal Form (NNF) in `Lattice.fs`. This guarantees structural equality matches semantic equality, enabling caching.
- [x] **`dotnet tool` Packaging:** Make CanonFlow installable with a single global command (`dotnet tool install -g canonflow`) so sample apps can invoke it cleanly in their build pipelines.
- [x] **The Killer Terminal Demo:** Polish the CLI so it connects to a DB, extracts rules, transpiles to TS, proves equivalence, and prints the High-Severity Drift Report all in one command.

## 🟡 Phase 2: Enterprise & AI Readiness
*These tasks solidify CanonFlow as the trusted Governance Primitive for AI agents.*

- [x] **OpenAPI Transpiler & Drift:** Write a second transpiler for OpenAPI/Swagger. Run the Drift Engine across both TypeScript AND OpenAPI simultaneously to catch three-way drift.
- [x] **Agent-Native OKF Catalog:** Ensure the generated OpenMetadata / OKF JSON includes the mathematically verified constraints, Lineage paths, and suggested "Safe Queries" so agents can read it.
- [x] **Mutation Audit on Laws:** Implement mutation testing to prove our FsCheck laws aren't vacuous. If `Lattice.fs` is mutated, the laws must fail.
- [x] **Diátaxis Documentation:** Write the 4-part docs structure: Tutorial (30-min demo), How-To (Write a Driver), Reference (XML docs), and Explanation (VISION.md).

## 🔴 Phase 3: Community & Scale
*Tasks for the public launch and ecosystem scale.*

- [ ] **Stranger Testing:** Hand the repo to two strangers. Have them attempt the tutorial unassisted. Ensure zero blockers.
- [ ] **Third-Party Driver Conformance Suite:** Extract the Postgres tests into a generalized `Conformance Suite` so the community can build MySQL or SQLite drivers without our help.
- [ ] **F# Advent Post:** Write the launch post detailing the Million Dollar "Proof Engine" pivot and the Constraint Fidelity Report.
- [ ] **Banyan Bench Data Published:** Prove AI coding agents generate fewer bugs when handed a CanonFlow Fidelity Report vs raw DB schemas.
