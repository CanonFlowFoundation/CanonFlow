namespace Canon.PgPrism

open Canon.Core
open System.Reflection

module Emit =

    /// Extracts the Lattice<Constraint> from a provided Refined type instance or via reflection if possible.
    /// In a fully fleshed out system, this would use F# quotations or source generators to extract the AST
    /// without needing an instance. For now, we simulate extraction by evaluating a dummy instance or having 
    /// the user provide the Lattice explicitly as a Witness.
    let extractConstraint<'T, 'Tag> (witness: Refined<'T, 'Tag>) : Lattice<Constraint> =
        witness.Schema

    /// Generates F# Domain code as a string (IR to F# generation)
    let generateFSharpDomain (def: TableDef) : string =
        let sb = System.Text.StringBuilder()
        sb.AppendLine($"module {def.Name}Domain") |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("open Canon.Core") |> ignore
        sb.AppendLine() |> ignore
        
        for col in def.Columns do
            let typeName =
                match col.Type with
                | SqlType.Uuid -> "System.Guid"
                | SqlType.Text | SqlType.Varchar _ -> "string"
                | SqlType.Int -> "int"
                | SqlType.BigInt -> "int64"
                | SqlType.Numeric _ -> "decimal"
                | SqlType.Bool -> "bool"
                | SqlType.TimestampTz | SqlType.Timestamp -> "System.DateTimeOffset"
                | SqlType.Date -> "System.DateTime"
                | SqlType.TextArray -> "string array"
                | SqlType.Jsonb -> "string"
                
            sb.AppendLine($"// Projected from {col.Name}") |> ignore
            sb.AppendLine($"type {col.Name} = private {col.Name} of {typeName}") |> ignore
            
        sb.ToString()
