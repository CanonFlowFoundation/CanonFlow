# CanonFlow

CanonFlow is a correctness framework rather than a traditional application framework. It is designed for software systems governed by strict business rules—such as tax calculations, banking rules, insurance claims, healthcare workflows, approvals, compliance mandates, or complex pricing. CanonFlow provides a mechanism to express these rules as executable, verifiable models, preventing the scattering of logic across thousands of lines of code.

## Ideal Use Cases
- Business domains with complex, evolving rules.
- Systems where incorrect decisions carry high costs (financial, legal, or reputational).
- Applications requiring auditability and explainability for system decisions.
- Distributed architectures where multiple systems must implement the same rules consistently.
- Codebases with contributions from AI agents or multiple developers.

## Non-Ideal Use Cases
- Simple CRUD applications.
- Systems with straightforward, static rules.
- Personal websites, portfolios, or landing pages.
- Prototypes where speed of delivery outweighs long-term correctness.
- Projects where the overhead of formal rule modeling exceeds its benefits.

## Example Scenario: E-Commerce Application

**Without CanonFlow:**
```javascript
if (customer.IsPremium && total > 5000) { ... }
```
A discount rule may be duplicated across multiple microservices. When marketing requirements change, updating all instances becomes error-prone, leading to inconsistent system behavior.

**With CanonFlow:**
A single `PremiumDiscountRule` serves as the verified source of truth. Every service queries CanonFlow, ensuring consistent evaluation across the entire architecture.

## AI Agent Integration

When integrated with AI, CanonFlow provides a verified domain model rather than relying on the AI to memorize complex business logic. The AI agent leverages the CanonFlow model to make decisions, preventing it from inventing undocumented rules.

## Trade-Offs

Adopting CanonFlow requires an upfront investment in modeling the domain. In exchange, the system gains:
- Consistency
- Explainability
- Reduced production bugs
- Easier long-term maintenance
- Greater confidence during rule modifications

## Core Proposition

CanonFlow is intended for applications where value is derived from accurate business decisions, rather than pure data storage. 

## Alternatives and Positioning

CanonFlow occupies a distinct niche and can be compared to various tools based on the specific problem being solved:

| Requirement | Alternative | Comparison to CanonFlow |
| --- | --- | --- |
| Simple business rules | Hand-written domain logic | Lower complexity, but lacks formal structure and verification |
| Workflow orchestration | Temporal, Camunda, Dapr Workflow | Excels at process orchestration, but not designed for business rule modeling |
| Rule engines | Drools, NRules | Mature engines, but typically lack strongly typed F# domain modeling |
| Decision modeling | Camunda DMN | Suitable for analysts and decision tables |
| Event sourcing | EventStoreDB, Marten | Solves history and state, but does not guarantee business rule correctness |
| Type-safe domain modeling | F# with Domain-Driven Design | Similar philosophy, but lacks CanonFlow's verification and projection concepts |

CanonFlow does not replace databases, web frameworks, workflow engines, or message buses. Instead, it serves as the authoritative model of business rules utilized by those systems.

- **Startups / Simple CRUD:** Hand-written logic is often sufficient.
- **Growing SaaS (Dynamic Rules):** CanonFlow or a mature rule engine becomes necessary.
- **Regulated Domains:** CanonFlow is highly compelling due to its correctness and traceability guarantees.
- **Long-Running Processes:** Workflow engines (Temporal, Camunda) can be paired with CanonFlow for verified rule evaluation within orchestrated processes.

## The Current Landscape

While several projects address parts of this problem space, there is no single drop-in alternative that offers CanonFlow's full feature set:

| Project | Focus | Comparison to CanonFlow |
| --- | --- | --- |
| PolicyFlow | AI workflow governance and policy-as-code | Focuses on agent workflows rather than business domain modeling |
| CanonSys | Policy DSL for governing autonomous agents | Overlaps in agent auditing, but is not a general business-rule engine |
| cascadeflow | Runtime policy enforcement for AI agents | Handles runtime governance for AI, not statutory or domain modeling |
| FPF (First Principles Framework) | Pattern language for architecture | A framework of practices, not an executable domain model |

CanonFlow distinguishes itself by combining the following into a cohesive system:
- Strongly typed domain model
- Executable business rules
- Rule provenance (tracking rule origins)
- Effective dates and supersession
- Verification and formal proofs
- AI-agent consumption capabilities
- Code generation
- Domain-specific correctness

