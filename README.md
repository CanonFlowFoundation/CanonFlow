# CanonFlow

CanonFlow is not another application framework. It is a correctness framework.

If your software has business rules that must be followed consistently—tax calculations, banking rules, insurance claims, healthcare workflows, approvals, compliance, or complex pricing—CanonFlow helps you express those rules as executable, verifiable models instead of scattering them across thousands of lines of code.

## Use CanonFlow if...
- Your business has complex rules that change over time.
- Wrong decisions cost money, legal exposure, or customer trust.
- You need to explain why a decision was made.
- Multiple systems must implement the same rules consistently.
- AI agents or multiple developers are contributing code.
- Auditors or customers may ask, "Why did the system decide this?"

## Don't use CanonFlow if...
- You're building a simple CRUD application.
- Your rules are straightforward and unlikely to change.
- A personal website, blog, portfolio, or landing page is your project.
- A prototype or weekend hack matters more than long-term correctness.
- The overhead of modeling rules outweighs the benefit.

## A simple example

Imagine a shopping app.

**Without CanonFlow:**
```javascript
if (customer.IsPremium && total > 5000) { ... }
```
The same rule appears in five services. Six months later, marketing changes it. One service is forgotten. Now customers get inconsistent discounts.

**With CanonFlow:**

`PremiumDiscountRule`

There is one source of truth. Every service asks CanonFlow. Everyone gets the same answer.

## For AI

Instead of telling an AI:
*"Please remember all these business rules..."*

you give it:
*"Use the CanonFlow model."*

The AI doesn't invent rules—it follows the verified model.

## The honest trade-off

CanonFlow is not free. You spend more effort upfront modeling your domain. In return you gain:
- consistency
- explainability
- fewer production bugs
- easier maintenance
- greater confidence when rules change

## The one-sentence pitch

If your application's value comes from getting business decisions right—not just storing data—CanonFlow is worth considering. If your app is mostly CRUD, it probably isn't.

## Alternatives to CanonFlow

CanonFlow competes with different kinds of tools depending on the problem.

| If you need... | Consider... | Compared to CanonFlow |
| --- | --- | --- |
| Simple business rules | Hand-written domain logic | Lowest complexity, least structure |
| Workflow orchestration | Temporal, Camunda, Dapr Workflow | Great for process orchestration, not business rule modeling |
| Rule engines | Drools, NRules | Mature rule engines, but generally don't emphasize strongly typed F# domain modeling |
| Decision modeling | Camunda DMN | Good for business analysts and decision tables |
| Event sourcing | EventStoreDB, Marten | Solves history and state, not business rule correctness by itself |
| Type-safe domain modeling | F# with Domain-Driven Design | Closest philosophy, but lacks CanonFlow's verification concepts |

CanonFlow isn't trying to replace your database, web framework, workflow engine, or message bus. It aims to become the authoritative model of business rules that those systems use.

- **Small startup CRUD app:** Don't use CanonFlow yet. Keep it simple.
- **Growing SaaS with many changing business rules:** Evaluate CanonFlow or a mature rule engine.
- **Regulated domains (tax, finance, insurance, healthcare):** CanonFlow becomes much more compelling because correctness and traceability matter.
- **Need to orchestrate long-running processes:** Use a workflow engine such as Temporal or Camunda, and potentially pair it with CanonFlow if you also need a verified rule model.

Use the best tool for each concern. Let CanonFlow own business decision logic, while databases store data, workflow engines orchestrate processes, and web frameworks handle APIs.

## The current landscape

I don't think there is one compelling drop-in alternative to CanonFlow. Instead, there are projects that overlap with parts of what you're building:

| Project | What it does | Compared to CanonFlow |
| --- | --- | --- |
| PolicyFlow | AI workflow governance and policy-as-code for agent development. | Similar governance ideas, but focused on agent workflows rather than business domain modeling. |
| CanonSys | Policy DSL for governing autonomous agents with evidence trails. | Strong overlap for agent authorization and auditing, but not a general business-rule engine. |
| cascadeflow | Runtime policy enforcement for AI agents (cost, approvals, routing, compliance). | Runtime governance for AI, not domain or statutory modeling. |
| FPF (First Principles Framework) | Pattern language for evidence, architecture, and engineering decisions. | Similar philosophy around evidence and disciplined engineering, but it's a framework of practices, not an executable domain model. |

We don't see an open-source project that combines all of these into one system:
- Strongly typed domain model
- Executable business rules
- Rule provenance (where did this rule come from?)
- Effective dates and supersession
- Verification/proof concepts
- AI-agent consumption
- Code generation
- Domain-specific correctness

That combination is what makes CanonFlow different.

## CanonFlow vs ...

*Detailed comparisons coming soon:*
- CanonFlow vs Drools
- CanonFlow vs Camunda DMN
- CanonFlow vs Temporal
- CanonFlow vs Event Sourcing
- CanonFlow vs DDD
- CanonFlow vs Plain F#

**Conclusion:** CanonFlow's biggest competitors are plain code (if/else, switch statements, services), Domain-Driven Design without a formal rule engine, DMN/rule engines for enterprises, and workflow engines that people mistakenly use to encode business rules.

Business rules are first-class assets, separate from workflows, APIs, and storage, and can be verified, evolved, and consumed by both humans and AI. This is where CanonFlow shines.

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

1. **`Canon.Core`**: The pure mathematical kernel. Contains the `Lattice<'Leaf>` algebra (True, False, Range, MaxLength, etc.) and `Refined<'T, 'P>` logic.
2. **`Canon.PgPrism`**: The 2-way boundary enforcement engine. Uses `PgSqlParser` (the `.NET` wrapper for `libpg_query`) to deterministically parse PostgreSQL DDL into `Lattice<Constraint>`, and flawlessly round-trip emit it back into F# and SQL DDL.
3. **`Canon.Introspect`**: The brownfield data extractor. Harvests `information_schema` and `pg_constraint` into F# `TableDef` structures.
4. **`Canon.Emit`**: The greenfield generator. Translates `TableDef` schemas out to DDL and OpenSearch index mappings.
5. **`Canon.Fable`**: The transpiler bridge. Walks the F# Lattice and generates isomorphic TypeScript/JavaScript validation functions.
6. **`Canon.Contracts`**: The semantic output layer. Emits OpenMetadata JSON and OKF markdown catalogs to empower AI Agents.
7. **`Canon.Cli`**: The entry orchestrator. Run `dotnet run --project src/Canon.Cli/Canon.Cli.fsproj -- --help`.

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
