# CanonFlow Tutorial

Welcome to the 30-minute CanonFlow proving ground. By the end of this tutorial, you will have successfully extracted an undocumented enterprise Postgres database, transpiled its rules to TypeScript and OpenAPI, and generated a Constraint Fidelity Report.

## 1. Installation

Install CanonFlow as a global .NET tool:

```bash
dotnet tool install -g CanonFlow.Cli
```

## 2. Connect to your Database

Run CanonFlow against your local Postgres database. CanonFlow uses read-only introspection to harvest `information_schema` and `pg_catalog`.

```bash
canonflow --pg "Host=localhost;Database=my_app;Username=postgres;Password=root"
```

## 3. The Fidelity Report

Upon execution, CanonFlow will map the database to its internal `Lattice<Constraint>` algebra and cross-compile it to your frontend and API layers. 

You will see output resembling this:

```
[Drift Detection Report - HIGH SEVERITY]
- Field: age (TypeScript)
  DB Truth: CHECK (age >= 18 AND age <= 120)
  Severity: Low
  Action: No drift, 100% fidelity.

- Field: regex_code (OpenAPI)
  DB Truth: CHECK (regex_code ~ '^[A-Z]{3}-\d{4}$')
  Severity: High
  Action: Implement custom backend middleware guard. Reason: Cannot transpile raw SQL to OpenAPI.
```

## 4. Using the Emitted Contracts

Check your `output/` folder. CanonFlow will generate:
- `validators.ts`: Pure TypeScript validators natively reflecting DB rules.
- `schema.json`: OpenAPI bounds ready to be merged into your Swagger definitions.
- `catalog.json`: An OKF/OpenMetadata catalog enriched with AI Lineage metrics and Safe Queries.

You have now established mathematically verified governance over your schema!