## Competitors and Synergies

*Detailed comparisons coming soon:*
- CanonFlow vs Drools
- CanonFlow vs Camunda DMN
- CanonFlow vs Temporal
- CanonFlow vs Event Sourcing
- CanonFlow vs DDD
- CanonFlow vs Plain F#

CanonFlow's primary competitors are plain code (if/else, switch statements, services), Domain-Driven Design without a formal rule engine, enterprise DMN/rule engines, and workflow engines incorrectly utilized for encoding business rules.

CanonFlow treats business rules as first-class assets—separate from workflows, APIs, and storage—that can be verified, evolved, and consumed by both humans and AI.

---

## What is CanonFlow technically?

> CanonFlow is a formal schema compiler that proves database rules, backend types, frontend validators, API contracts, and AI metadata are the same truth.

Before AI agents change code, CanonFlow gives them a verified contract of database truth. It extracts constraints from live systems (Postgres, DuckDB) into a mathematical lattice, transpiles them across the stack (Fable/TypeScript, OpenSearch), and proves constitutional symmetry. 

Built as an agent-assisted "Second Secret Sauce" to [SqlHydra](https://github.com/ArunNotFound/SqlHydra) and NoSqlHydra, CanonFlow proves that you can extract a database schema, represent it as a formal bounded lattice, and deterministically project it across boundaries without loss of fidelity.

## The Core Philosophy (The Law)

> **`introspect(emit(domain)) ≅ domain`**

If a schema is ingested from Postgres (`introspect`), translated into our `Lattice` domain algebra, and then projected into DDL (`emit`), the resulting DDL must structurally map back to the original domain without loss of constraint logic.

Read the full ethos in the [MANIFESTO](MANIFESTO.md) and our [GOVERNANCE](GOVERNANCE.md) policies.

## Project Architecture

The solution follows a strict pipeline:

1. **CanonFlow.Assurance**: The pure evidence and verdict mathematical kernel.. Contains the `Lattice<'Leaf>` algebra (True, False, Range, MaxLength, etc.) and `Refined<'T, 'P>` logic.
2. **`Canon.PgPrism`**: The 2-way boundary enforcement engine. Uses `PgSqlParser` (the `.NET` wrapper for `libpg_query`) to deterministically parse PostgreSQL DDL into `Lattice<Constraint>`, and flawlessly round-trip emit it back into F# and SQL DDL.
3. **`Canon.Introspect`**: The brownfield data extractor. Harvests `information_schema` and `pg_constraint` into F# `TableDef` structures.
4. **`Canon.Emit`**: The greenfield generator. Translates `TableDef` schemas out to DDL and OpenSearch index mappings.
5. **`Canon.Fable`**: The transpiler bridge. Walks the F# Lattice and generates isomorphic TypeScript/JavaScript validation functions.
6. **`Canon.Contracts`**: The semantic output layer. Emits OpenMetadata JSON and OKF markdown catalogs to empower AI Agents.
7. **Canon.Cli**: The entry orchestrator.
8. **CanonFlow.Evaluator**: The orchestrated deterministic engine for offline scanning.
9. **CanonFlow.Reports**: Verifiable view projectors for CanonFlow execution. Run `dotnet run --project src/Canon.Cli/Canon.Cli.fsproj -- --help`.

## Execution Plan & Decisions
All architectural decisions and phase milestones are documented in the [CanonFlow Execution Plan](docs/CanonFlow_Execution_Plan.md). We actively drew inspiration from the capabilities of *Symphony* (for OKF / Expression algebra) and *Helios* (for Semantic Capability-typed cataloging).

## Getting Started

To run the "30-Minute Stranger Demo" and view the extraction loop:

```bash
cd src/Canon.Cli
dotnet run -- --pg "Host=localhost;Database=mydb;Username=user;Password=pass" --contracts --demo
```
Check the `output/` folder for the generated `openmetadata` JSON and `catalog.md` files, and `client/src/validators.ts` for your native TypeScript validation logic!

## 🌐 Foundation

Learn more about the underlying philosophy and mathematical model at the [CanonFlow Foundation](https://canonflowfoundation.github.io).

## License
CanonFlow is steward-led Open Source under the [Apache 2.0 License](LICENSE).
