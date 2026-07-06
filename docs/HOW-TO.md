# How-To: Write a CanonFlow Driver

CanonFlow ships with Postgres out of the box. But what if you need to extract rules from MySQL, SQL Server, or DuckDB? You can write a custom Driver.

## 1. Implement `ISchemaProvider`

Every driver must implement the `ISchemaProvider` interface found in `Canon.Introspect`.

```fsharp
type MySqlSchemaProvider(connectionString: string) =
    interface ISchemaProvider with
        member this.Harvest() =
            // Query information_schema
            // Yield TableDef structures
```

## 2. Extract Check Constraints

The hardest part of a driver is fetching raw CHECK constraints. In MySQL, you'd query `information_schema.check_constraints`. 

Pass the raw SQL strings into `Canon.Introspect.SqlParser.parseConstraint string`. It will automatically turn standard SQL (like `value > 5 AND value < 10`) into `Lattice.And(Lattice.Leaf(Range(>5)), Lattice.Leaf(Range(<10)))`.

## 3. Handle Opaque Constraints

If the database enforces a rule CanonFlow's `SqlParser` doesn't understand (like a proprietary geospatial function), emit it as `Lattice.Leaf(Opaque("RAW SQL"))`.

CanonFlow's Proof Engine will handle `Opaque` properly by immediately grading it as `Unsupported` during transpilation, surfacing the Drift to the user.
