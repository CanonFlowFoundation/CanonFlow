# Batch 1 project graph

Base graph (`ae89cd7deca60d3bfa6146c460a417686b0754d3`): 23 projects.

```text
src/Canon.Cli/Canon.Cli.fsproj
src/Canon.Conformance/Canon.Conformance.fsproj
src/Canon.Contracts/Canon.Contracts.fsproj
src/Canon.Core/Canon.Core.fsproj
src/Canon.Emit/Canon.Emit.fsproj
src/Canon.Fable/Canon.Fable.fsproj
src/Canon.Flow/Canon.Flow.fsproj
src/Canon.Introspect/Canon.Introspect.fsproj
src/Canon.SqlHydra/Canon.SqlHydra.fsproj
src/Canon.PgPrism/Canon.PgPrism.fsproj
src/CanonFlow.Assurance.Xp/CanonFlow.Assurance.Xp.fsproj  [moved]
src/CanonFlow.Assurance/CanonFlow.Assurance.fsproj
src/CanonFlow.Assurance.Contracts/CanonFlow.Assurance.Contracts.fsproj
src/CanonFlow.Evaluator/CanonFlow.Evaluator.fsproj
src/CanonFlow.Reports/CanonFlow.Reports.fsproj
src/Cff.Harness/Cff.Harness.fsproj
src/Cff.Runner/Cff.Runner.fsproj
tests/CanonFlow.Assurance.Tests/CanonFlow.Assurance.Tests.fsproj
tests/CanonFlow.Assurance.Xp.Tests/CanonFlow.Assurance.Xp.Tests.fsproj [moved]
tests/Cff.Harness.Tests/Cff.Harness.Tests.fsproj
tests/integration/Canon.IntegrationTests.fsproj
tests/laws/Canon.Core.Tests.fsproj
src/CanonFlow.Profile.Pgsql/CanonFlow.Profile.Pgsql.fsproj
```

Result graph: 21 projects. It is the base graph minus the two entries marked
`[moved]`; all other paths and ordering are unchanged.
