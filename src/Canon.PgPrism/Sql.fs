namespace Canon.PgPrism

open Canon.Core
open System.Text

module Sql =

    let rec typeToSql (t: SqlType) =
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
        | SqlType.Jsonb -> "JSONB"
        | SqlType.Array inner -> $"{typeToSql inner}[]"
        | SqlType.Enum (name, _) -> name

    type C = Canon.Core.Constraint

    let rec emitConstraint (c: Lattice<Constraint>) : string =
        match c with
        | True -> "TRUE"
        | False -> "FALSE"
        | Leaf (C.FieldBound (col, C.Range(Some(Exclusive min), None))) -> $"{col} > {min}"
        | Leaf (C.FieldBound (col, C.Range(Some(Inclusive min), None))) -> $"{col} >= {min}"
        | Leaf (C.FieldBound (col, C.IsNull)) -> $"{col} IS NULL"
        | Leaf (C.FieldBound (col, C.IsNotNull)) -> $"{col} IS NOT NULL"
        | Leaf (C.FieldBound (col, C.Opaque sql)) ->
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
        
        let colDefs =
            def.Columns |> List.map (fun col ->
                let colSb = StringBuilder()
                colSb.Append($"    {col.Name} {typeToSql col.Type}") |> ignore
                if col.NotNull then colSb.Append(" NOT NULL") |> ignore
                if col.PrimaryKey then colSb.Append(" PRIMARY KEY") |> ignore
                match col.Default with
                | Some d -> colSb.Append($" DEFAULT {d}") |> ignore
                | None -> ()
                if col.Unique then colSb.Append(" UNIQUE") |> ignore
                colSb.ToString()
            )
            
        sb.Append(String.concat ",\n" colDefs) |> ignore
        
        for c in def.Constraints do
            sb.AppendLine(",") |> ignore
            sb.Append($"    CONSTRAINT {c.Name} CHECK ({emitConstraint c.Expr})") |> ignore

        for fk in def.ForeignKeys do
            sb.AppendLine(",") |> ignore
            let localStr = String.concat ", " fk.LocalColumns
            let targetStr = String.concat ", " fk.TargetColumns
            sb.Append($"    CONSTRAINT {fk.Name} FOREIGN KEY ({localStr}) REFERENCES {fk.TargetTable} ({targetStr})") |> ignore
            match fk.OnDelete with
            | Some "c" -> sb.Append(" ON DELETE CASCADE") |> ignore
            | Some "r" -> sb.Append(" ON DELETE RESTRICT") |> ignore
            | Some "n" -> sb.Append(" ON DELETE SET NULL") |> ignore
            | _ -> ()
            match fk.OnUpdate with
            | Some "c" -> sb.Append(" ON UPDATE CASCADE") |> ignore
            | Some "r" -> sb.Append(" ON UPDATE RESTRICT") |> ignore
            | Some "n" -> sb.Append(" ON UPDATE SET NULL") |> ignore
            | _ -> ()
            
        sb.AppendLine() |> ignore
        sb.AppendLine(");") |> ignore
        sb.ToString()
