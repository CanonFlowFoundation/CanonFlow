namespace Canon.PgPrism

open Canon.Core
open System.Reflection

module Emit =

    /// Extracts the Lattice<Constraint> from a provided Refined type instance or via reflection if possible.
    let extractConstraint<'T, 'Tag> (witness: Refined<'T, 'Tag>) : Lattice<Constraint> =
        witness.Schema

    let rec toFSharpType (sqlType: SqlType) : string =
        match sqlType with
        | SqlType.Uuid -> "System.Guid"
        | SqlType.Text | SqlType.Varchar _ -> "string"
        | SqlType.Int -> "int"
        | SqlType.BigInt -> "int64"
        | SqlType.Numeric _ -> "decimal"
        | SqlType.Bool -> "bool"
        | SqlType.TimestampTz | SqlType.Timestamp -> "System.DateTimeOffset"
        | SqlType.Date -> "System.DateTime"
        | SqlType.Jsonb -> "string"
        | SqlType.Array inner -> $"{toFSharpType inner} array"
        | SqlType.Enum (name, _) -> name

    /// Generates F# Domain code as a string (IR to F# generation)
    let generateFSharpDomain (def: TableDef) : string =
        let sb = System.Text.StringBuilder()
        sb.AppendLine($"module {def.Name}Domain") |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("open Canon.Core") |> ignore
        sb.AppendLine() |> ignore
        
        // Emit Enums as DUs
        let emittedEnums = System.Collections.Generic.HashSet<string>()
        for col in def.Columns do
            match col.Type with
            | SqlType.Enum(name, vals) when not (emittedEnums.Contains(name)) ->
                emittedEnums.Add(name) |> ignore
                sb.AppendLine($"type {name} =") |> ignore
                for v in vals do
                    // Make sure first letter is uppercase for F# DU cases
                    let capitalized = string (System.Char.ToUpper(v.[0])) + v.Substring(1)
                    sb.AppendLine($"    | {capitalized}") |> ignore
                sb.AppendLine() |> ignore
            | _ -> ()
            
        for col in def.Columns do
            let typeName = toFSharpType col.Type
            sb.AppendLine($"// Projected from {col.Name}") |> ignore
            sb.AppendLine($"type {col.Name} = private {col.Name} of {typeName}") |> ignore
            
        sb.ToString()
