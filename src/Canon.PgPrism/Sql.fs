namespace Canon.PgPrism

open Canon.Core
open System.Text

module Sql =

    let typeToSql (t: SqlType) =
        match t with
        | SqlType.Uuid -> "UUID"
        | SqlType.Text -> "TEXT"
        | SqlType.Varchar n -> $"VARCHAR({n})"
        | SqlType.Int -> "INTEGER"
        | SqlType.BigInt -> "BIGINT"
        | SqlType.Numeric (0, 0) -> "NUMERIC"
        | SqlType.Numeric (p, s) -> $"NUMERIC({p},{s})"
        | SqlType.Bool -> "BOOLEAN"
        | SqlType.TimestampTz -> "TIMESTAMPTZ"
        | SqlType.Timestamp -> "TIMESTAMP"
        | SqlType.Date -> "DATE"
        | SqlType.TextArray -> "TEXT[]"
        | SqlType.Jsonb -> "JSONB"

    let rec emitConstraint (c: Lattice<Constraint>) : string =
        match c with
        | True -> "TRUE"
        | False -> "FALSE"
        | Leaf (FieldBound (col, Range(Some(Exclusive min), None))) -> $"{col} > {min}"
        | Leaf (FieldBound (col, Range(Some(Inclusive min), None))) -> $"{col} >= {min}"
        | Leaf (FieldBound (col, Opaque sql)) ->
            if sql.StartsWith("RegexMatch: ") then
                let pattern = sql.Substring(12)
                $"{col} ~ '{pattern}'"
            else sql // Fallback for unsupported opaqueness for now
        | And (left, right) -> $"({emitConstraint left} AND {emitConstraint right})"
        | Or (left, right) -> $"({emitConstraint left} OR {emitConstraint right})"
        | Not inner -> $"(NOT {emitConstraint inner})"
        | _ -> "/* Unsupported Constraint */ TRUE"

    let emitTable (def: TableDef) : string =
        let sb = StringBuilder()
        sb.AppendLine($"CREATE TABLE {def.Name} (") |> ignore
        
        let mutable isFirst = true
        for col in def.Columns do
            if not isFirst then sb.AppendLine(",") |> ignore
            isFirst <- false
            
            sb.Append($"    {col.Name} {typeToSql col.Type}") |> ignore
            if col.NotNull then sb.Append(" NOT NULL") |> ignore
            if col.PrimaryKey then sb.Append(" PRIMARY KEY") |> ignore
            match col.Default with
            | Some d -> sb.Append($" DEFAULT {d}") |> ignore
            | None -> ()
            if col.Unique then sb.Append(" UNIQUE") |> ignore
            
        for c in def.Constraints do
            sb.AppendLine(",") |> ignore
            sb.Append($"    CONSTRAINT {c.Name} CHECK ({emitConstraint c.Expr})") |> ignore
            
        sb.AppendLine() |> ignore
        sb.AppendLine(");") |> ignore
        sb.ToString()
