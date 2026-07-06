# CanonFlow

**CanonFlow** is a formal, mathematics-first bridge between relational database constraints, strongly-typed domain models (F#), and downstream clients (TypeScript/OpenAPI/OpenMetadata). 

Built as an agent-assisted "Second Secret Sauce" to [SqlHydra](https://github.com/ArunNotFound/SqlHydra) and NoSqlHydra, CanonFlow proves that you can extract a database schema, represent it as a formal bounded lattice, and deterministically project it across boundaries without loss of fidelity.

## The Core Philosophy (The Law)

> **`introspect(emit(domain)) ≅ domain`**

If a schema is ingested from Postgres (`introspect`), translated into our `Lattice` domain algebra, and then projected into DDL (`emit`), the resulting DDL must structurally map back to the original domain without loss of constraint logic.

Read the full ethos in the [MANIFESTO](MANIFESTO.md) and our [GOVERNANCE](GOVERNANCE.md) policies.

## Project Architecture

The solution follows a strict pipeline:

1. **`Canon.Core`**: The pure mathematical kernel. Contains the `Lattice<'Leaf>` algebra (True, False, Range, MaxLength, etc.) and `Refined<'T, 'P>` logic.
2. **`Canon.Introspect`**: The brownfield data extractor. Contains `PostgresSchemaProvider` (built on Npgsql) which harvests `information_schema` and `pg_constraint` into F# `TableDef` structures.
3. **`Canon.Emit`**: The greenfield generator. Translates `TableDef` schemas out to DDL and OpenSearch index mappings.
4. **`Canon.Fable`**: The transpiler bridge. Walks the F# Lattice and generates isomorphic TypeScript/JavaScript validation functions.
5. **`Canon.Contracts`**: The semantic output layer. Emits OpenMetadata JSON and OKF (Open Knowledge Foundation) markdown catalogs to empower AI Agents and enterprise catalogs.
6. **`Canon.Cli`**: The entry orchestrator. Run `dotnet run --project src/Canon.Cli/Canon.Cli.fsproj -- --help` to execute introspect, emit, and contract commands.

## Execution Plan & Decisions
All architectural decisions and phase milestones are documented in the [CanonFlow Execution Plan](docs/CanonFlow_Execution_Plan.md). We actively drew inspiration from the capabilities of *Symphony* (for OKF / Expression algebra) and *Helios* (for Semantic Capability-typed cataloging).

## Getting Started

To run the "30-Minute Stranger Demo" and view the extraction loop:

```bash
cd src/Canon.Cli
dotnet run -- --pg "Host=localhost;Database=mydb;Username=user;Password=pass" --contracts --demo
```
Check the `output/` folder for the generated `openmetadata` JSON and `catalog.md` files, and `client/src/validators.ts` for your native TypeScript validation logic!

## License
CanonFlow is steward-led Open Source under the [Apache 2.0 License](LICENSE).
