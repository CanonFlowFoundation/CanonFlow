# CanonFlow: The Missing Type-Provider for AI Agents

*Draft for F# Advent Calendar 2026*

## The Problem
As AI agents gain autonomous capabilities to write database records and spin up endpoints, we face a massive crisis: LLM hallucination in data schemas. If an LLM-generated form submits `age: 150` because it didn't know about a Postgres `CHECK (age <= 120)` constraint, your data integrity collapses.

Most ORMs approach this by generating TypeScript or C# from the database, but this is a one-way street without machine-verifiable evidence of total semantic equivalence.

## Enter CanonFlow
We built CanonFlow, an F#-native engine that elevates database constraints into a mathematical Lattice. 

Using `FsCheck`, we rigorously prove that the abstract syntax tree of your database rules (Negation Normal Form) obeys De Morgan's Laws and Involution. 

We then built a multi-target transpiler. CanonFlow takes your `Lattice<Constraint>` and compiles it down to:
- TypeScript Validators
- OpenAPI JSON Schemas
- Agent-Native OKF Catalogs

## The Drift Engine
Because we hold the canonical representation, we built a **Drift Engine**. When CanonFlow maps a rule into TypeScript, it emits a `Fidelity` score. `Exact`, `Approximate`, or `Unsupported`. We can mathematically prove exactly where your API or Frontend hallucinated a rule and failed to match the database truth.

F# was the perfect language for this. The discriminated unions made modeling the AST trivial, and FsCheck made it provable.

CanonFlow is now an open-source Governance Primitive. We've published the `CanonFlow.ConformanceSuite` so anyone can build verified drivers for MySQL or SQLite.
