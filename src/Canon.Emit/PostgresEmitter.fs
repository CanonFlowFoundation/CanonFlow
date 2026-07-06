namespace Canon.Emit.Postgres

open System
open Canon.Emit
open Canon.Introspect

/// Emits Postgres DDL (CREATE TABLE) from the domain schema.
type PostgresEmitter() =
    
    interface IEmitter with
        member this.Emit(tables: TableDef list) =
            let sb = Text.StringBuilder()
            
            for table in tables do
                sb.AppendLine($"CREATE TABLE {table.Name} (") |> ignore
                
                let colDefs = 
                    table.Columns |> List.map (fun col ->
                        let nullability = if col.IsNullable then "NULL" else "NOT NULL"
                        let typeStr = 
                            match col.MaxLength with
                            | Some len -> $"{col.DataType}({len})"
                            | None -> col.DataType
                            
                        let checks = 
                            if col.CheckConstraints.IsEmpty then ""
                            else 
                                let combinedChecks = String.Join(" AND ", col.CheckConstraints)
                                $" CHECK ({combinedChecks})"
                                
                        $"    {col.Name} {typeStr} {nullability}{checks}"
                    )
                
                let combinedCols = String.Join(",\n", colDefs)
                sb.AppendLine(combinedCols) |> ignore
                sb.AppendLine(");") |> ignore
                sb.AppendLine() |> ignore
                
            [ sb.ToString(), Canon.Core.Fidelity.Exact ] // Return single DDL string for all tables, Postgres is exact to the TableDef
