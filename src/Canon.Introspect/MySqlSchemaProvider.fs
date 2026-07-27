namespace Canon.Introspect.MySql
#if !FABLE_COMPILER

open System
open Canon.Introspect

/// Placeholder schema provider for MySQL.
/// Demonstrates that CanonFlow's abstraction is database-agnostic.
module MySqlSchemaProvider =
    let createProvider (connectionString: string) : SchemaProvider =
        { Harvest = fun () ->
            // TODO: Implement MySQL introspection logic
            // e.g. querying information_schema.tables, information_schema.columns, information_schema.check_constraints
            printfn "MySQL introspection is not yet implemented."
            []
        }

#endif
