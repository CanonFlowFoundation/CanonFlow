namespace Canon.Introspect.SqlServer
#if !FABLE_COMPILER

open System
open Canon.Introspect

/// Placeholder schema provider for SQL Server.
/// Demonstrates that CanonFlow's abstraction is database-agnostic.
module SqlServerSchemaProvider =
    let createProvider (connectionString: string) : SchemaProvider =
        { Harvest = fun () ->
            // TODO: Implement SQL Server introspection logic
            // e.g. querying sys.tables, sys.columns, sys.check_constraints
            printfn "SQL Server introspection is not yet implemented."
            []
        }

#endif
